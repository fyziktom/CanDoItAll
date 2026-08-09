using System.Buffers.Binary;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.AgentFramework.Core;

public interface IWorkspaceImageOperationService
{
    Task<WorkspaceImageInspectionResult> InspectImageFile(string path);

    Task<WorkspaceImageContentResult> ReadImageFile(
        string path,
        long maxBytes = 10 * 1024 * 1024,
        string operationName = "workspace_analyze_image");
}

public sealed record WorkspaceImageContentResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string Path,
    string Format,
    string ContentType,
    long SizeBytes,
    int? Width,
    int? Height,
    byte[] Bytes,
    string Diagnostics);

public sealed class WorkspaceImageOperationService : IWorkspaceImageOperationService
{
    private readonly WorkspacePathPolicy pathPolicy;
    private readonly WorkspaceFileReceiptWriter receiptWriter;
    private readonly Func<string, byte[]> readAllBytes;

    public WorkspaceImageOperationService(
        string workspaceRoot,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IExternalTargetPathRegistry? externalTargetRegistry = null)
        : this(
            workspaceRoot,
            physicalPathPolicyFactory,
            workspaceScope,
            File.ReadAllBytes,
            externalTargetRegistry)
    {
    }

    internal WorkspaceImageOperationService(
        string workspaceRoot,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        WorkspaceScopeDescriptor? workspaceScope,
        Func<string, byte[]> readAllBytes,
        IExternalTargetPathRegistry? externalTargetRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(readAllBytes);
        pathPolicy = new WorkspacePathPolicy(
            workspaceRoot,
            physicalPathPolicyFactory,
            workspaceScope,
            externalTargetRegistry);
        receiptWriter = new WorkspaceFileReceiptWriter(workspaceRoot, workspaceScope);
        this.readAllBytes = readAllBytes;
    }

