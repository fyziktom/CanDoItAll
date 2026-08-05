using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IWorkspaceArtifactToolService
{
    Task<WorkspaceDocumentConversionResult> ConvertDocumentToMarkdown(
        string path,
        string? outputPath = null,
        int previewCharacters = 4000,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSpreadsheetInspectionResult> InspectSpreadsheetFile(
        string path,
        int maxRows = 8,
        int maxColumns = 8,
        int previewCharacters = 4000);

    Task<WorkspaceImageInspectionResult> InspectImageFile(string path);

    Task<WorkspaceImageContentResult> ReadImageFile(
        string path,
        long maxBytes = 10 * 1024 * 1024,
        string operationName = "workspace_analyze_image");
}

public sealed class WorkspaceArtifactToolService(
    string workspaceRoot,
    IWorkspaceCommandExecutionService commandExecutionService,
    IWorkspaceDocumentMarkdownConverter documentMarkdownConverter,
    WorkspaceScopeDescriptor? workspaceScope,
    IWorkspaceImageOperationService imageOperationService) : IWorkspaceArtifactToolService
{
    private readonly WorkspacePathPolicy pathPolicy = new(workspaceRoot, workspaceScope);
    private readonly WorkspaceFileReceiptWriter receiptWriter = new(workspaceRoot, workspaceScope);
    private readonly WorkspaceScopeDescriptor workspaceScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
    private readonly IWorkspaceImageOperationService imageOperationService = imageOperationService
        ?? throw new ArgumentNullException(nameof(imageOperationService));

    public WorkspaceArtifactToolService(
        string workspaceRoot,
        IWorkspaceCommandExecutionService commandExecutionService,
        IWorkspaceDocumentMarkdownConverter documentMarkdownConverter,
        WorkspaceScopeDescriptor? workspaceScope = null)
        : this(
            workspaceRoot,
            commandExecutionService,
            documentMarkdownConverter,
            workspaceScope,
            new WorkspaceImageOperationService(workspaceRoot, workspaceScope))
    {
    }

    public async Task<WorkspaceDocumentConversionResult> ConvertDocumentToMarkdown(
        string path,
        string? outputPath = null,
        int previewCharacters = 4000,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startedAtUtc = DateTimeOffset.UtcNow;
        var resolvedOutputPath = string.IsNullOrWhiteSpace(outputPath)
            ? BuildDefaultMarkdownOutputPath(path)
            : WorkspacePathPolicy.NormalizeRelativePath(outputPath);
        if (LooksLikeImagePath(path))
        {
            var message = $"'{path}' is an image asset. Use workspace_inspect_image or workspace_analyze_image for visual evidence instead of workspace_convert_document.";
            return CreateDocumentConversionResult(
                succeeded: false,
                outcome: "Failed",
                message,
                path,
                resolvedOutputPath,
                diagnostics: message,
                startedAtUtc,
                targetPaths: [path]);
        }

        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var sourceResolution, out var sourceValidationMessage))
        {
            return CreateDocumentConversionResult(
                succeeded: false,
                outcome: "Denied",
                sourceValidationMessage,
                path,
                resolvedOutputPath,
                diagnostics: sourceValidationMessage,
                startedAtUtc,
                targetPaths: [path]);
        }

        if (!File.Exists(sourceResolution.FullPath))
        {
            var missingMessage = $"Document file '{sourceResolution.DisplayPath}' was not found.";
            return CreateDocumentConversionResult(
                succeeded: false,
                outcome: "Failed",
                missingMessage,
                sourceResolution.RelativePath,
                resolvedOutputPath,
                diagnostics: missingMessage,
                startedAtUtc,
                targetPaths: [sourceResolution.RelativePath]);
        }

        if (!pathPolicy.TryResolveWorkspacePath(resolvedOutputPath, allowWorkspaceRoot: false, out var outputResolution, out var outputValidationMessage))
        {
            return CreateDocumentConversionResult(
                succeeded: false,
                outcome: "Denied",
                outputValidationMessage,
                sourceResolution.RelativePath,
                resolvedOutputPath,
                diagnostics: outputValidationMessage,
                startedAtUtc,
                targetPaths: [sourceResolution.RelativePath, resolvedOutputPath]);
        }

        var conversion = await documentMarkdownConverter
            .ConvertToMarkdownAsync(new WorkspaceDocumentMarkdownConversionRequest(
                sourceResolution.FullPath), cancellationToken)
            .ConfigureAwait(false);
        if (!conversion.Succeeded)
        {
            return CreateDocumentConversionResult(
                succeeded: false,
                outcome: "Failed",
                conversion.Message,
                sourceResolution.RelativePath,
                outputResolution.RelativePath,
                conversion.Diagnostics,
                startedAtUtc,
                targetPaths: [sourceResolution.RelativePath, outputResolution.RelativePath]);
        }

        if (conversion.IsTruncated || conversion.TotalMarkdownCharacters != conversion.Markdown.Length)
        {
            var incompleteMessage = "Document converter returned incomplete markdown even though the artifact service requested the full content.";
            return CreateDocumentConversionResult(
                succeeded: false,
                outcome: "Failed",
                incompleteMessage,
                sourceResolution.RelativePath,
                outputResolution.RelativePath,
                diagnostics: incompleteMessage,
                startedAtUtc,
                targetPaths: [sourceResolution.RelativePath, outputResolution.RelativePath]);
        }

        await WriteMarkdownAtomicallyAsync(
            outputResolution.FullPath,
            conversion.Markdown,
            cancellationToken).ConfigureAwait(false);

        var preview = Truncate(conversion.Markdown, previewCharacters, alreadyTruncated: false);
        var receipt = CreateDocumentConversionReceipt(
            succeeded: true,
            outcome: "Succeeded",
            conversion.Message,
            startedAtUtc,
            [sourceResolution.RelativePath, outputResolution.RelativePath]);

        return new WorkspaceDocumentConversionResult(
            Succeeded: true,
            Message: conversion.Message,
            Receipt: receipt,
            SourcePath: sourceResolution.RelativePath,
            OutputPath: outputResolution.RelativePath,
            MarkdownPreview: preview.Content,
            PreviewTruncated: preview.Truncated,
            Diagnostics: conversion.Diagnostics);
    }

    public async Task<WorkspaceSpreadsheetInspectionResult> InspectSpreadsheetFile(
        string path,
        int maxRows = 8,
        int maxColumns = 8,
        int previewCharacters = 4000)
    {
        var result = await commandExecutionService
            .InspectSpreadsheetPreview(path, maxRows, maxColumns)
            .ConfigureAwait(false);
        var preview = Truncate(result.StdoutPreview, previewCharacters, result.StdoutTruncated);

        return new WorkspaceSpreadsheetInspectionResult(
            Succeeded: result.Succeeded,
            Message: result.Message,
            Receipt: result.Receipt,
            Path: path,
            Preview: preview.Content,
            PreviewTruncated: preview.Truncated,
            Diagnostics: BuildDiagnostics(result));
    }

    public Task<WorkspaceImageInspectionResult> InspectImageFile(string path)
        => imageOperationService.InspectImageFile(path);

    public Task<WorkspaceImageContentResult> ReadImageFile(
        string path,
        long maxBytes = 10 * 1024 * 1024,
        string operationName = "workspace_analyze_image")
        => imageOperationService.ReadImageFile(path, maxBytes, operationName);

    private static async Task WriteMarkdownAtomicallyAsync(
        string outputPath,
        string markdown,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new InvalidOperationException($"Document output path '{outputPath}' does not include a directory.");
        }

        Directory.CreateDirectory(outputDirectory);
        var temporaryPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, markdown, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(outputPath))
            {
                File.Replace(
                    temporaryPath,
                    outputPath,
                    destinationBackupFileName: null,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, outputPath);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static (string Content, bool Truncated) Truncate(string content, int maxCharacters, bool alreadyTruncated)
    {
        var limit = Math.Max(0, maxCharacters);
        if (content.Length <= limit)
        {
            return (content, alreadyTruncated);
        }

        return (content[..limit], true);
    }

    private string BuildDefaultMarkdownOutputPath(string sourcePath)
    {
        var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        var slug = Slugify(sourceName);
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{slug}.md";
        return workspaceScope.CombineArtifactPath("converted-documents", fileName);
    }

    private WorkspaceDocumentConversionResult CreateDocumentConversionResult(
        bool succeeded,
        string outcome,
        string message,
        string sourcePath,
        string outputPath,
        string diagnostics,
        DateTimeOffset startedAtUtc,
        IReadOnlyList<string> targetPaths)
    {
        var receipt = receiptWriter.CreateReceipt(
            "workspace_convert_document",
            mutatesWorkspace: false,
            outcome,
            message,
            receiptRelativePath: string.Empty,
            targetPaths,
            artifactReferences: [],
            startedAtUtc);

        return new WorkspaceDocumentConversionResult(
            Succeeded: succeeded,
            Message: message,
            Receipt: receipt,
            SourcePath: sourcePath,
            OutputPath: outputPath,
            MarkdownPreview: string.Empty,
            PreviewTruncated: false,
            Diagnostics: diagnostics);
    }

    private WorkspaceToolReceipt CreateDocumentConversionReceipt(
        bool succeeded,
        string outcome,
        string message,
        DateTimeOffset startedAtUtc,
        IReadOnlyList<string> targetPaths)
    {
        if (succeeded)
        {
            var artifacts = receiptWriter.BuildTargetArtifactReferences(
                targetPaths.Skip(1),
                "workspace_convert_document");
            return receiptWriter.WriteMutationReceipt(
                "workspace_convert_document",
                message,
                targetPaths,
                artifacts,
                startedAtUtc);
        }

        return receiptWriter.CreateReceipt(
            "workspace_convert_document",
            mutatesWorkspace: false,
            outcome,
            message,
            receiptRelativePath: string.Empty,
            targetPaths,
            artifactReferences: [],
            startedAtUtc);
    }

    private static bool LooksLikeImagePath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDiagnostics(WorkspaceCommandExecutionResult result)
    {
        var diagnostics = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.StderrPreview))
        {
            diagnostics.Add(result.StderrPreview);
        }

        if (!result.Succeeded && !string.IsNullOrWhiteSpace(result.StdoutPreview))
        {
            diagnostics.Add(result.StdoutPreview);
        }

        return string.Join(Environment.NewLine, diagnostics);
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "document";
        }

        var characters = value
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var collapsed = new string(characters).Trim('-');
        while (collapsed.Contains("--", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(collapsed) ? "document" : collapsed;
    }
}
