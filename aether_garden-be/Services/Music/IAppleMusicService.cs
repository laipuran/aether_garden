using aether_garden_be.Models;

namespace aether_garden_be.Services.Music;

public interface IAppleMusicService
{
    Task<IReadOnlyList<MusicTrack>> GetFavoriteTracksAsync(CancellationToken cancellationToken = default);
    Task<MusicTrack?> ResolveSongAsync(string songUrl, CancellationToken cancellationToken = default);
}
