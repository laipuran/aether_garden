using System.Net;
using System.Text.RegularExpressions;
using PostModel = aether_garden_be.Models.PostDetail;
using PostSummaryModel = aether_garden_be.Models.PostSummary;
using aether_garden_be.Options;
using Markdig;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace aether_garden_be.Services.Content;

public class MarkdownContentService : IContentProvider, IContentReloadService
{
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex MultiSpaceRegex = new("\\s+", RegexOptions.Compiled);

    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly IOptionsMonitor<ContentOptions> _options;
    private readonly ILogger<MarkdownContentService> _logger;
    private readonly IDeserializer _yamlDeserializer;

    private ContentSnapshot _snapshot = ContentSnapshot.Empty;

    public MarkdownContentService(IOptionsMonitor<ContentOptions> options, ILogger<MarkdownContentService> logger)
    {
        _options = options;
        _logger = logger;
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public IReadOnlyList<PostSummaryModel> GetBlogs() => _snapshot.Blogs.Select(PostModel.ToSummary).ToList();

    public PostModel? GetBlogBySlug(string slug) =>
        _snapshot.Blogs.FirstOrDefault(post => post.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<PostSummaryModel> GetNotes() => _snapshot.Notes.Select(PostModel.ToSummary).ToList();

    public PostModel? GetNoteBySlug(string slug) =>
        _snapshot.Notes.FirstOrDefault(post => post.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    public async Task<ContentReloadResult> ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _reloadLock.WaitAsync(cancellationToken);
        try
        {
            var options = _options.CurrentValue;
            var failedFiles = new List<ContentLoadFailure>();

            var blogs = LoadPosts(options.RootPath, options.BlogSubPath, failedFiles);
            var notes = LoadPosts(options.RootPath, options.NotesSubPath, failedFiles);

            _snapshot = new ContentSnapshot(blogs, notes);

            _logger.LogInformation(
                "Content reloaded: {BlogCount} blogs, {NotesCount} notes, {FailureCount} failures",
                blogs.Count,
                notes.Count,
                failedFiles.Count
            );

            return new ContentReloadResult(
                ReloadedAt: DateTimeOffset.UtcNow,
                BlogCount: blogs.Count,
                NotesCount: notes.Count,
                FailedFiles: failedFiles
            );
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private List<PostModel> LoadPosts(string rootPath, string subPath, List<ContentLoadFailure> failedFiles)
    {
        var fullPath = ResolveDirectory(rootPath, subPath);
        if (!Directory.Exists(fullPath))
        {
            _logger.LogWarning("Content directory does not exist: {Path}", fullPath);
            return [];
        }

        var bySlug = new Dictionary<string, PostModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(fullPath, "*.md", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var markdown = File.ReadAllText(file);
                var parsed = ParseMarkdown(markdown, file);

                if (!parsed.Status.Equals("published", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!bySlug.TryAdd(parsed.Model.Slug, parsed.Model))
                {
                    throw new InvalidOperationException($"Duplicate slug '{parsed.Model.Slug}'");
                }
            }
            catch (Exception ex)
            {
                failedFiles.Add(new ContentLoadFailure(file, ex.Message));
                _logger.LogWarning(ex, "Failed to parse content file {File}", file);
            }
        }

        return bySlug.Values
            .OrderByDescending(post => ParseDateOrDefault(post.Date))
            .ThenBy(post => post.Slug, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveDirectory(string rootPath, string subPath)
    {
        var resolvedRoot = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), rootPath));

        return Path.GetFullPath(Path.Combine(resolvedRoot, subPath));
    }

    private ParsedPost ParseMarkdown(string markdown, string filePath)
    {
        if (!TrySplitFrontMatter(markdown, out var frontMatter, out var body))
        {
            throw new InvalidOperationException("Missing YAML front matter");
        }

        var metadata = _yamlDeserializer.Deserialize<PostFrontMatter>(frontMatter)
                       ?? throw new InvalidOperationException("Failed to deserialize front matter");

        ValidateMetadata(metadata, filePath);

        var paragraphs = ParseParagraphs(body);
        var excerpt = string.IsNullOrWhiteSpace(metadata.Excerpt)
            ? BuildExcerpt(body)
            : metadata.Excerpt.Trim();

        var model = new PostModel(
            Slug: metadata.Slug.Trim(),
            Title: metadata.Title.Trim(),
            Excerpt: excerpt,
            Date: metadata.Date.Trim(),
            Tags: (metadata.Tags ?? []).Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).ToList(),
            Markdown: body,
            Content: paragraphs
        );

        return new ParsedPost(model, metadata.Status?.Trim() ?? "published");
    }

    private static void ValidateMetadata(PostFrontMatter metadata, string filePath)
    {
        if (string.IsNullOrWhiteSpace(metadata.Slug))
        {
            throw new InvalidOperationException($"Missing required field 'slug' in {filePath}");
        }

        if (string.IsNullOrWhiteSpace(metadata.Title))
        {
            throw new InvalidOperationException($"Missing required field 'title' in {filePath}");
        }

        if (string.IsNullOrWhiteSpace(metadata.Date))
        {
            throw new InvalidOperationException($"Missing required field 'date' in {filePath}");
        }
    }

    private static bool TrySplitFrontMatter(string markdown, out string frontMatter, out string body)
    {
        frontMatter = string.Empty;
        body = markdown;

        var normalized = markdown.Replace("\r\n", "\n");
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return false;
        }

        var markerIndex = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return false;
        }

        frontMatter = normalized[4..markerIndex];
        body = normalized[(markerIndex + 5)..].Trim();
        return true;
    }

    private static List<string> ParseParagraphs(string markdownBody)
    {
        return markdownBody
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(MarkdownBlockToPlainText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    private static string BuildExcerpt(string markdownBody)
    {
        var plain = MarkdownBlockToPlainText(markdownBody);
        return plain.Length <= 60 ? plain : $"{plain[..60].TrimEnd()}...";
    }

    private static string MarkdownBlockToPlainText(string markdown)
    {
        var html = Markdown.ToHtml(markdown);
        var noTags = HtmlTagRegex.Replace(html, " ");
        var decoded = WebUtility.HtmlDecode(noTags);
        return MultiSpaceRegex.Replace(decoded, " ").Trim();
    }

    private static DateTime ParseDateOrDefault(string date)
    {
        return DateTime.TryParse(date, out var parsed) ? parsed : DateTime.MinValue;
    }

    private record ContentSnapshot(List<PostModel> Blogs, List<PostModel> Notes)
    {
        public static ContentSnapshot Empty { get; } = new([], []);
    }

    private record PostFrontMatter
    {
        public string Slug { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Excerpt { get; init; } = string.Empty;
        public string Date { get; init; } = string.Empty;
        public List<string> Tags { get; init; } = [];
        public string Status { get; init; } = "published";
        public string UpdatedAt { get; init; } = string.Empty;
    }

    private sealed record ParsedPost(PostModel Model, string Status);
}
