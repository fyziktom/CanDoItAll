namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureAssetMediaTypePolicy
{
    public static string Resolve(string? requestedContentType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(requestedContentType))
        {
            return requestedContentType.Trim();
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".json" => "application/json",
            ".md" => "text/markdown",
            ".mmd" or ".mermaid" => ProjectStructureFileInteractionPolicy.MermaidMediaType,
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
    }
}

internal static class ProjectStructureWorkspaceAssetReader
{
    private const int BufferSize = 81920;

    public static async Task<byte[]> ReadAsync(string fullPath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            fullPath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = BufferSize,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });

        EnsureWithinLimit(stream.Length);

        using var memory = new MemoryStream((int)stream.Length);
        var buffer = new byte[BufferSize];
        long totalBytes = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalBytes += read;
            EnsureWithinLimit(totalBytes);
            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    private static void EnsureWithinLimit(long byteCount)
    {
        if (byteCount <= ProjectStructureAssetUploadLimits.MaximumFileBytes)
        {
            return;
        }

        throw ProjectStructureAgentException.CreateAgentVisible(
            413,
            "SourceWorkspaceFileTooLarge",
            $"Workspace asset sources are limited to {ProjectStructureAssetUploadLimits.MaximumFileBytes / (1024 * 1024)} MiB.",
            canRetryWithCorrectedInput: true);
    }
}
