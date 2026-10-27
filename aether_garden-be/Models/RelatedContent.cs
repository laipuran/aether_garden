namespace aether_garden_be.Models;

// A minimal reference to a related Post or Note, served by the related-content
// endpoints. Kind disambiguates routing on the client (/blog/:slug vs /note/:slug).
public record RelatedContent(
    ContentKind Kind,
    string Slug,
    string Title,
    string Date
);
