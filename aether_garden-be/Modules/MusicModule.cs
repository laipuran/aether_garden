using aether_garden_be.Models;
using aether_garden_be.Options;
using aether_garden_be.Services.Music;

namespace aether_garden_be.Modules;

public class MusicModule : IEndpointModule
{
    public bool IsEnabled(FeatureOptions features) => features.Music;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/music/favorites", async (IAppleMusicService appleMusicService, CancellationToken cancellationToken) =>
            TypedResults.Ok(await appleMusicService.GetFavoriteTracksAsync(cancellationToken))
        );

        endpoints.MapGet("/api/music/convert", async (string url, IAppleMusicService appleMusicService, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return Results.BadRequest(new { error = "Url is required" });
            }

            var track = await appleMusicService.ResolveSongAsync(url, cancellationToken);
            if (track is null)
            {
                return Results.NotFound(new { error = "Song not found or conversion failed" });
            }

            return Results.Ok(track);
        }).Produces<MusicTrack>(200).Produces(404).Produces(400);
    }
}
