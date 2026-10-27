namespace aether_garden_be.Options;

public class GithubOptions
{
    public string Username { get; set; } = "laipuran";
    public int RepoCount { get; set; } = 3;
    public string[] ExcludedRepos { get; set; } = ["aether_garden"];
    public int CacheHours { get; set; } = 1;
    public int EventPages { get; set; } = 3;
}
