using aether_garden_be.Models;

namespace aether_garden_be.Services.Content;

public interface IContentProvider
{
    IReadOnlyList<PostSummary> GetBlogs();
    PostDetail? GetBlogBySlug(string slug);
    IReadOnlyList<PostSummary> GetNotes();
    PostDetail? GetNoteBySlug(string slug);
}
