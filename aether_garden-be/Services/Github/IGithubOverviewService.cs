using aether_garden_be.Models;

namespace aether_garden_be.Services.Github;

public interface IGithubOverviewService
{
    Task<GithubOverview> GetOverviewAsync(CancellationToken cancellationToken = default);
}
