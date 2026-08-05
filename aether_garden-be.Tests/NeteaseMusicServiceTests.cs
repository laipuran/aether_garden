using aether_garden_be.Options;
using aether_garden_be.Services.Music;

namespace aether_garden_be.Tests;

public sealed class NeteaseMusicServiceTests
{
    private static NeteaseMusicService CreateService(IHttpClientFactory httpClientFactory) =>
        new(httpClientFactory, new FakeOptionsMonitor<MusicOptions>(new MusicOptions { CacheHours = 12 }));

    private static object SearchResponse(long id, string name, string artist) => new
    {
        result = new
        {
            songs = new[]
            {
                new { id, name, artists = new[] { new { name = artist } } }
            }
        }
    };

    [Fact]
    public async Task CachesLookup_SoSecondCallDoesNotHitHttp()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(
                        SearchResponse(111, "Lemon", "Miya"),
                        new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
                    System.Text.Encoding.UTF8,
                    "application/json")
            }
        );
        var service = CreateService(FakeHttpClientFactory.From(handler));

        var first = await service.ResolveSongUrlAsync("Lemon", "Miya");
        var second = await service.ResolveSongUrlAsync("Lemon", "Miya");

        Assert.Equal("https://music.163.com/song?id=111", first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task PicksBestMatch_WhenNameAndArtistContainQuery()
    {
        var payload = new
        {
            result = new
            {
                songs = new[]
                {
                    new { id = 1, name = "Some Other Song", artists = new[] { new { name = "Someone" } } },
                    new { id = 2, name = "Lemon", artists = new[] { new { name = "Miya" } } }
                }
            }
        };
        var service = CreateService(FakeHttpClientFactory.FromJson(payload));

        var result = await service.ResolveSongUrlAsync("Lemon", "Miya");

        Assert.Equal("https://music.163.com/song?id=2", result);
    }

    [Fact]
    public async Task FallsBackToFirstSong_WhenNoMatch()
    {
        var payload = new
        {
            result = new
            {
                songs = new[]
                {
                    new { id = 7, name = "Alpha", artists = new[] { new { name = "Beta" } } },
                    new { id = 8, name = "Gamma", artists = new[] { new { name = "Delta" } } }
                }
            }
        };
        var service = CreateService(FakeHttpClientFactory.FromJson(payload));

        var result = await service.ResolveSongUrlAsync("Zzz", "Zzz");

        Assert.Equal("https://music.163.com/song?id=7", result);
    }

    [Fact]
    public async Task ReturnsNull_AndDoesNotCache_WhenSearchFails()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
        );
        var service = CreateService(FakeHttpClientFactory.From(handler));

        var first = await service.ResolveSongUrlAsync("Lemon", "Miya");
        var second = await service.ResolveSongUrlAsync("Lemon", "Miya");

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(2, handler.CallCount);
    }
}
