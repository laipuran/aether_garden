namespace aether_garden_be.Models;

public record PostSummary(
    string Slug,
    string Title,
    string Excerpt,
    string Date,
    List<string> Tags
);
