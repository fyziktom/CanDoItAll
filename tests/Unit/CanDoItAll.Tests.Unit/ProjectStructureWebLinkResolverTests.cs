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

    [Fact]
    public void TryResolve_marks_Google_as_non_embeddable()
    {
        var node = CreateNode(
            ProjectObjectType.Link,
            new ProjectObjectMetadataEnvelope
            {
                Link = new ProjectLinkMetadata
                {
                    Url = "https://google.com/"
                }
            });

        bool succeeded = ProjectStructureWebLinkResolver.TryResolve(node, out ProjectStructureWebLink? link);

        Assert.True(succeeded);
        ProjectStructureWebLink resolved = Assert.IsType<ProjectStructureWebLink>(link);
        Assert.False(resolved.CanEmbed);
        Assert.Contains("Google", resolved.EmbedUnavailableReason, StringComparison.Ordinal);
        Assert.Contains("browser tab", resolved.EmbedUnavailableReason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://www.google.com/maps/embed?pb=map")]
    [InlineData("https://www.google.com/maps/embed/v1/place?q=La+Paz")]
    [InlineData("https://docs.google.com/document/d/document-id/preview")]
    [InlineData("https://docs.google.com/spreadsheets/d/sheet-id/preview?widget=true")]
    [InlineData("https://www.google.com/recaptcha/api2/anchor?k=site-key")]
    [InlineData("https://www.google.com/recaptcha/api2/bframe?k=site-key")]
    [InlineData("https://www.google.co.uk/recaptcha/enterprise/anchor?k=site-key")]
    public void TryResolve_allows_known_Google_embed_endpoints(string url)
    {
        ProjectStructureWebLink resolved = ResolveLink(url);

        Assert.True(resolved.CanEmbed);
        Assert.Empty(resolved.EmbedUnavailableReason);
    }

    [Theory]
    [InlineData("https://www.google.com/")]
    [InlineData("https://accounts.google.com/signin")]
    [InlineData("https://www.google.co.uk/search?q=test")]
    [InlineData("https://maps.google.de./maps")]
    [InlineData("https://www.google.com./search?q=test")]
    public void TryResolve_blocks_generic_Google_hosts_including_country_and_trailing_dot_domains(string url)
    {
        ProjectStructureWebLink resolved = ResolveLink(url);

        Assert.False(resolved.CanEmbed);
        Assert.Contains("Google", resolved.EmbedUnavailableReason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://google.com.example.test/")]
    [InlineData("https://www.google.co.uk.example.test/search")]
    [InlineData("https://docs.google.com.example.test/document/d/id/preview")]
    public void TryResolve_does_not_treat_lookalike_hosts_as_Google(string url)
    {
        ProjectStructureWebLink resolved = ResolveLink(url);

        Assert.True(resolved.CanEmbed);
        Assert.Empty(resolved.EmbedUnavailableReason);
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

    private static ProjectStructureWebLink ResolveLink(string url)
    {
        var node = CreateNode(
            ProjectObjectType.Link,
            new ProjectObjectMetadataEnvelope
            {
                Link = new ProjectLinkMetadata
                {
                    Url = url
                }
            });

        bool succeeded = ProjectStructureWebLinkResolver.TryResolve(node, out ProjectStructureWebLink? link);

        Assert.True(succeeded);
        return Assert.IsType<ProjectStructureWebLink>(link);
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
