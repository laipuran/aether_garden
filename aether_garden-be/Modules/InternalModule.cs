using System.Security.Cryptography;
using System.Text;
using aether_garden_be.Options;
using aether_garden_be.Services.Content;
using Microsoft.Extensions.Options;

namespace aether_garden_be.Modules;

public class InternalModule : IEndpointModule
{
    public bool IsEnabled(FeatureOptions features) => features.InternalOps;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/content/reload", async (
            HttpContext context,
            IContentReloadService contentReloadService,
            IOptions<InternalAuthOptions> authOptions,
            CancellationToken cancellationToken) =>
        {
            var token = authOptions.Value.ReloadToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                return Results.Problem(
                    title: "Internal reload token is not configured",
                    statusCode: StatusCodes.Status503ServiceUnavailable
                );
            }

            var suppliedToken = context.Request.Headers["X-Reload-Token"].ToString();
            if (!SecureEquals(token, suppliedToken))
            {
                return Results.Unauthorized();
            }

            var result = await contentReloadService.ReloadAsync(cancellationToken);
            return Results.Ok(result);
        }).Produces<ContentReloadResult>(200).Produces(401).Produces(503);
    }

    private static bool SecureEquals(string left, string right)
    {
        if (string.IsNullOrEmpty(right))
        {
            return false;
        }

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
