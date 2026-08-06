using System.Text.Json;
using aether_garden_be.Models;
using aether_garden_be.Options;
using Microsoft.Extensions.Options;

namespace aether_garden_be.Services.Github;

public class GithubOverviewService
{
    private static readonly HashSet<string> ContributionEventTypes = new(StringComparer.Ordinal)
    {
        "PushEvent",
        "PullRequestEvent",
        "PullRequestReviewEvent",
        "PullRequestReviewCommentEvent",
        "IssuesEvent",
        "IssueCommentEvent",
        "CommitCommentEvent",
        "CreateEvent",
        "ReleaseEvent"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<GithubOptions> _options;
    private readonly RefreshGate<CachedOverview> _refresh = new();

    private CachedOverview? _cache;

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

        var cached = await _refresh.RefreshAsync(
            () => _cache,
            cache => DateTimeOffset.UtcNow < cache.ExpiresAt,
            async ct =>
            {
                var overview = await FetchOverviewAsync(username, ct);
                return overview is null
                    ? null
                    : new CachedOverview(overview, DateTimeOffset.UtcNow.Add(GetCacheTtl()));
            },
            cancellationToken,
            fresh => _cache = fresh
        );

        return cached?.Overview ?? FallbackGithub(username);
    }

    private async Task<GithubOverview?> FetchOverviewAsync(string username, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            AddHeaders(client);

            var userResponse = await client.GetAsync($"https://api.github.com/users/{username}", ct);
            if (!userResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var reposResponse = await client.GetAsync(
                $"https://api.github.com/users/{username}/repos?per_page=100",
                ct
            );
            if (!reposResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var userPayload = await userResponse.Content.ReadAsStringAsync(ct);
            var reposPayload = await reposResponse.Content.ReadAsStringAsync(ct);

            var user = JsonSerializer.Deserialize<GithubUserDto>(userPayload);
            var repos = JsonSerializer.Deserialize<List<GithubRepoDto>>(reposPayload) ?? [];
            if (user is null)
            {
                return null;
            }

            var ownRepos = repos
                .GroupBy(r => r.name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First());
            var ownLookup = ownRepos.ToDictionary(
                pair => $"{username}/{pair.Key}",
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase
            );

            var contributions = await CountContributionsAsync(username, ct);
            var excludedSet = new HashSet<string>(
                _options.CurrentValue.ExcludedRepos ?? [],
                StringComparer.OrdinalIgnoreCase
            );
            var repoCount = Math.Max(0, _options.CurrentValue.RepoCount);

            var ownPicked = contributions
                .Where(pair => ownLookup.TryGetValue(pair.Key, out _))
                .Where(pair => !IsExcluded(pair.Key, excludedSet))
                .Select(pair => new
                {
                    FullName = pair.Key,
                    Count = pair.Value,
                    Repo = ownLookup[pair.Key],
                    IsOwned = true
                })
                .ToList();

            var externalKeys = contributions
                .Keys
                .Where(key => !ownLookup.ContainsKey(key))
                .Where(key => !IsExcluded(key, excludedSet))
                .ToList();

            var externalRepos = await FetchExternalReposAsync(externalKeys, ct);

            var externalPicked = contributions
                .Where(pair => externalRepos.TryGetValue(pair.Key, out _))
                .Select(pair => new
                {
                    FullName = pair.Key,
                    Count = pair.Value,
                    Repo = externalRepos[pair.Key],
                    IsOwned = false
                })
                .ToList();

            var picked = ownPicked
                .Concat(externalPicked)
                .OrderByDescending(item => item.Count)
                .ThenByDescending(item => item.Repo.updated_at ?? string.Empty)
                .Take(repoCount)
                .Select(item => ToRepo(item.FullName, item.Repo, item.Count, item.IsOwned))
                .ToList();

            if (picked.Count == 0)
            {
                picked = ownRepos
                    .Values
                    .Where(repo => !excludedSet.Contains(repo.name))
                    .OrderByDescending(repo => repo.updated_at ?? string.Empty)
                    .Take(repoCount)
                    .Select(repo => ToRepo($"{username}/{repo.name}", repo, 0, true))
                    .ToList();
            }

            return new GithubOverview(
                Username: user.login,
                ProfileUrl: user.html_url,
                PublicRepos: user.public_repos,
                Followers: user.followers,
                Following: user.following,
                Picked: picked,
                Source: "github-live"
            );
        }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<string, int>> CountContributionsAsync(string username, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        AddHeaders(client);

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var pages = Math.Max(1, _options.CurrentValue.EventPages);

        for (var page = 1; page <= pages; page++)
        {
            var response = await client.GetAsync(
                $"https://api.github.com/users/{username}/events/public?per_page=100&page={page}",
                ct
            );
            if (!response.IsSuccessStatusCode)
            {
                break;
            }

            var payload = await response.Content.ReadAsStringAsync(ct);
            var events = JsonSerializer.Deserialize<List<GithubEventDto>>(payload);
            if (events is null || events.Count == 0)
            {
                break;
            }

            foreach (var ev in events)
            {
                if (ev.type is null || !ContributionEventTypes.Contains(ev.type))
                {
                    continue;
                }

                var fullName = ev.repo?.name;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    continue;
                }

                counts[fullName] = counts.GetValueOrDefault(fullName) + 1;
            }
        }

        return counts;
    }

    private async Task<Dictionary<string, GithubRepoDto>> FetchExternalReposAsync(
        IReadOnlyCollection<string> fullNames,
        CancellationToken ct
    )
    {
        var result = new Dictionary<string, GithubRepoDto>(StringComparer.OrdinalIgnoreCase);
        if (fullNames.Count == 0)
        {
            return result;
        }

        var tasks = fullNames.Select(async fullName =>
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                AddHeaders(client);
                var response = await client.GetAsync(
                    $"https://api.github.com/repos/{fullName}",
                    ct
                );
                if (!response.IsSuccessStatusCode)
                {
                    return (FullName: fullName, Repo: null);
                }

                var payload = await response.Content.ReadAsStringAsync(ct);
                var repo = JsonSerializer.Deserialize<GithubRepoDto>(payload);
                return (fullName, repo);
            }
            catch
            {
                return (fullName, null);
            }
        });

