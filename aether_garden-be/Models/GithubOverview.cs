namespace aether_garden_be.Models;

public record GithubOverview(
    string Username,
    string ProfileUrl,
    int PublicRepos,
    int Followers,
    int Following,
    List<GithubRepo> Picked,
    string Source
);
