using CanDoItAll.Modules.Workbench;
using System.Text.Json;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureAssetSourcePolicyTests
{
    [Fact]
    public void Structure_read_tool_data_serializes_exact_collection_counts()
    {
        var response = new ProjectStructureReadToolData(
            Guid.NewGuid(),
            "Counted project",
            new ProjectStructureCompactNode[11],
            new ProjectStructureLinkSummary[14],
            []);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.Equal(11, response.NodeCount);
        Assert.Equal(14, response.LinkCount);
        Assert.Equal(11, document.RootElement.GetProperty("nodeCount").GetInt32());
        Assert.Equal(14, document.RootElement.GetProperty("linkCount").GetInt32());
    }

    [Theory]
    [InlineData("report.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("legacy.xls", "application/vnd.ms-excel")]
    [InlineData("table.csv", "text/csv")]
    [InlineData("brief.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("legacy.doc", "application/msword")]
    [InlineData("diagram.mmd", ProjectStructureFileInteractionPolicy.MermaidMediaType)]
    [InlineData("diagram.mermaid", ProjectStructureFileInteractionPolicy.MermaidMediaType)]
    public void Media_type_policy_infers_supported_asset_extensions(string fileName, string expectedContentType)
    {
        Assert.Equal(expectedContentType, ProjectStructureAssetMediaTypePolicy.Resolve(null, fileName));
    }

    [Fact]
    public void Media_type_policy_preserves_explicit_content_type()
    {
        Assert.Equal(
            "application/custom",
            ProjectStructureAssetMediaTypePolicy.Resolve("  application/custom  ", "report.xlsx"));
    }

    [Fact]
    public async Task Workspace_reader_returns_content_within_limit()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "asset.bin");
            var expected = new byte[] { 1, 2, 3, 4 };
            await File.WriteAllBytesAsync(path, expected);

            var actual = await ProjectStructureWorkspaceAssetReader.ReadAsync(path, CancellationToken.None);

            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Workspace_reader_rejects_oversized_file_before_allocating_payload()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "oversized.bin");
            await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.SetLength(ProjectStructureAssetUploadLimits.MaximumFileBytes + 1);
            }

            var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(
                () => ProjectStructureWorkspaceAssetReader.ReadAsync(path, CancellationToken.None));

            Assert.Equal(413, exception.StatusCode);
            Assert.Equal("SourceWorkspaceFileTooLarge", exception.ErrorCode);
            Assert.True(exception.IsSafeToExpose);
            Assert.True(exception.CanRetryWithCorrectedInput);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"candoitall-asset-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
