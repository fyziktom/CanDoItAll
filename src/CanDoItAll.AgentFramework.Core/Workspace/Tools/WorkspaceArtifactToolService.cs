using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IWorkspaceArtifactToolService
{
    Task<WorkspaceDocumentConversionResult> ConvertDocumentToMarkdown(
        string path,
        string? outputPath = null,
        int previewCharacters = 4000);

    Task<WorkspaceSpreadsheetInspectionResult> InspectSpreadsheetFile(
        string path,
        int maxRows = 8,
        int maxColumns = 8,
        int previewCharacters = 4000);
}

public sealed class WorkspaceArtifactToolService(
    string workspaceRoot,
    IWorkspaceCommandExecutionService commandExecutionService,
    WorkspaceScopeDescriptor? workspaceScope = null) : IWorkspaceArtifactToolService
{
    private readonly WorkspacePathPolicy pathPolicy = new(workspaceRoot, workspaceScope);
    private readonly WorkspaceScopeDescriptor workspaceScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;

    public async Task<WorkspaceDocumentConversionResult> ConvertDocumentToMarkdown(
        string path,
        string? outputPath = null,
        int previewCharacters = 4000)
    {
        var resolvedOutputPath = string.IsNullOrWhiteSpace(outputPath)
            ? BuildDefaultMarkdownOutputPath(path)
            : WorkspacePathPolicy.NormalizeRelativePath(outputPath);
        var result = await commandExecutionService
            .ConvertDocumentWithMarkItDown(path, resolvedOutputPath)
            .ConfigureAwait(false);
        var preview = ReadPreview(resolvedOutputPath, previewCharacters);

        return new WorkspaceDocumentConversionResult(
            Succeeded: result.Succeeded,
            Message: result.Message,
            Receipt: result.Receipt,
            SourcePath: path,
            OutputPath: resolvedOutputPath,
            MarkdownPreview: preview.Content,
            PreviewTruncated: preview.Truncated,
            Diagnostics: BuildDiagnostics(result));
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

    private (string Content, bool Truncated) ReadPreview(string relativePath, int maxCharacters)
    {
        if (!pathPolicy.TryResolveWorkspacePath(relativePath, allowWorkspaceRoot: false, out var resolution, out _)
            || !File.Exists(resolution.FullPath))
        {
            return (string.Empty, false);
        }

        var content = File.ReadAllText(resolution.FullPath);
        return Truncate(content, maxCharacters, false);
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
