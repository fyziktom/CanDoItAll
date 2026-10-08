using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowDocumentStagingTests : IDisposable {
    private readonly string root = TestFileSystem.CreateTemporaryRoot("workflow-document");
    private static readonly byte[] Pdf = "%PDF-1.7\n%%EOF"u8.ToArray();

    [Fact]
    public async Task Upload_preserves_bytes_and_ignores_both_platforms_client_directories() {
        var service = new WorkflowDocumentStagingService(new Paths(root));
        using var input = new MemoryStream(Pdf);
        var result = await service.StageAsync("../../private\\menu.pdf", "application/pdf", Pdf.Length, input, default);
        Assert.StartsWith("artifacts/workflow-documents/", result.RelativePath, StringComparison.Ordinal);
        Assert.DoesNotContain("..", result.RelativePath, StringComparison.Ordinal);
        Assert.DoesNotContain("private", result.RelativePath, StringComparison.Ordinal);
        Assert.Equal(Pdf, await File.ReadAllBytesAsync(Path.Combine(root, result.RelativePath)));
    }

    [Theory]
    [InlineData("menu.txt", "application/pdf", "%PDF-1.7", 8)]
    [InlineData("menu.pdf", "text/plain", "%PDF-1.7", 8)]
    [InlineData("menu.pdf", "application/pdf", "<script>", 8)]
    [InlineData("menu.pdf", "application/pdf", "%PDF-1.7", 7)]
    [InlineData("menu.pdf", "application/pdf", "%PDF-1.7", 9)]
    [InlineData("menu.pdf", "application/pdf", "", 0)]
    [InlineData("menu.pdf", "application/pdf", "%PDF-1.7", 10485761)]
    public async Task Invalid_upload_creates_no_files(string name, string type, string body, int length) {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(body));
        var service = new WorkflowDocumentStagingService(new Paths(root));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StageAsync(name, type, length, input, default));
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Cancelled_upload_creates_no_files() {
        using var input = new MemoryStream(Pdf);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new WorkflowDocumentStagingService(new Paths(root));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.StageAsync("menu.pdf", "application/pdf", Pdf.Length, input, cancellation.Token));
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Path_owner_cannot_redirect_upload_outside_workspace() {
        using var input = new MemoryStream(Pdf);
        var service = new WorkflowDocumentStagingService(new Paths(root, false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StageAsync("menu.pdf", "application/pdf", Pdf.Length, input, default));
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
    }

    public void Dispose() => TestFileSystem.DeleteDirectoryWithRetry(root);

    private sealed class Paths(string root, bool managed = true) : IWorkspacePathResolutionService {
        public WorkspaceResolvedPath ResolveFilePath(string path, bool allowMissing) => new(Path.Combine(root, path), path, managed);
        public WorkspaceResolvedPath ResolveDirectoryPath(string path, bool allowMissing) => throw new NotSupportedException();
    }
}
