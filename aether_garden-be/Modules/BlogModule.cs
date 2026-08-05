using aether_garden_be.Models;
using aether_garden_be.Options;
using aether_garden_be.Services.Content;

namespace aether_garden_be.Modules;

public class BlogModule : IEndpointModule
{
    public bool IsEnabled(FeatureOptions features) => features.Blog;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/blog", (IContentProvider contentProvider) => TypedResults.Ok(contentProvider.GetBlogs()));

        endpoints.MapGet("/api/blog/{slug}", (string slug, IContentProvider contentProvider) =>
        {
            var post = contentProvider.GetBlogBySlug(slug);
            if (post is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(post);
        }).Produces<PostDetail>(200).Produces(404);
    }
}
