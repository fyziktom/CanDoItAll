using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceArtifactToolServiceTests
{
    [Fact]
    public async Task ConvertDocumentToMarkdown_rejects_image_assets_with_bounded_guidance()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var imagePath = Path.Combine(root, "managed-files", "project-media", "images", "proposal.png");
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
            await File.WriteAllBytesAsync(imagePath, MinimalPngBytes());

            var service = new WorkspaceArtifactToolService(
                root,
                new WorkspaceCommandExecutionService(root, new LocalWorkspaceProcessHost()));

            var result = await service.ConvertDocumentToMarkdown(
                "managed-files/project-media/images/proposal.png",
                previewCharacters: 1200);

            Assert.False(result.Succeeded);
            Assert.Empty(result.MarkdownPreview);
            Assert.False(result.PreviewTruncated);
            Assert.Equal("workspace_convert_document", result.Receipt.Operation);
            Assert.Contains("image asset", result.Message, StringComparison.Ordinal);
            Assert.Contains("workspace_inspect_image", result.Message, StringComparison.Ordinal);
            Assert.Contains("workspace_analyze_image", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ReadImageFile_uses_supplied_operation_name_for_receipt()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var imagePath = Path.Combine(root, "artifacts", "images", "frame.png");
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
            await File.WriteAllBytesAsync(imagePath, MinimalPngBytes());

            var service = new WorkspaceArtifactToolService(
                root,
                new WorkspaceCommandExecutionService(root, new LocalWorkspaceProcessHost()));

            var result = await service.ReadImageFile(
                "artifacts/images/frame.png",
                operationName: "workspace_analyze_images");

            Assert.True(result.Succeeded, result.Diagnostics);
            Assert.Equal("workspace_analyze_images", result.Receipt.Operation);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateWorkspaceRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "candoitall-workspace-artifact-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static byte[] MinimalPngBytes()
        =>
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00,
            0x1F, 0x15, 0xC4, 0x89,
            0x00, 0x00, 0x00, 0x0A,
            0x49, 0x44, 0x41, 0x54,
            0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00, 0x05, 0x00, 0x01,
            0x0D, 0x0A, 0x2D, 0xB4,
            0x00, 0x00, 0x00, 0x00,
            0x49, 0x45, 0x4E, 0x44,
            0xAE, 0x42, 0x60, 0x82
        ];
}
