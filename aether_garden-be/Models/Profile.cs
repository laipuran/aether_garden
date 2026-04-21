namespace aether_garden_be.Models;

public record Profile(
    string Name,
    string Title,
    string Bio,
    string Location,
    string School,
    string Website,
    string Github,
    List<string> Interests,
    string ContactEmail
);
