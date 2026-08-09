using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentChatAttachmentStagingService
{
    Task<AgentChatAttachmentStagingResult> StageImageAsync(
        string fileName,
        string? contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken = default);
}

public sealed record AgentChatAttachmentStagingResult(
    string RelativePath,
    string ContentType,
    long SizeBytes);

public sealed class AgentChatAttachmentStagingService(IWorkspacePathResolutionService pathResolutionService)
    : IAgentChatAttachmentStagingService
{
    public const long MaxImageAttachmentBytes =
        AgentRuntimeInputAttachmentPolicy.MaximumImageBytes;

    private static readonly IReadOnlyDictionary<string, string> AllowedImageContentTypesByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".gif"] = "image/gif",
            [".jpeg"] = "image/jpeg",
            [".jpg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

    public async Task<AgentChatAttachmentStagingResult> StageImageAsync(
        string fileName,
        string? contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        if (sizeBytes <= 0)
        {
            throw new InvalidOperationException("Attachment image is empty.");
        }

        if (sizeBytes > MaxImageAttachmentBytes)
        {
            throw new InvalidOperationException(
                $"Attachment image exceeds the {MaxImageAttachmentBytes / 1024 / 1024} MB limit.");
        }

        var safeFileName = NormalizeFileName(fileName);
        var extension = Path.GetExtension(safeFileName);
        if (!AllowedImageContentTypesByExtension.TryGetValue(extension, out var expectedContentType))
        {
            throw new InvalidOperationException("Only PNG, JPEG, GIF, and WebP image attachments are supported.");
        }

        var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? expectedContentType
            : contentType.Trim().ToLowerInvariant();
        if (!string.Equals(normalizedContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Attachment content type '{normalizedContentType}' does not match supported image type '{expectedContentType}'.");
        }

        var relativePath = BuildAttachmentRelativePath(safeFileName);
        var resolved = pathResolutionService.ResolveFilePath(relativePath, allowMissing: true);
        if (!resolved.IsWorkspacePath)
        {
            throw new InvalidOperationException("Attachment upload destination must resolve inside the managed workspace.");
        }

        var directory = Path.GetDirectoryName(resolved.FullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        long copiedBytes;
        try
        {
            await using var output = new FileStream(
                resolved.FullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous);
            copiedBytes =
                await AgentRuntimeInputAttachmentPolicy.CopyBoundedAsync(
                    content,
                    output,
                    safeFileName,
                    MaxImageAttachmentBytes,
                    cancellationToken);
            await output.FlushAsync(cancellationToken);
        }
        catch
        {
            DeleteFailedStagingFile(resolved.FullPath);
            throw;
        }

        if (copiedBytes != sizeBytes)
        {
            DeleteFailedStagingFile(resolved.FullPath);
            throw new InvalidOperationException(
                $"Attachment image declared {sizeBytes:N0} bytes but supplied {copiedBytes:N0} bytes.");
        }

        return new AgentChatAttachmentStagingResult(
            relativePath,
            expectedContentType,
            copiedBytes);
    }

    private static string BuildAttachmentRelativePath(string fileName)
    {
        var stamp = DateTime.UtcNow;
        return string.Join(
            '/',
            "artifacts",
            "chat-attachments",
            stamp.ToString("yyyyMMdd"),
            $"{stamp:HHmmssfff}-{Guid.NewGuid():N}-{fileName}");
    }

    private static string NormalizeFileName(string fileName)
    {
        string[] segments = fileName.Trim().Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        string name = segments.LastOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "image.png";
        }

        var extension = Path.GetExtension(name);
        var baseName = Path.GetFileNameWithoutExtension(name);
        string safeBaseName = PortablePhysicalFileNamePolicy.Encode(
            string.IsNullOrWhiteSpace(baseName) ? "image" : baseName,
            maximumUtf8Bytes: 96).PhysicalName;
        return $"{safeBaseName}{extension.ToLowerInvariant()}";
    }

    private static void DeleteFailedStagingFile(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
