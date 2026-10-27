namespace aether_garden_be.Models;

// The GitHub overview exposed to the frontend. Fetched live when GitHub is reachable,
// otherwise a hardcoded Fallback with Source = "fallback".
public record GithubOverview(
    string Username,
    string ProfileUrl,
    int PublicRepos,
    int Followers,
    int Following,
    List<GithubRepo> Picked, // the repos with the most recent contribution events
    string Source // "github-live" when live, "fallback" when GitHub was unreachable
);