        foreach (var (fullName, repo) in await Task.WhenAll(tasks))
        {
            if (repo is not null)
            {
                result[fullName] = repo;
            }
        }

        return result;
    }

    private static GithubRepo ToRepo(string fullName, GithubRepoDto repo, int contributions, bool isOwned) =>
        new(
            Name: isOwned ? BareName(fullName) : fullName,
            Description: repo.description ?? string.Empty,
            Url: repo.html_url,
            Language: repo.language ?? string.Empty,
            Stars: repo.stargazers_count,
            Contributions: contributions,
            IsOwned: isOwned
        );

    private static bool IsExcluded(string fullName, HashSet<string> excludedSet) =>
        excludedSet.Contains(BareName(fullName));

    private static string BareName(string fullName)
    {
        var slashIndex = fullName.IndexOf('/');
        return slashIndex >= 0 ? fullName[(slashIndex + 1)..] : fullName;
    }

    private static void AddHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Add("User-Agent", "aether-garden-site");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    private TimeSpan GetCacheTtl()
    {
        var hours = _options.CurrentValue.CacheHours;
        return TimeSpan.FromHours(Math.Max(0, hours));
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
                new("Gear", "A PPT classifier tool for class and a tool for notifying.", "https://github.com/laipuran/Gear", "C#", 6, 12, true),
                new("DailyCodes", "Daily coding snippets and practices.", "https://github.com/laipuran/DailyCodes", "C++", 0, 8, true),
                new("DuckChat", "A chat project in C++.", "https://github.com/laipuran/DuckChat", "C++", 1, 5, true)
            ],
            Source: "fallback"
        );

    // Mirror the JSON of the GitHub REST API (api.github.com), hence snake_case.
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
        int stargazers_count,
        string? updated_at
    );

    private record GithubEventDto(string? type, GithubEventRepoDto? repo);

    private record GithubEventRepoDto(string? name);

    private sealed record CachedOverview(GithubOverview Overview, DateTimeOffset ExpiresAt);
}
