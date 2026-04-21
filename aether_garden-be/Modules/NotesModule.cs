using aether_garden_be.Options;
using aether_garden_be.Services.Content;

namespace aether_garden_be.Modules;

public class NotesModule : IEndpointModule
{
    public bool IsEnabled(FeatureOptions features) => features.Notes;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/notes", (IContentProvider contentProvider) => Results.Ok(contentProvider.GetNotes()));

        endpoints.MapGet("/api/notes/{slug}", (string slug, IContentProvider contentProvider) =>
        {
            var note = contentProvider.GetNoteBySlug(slug);
            return note is null ? Results.NotFound() : Results.Ok(note);
        });
    }
}
