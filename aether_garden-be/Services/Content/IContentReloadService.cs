namespace aether_garden_be.Services.Content;

public interface IContentReloadService
{
    Task<ContentReloadResult> ReloadAsync(CancellationToken cancellationToken = default);
}
