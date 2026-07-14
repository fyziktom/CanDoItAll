using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureWebLinkResolverTests
{
    [Fact]
    public void TryResolve_returns_an_embeddable_https_link_from_typed_metadata()
    {
        var node = CreateNode(
            ProjectObjectType.Link,
            new ProjectObjectMetadataEnvelope
            {
                Link = new ProjectLinkMetadata
                {
                    Url = "https://docs.example.com/guides/start"
                }
            });

        bool succeeded = ProjectStructureWebLinkResolver.TryResolve(node, out ProjectStructureWebLink? link);

        Assert.True(succeeded);
        ProjectStructureWebLink resolved = Assert.IsType<ProjectStructureWebLink>(link);
        Assert.Equal(new Uri("https://docs.example.com/guides/start"), resolved.Uri);
        Assert.Equal("Web link", resolved.SourceLabel);
        Assert.True(resolved.CanEmbed);
        Assert.Empty(resolved.EmbedUnavailableReason);
    }

    [Fact]
    public void TryResolve_marks_a_GitHub_repository_as_non_embeddable()
    {
        var node = CreateNode(
            ProjectObjectType.Repository,
            new ProjectObjectMetadataEnvelope
            {
                Repository = new ProjectRepositoryMetadata
                {
                    RepositoryUrl = "https://github.com/example/project"
                }
            });

        bool succeeded = ProjectStructureWebLinkResolver.TryResolve(node, out ProjectStructureWebLink? link);

        Assert.True(succeeded);
        ProjectStructureWebLink resolved = Assert.IsType<ProjectStructureWebLink>(link);
        Assert.Equal("Repository", resolved.SourceLabel);
        Assert.False(resolved.CanEmbed);
        Assert.Contains("GitHub", resolved.EmbedUnavailableReason, StringComparison.Ordinal);
        Assert.Contains("browser", resolved.EmbedUnavailableReason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("javascript:alert('unsafe')")]
    [InlineData("file:///C:/private/report.html")]
    [InlineData("http://example.com/insecure")]
    [InlineData("https://user:password@example.com/private")]
    public void TryResolve_rejects_unsafe_link_metadata(string value)
    {
        var node = CreateNode(
            ProjectObjectType.Link,
            new ProjectObjectMetadataEnvelope
            {
                Link = new ProjectLinkMetadata
                {
                    Url = value
                }
            });

        bool succeeded = ProjectStructureWebLinkResolver.TryResolve(node, out ProjectStructureWebLink? link);

        Assert.False(succeeded);
        Assert.Null(link);
    }

    [Fact]
    public void TryResolve_allows_http_for_a_loopback_environment_only()
    {
        var node = CreateNode(
            ProjectObjectType.Environment,
            new ProjectObjectMetadataEnvelope
            {
                Environment = new ProjectEnvironmentMetadata
                {
                    LocalhostUrl = "http://localhost:5173/health"
                }
            });

        bool succeeded = ProjectStructureWebLinkResolver.TryResolve(node, out ProjectStructureWebLink? link);

        Assert.True(succeeded);
        ProjectStructureWebLink resolved = Assert.IsType<ProjectStructureWebLink>(link);
        Assert.True(resolved.Uri.IsLoopback);
        Assert.True(resolved.CanEmbed);
    }

    private static ProjectStructureNode CreateNode(
        ProjectObjectType objectType,
        ProjectObjectMetadataEnvelope metadata)
        => new(
            "node-1",
            "project:1",
            objectType,
            objectType.ToString(),
            "Node",
            string.Empty,
            "Planned",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("rect", "accent", "link", string.Empty),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0,
            MetadataJson: ProjectObjectMetadataSerializer.Serialize(metadata));
}
