using aether_garden_be.Options;
using aether_garden_be.Services.Music;

namespace aether_garden_be.Modules;

public class MusicModule : IEndpointModule
{
    public bool IsEnabled(FeatureOptions features) => features.Music;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/music/favorites", async (IAppleMusicService appleMusicService, CancellationToken cancellationToken) =>
            Results.Ok(await appleMusicService.GetFavoriteTracksAsync(cancellationToken))
        );
    }
}
