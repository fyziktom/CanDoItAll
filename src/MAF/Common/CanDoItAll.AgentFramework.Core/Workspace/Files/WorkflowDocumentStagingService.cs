using CanDoItAll.SharedKernel;
using System.ComponentModel;

namespace CanDoItAll.AgentFramework.Core;

public sealed record WorkflowDocumentStagingResult(
    [property: Description("Managed workspace-relative path to supply as document.to-markdown sourcePath.")] string RelativePath,
    [property: Description("Validated media type: application/pdf.")] string ContentType,
    [property: Description("Stored document size in bytes, at most 10485760.")] long SizeBytes);

public sealed class WorkflowDocumentStagingService(IWorkspacePathResolutionService paths) {
    public const int MaximumBytes = 10 * 1024 * 1024;
    public const string PdfContentType = "application/pdf";

    public async Task<WorkflowDocumentStagingResult> StageAsync(
        string fileName, string? contentType, long length, Stream input, CancellationToken cancellationToken) {
        if (length is <= 0 or > MaximumBytes) {
            throw new InvalidOperationException("Upload one nonempty PDF of at most 10 MiB.");
        }
        var name = fileName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(contentType, PdfContentType, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("The file extension and media type must identify a PDF.");
        }
        using var bytes = new MemoryStream((int)length);
        var buffer = new byte[81920];
        while (true) {
            var count = await input.ReadAsync(buffer, cancellationToken);
            if (count == 0) {
                break;
            }
            if (bytes.Length + count > MaximumBytes || bytes.Length + count > length) {
                throw new InvalidOperationException("The received document exceeds its declared size or upload limit.");
            }
            await bytes.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
        if (bytes.Length != length || bytes.Length < 5 || !bytes.GetBuffer().AsSpan(0, 5).SequenceEqual("%PDF-"u8)) {
            throw new InvalidOperationException("The document is incomplete or does not have a PDF signature.");
        }
        var safeName = PortablePhysicalFileNamePolicy.Encode(Path.GetFileNameWithoutExtension(name), maximumUtf8Bytes: 96).PhysicalName;
        var relativePath = $"artifacts/workflow-documents/{Guid.NewGuid():N}-{safeName}.pdf";
        var destination = paths.ResolveFilePath(relativePath, allowMissing: true);
        if (!destination.IsWorkspacePath) {
            throw new InvalidOperationException("Document uploads must remain inside the managed workspace.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(destination.FullPath)!);
        var created = false;
        try {
            await using var output = new FileStream(destination.FullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous);
            created = true;
            bytes.Position = 0;
            await bytes.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);
        } catch {
            if (created) {
                File.Delete(destination.FullPath);
            }
            throw;
        }
        return new(relativePath, PdfContentType, length);
    }
}
