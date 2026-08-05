using aether_garden_be.Models;
using aether_garden_be.Options;
using aether_garden_be.Services.Content;

namespace aether_garden_be.Modules;

public class NotesModule : IEndpointModule
{
    public bool IsEnabled(FeatureOptions features) => features.Notes;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/notes", (IContentProvider contentProvider) => TypedResults.Ok(contentProvider.GetNotes()));

        endpoints.MapGet("/api/notes/{slug}", (string slug, IContentProvider contentProvider) =>
        {
            var note = contentProvider.GetNoteBySlug(slug);
            if (note is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(note);
        }).Produces<PostDetail>(200).Produces(404);
    }
}
