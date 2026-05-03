namespace aether_garden_be.Services.Music;

public interface INeteaseMusicService
{
    Task<string?> ResolveSongUrlAsync(string trackName, string artistName, CancellationToken cancellationToken = default);
}
