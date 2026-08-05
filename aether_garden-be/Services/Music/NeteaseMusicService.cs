using System.Text.Encodings.Web;
using System.Text.Json;
using aether_garden_be.Options;
using Microsoft.Extensions.Options;

namespace aether_garden_be.Services.Music;

public class NeteaseMusicService : INeteaseMusicService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<MusicOptions> _options;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly UrlEncoder _urlEncoder = UrlEncoder.Default;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly Dictionary<string, CachedSong> _cache = new(StringComparer.OrdinalIgnoreCase);

    public NeteaseMusicService(IHttpClientFactory httpClientFactory, IOptionsMonitor<MusicOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public async Task<string?> ResolveSongUrlAsync(string trackName, string artistName, CancellationToken cancellationToken = default)
    {
        var queryKey = BuildCacheKey(trackName, artistName);
        if (TryGetCached(queryKey, out var cachedUrl))
        {
            return cachedUrl;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCached(queryKey, out cachedUrl))
            {
                return cachedUrl;
            }

            var songId = await SearchSongIdAsync(trackName, artistName, cancellationToken);
            var url = songId is null ? null : BuildSongUrl(songId.Value);

            if (!string.IsNullOrWhiteSpace(url))
            {
                var ttl = TimeSpan.FromHours(Math.Max(0, _options.CurrentValue.CacheHours));
                _cache[queryKey] = new CachedSong(url, DateTimeOffset.UtcNow.Add(ttl));
            }

            return url;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<long?> SearchSongIdAsync(string trackName, string artistName, CancellationToken cancellationToken)
    {
        var query = BuildQuery(trackName, artistName);
        var encoded = _urlEncoder.Encode(query);
        var requestUrl = $"https://music.163.com/api/search/get/web?csrf_token=&s={encoded}&type=1&offset=0&total=true&limit=5";

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("aether-garden-site");
        client.DefaultRequestHeaders.Referrer = new Uri("https://music.163.com/");

        using var response = await client.GetAsync(requestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<NeteaseSearchResponse>(stream, _jsonOptions, cancellationToken);
        var songs = payload?.Result?.Songs;
        if (songs is null || songs.Count == 0)
        {
            return null;
        }

        var normalizedTrack = Normalize(trackName);
        var normalizedArtist = Normalize(artistName);

        var best = songs.FirstOrDefault(song =>
            Normalize(song.Name).Contains(normalizedTrack, StringComparison.OrdinalIgnoreCase) &&
            song.Artists.Any(artist => Normalize(artist.Name).Contains(normalizedArtist, StringComparison.OrdinalIgnoreCase))
        );

        return best?.Id ?? songs[0].Id;
    }

    private static string BuildCacheKey(string trackName, string artistName) =>
        $"{trackName.Trim()}::{artistName.Trim()}";

    private bool TryGetCached(string key, out string? url)
    {
        url = null;
        if (_cache.TryGetValue(key, out var cached) && DateTimeOffset.UtcNow < cached.ExpiresAt)
        {
            url = cached.Url;
            return true;
        }

        if (_cache.TryGetValue(key, out cached) && DateTimeOffset.UtcNow >= cached.ExpiresAt)
        {
            _cache.Remove(key);
        }

        return false;
    }

    private static string BuildQuery(string trackName, string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName))
        {
            return trackName.Trim();
        }

        return $"{trackName.Trim()} {artistName.Trim()}";
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static string BuildSongUrl(long songId) => $"https://music.163.com/song?id={songId}";

    private sealed record CachedSong(string Url, DateTimeOffset ExpiresAt);

    // Mirror the JSON of the Netease search API (music.163.com/api/search/get/web).
    private sealed class NeteaseSearchResponse
    {
        public NeteaseSearchResult? Result { get; set; }
    }

    private sealed class NeteaseSearchResult
    {
        public List<NeteaseSong> Songs { get; set; } = [];
    }

    private sealed class NeteaseSong
    {
        public long Id { get; set; } // the 163.com song id, used to build the Netease link
        public string Name { get; set; } = string.Empty;
        public List<NeteaseArtist> Artists { get; set; } = [];
    }

    private sealed class NeteaseArtist
    {
        public string Name { get; set; } = string.Empty;
    }
}
