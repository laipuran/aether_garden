namespace aether_garden_be.Services.Music;

// Mirror the JSON returned by Apple's private web API (amp-api.music.apple.com).
// These classes are never constructed directly — System.Text.Json populates them.
internal sealed class SongResponse
{
    public List<TrackData>? Data { get; set; }
}

// A playlist's direct tracks live under Data[].Relationships.Tracks.Data;
internal sealed class PlaylistResponse
{
    public List<PlaylistData>? Data { get; set; }
}

internal sealed class PlaylistData
{
    public PlaylistRelationships? Relationships { get; set; }
}

internal sealed class PlaylistRelationships
{
    public TrackRelationship? Tracks { get; set; }
}

internal sealed class TrackRelationship
{
    public List<TrackData>? Data { get; set; }
}

internal sealed class TrackData
{
    // Type discriminates what the entry is ("songs" vs. "music-videos", etc.);
    public string? Type { get; set; }
    public TrackAttributes? Attributes { get; set; }
}

internal sealed class TrackAttributes
{
    public string? Name { get; set; }
    public string? ArtistName { get; set; }
    public string? Url { get; set; }
    public TrackArtwork? Artwork { get; set; }
}

// Artwork.Url is Apple's templated artwork URL with {w}/{h} placeholders;
// ToTrack replaces them with concrete pixel sizes.
internal sealed class TrackArtwork
{
    public string? Url { get; set; }
}
