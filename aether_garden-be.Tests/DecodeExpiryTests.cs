using System.Text;
using System.Text.Json;
using aether_garden_be.Services.Music;

namespace aether_garden_be.Tests;

public sealed class DecodeExpiryTests
{
    private static string EncodeSegment(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string BuildJwt(long exp) =>
        $"eyJhbGciOiJub25lIn0.{EncodeSegment(new { iss = "AMPWebPlay", exp })}.signature";

    [Fact]
    public void DecodesExpFromJwtPayload()
    {
        var exp = 1790643483L;

        var result = AppleMusicService.DecodeExpiry(BuildJwt(exp));

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(exp), result);
    }
}
