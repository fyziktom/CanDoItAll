using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceArtifactToolServiceTests
{
    [Fact]
    public async Task ReadImageFile_uses_supplied_operation_name_for_receipt()
    {
        var root = CreateWorkspaceRoot();
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

    private static string CreateWorkspaceRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "candoitall-workspace-artifact-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
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
