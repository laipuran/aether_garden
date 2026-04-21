namespace aether_garden_be.Services.Content;

public record ContentLoadFailure(string FilePath, string Error);

public record ContentReloadResult(
    DateTimeOffset ReloadedAt,
    int BlogCount,
    int NotesCount,
    IReadOnlyList<ContentLoadFailure> FailedFiles
);
