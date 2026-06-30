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
    public const long MaxImageAttachmentBytes = 10 * 1024 * 1024;

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

        await using var output = new FileStream(
            resolved.FullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous);
        await content.CopyToAsync(output, cancellationToken);

        return new AgentChatAttachmentStagingResult(relativePath, expectedContentType, sizeBytes);
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
        var name = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "image.png";
        }

        var extension = Path.GetExtension(name);
        var baseName = Path.GetFileNameWithoutExtension(name);
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var safeBaseName = new string(baseName
            .Select(character => invalidCharacters.Contains(character) ? '-' : character)
            .ToArray())
            .Trim(' ', '.', '-');
        if (string.IsNullOrWhiteSpace(safeBaseName))
        {
            safeBaseName = "image";
        }

        if (safeBaseName.Length > 80)
        {
            safeBaseName = safeBaseName[..80].Trim(' ', '.', '-');
        }

        return $"{safeBaseName}{extension.ToLowerInvariant()}";
    }
}
