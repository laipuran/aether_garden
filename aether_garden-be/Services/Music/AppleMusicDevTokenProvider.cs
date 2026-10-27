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
    private static readonly Uri AppleMusicOrigin = new("https://music.apple.com/");
    private static readonly Regex ScriptSourceRegex = new(
        """<script\b[^>]*\bsrc\s*=\s*["'](?<src>[^"']+)["']""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private readonly IHttpClientFactory _httpClientFactory;

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

        using var pageResponse = await client.GetAsync(AppleMusicOrigin, ct);
        if (!pageResponse.IsSuccessStatusCode)
        {
            return null;
        }

        // Discover the current entry bundles; both modern and legacy filenames
        // rotate independently of the token's lifetime.
        var html = await pageResponse.Content.ReadAsStringAsync(ct);
        var pageUri = pageResponse.RequestMessage?.RequestUri ?? AppleMusicOrigin;
        var scripts = ScriptSourceRegex.Matches(html)
            .Select(match => Uri.TryCreate(pageUri, match.Groups["src"].Value, out var uri) ? uri : null)
            .Where(uri => uri is not null
                && uri.Scheme == AppleMusicOrigin.Scheme
                && uri.Authority == AppleMusicOrigin.Authority
                && uri.AbsolutePath.StartsWith("/assets/index", StringComparison.Ordinal)
                && uri.AbsolutePath.EndsWith(".js", StringComparison.Ordinal))
            .Distinct();

        foreach (var script in scripts)
        {
            using var response = await client.GetAsync(script, ct);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            var js = await response.Content.ReadAsStringAsync(ct);
            var match = JwtRegex.Match(js);
            if (!match.Success)
            {
                continue;
            }

            var expiry = DecodeExpiry(match.Value);
            var expiresAt = expiry is null
                ? DateTimeOffset.UtcNow.Add(TimeSpan.FromHours(24))
                : expiry.Value.Subtract(TimeSpan.FromMinutes(5));
            return new CachedDevToken(match.Value, expiresAt);
        }

        return null;
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

    private sealed record CachedDevToken(
        string Token,
        DateTimeOffset ExpiresAt
    );
}