    public Task<WorkspaceImageInspectionResult> InspectImageFile(string path)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var resolution, out var validationMessage))
        {
            return Task.FromResult(CreateImageInspectionResult(
                succeeded: false,
                outcome: "Denied",
                message: validationMessage,
                path,
                format: string.Empty,
                contentType: string.Empty,
                sizeBytes: 0,
                width: null,
                height: null,
                diagnostics: validationMessage,
                startedAtUtc,
                targetPaths: [path]));
        }

        if (!File.Exists(resolution.FullPath))
        {
            var missingMessage = $"Image file '{resolution.DisplayPath}' was not found.";
            return Task.FromResult(CreateImageInspectionResult(
                succeeded: false,
                outcome: "Failed",
                missingMessage,
                resolution.RelativePath,
                format: string.Empty,
                contentType: string.Empty,
                sizeBytes: 0,
                width: null,
                height: null,
                diagnostics: missingMessage,
                startedAtUtc,
                targetPaths: [resolution.RelativePath]));
        }

        var bytes = readAllBytes(resolution.FullPath);

        if (!TryReadImageMetadata(bytes, out var metadata, out var diagnostics))
        {
            return Task.FromResult(CreateImageInspectionResult(
                succeeded: false,
                outcome: "Failed",
                message: diagnostics,
                resolution.RelativePath,
                metadata.Format,
                metadata.ContentType,
                bytes.LongLength,
                metadata.Width,
                metadata.Height,
                diagnostics,
                startedAtUtc,
                targetPaths: [resolution.RelativePath]));
        }

        var successMessage = $"{metadata.Format} image {metadata.Width}x{metadata.Height}, {bytes.LongLength} bytes.";
        return Task.FromResult(CreateImageInspectionResult(
            succeeded: true,
            outcome: "Succeeded",
            successMessage,
            resolution.RelativePath,
            metadata.Format,
            metadata.ContentType,
            bytes.LongLength,
            metadata.Width,
            metadata.Height,
            diagnostics: string.Empty,
            startedAtUtc,
            targetPaths: [resolution.RelativePath]));
    }

    public Task<WorkspaceImageContentResult> ReadImageFile(
        string path,
        long maxBytes = 10 * 1024 * 1024,
        string operationName = "workspace_analyze_image")
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var normalizedOperationName = string.IsNullOrWhiteSpace(operationName)
            ? "workspace_analyze_image"
            : operationName.Trim();
        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var resolution, out var validationMessage))
        {
            return Task.FromResult(CreateImageContentResult(
                normalizedOperationName,
                succeeded: false,
                outcome: "Denied",
                message: validationMessage,
                path,
                format: string.Empty,
                contentType: string.Empty,
                sizeBytes: 0,
                width: null,
                height: null,
                bytes: [],
                diagnostics: validationMessage,
                startedAtUtc,
                targetPaths: [path]));
        }

        if (!File.Exists(resolution.FullPath))
        {
            var missingMessage = $"Image file '{resolution.DisplayPath}' was not found.";
            return Task.FromResult(CreateImageContentResult(
                normalizedOperationName,
                succeeded: false,
                outcome: "Failed",
                missingMessage,
                resolution.RelativePath,
                format: string.Empty,
                contentType: string.Empty,
                sizeBytes: 0,
                width: null,
                height: null,
                bytes: [],
                diagnostics: missingMessage,
                startedAtUtc,
                targetPaths: [resolution.RelativePath]));
        }

        var info = new FileInfo(resolution.FullPath);
        if (info.Length > maxBytes)
        {
            var sizeMessage = $"Image file '{resolution.RelativePath}' is {info.Length:N0} bytes, which exceeds the {maxBytes:N0}-byte analysis limit.";
            return Task.FromResult(CreateImageContentResult(
                normalizedOperationName,
                succeeded: false,
                outcome: "Failed",
                sizeMessage,
                resolution.RelativePath,
                format: string.Empty,
                contentType: string.Empty,
                sizeBytes: info.Length,
                width: null,
                height: null,
                bytes: [],
                diagnostics: sizeMessage,
                startedAtUtc,
                targetPaths: [resolution.RelativePath]));
        }

        var bytes = readAllBytes(resolution.FullPath);

        if (!TryReadImageMetadata(bytes, out var metadata, out var diagnostics))
        {
            return Task.FromResult(CreateImageContentResult(
                normalizedOperationName,
                succeeded: false,
                outcome: "Failed",
                message: diagnostics,
                resolution.RelativePath,
                metadata.Format,
                metadata.ContentType,
                bytes.LongLength,
                metadata.Width,
                metadata.Height,
                bytes: [],
                diagnostics,
                startedAtUtc,
                targetPaths: [resolution.RelativePath]));
        }

        var successMessage = $"{metadata.Format} image {metadata.Width}x{metadata.Height}, {bytes.LongLength} bytes loaded for analysis.";
        return Task.FromResult(CreateImageContentResult(
            normalizedOperationName,
            succeeded: true,
            outcome: "Succeeded",
            successMessage,
            resolution.RelativePath,
            metadata.Format,
            metadata.ContentType,
            bytes.LongLength,
            metadata.Width,
            metadata.Height,
            bytes,
            diagnostics: string.Empty,
            startedAtUtc,
            targetPaths: [resolution.RelativePath]));
    }

    private WorkspaceImageInspectionResult CreateImageInspectionResult(
        bool succeeded,
        string outcome,
        string message,
        string path,
        string format,
        string contentType,
        long sizeBytes,
        int? width,
        int? height,
        string diagnostics,
        DateTimeOffset startedAtUtc,
        IReadOnlyList<string> targetPaths)
    {
        var receipt = receiptWriter.CreateReceipt(
            "workspace_inspect_image",
            mutatesWorkspace: false,
            outcome,
            message,
            receiptRelativePath: string.Empty,
            targetPaths,
            artifactReferences: [],
            startedAtUtc);

        return new WorkspaceImageInspectionResult(
            Succeeded: succeeded,
            Message: message,
            Receipt: receipt,
            Path: path,
            Format: format,
            ContentType: contentType,
            SizeBytes: sizeBytes,
            Width: width,
            Height: height,
            Diagnostics: diagnostics);
    }

    private WorkspaceImageContentResult CreateImageContentResult(
        string operationName,
        bool succeeded,
        string outcome,
        string message,
        string path,
        string format,
        string contentType,
        long sizeBytes,
        int? width,
        int? height,
        byte[] bytes,
        string diagnostics,
        DateTimeOffset startedAtUtc,
        IReadOnlyList<string> targetPaths)
    {
        var receipt = receiptWriter.CreateReceipt(
            operationName,
            mutatesWorkspace: false,
            outcome,
            message,
            receiptRelativePath: string.Empty,
            targetPaths,
            artifactReferences: [],
            startedAtUtc);

        return new WorkspaceImageContentResult(
            Succeeded: succeeded,
            Message: message,
            Receipt: receipt,
            Path: path,
            Format: format,
            ContentType: contentType,
            SizeBytes: sizeBytes,
            Width: width,
            Height: height,
            Bytes: bytes,
            Diagnostics: diagnostics);
    }

    private static bool TryReadImageMetadata(
        byte[] bytes,
        out ImageMetadata metadata,
        out string diagnostics)
    {
        if (TryReadPngMetadata(bytes, out metadata))
        {
            diagnostics = string.Empty;
            return true;
        }

        if (TryReadJpegMetadata(bytes, out metadata))
        {
            diagnostics = string.Empty;
            return true;
        }

        if (TryReadGifMetadata(bytes, out metadata))
        {
            diagnostics = string.Empty;
            return true;
        }

        metadata = new ImageMetadata(string.Empty, string.Empty, null, null);
        diagnostics = "Unsupported or invalid image file. Supported inspection formats are PNG, JPEG, and GIF.";
        return false;
    }

    private static bool TryReadPngMetadata(byte[] bytes, out ImageMetadata metadata)
    {
        metadata = new ImageMetadata(string.Empty, string.Empty, null, null);
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        if (bytes.Length < 24 || !bytes.AsSpan(0, signature.Length).SequenceEqual(signature))
        {
            return false;
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        metadata = new ImageMetadata("PNG", "image/png", width, height);
        return true;
    }

    private static bool TryReadJpegMetadata(byte[] bytes, out ImageMetadata metadata)
    {
        metadata = new ImageMetadata(string.Empty, string.Empty, null, null);
        if (bytes.Length < 4 || bytes[0] != 0xff || bytes[1] != 0xd8)
        {
            return false;
        }

        var offset = 2;
        while (offset + 3 < bytes.Length)
        {
            if (bytes[offset] != 0xff)
            {
                offset++;
                continue;
            }

            while (offset < bytes.Length && bytes[offset] == 0xff)
            {
                offset++;
            }

            if (offset >= bytes.Length)
            {
                break;
            }

            var marker = bytes[offset++];
            if (marker is 0xd9 or 0xda)
            {
                break;
            }

            if (offset + 2 > bytes.Length)
            {
                break;
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
            if (segmentLength < 2 || offset + segmentLength > bytes.Length)
            {
                break;
            }

            if (IsJpegStartOfFrameMarker(marker) && segmentLength >= 7)
            {
                var height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 3, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 5, 2));
                if (width > 0 && height > 0)
                {
                    metadata = new ImageMetadata("JPEG", "image/jpeg", width, height);
                    return true;
                }
            }

            offset += segmentLength;
        }

        return false;
    }

    private static bool TryReadGifMetadata(byte[] bytes, out ImageMetadata metadata)
    {
        metadata = new ImageMetadata(string.Empty, string.Empty, null, null);
        if (bytes.Length < 10 ||
            !(bytes.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || bytes.AsSpan(0, 6).SequenceEqual("GIF89a"u8)))
        {
            return false;
        }

        var width = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6, 2));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2));
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        metadata = new ImageMetadata("GIF", "image/gif", width, height);
        return true;
    }

    private static bool IsJpegStartOfFrameMarker(byte marker)
        => marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7 or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf;

    private sealed record ImageMetadata(string Format, string ContentType, int? Width, int? Height);
}
