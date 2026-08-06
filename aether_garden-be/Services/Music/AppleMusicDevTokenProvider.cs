using System.Text.Json;
using System.Text.RegularExpressions;

namespace aether_garden_be.Services.Music;

public sealed class AppleMusicDevTokenProvider
{
    // Dev token comes from scraping Apple's web bundle at runtime, not from our
    // own MusicKit credentials — see docs/adr/0001-apple-music-dev-token-scraped-from-web-bundle.md.
    private static readonly Regex JwtRegex = new(
        "eyJ[A-Za-z0-9_-]{10,}\\.[A-Za-z0-9_-]{10,}\\.[A-Za-z0-9_-]{10,}",
        RegexOptions.Compiled
    );
    private static readonly Regex DevTokenAssetRegex = new(
        "index-legacy~[A-Za-z0-9~_-]+\\.js",
        RegexOptions.Compiled
    );

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private CachedDevToken? _devToken;
    private readonly RefreshGate<CachedDevToken> _devTokenRefresh = new();

    public AppleMusicDevTokenProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _devTokenRefresh.RefreshAsync(
            () => _devToken,
            token => !string.IsNullOrWhiteSpace(token.Token) && DateTimeOffset.UtcNow < token.ExpiresAt,
            ScrapeDevTokenAsync,
            cancellationToken,
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

    internal static DateTimeOffset? DecodeExpiry(string jwt)
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

    private sealed record CachedDevToken(
        string Token,
        DateTimeOffset ExpiresAt
    );
}
