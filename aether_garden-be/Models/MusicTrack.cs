namespace aether_garden_be.Models;

// A single song exposed by the music module: the Apple Music URL paired with
// its Netease link. NeteaseUrl is empty when the Netease search fails.
public record MusicTrack(
    string Name,
    string Artist,
    string ArtworkUrl, // Apple artwork template with {w}/{h} already replaced by 300
    string AppleMusicUrl,
    string NeteaseUrl
);
