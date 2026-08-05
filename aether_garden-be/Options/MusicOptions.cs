namespace aether_garden_be.Options;

public class MusicOptions
{
    public string PlaylistUrl { get; set; } = string.Empty;
    public int CacheHours { get; set; } = 12;
    public int CacheLimit { get; set; } = 4;
}
