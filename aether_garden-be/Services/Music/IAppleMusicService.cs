using aether_garden_be.Models;

namespace aether_garden_be.Services.Music;

public interface IAppleMusicService
{
    Task<IReadOnlyList<AppleMusicTrack>> GetFavoriteTracksAsync(CancellationToken cancellationToken = default);
    Task<AppleMusicTrack?> ResolveSongAsync(string songUrl, CancellationToken cancellationToken = default);
}
