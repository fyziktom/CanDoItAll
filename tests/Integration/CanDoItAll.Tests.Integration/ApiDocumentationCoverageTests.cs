using System.Text.Json.Nodes;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ApiDocumentationCoverageTests(ApiDocumentationDocumentFixture fixture)
    : IClassFixture<ApiDocumentationDocumentFixture>
{
    private static readonly string[] Families =
    [
        "/_dev",
        "/api/access",
        "/api/agent-recruiting",
        "/api/agents",
        "/api/crm-hr",
        "/api/llm-chat-operations",
        "/api/llm-chats",
        "/api/llm-conversations",
        "/api/memory-providers",
        "/api/plugins",
        "/api/processes",
        "/api/project-structure",
        "/api/projects",
        "/api/prompt-gallery",
        "/api/runtime",
        "/api/shared-providers",
        "/api/storage-placement-recovery",
        "/api/workflows",
        "/authorized-files",
        "/managed-files",
        "/storage"
    ];

    [Theory]
    [InlineData("/_dev")]
    [InlineData("/api/access")]
    [InlineData("/api/agent-recruiting")]
    [InlineData("/api/agents")]
    [InlineData("/api/crm-hr")]
    [InlineData("/api/llm-chat-operations")]
    [InlineData("/api/llm-chats")]
    [InlineData("/api/llm-conversations")]
    [InlineData("/api/memory-providers")]
    [InlineData("/api/plugins")]
    [InlineData("/api/processes")]
    [InlineData("/api/project-structure")]
    [InlineData("/api/projects")]
    [InlineData("/api/prompt-gallery")]
    [InlineData("/api/runtime")]
    [InlineData("/api/shared-providers")]
    [InlineData("/api/storage-placement-recovery")]
    [InlineData("/api/workflows")]
    [InlineData("/authorized-files")]
    [InlineData("/managed-files")]
    [InlineData("/storage")]
    public void Every_operation_and_schema_of_the_family_is_described(string family)
    {
        var paths = fixture.Document["paths"]?.AsObject() ?? [];
        Assert.Contains(paths, path => path.Key == family || path.Key.StartsWith(family + "/", StringComparison.Ordinal));

        var gaps = OpenApiDescriptionCoverage.FindGaps(fixture.Document, family);

        Assert.True(
            gaps.Count == 0,
            $"{gaps.Count} documentation gaps in {family}:{Environment.NewLine}{string.Join(Environment.NewLine, gaps.Take(200))}");
    }

    [Fact]
    public void Every_documented_path_belongs_to_a_covered_family()
    {
        var uncovered = (fixture.Document["paths"]?.AsObject() ?? [])
            .Select(path => path.Key)
            .Where(path => !Families.Any(family =>
                path == family || path.StartsWith(family + "/", StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(uncovered);
    }
}
