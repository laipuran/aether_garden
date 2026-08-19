using aether_garden_be.Models;
using aether_garden_be.Options;
using aether_garden_be.Services.Content;
using Microsoft.Extensions.Options;

namespace aether_garden_be.Modules;

public class NotesModule : IEndpointModule
{
    public bool IsEnabled(FeatureOptions features) => features.Notes;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/note", (IContentProvider contentProvider) => TypedResults.Ok(contentProvider.GetNotes()));

        endpoints.MapGet("/api/note/{slug}", (string slug, IContentProvider contentProvider) =>
        {
            var note = contentProvider.GetNoteBySlug(slug);
            if (note is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(note);
        }).Produces<PostDetail>(200).Produces(404);

        endpoints.MapGet("/api/note/{slug}/related", (string slug, IContentProvider contentProvider, IOptions<ContentOptions> options) =>
        {
            var related = contentProvider.GetRelatedContent(ContentKind.Note, slug, options.Value.RelatedLimit);
            if (related is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(related);
        }).Produces<IReadOnlyList<RelatedContent>>(200).Produces(404);
    }
}
