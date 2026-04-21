using aether_garden_be.Options;
using aether_garden_be.Services.Profile;

namespace aether_garden_be.Modules;

public class ProfileModule : IEndpointModule
{
    public bool IsEnabled(FeatureOptions features) => features.Profile;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/profile", (IProfileProvider profileProvider) => Results.Ok(profileProvider.GetProfile()));
    }
}
