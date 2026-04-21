namespace aether_garden_be.Models;

public record PostDetail(
    string Slug,
    string Title,
    string Excerpt,
    string Date,
    List<string> Tags,
    string Markdown,
    List<string> Content
)
{
    public static PostSummary ToSummary(PostDetail item) =>
        new(item.Slug, item.Title, item.Excerpt, item.Date, item.Tags);
}
