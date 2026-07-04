using System.Text;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureAgentRuntimeAssetContentSanitizerTests
{
    [Fact]
    public void BoundForAgentRuntime_omits_image_base64_and_points_to_image_tools()
    {
        var content = new ProjectStructureAssetContentDescriptor(
            CreateAsset(ProjectObjectType.ImageAsset, "image/png", "managed-files/project-media/images/calculator/proposal.png"),
            ContentLength: 250_000,
            Base64Data: Convert.ToBase64String(new byte[512]));

        var bounded = ProjectStructureAgentRuntimeAssetContentSanitizer.BoundForAgentRuntime(content);

        Assert.True(bounded.Base64DataOmitted);
        Assert.Empty(bounded.Base64Data);
        Assert.Contains("image/png", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.Contains("workspace_inspect_image", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image", bounded.ContentSummary, StringComparison.Ordinal);
        Assert.Contains("managed-files/project-media/images/calculator/proposal.png", bounded.ContentSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundForAgentRuntime_keeps_small_non_media_base64()
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
        Assert.Contains("small non-media asset", bounded.ContentSummary, StringComparison.Ordinal);
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
