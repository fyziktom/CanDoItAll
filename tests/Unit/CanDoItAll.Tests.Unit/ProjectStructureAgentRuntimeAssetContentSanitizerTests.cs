using System.Text;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureAgentRuntimeAssetContentSanitizerTests
{
    [Fact]
    public void BoundForAgentRuntime_omits_image_base64_and_points_to_project_authorized_image_tool()
    {
        var content = new ProjectStructureAssetContentDescriptor(
            CreateAsset(ProjectObjectType.ImageAsset, "image/png", "managed-files/project-media/images/calculator/proposal.png"),
            ContentLength: 250_000,
            Base64Data: Convert.ToBase64String(new byte[512]));

        var bounded = ProjectStructureAgentRuntimeAssetContentSanitizer.BoundForAgentRuntime(content);

        Assert.True(bounded.Base64DataOmitted);
        Assert.Empty(bounded.Base64Data);
        Assert.Contains("image/png", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_image_analyze", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.Contains(content.Asset.ProjectId.ToString("D"), bounded.ContentSummary, StringComparison.Ordinal);
        Assert.Contains(content.Asset.NodeId, bounded.ContentSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_analyze_image", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.DoesNotContain(content.Asset.MediaRelativePath, bounded.ContentSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundForAgentRuntime_keeps_small_safe_text_base64()
    {
        var textBytes = Encoding.UTF8.GetBytes("short release note");
        var base64 = Convert.ToBase64String(textBytes);
        var content = new ProjectStructureAssetContentDescriptor(
            CreateAsset(ProjectObjectType.File, "text/plain", "managed-files/project-media/files/readme.txt"),
            textBytes.Length,
            base64);

        var bounded = ProjectStructureAgentRuntimeAssetContentSanitizer.BoundForAgentRuntime(content);

        Assert.False(bounded.Base64DataOmitted);
        Assert.Equal(base64, bounded.Base64Data);
    }

    [Theory]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("application/zip")]
    public void BoundForAgentRuntime_omits_small_package_binary_base64(string contentType)
    {
        byte[] packageBytes = [0x50, 0x4B, 0x03, 0x04];
        var content = new ProjectStructureAssetContentDescriptor(
            CreateAsset(ProjectObjectType.File, contentType, "managed-files/project-media/files/package.bin"),
            packageBytes.Length,
            Convert.ToBase64String(packageBytes));

        var bounded = ProjectStructureAgentRuntimeAssetContentSanitizer.BoundForAgentRuntime(content);

        Assert.True(bounded.Base64DataOmitted);
        Assert.Empty(bounded.Base64Data);
    }

    [Fact]
    public void BoundForAgentRuntime_omits_pdf_base64_and_points_to_document_conversion()
    {
        var content = new ProjectStructureAssetContentDescriptor(
            CreateAsset(ProjectObjectType.File, "application/pdf", "managed-files/project-media/files/calculator/quote.pdf"),
            ContentLength: 420_000,
            Base64Data: Convert.ToBase64String(new byte[512]));

        var bounded = ProjectStructureAgentRuntimeAssetContentSanitizer.BoundForAgentRuntime(content);

        Assert.True(bounded.Base64DataOmitted);
        Assert.Empty(bounded.Base64Data);
        Assert.Contains("application/pdf", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.Contains("workspace_convert_document", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_analyze_image", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.Contains("managed-files/project-media/files/calculator/quote.pdf", bounded.ContentSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundForAgentRuntime_routes_svg_to_project_authorized_text_tool()
    {
        var content = new ProjectStructureAssetContentDescriptor(
            CreateAsset(ProjectObjectType.ImageAsset, "image/svg+xml", "managed-files/project-media/images/calculator/proposal.svg"),
            ContentLength: 7_159,
            Base64Data: Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg/>")));

        var bounded = ProjectStructureAgentRuntimeAssetContentSanitizer.BoundForAgentRuntime(content);

        Assert.True(bounded.Base64DataOmitted);
        Assert.Contains("project_structure_asset_text_get", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.Contains("inert text", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_analyze_image", bounded.ContentSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundForAgentRuntime_reports_image_analysis_permission_boundary()
    {
        var content = new ProjectStructureAssetContentDescriptor(
            CreateAsset(ProjectObjectType.ImageAsset, "image/png", "artifacts/process-runs/run/browser/calculator.png"),
            ContentLength: 250_000,
            Base64Data: Convert.ToBase64String(new byte[512]));

        var bounded = ProjectStructureAgentRuntimeAssetContentSanitizer.BoundForAgentRuntime(
            content,
            canTransformArtifacts: false);

        Assert.Contains("lacks artifact-transformation access", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.DoesNotContain(content.Asset.MediaRelativePath, bounded.ContentSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_analyze_image", bounded.ContentSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundForAgentRuntime_reports_document_conversion_permission_boundary_instead_of_naming_an_unavailable_tool()
    {
        var content = new ProjectStructureAssetContentDescriptor(
            CreateAsset(ProjectObjectType.File, "application/pdf", "managed-files/project-media/files/calculator/quote.pdf"),
            ContentLength: 420_000,
            Base64Data: Convert.ToBase64String(new byte[512]));

        var bounded = ProjectStructureAgentRuntimeAssetContentSanitizer.BoundForAgentRuntime(
            content,
            canTransformArtifacts: false);

        Assert.True(bounded.Base64DataOmitted);
        Assert.Contains("lacks artifact-transformation access", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_convert_document", bounded.ContentSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundForAgentRuntime_routes_large_markdown_to_project_authorized_text_tool_without_transform_access()
    {
        var content = new ProjectStructureAssetContentDescriptor(
            CreateAsset(ProjectObjectType.File, "text/markdown", "managed-files/project-media/files/calculator/summary.md"),
            ContentLength: 120_000,
            Base64Data: Convert.ToBase64String(new byte[512]));

        var bounded = ProjectStructureAgentRuntimeAssetContentSanitizer.BoundForAgentRuntime(
            content,
            canTransformArtifacts: false);

        Assert.True(bounded.Base64DataOmitted);
        Assert.Contains("project_structure_asset_text_get", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_convert_document", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("lacks artifact-transformation access", bounded.ContentSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void AssetTextReader_reads_svg_as_bounded_utf8_text()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>Calculator</text></svg>";
        var bytes = Encoding.UTF8.GetBytes(svg);
        var content = new ProjectStructureAssetBinaryContent(
            CreateAsset(ProjectObjectType.ImageAsset, "image/svg+xml", "managed-files/project-media/images/calculator/proposal.svg"),
            bytes);

        var result = ProjectStructureAgentRuntimeAssetTextReader.Read(content);

        Assert.Equal(svg, result.TextContent);
        Assert.Equal(svg.Length, result.CharacterCount);
        Assert.False(result.IsTruncated);
    }

    [Fact]
    public void ImageAssetPolicy_accepts_png_bytes_without_a_workspace_path()
    {
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];
        var content = new ProjectStructureAssetBinaryContent(
            CreateAsset(ProjectObjectType.ImageAsset, "image/png", "artifacts/process-runs/run/browser/calculator.png"),
            bytes);

        var source = ProjectStructureAgentRuntimeImageAssetPolicy.CreateAnalysisSource(content);

        Assert.Equal("image/png", source.ContentType);
        Assert.Equal("calculator.png", source.Name);
        Assert.Same(bytes, source.Bytes);
    }

    private static ProjectStructureAssetDescriptor CreateAsset(
        ProjectObjectType objectType,
        string contentType,
        string mediaRelativePath)
    {
        return new ProjectStructureAssetDescriptor(
            ProjectId: Guid.Parse("3324868f-66e2-478a-bb8f-14f32a5db1e9"),
            NodeId: "custom:asset",
            ObjectType: objectType,
            ObjectSubtype: "generated",
            Title: "Asset",
            Subtitle: "Runtime asset",
            Route: "project://asset",
            MediaRelativePath: mediaRelativePath,
            MediaContentType: contentType,
            MediaOriginalFileName: Path.GetFileName(mediaRelativePath),
            MetadataJson: "{}",
            IsReadonly: false,
            RevisionParentNodeId: string.Empty);
    }
}
