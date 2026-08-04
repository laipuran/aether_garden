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
    private readonly IOptionsMonitor<AppleMusicOptions> _options;
    private readonly INeteaseMusicService _neteaseMusicService;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private CachedTracks? _cache;

    public AppleMusicService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<AppleMusicOptions> options,
        INeteaseMusicService neteaseMusicService
    )
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _neteaseMusicService = neteaseMusicService;
    }

    public async Task<IReadOnlyList<AppleMusicTrack>> GetFavoriteTracksAsync(
        CancellationToken cancellationToken = default
    )
    {
        var ttl = GetCacheTtl();
        var cached = _cache;
        if (
            cached is not null
            && cached.Tracks.Count > 0
            && DateTimeOffset.UtcNow < cached.ExpiresAt
        )
        {
            return cached.Tracks;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            cached = _cache;
            if (
                cached is not null
                && cached.Tracks.Count > 0
                && DateTimeOffset.UtcNow < cached.ExpiresAt
            )
            {
                return cached.Tracks;
            }

            var tracks = await FetchTracksAsync(cancellationToken);
            if (tracks.Count > 0)
            {
                _cache = new CachedTracks(tracks, DateTimeOffset.UtcNow.Add(ttl));
                return tracks;
            }

            return cached?.Tracks ?? tracks;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<AppleMusicTrack>> FetchTracksAsync(
        CancellationToken cancellationToken
    )
    {
        var playlistUrl = _options.CurrentValue.PlaylistUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(playlistUrl))
        {
            return Array.Empty<AppleMusicTrack>();
        }

        if (!TryParsePlaylist(playlistUrl, out var storefront, out var playlistId))
        {
            return Array.Empty<AppleMusicTrack>();
        }

        var devToken = await FetchDeveloperTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(devToken))
        {
            return Array.Empty<AppleMusicTrack>();
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("aether-garden-site");

        var apiUrl =
            $"https://amp-api.music.apple.com/v1/catalog/{storefront}/playlists/{playlistId}?include=tracks";
        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.Add("Authorization", $"Bearer {devToken}");
        request.Headers.Add("Origin", "https://music.apple.com");
        request.Headers.Add("Referer", playlistUrl);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<AppleMusicTrack>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var playlist = await JsonSerializer.DeserializeAsync<PlaylistResponse>(
            stream,
            _jsonOptions,
            cancellationToken
        );
        var tracks = playlist?.Data?.FirstOrDefault()?.Relationships?.Tracks?.Data;
        if (tracks is null)
        {
            return Array.Empty<AppleMusicTrack>();
        }

        var directTracks = tracks
            .Where(track => track.Attributes is not null)
            .Select(track => track.Attributes!)
            .Where(track => !string.IsNullOrWhiteSpace(track.Name))
            .ToList();

        if (directTracks.Count > 0)
        {
            return await BuildTracksWithNeteaseAsync(directTracks, cancellationToken);
        }

        if (playlist?.Included is null)
        {
            return Array.Empty<AppleMusicTrack>();
        }

        var includedTracks = playlist
            .Included.Where(item => item.Type == "songs" && item.Attributes is not null)
            .Select(item => item.Attributes!)
            .Where(track => !string.IsNullOrWhiteSpace(track.Name))
            .ToList();

        return await BuildTracksWithNeteaseAsync(includedTracks, cancellationToken);
    }

    private static AppleMusicTrack ToTrack(TrackAttributes attributes, string neteaseUrl)
    {
        var artwork = attributes.Artwork?.Url ?? string.Empty;
        var artworkUrl = string.IsNullOrWhiteSpace(artwork)
            ? string.Empty
            : artwork.Replace("{w}", "300").Replace("{h}", "300");

        return new AppleMusicTrack(
            attributes.Name ?? string.Empty,
            attributes.ArtistName ?? string.Empty,
            artworkUrl,
            attributes.Url ?? string.Empty,
            neteaseUrl
        );
    }

    public async Task<AppleMusicTrack?> ResolveSongAsync(
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
        CancellationToken cancellationToken
    )
    {
        var devToken = await FetchDeveloperTokenAsync(cancellationToken);
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

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var songResponse = await JsonSerializer.DeserializeAsync<SongResponse>(
            stream,
            _jsonOptions,
            cancellationToken
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

    private async Task<string?> FetchDeveloperTokenAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("aether-garden-site");

        var assetUrl = await ResolveDevTokenAssetUrlAsync(client, cancellationToken);
        if (string.IsNullOrWhiteSpace(assetUrl))
        {
            return null;
        }

        var response = await client.GetAsync(assetUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var js = await response.Content.ReadAsStringAsync(cancellationToken);
        var match = JwtRegex.Match(js);
        return match.Success ? match.Value : null;
    }

    private async Task<string?> ResolveDevTokenAssetUrlAsync(
        HttpClient client,
        CancellationToken cancellationToken
    )
    {
        var htmlResponse = await client.GetAsync("https://music.apple.com/cn", cancellationToken);
        if (!htmlResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var html = await htmlResponse.Content.ReadAsStringAsync(cancellationToken);
        var match = DevTokenAssetRegex.Match(html);
        return match.Success ? $"https://music.apple.com/assets/{match.Value}" : null;
    }

    private TimeSpan GetCacheTtl()
    {
        var hours = _options.CurrentValue.CacheHours;
        if (hours <= 0)
        {
            hours = 12;
        }

        return TimeSpan.FromHours(hours);
    }

    private sealed record CachedTracks(
        IReadOnlyList<AppleMusicTrack> Tracks,
        DateTimeOffset ExpiresAt
    );

    private sealed class SongResponse
    {
        public List<TrackData>? Data { get; set; }
    }

    private sealed class PlaylistResponse
    {
        public List<PlaylistData>? Data { get; set; }
        public List<TrackData>? Included { get; set; }
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

    private sealed class TrackArtwork
    {
        public string? Url { get; set; }
    }

    private async Task<IReadOnlyList<AppleMusicTrack>> BuildTracksWithNeteaseAsync(
        List<TrackAttributes> tracks,
        CancellationToken cancellationToken
    )
    {
        var limited = tracks.Take(4).ToList();
        var results = new List<AppleMusicTrack>(limited.Count);

        foreach (var track in limited)
        {
            var neteaseUrl = await _neteaseMusicService.ResolveSongUrlAsync(
                track.Name ?? string.Empty,
                track.ArtistName ?? string.Empty,
                cancellationToken
            );

            results.Add(ToTrack(track, neteaseUrl ?? string.Empty));
        }

        return results;
    }
}
