using aether_garden_be.Models;
using aether_garden_be.Options;
using aether_garden_be.Services.Content;
using Microsoft.Extensions.Logging.Abstractions;

namespace aether_garden_be.Tests;

public sealed class GetRelatedContentTests
{
    private static PostDetail Post(string slug, string date, params string[] tags) =>
        new(slug, $"Title {slug}", "excerpt", date, tags.ToList(), "");

    private static MarkdownContentService Service(
        IReadOnlyList<PostDetail> blogs,
        IReadOnlyList<PostDetail> notes
    )
    {
        var options = new FakeOptionsMonitor<ContentOptions>(new ContentOptions { RelatedLimit = 4 });
        return new MarkdownContentService(blogs, notes, options, NullLogger<MarkdownContentService>.Instance);
    }

    [Fact]
    public void ReturnsNull_WhenTargetNotFound()
    {
        var service = Service([], []);

        Assert.Null(service.GetRelatedContent(ContentKind.Blog, "missing", 4));
        Assert.Null(service.GetRelatedContent(ContentKind.Note, "missing", 4));
    }

    [Fact]
    public void ReturnsEmpty_WhenNoSharedTags()
    {
        var service = Service(
            [Post("current", "2026-01-01", "linux")],
            [Post("unrelated", "2026-01-02", "music")]
        );

        var result = service.GetRelatedContent(ContentKind.Blog, "current", 4);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void MixesKinds_AndRanksByOverlapThenDate()
    {
        var service = Service(
            [
                Post("current", "2026-01-01", "linux", "arch"),
                Post("blog-b", "2026-02-01", "linux", "arch", "nvidia"),
                Post("blog-c", "2026-03-01", "linux"),
            ],
            [
                Post("note-d", "2026-04-01", "linux", "arch"),
                Post("note-e", "2025-01-01", "linux"),
                Post("note-f", "2026-05-01", "windows"),
            ]
        );

        var result = service.GetRelatedContent(ContentKind.Blog, "current", 4);

        var expected = new[]
        {
            (ContentKind.Note, "note-d"), // overlap 2, newest
            (ContentKind.Blog, "blog-b"), // overlap 2
            (ContentKind.Blog, "blog-c"), // overlap 1, newer than note-e
            (ContentKind.Note, "note-e"), // overlap 1
        };
        Assert.Equal(
            expected,
            result!.Select(item => (item.Kind, item.Slug)).ToArray()
        );
    }

    [Fact]
    public void RespectsLimit()
    {
        var service = Service(
            [Post("current", "2026-01-01", "linux")],
            [Post("a", "2026-02-01", "linux"), Post("b", "2026-03-01", "linux")]
        );

        var result = service.GetRelatedContent(ContentKind.Blog, "current", 1);

        Assert.Equal([(ContentKind.Note, "b")], result!.Select(item => (item.Kind, item.Slug)).ToArray());
    }

    [Fact]
    public void ExcludesSelfByKind_ButKeepsSameSlugInOtherKind()
    {
        var service = Service(
            [Post("dup", "2026-01-01", "linux")],
            [Post("dup", "2026-01-02", "linux")]
        );

        var result = service.GetRelatedContent(ContentKind.Blog, "dup", 4);

        Assert.Equal([(ContentKind.Note, "dup")], result!.Select(item => (item.Kind, item.Slug)).ToArray());
    }

    [Fact]
    public void MatchesSlugAndTagsCaseInsensitively()
    {
        var service = Service(
            [Post("Current", "2026-01-01", "Linux")],
            [Post("related", "2026-01-02", "linux")]
        );

        var result = service.GetRelatedContent(ContentKind.Blog, "current", 4);

        Assert.Equal([(ContentKind.Note, "related")], result!.Select(item => (item.Kind, item.Slug)).ToArray());
    }
}
