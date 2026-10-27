using System.Net;
using aether_garden_be.Options;
using aether_garden_be.Services.Music;
using Microsoft.Extensions.Options;
using Xunit;

namespace aether_garden_be.Tests;

public class AppleMusicServiceTests
{
    private const string SongUrl = "https://music.apple.com/cn/album/flo-lo/1731112820?i=1731112829&ls";
    private const string Token = "eyJtestheader123.testpayload123.testsignature123";

    [Theory]
    [InlineData("/assets/index~new-version.js")]
    [InlineData("/assets/index-legacy~next-version.js")]
    [InlineData("https://music.apple.com/assets/index~another-version.js")]
    public async Task ResolveSongDiscoversCurrentAssetInsteadOfUsingExpiredBundle(string asset)
    {
        using var handler = new AppleHandler($"<script type='module' src='{asset}'></script>");
        using var client = new HttpClient(handler);
        var service = CreateService(client);

        var track = await service.ResolveSongAsync(SongUrl);

        Assert.NotNull(track);
        Assert.Equal("Flo-Lo", track.Name);
        Assert.Equal("Titancube", track.Artist);
        Assert.Equal("https://music.163.com/song?id=2130300298", track.NeteaseUrl);
        Assert.Equal("https://example.com/300x300.jpg", track.ArtworkUrl);
        Assert.Contains(new Uri(new Uri("https://music.apple.com/"), asset).AbsoluteUri, handler.Requests);
        Assert.DoesNotContain(handler.Requests, url => url.Contains("0484eb127f"));
    }

    [Fact]
    public async Task FavoritesAlsoUseCurrentDeveloperToken()
    {
        using var handler = new AppleHandler("<script src=\"/assets/index~new-version.js\"></script>");
        using var client = new HttpClient(handler);

        var tracks = await CreateService(client).GetFavoriteTracksAsync();

        Assert.Equal("Flo-Lo", Assert.Single(tracks).Name);
    }

    [Theory]
    [InlineData("<html>No scripts available</html>")]
    [InlineData("<script src='https://untrusted.example/assets/index~new-version.js'></script>")]
    public async Task MissingOrUntrustedAssetsDoNotResolveOrFetchExternalScripts(string html)
    {
        using var handler = new AppleHandler(html);
        using var client = new HttpClient(handler);

        Assert.Null(await CreateService(client).ResolveSongAsync(SongUrl));
        Assert.Equal("https://music.apple.com/", Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task UnavailableEntryBundleDoesNotResolve()
    {
        using var handler = new AppleHandler("<script src='/assets/index~missing.js'></script>");
        using var client = new HttpClient(handler);

        Assert.Null(await CreateService(client).ResolveSongAsync(SongUrl));
    }

    private static AppleMusicService CreateService(HttpClient client)
    {
        var factory = new ClientFactory(client);
        var options = new StaticOptions(new AppleMusicOptions
        {
            PlaylistUrl = "https://music.apple.com/cn/playlist/test/pl.test"
        });
        return new AppleMusicService(factory, options, new NeteaseMusicService(factory, options));
    }

    private sealed class AppleHandler(string html) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Requests.Add(url);
            var status = HttpStatusCode.OK;
            string body;
            if (url == "https://music.apple.com/")
            {
                body = html;
            }
            else if (url.Contains("/assets/") && !url.Contains("0484eb127f") && !url.Contains("missing"))
            {
                body = $"const token = '{Token}';";
            }
            else if (url.StartsWith("https://amp-api.music.apple.com/"))
            {
                Assert.Equal($"Bearer {Token}", request.Headers.Authorization?.ToString());
                Assert.Equal("https://music.apple.com", Assert.Single(request.Headers.GetValues("Origin")));
                const string attributes = """
                    {"name":"Flo-Lo","artistName":"Titancube","url":"https://music.apple.com/cn/album/flo-lo/1731112820?i=1731112829","artwork":{"url":"https://example.com/{w}x{h}.jpg"}}
                    """;
                body = url.Contains("/playlists/")
                    ? "{\"data\":[{\"relationships\":{\"tracks\":{\"data\":[{\"attributes\":" + attributes + "}]}}}]}"
                    : "{\"data\":[{\"attributes\":" + attributes + "}]}";
                if (!url.Contains("/playlists/"))
                {
                    Assert.EndsWith("/cn/songs/1731112829", url);
                }
            }
            else if (url.StartsWith("https://music.163.com/api/search/"))
            {
                body = """{"result":{"songs":[{"id":2130300298,"name":"Flo-Lo","artists":[{"name":"Titancube"}]}]}}""";
            }
            else
            {
                status = HttpStatusCode.NotFound;
                body = "Unavailable legacy asset";
            }

            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StaticOptions(AppleMusicOptions value) : IOptionsMonitor<AppleMusicOptions>
    {
        public AppleMusicOptions CurrentValue => value;
        public AppleMusicOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<AppleMusicOptions, string?> listener) => null;
    }
}
