using System.Text.Json;
using System.Text.RegularExpressions;
using aether_garden_be.Models;
using aether_garden_be.Options;
using Microsoft.Extensions.Options;

namespace aether_garden_be.Services.Music;

public class AppleMusicService : IAppleMusicService
{
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
    private readonly NeteaseMusicService _neteaseMusicService;
    private readonly AppleMusicDevTokenProvider _devTokenProvider;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private CachedTracks? _cache;
    private readonly RefreshGate<CachedTracks> _favoriteRefresh = new();

    public AppleMusicService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<MusicOptions> options,
        NeteaseMusicService neteaseMusicService,
        AppleMusicDevTokenProvider devTokenProvider
    )
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _neteaseMusicService = neteaseMusicService;
        _devTokenProvider = devTokenProvider;
    }

    public async Task<IReadOnlyList<MusicTrack>> GetFavoriteTracksAsync(
        CancellationToken cancellationToken = default
    )
    {
        var cached = await _favoriteRefresh.RefreshAsync(
            () => _cache,
            cache => cache.Tracks.Count > 0 && DateTimeOffset.UtcNow < cache.ExpiresAt,
            async ct =>
            {
                var tracks = await FetchTracksAsync(ct);
                return tracks.Count > 0
                    ? new CachedTracks(tracks, DateTimeOffset.UtcNow.Add(GetCacheTtl()))
                    : null;
            },
            cancellationToken,
            fresh => _cache = fresh
        );
        return cached?.Tracks ?? [];
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

        var devToken = await _devTokenProvider.GetTokenAsync(ct);
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
        var devToken = await _devTokenProvider.GetTokenAsync(ct);
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

    private TimeSpan GetCacheTtl()
    {
        var hours = _options.CurrentValue.CacheHours;
        return TimeSpan.FromHours(Math.Max(0, hours));
    }

    private sealed record CachedTracks(
        IReadOnlyList<MusicTrack> Tracks,
        DateTimeOffset ExpiresAt
    );

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
