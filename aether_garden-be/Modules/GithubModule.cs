using aether_garden_be.Options;
using aether_garden_be.Services.Github;

namespace aether_garden_be.Modules;

public class GithubModule : IEndpointModule
{
    public bool IsEnabled(FeatureOptions features) => features.Github;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/github/overview", async (GithubOverviewService githubOverviewService, CancellationToken cancellationToken) =>
            TypedResults.Ok(await githubOverviewService.GetOverviewAsync(cancellationToken))
        );
    }
}
