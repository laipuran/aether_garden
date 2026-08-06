namespace aether_garden_be.Models;

public record GithubRepo(
    string Name,
    string Description,
    string Url,
    string Language,
    int Stars,
    int Contributions,
    bool IsOwned
);
