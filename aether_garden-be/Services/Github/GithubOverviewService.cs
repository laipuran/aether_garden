using System.Text.Json;
using aether_garden_be.Models;
using aether_garden_be.Options;
using Microsoft.Extensions.Options;

namespace aether_garden_be.Services.Github;

public class GithubOverviewService : IGithubOverviewService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<GithubOptions> _options;

    public GithubOverviewService(IHttpClientFactory httpClientFactory, IOptionsMonitor<GithubOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public async Task<GithubOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        const string defaultUser = "laipuran";
        var username = _options.CurrentValue.Username;
        if (string.IsNullOrWhiteSpace(username))
        {
            username = defaultUser;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "aether-garden-site");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

            var userResponse = await client.GetAsync($"https://api.github.com/users/{username}", cancellationToken);
            if (!userResponse.IsSuccessStatusCode)
            {
                return FallbackGithub(username);
            }

            var reposResponse = await client.GetAsync(
                $"https://api.github.com/users/{username}/repos?sort=updated&per_page=3",
                cancellationToken
            );
            if (!reposResponse.IsSuccessStatusCode)
            {
                return FallbackGithub(username);
            }

            var userPayload = await userResponse.Content.ReadAsStringAsync(cancellationToken);
            var reposPayload = await reposResponse.Content.ReadAsStringAsync(cancellationToken);

            var user = JsonSerializer.Deserialize<GithubUserDto>(userPayload);
            var repos = JsonSerializer.Deserialize<List<GithubRepoDto>>(reposPayload) ?? [];

            if (user is null)
            {
                return FallbackGithub(username);
            }

            return new GithubOverview(
                Username: user.login,
                ProfileUrl: user.html_url,
                PublicRepos: user.public_repos,
                Followers: user.followers,
                Following: user.following,
                Picked: repos.Select(r => new GithubRepo(
                    Name: r.name,
                    Description: r.description ?? string.Empty,
                    Url: r.html_url,
                    Language: r.language ?? string.Empty,
                    Stars: r.stargazers_count
                )).ToList(),
                Source: "github-live"
            );
        }
        catch
        {
            return FallbackGithub(username);
        }
    }

    private static GithubOverview FallbackGithub(string username) =>
        new(
            Username: username,
            ProfileUrl: $"https://github.com/{username}",
            PublicRepos: 19,
            Followers: 15,
            Following: 19,
            Picked:
            [
                new("Gear", "A PPT classifier tool for class and a tool for notifying.", "https://github.com/laipuran/Gear", "C#", 6),
                new("DailyCodes", "Daily coding snippets and practices.", "https://github.com/laipuran/DailyCodes", "C++", 0),
                new("DuckChat", "A chat project in C++.", "https://github.com/laipuran/DuckChat", "C++", 1)
            ],
            Source: "fallback"
        );

    private record GithubUserDto(
        string login,
        string html_url,
        int public_repos,
        int followers,
        int following
    );

    private record GithubRepoDto(
        string name,
        string? description,
        string html_url,
        string? language,
        int stargazers_count
    );
}
