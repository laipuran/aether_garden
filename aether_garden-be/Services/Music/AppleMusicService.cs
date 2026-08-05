using System.Text.Json;
using System.Text.RegularExpressions;
using aether_garden_be.Models;
using aether_garden_be.Options;
using Microsoft.Extensions.Options;

namespace aether_garden_be.Services.Music;

public class AppleMusicService : IAppleMusicService
{
    private static readonly Regex JwtRegex = new(
        "eyJ[A-Za-z0-9_-]{10,}\\.[A-Za-z0-9_-]{10,}\\.[A-Za-z0-9_-]{10,}",
        RegexOptions.Compiled
    );
    private static readonly Regex DevTokenAssetRegex = new(
        "index-legacy~[A-Za-z0-9~_-]+\\.js",
        RegexOptions.Compiled
    );
    // Dev token comes from scraping Apple's web bundle at runtime, not from our
    // own MusicKit credentials — see docs/adr/0001-apple-music-dev-token-scraped-from-web-bundle.md.
    private static readonly Regex PlaylistIdRegex = new(
        "pl\\.[A-Za-z0-9-]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );
    private static readonly Regex StorefrontRegex = new(
        "music\\.apple\\.com\\/([a-z]{2})\\/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );
    private static readonly Regex AlbumSongIdRegex = new("[?&]i=(\\d+)", RegexOptions.Compiled);
    private static readonly Regex PathSongIdRegex = new("/(\\d+)$", RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<MusicOptions> _options;
    private readonly INeteaseMusicService _neteaseMusicService;
    private readonly SemaphoreSlim _gateFavorite = new(1, 1);
    private readonly SemaphoreSlim _gateDevToken = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private CachedTracks? _cache;

    private CachedDevToken? _devToken;

    public AppleMusicService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<MusicOptions> options,
        INeteaseMusicService neteaseMusicService
    )
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _neteaseMusicService = neteaseMusicService;
    }

    public async Task<IReadOnlyList<MusicTrack>> GetFavoriteTracksAsync(
        CancellationToken cancellationToken = default
    )
    {
        var cached = await RefreshIfStaleAsync(
            () => _cache,
            cache => cache.Tracks.Count > 0 && DateTimeOffset.UtcNow < cache.ExpiresAt,
            async ct =>
            {
                var tracks = await FetchTracksAsync(ct);
                return tracks.Count > 0
                    ? new CachedTracks(tracks, DateTimeOffset.UtcNow.Add(GetCacheTtl()))
                    : null;
            },
            _gateFavorite,
            cancellationToken,
            fresh => _cache = fresh
        );
        return cached?.Tracks ?? [];
    }

    private static async Task<T?> RefreshIfStaleAsync<T>(
        Func<T?> read,
        Func<T, bool> isValid,
        Func<CancellationToken, Task<T?>> refresh,
        SemaphoreSlim gate,
        CancellationToken ct,
        Action<T> set)
        where T : class
    {
        var cached = read();
        if (cached is not null && isValid(cached))
        {
            return cached;
        }

        await gate.WaitAsync(ct);
        try
        {
            cached = read();
            if (cached is not null && isValid(cached))
            {
                return cached;
            }

            var fresh = await refresh(ct);
            if (fresh is not null)
            {
                set(fresh);
                cached = fresh;
            }

            // Returns stale if failure.
            return cached;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<MusicTrack>> FetchTracksAsync(
        CancellationToken ct
    )
    {
        var playlistUrl = _options.CurrentValue.PlaylistUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(playlistUrl))
        {
            return [];
        }

        if (!TryParsePlaylist(playlistUrl, out var storefront, out var playlistId))
        {
            return [];
        }

        var devToken = await FetchDeveloperTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(devToken))
        {
            return [];
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("aether-garden-site");

        var apiUrl =
            $"https://amp-api.music.apple.com/v1/catalog/{storefront}/playlists/{playlistId}?include=tracks";
        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.Add("Authorization", $"Bearer {devToken}");
        request.Headers.Add("Origin", "https://music.apple.com");
        request.Headers.Add("Referer", playlistUrl);

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var playlist = await JsonSerializer.DeserializeAsync<PlaylistResponse>(
            stream,
            _jsonOptions,
            ct
        );
        var tracks = playlist?.Data?.FirstOrDefault()?.Relationships?.Tracks?.Data;
        if (tracks is null)
        {
            return [];
        }

        var directTracks = tracks
            .Where(track => track.Attributes is not null)
            .Select(track => track.Attributes!)
            .Where(track => !string.IsNullOrWhiteSpace(track.Name))
            .Take(_options.CurrentValue.CacheLimit)
            .ToList();

        if (directTracks.Count > 0)
        {
            return await BuildTracksWithNeteaseAsync(directTracks, ct);
        }
        else
        {
            return [];
        }
    }

    private static MusicTrack ToTrack(TrackAttributes attributes, string neteaseUrl)
    {
        var artwork = attributes.Artwork?.Url ?? string.Empty;
        var artworkUrl = string.IsNullOrWhiteSpace(artwork)
            ? string.Empty
            : artwork.Replace("{w}", "300").Replace("{h}", "300");

        return new MusicTrack(
            attributes.Name ?? string.Empty,
            attributes.ArtistName ?? string.Empty,
            artworkUrl,
            attributes.Url ?? string.Empty,
            neteaseUrl
        );
    }

    public async Task<MusicTrack?> ResolveSongAsync(
        string songUrl,
        CancellationToken cancellationToken = default
    )
    {
        if (
            string.IsNullOrWhiteSpace(songUrl)
            || !TryParseSongUrl(songUrl, out var storefront, out var songId)
        )
        {
            return null;
        }

        var attributes = await FetchSongAsync(storefront, songId, songUrl, cancellationToken);
        if (attributes is null)
        {
            return null;
        }

        var neteaseUrl = await _neteaseMusicService.ResolveSongUrlAsync(
            attributes.Name ?? string.Empty,
            attributes.ArtistName ?? string.Empty,
            cancellationToken
        );

        return ToTrack(attributes, neteaseUrl ?? string.Empty);
    }

    private static bool TryParseSongUrl(string songUrl, out string storefront, out string songId)
    {
        storefront = "cn";
        songId = string.Empty;

        var sfMatch = StorefrontRegex.Match(songUrl);
        if (sfMatch.Success)
        {
            storefront = sfMatch.Groups[1].Value.ToLowerInvariant();
        }

        var iMatch = AlbumSongIdRegex.Match(songUrl);
        if (iMatch.Success)
        {
            songId = iMatch.Groups[1].Value;
            return true;
        }

        var pathMatch = PathSongIdRegex.Match(songUrl.AsSpan().TrimEnd('/').ToString());
        if (pathMatch.Success)
        {
            songId = pathMatch.Groups[1].Value;
            return true;
        }

        return false;
    }

    private async Task<TrackAttributes?> FetchSongAsync(
        string storefront,
        string songId,
        string refererUrl,
        CancellationToken ct
    )
    {
        var devToken = await FetchDeveloperTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(devToken))
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("aether-garden-site");

        var apiUrl = $"https://amp-api.music.apple.com/v1/catalog/{storefront}/songs/{songId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.Add("Authorization", $"Bearer {devToken}");
        request.Headers.Add("Origin", "https://music.apple.com");
        request.Headers.Add("Referer", refererUrl);

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var songResponse = await JsonSerializer.DeserializeAsync<SongResponse>(
            stream,
            _jsonOptions,
            ct
        );
        return songResponse?.Data?.FirstOrDefault()?.Attributes;
    }

    private static bool TryParsePlaylist(
        string playlistUrl,
        out string storefront,
        out string playlistId
    )
    {
        storefront = "cn";
        playlistId = string.Empty;

        var idMatch = PlaylistIdRegex.Match(playlistUrl);
        if (!idMatch.Success)
        {
            return false;
        }

        playlistId = idMatch.Value;

        var storefrontMatch = StorefrontRegex.Match(playlistUrl);
        if (storefrontMatch.Success)
        {
            storefront = storefrontMatch.Groups[1].Value.ToLowerInvariant();
        }

        return true;
    }

    private async Task<string?> FetchDeveloperTokenAsync(CancellationToken ct)
    {
        var cached = await RefreshIfStaleAsync(
            () => _devToken,
            token => !string.IsNullOrWhiteSpace(token.Token) && DateTimeOffset.UtcNow < token.ExpiresAt,
            ScrapeDevTokenAsync,
            _gateDevToken,
            ct,
            fresh => _devToken = fresh
        );
        return cached?.Token;
    }

    private async Task<CachedDevToken?> ScrapeDevTokenAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("aether-garden-site");

        var assetUrl = await ResolveDevTokenAssetUrlAsync(client, ct);
        if (string.IsNullOrWhiteSpace(assetUrl))
        {
            return null;
        }

        var response = await client.GetAsync(assetUrl, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var js = await response.Content.ReadAsStringAsync(ct);
        var match = JwtRegex.Match(js);
        if (!match.Success)
        {
            return null;
        }

        var expiry = DecodeExpiry(match.Value);
        var expiresAt = expiry is null
            ? DateTimeOffset.UtcNow.Add(TimeSpan.FromHours(24))
            : expiry.Value.Subtract(TimeSpan.FromMinutes(5));
        return new CachedDevToken(match.Value, expiresAt);
    }

    private static DateTimeOffset? DecodeExpiry(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var base64 = payload.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        var bytes = Convert.FromBase64String(base64);
        using var doc = JsonDocument.Parse(bytes);
        if (doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return null;
    }

    private async Task<string?> ResolveDevTokenAssetUrlAsync(
        HttpClient client,
        CancellationToken ct
    )
    {
        var htmlResponse = await client.GetAsync("https://music.apple.com/cn", ct);
        if (!htmlResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var html = await htmlResponse.Content.ReadAsStringAsync(ct);
        var match = DevTokenAssetRegex.Match(html);
        return match.Success ? $"https://music.apple.com/assets/{match.Value}" : null;
    }

    private TimeSpan GetCacheTtl()
    {
        var hours = _options.CurrentValue.CacheHours;
        return TimeSpan.FromHours(Math.Max(0, hours));
    }

    private sealed record CachedTracks(
        IReadOnlyList<MusicTrack> Tracks,
        DateTimeOffset ExpiresAt
    );

    private sealed record CachedDevToken(
        string Token,
        DateTimeOffset ExpiresAt
    );

    // Mirror the JSON returned by Apple's private web API (amp-api.music.apple.com).
    // These classes are never constructed directly — System.Text.Json populates them.
    private sealed class SongResponse
    {
        public List<TrackData>? Data { get; set; }
    }

    // A playlist's direct tracks live under Data[].Relationships.Tracks.Data;
    private sealed class PlaylistResponse
    {
        public List<PlaylistData>? Data { get; set; }
    }

    private sealed class PlaylistData
    {
        public PlaylistRelationships? Relationships { get; set; }
    }

    private sealed class PlaylistRelationships
    {
        public TrackRelationship? Tracks { get; set; }
    }

    private sealed class TrackRelationship
    {
        public List<TrackData>? Data { get; set; }
    }

    private sealed class TrackData
    {
        // Type discriminates what the entry is ("songs" vs. "music-videos", etc.);
        public string? Type { get; set; }
        public TrackAttributes? Attributes { get; set; }
    }

    private sealed class TrackAttributes
    {
        public string? Name { get; set; }
        public string? ArtistName { get; set; }
        public string? Url { get; set; }
        public TrackArtwork? Artwork { get; set; }
    }

    // Artwork.Url is Apple's templated artwork URL with {w}/{h} placeholders;
    // ToTrack replaces them with concrete pixel sizes.
    private sealed class TrackArtwork
    {
        public string? Url { get; set; }
    }

    private async Task<IReadOnlyList<MusicTrack>> BuildTracksWithNeteaseAsync(
        List<TrackAttributes> tracks,
        CancellationToken ct
    )
    {
        var results = new List<MusicTrack>(tracks.Count);

        foreach (var track in tracks)
        {
            var neteaseUrl = await _neteaseMusicService.ResolveSongUrlAsync(
                track.Name ?? string.Empty,
                track.ArtistName ?? string.Empty,
                ct
            );

            results.Add(ToTrack(track, neteaseUrl ?? string.Empty));
        }

        return results;
    }
}
