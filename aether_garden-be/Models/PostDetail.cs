namespace aether_garden_be.Models;

// A Post or Note as served by the content endpoints.
// Markdown is the raw source; Content is the rendered markdown reduced to
// plain-text Paragraphs (tags stripped, HTML entities decoded).
public record PostDetail(
    string Slug,
    string Title,
    string Excerpt,
    string Date,
    List<string> Tags,
    string Markdown
)
{
    public static PostSummary ToSummary(PostDetail item) =>
        new(item.Slug, item.Title, item.Excerpt, item.Date, item.Tags);
}
