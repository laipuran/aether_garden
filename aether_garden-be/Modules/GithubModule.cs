using aether_garden_be.Options;
using aether_garden_be.Services.Github;

namespace aether_garden_be.Modules;

public class GithubModule : IEndpointModule
{
    public bool IsEnabled(FeatureOptions features) => features.Github;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/github/overview", async (IGithubOverviewService githubOverviewService, CancellationToken cancellationToken) =>
            Results.Ok(await githubOverviewService.GetOverviewAsync(cancellationToken))
        );
    }
}
