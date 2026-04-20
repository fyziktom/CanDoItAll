using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkspaceArtifactToolService(
    string workspaceRoot,
    IWorkspaceCommandExecutionService commandExecutionService,
    WorkspaceScopeDescriptor? workspaceScope = null) : IWorkspaceArtifactToolService
{
    private readonly WorkspacePathPolicy pathPolicy = new(workspaceRoot, workspaceScope);
    private readonly IWorkspaceCommandExecutionService commandExecutionService = commandExecutionService;
    private readonly WorkspaceScopeDescriptor workspaceScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;

    public async Task<WorkspaceDocumentConversionResult> ConvertDocumentToMarkdown(string path, string? outputPath = null, int previewCharacters = 4000, int timeoutSeconds = 300)
    {
        var sourceDisplayPath = NormalizeWorkspaceDisplayPath(path);
        var resolvedOutputPath = string.IsNullOrWhiteSpace(outputPath)
            ? BuildDefaultMarkdownOutputPath(sourceDisplayPath)
            : NormalizeWorkspaceDisplayPath(outputPath);

        var execution = await commandExecutionService.ConvertDocumentWithMarkItDown(sourceDisplayPath, resolvedOutputPath, timeoutSeconds).ConfigureAwait(false);
        var diagnostics = BuildDiagnostics(execution);
        var outputFullPath = ResolveWorkspacePath(resolvedOutputPath);
        if (!execution.Succeeded)
        {
            return new WorkspaceDocumentConversionResult(
                Succeeded: false,
                Message: BuildDocumentFailureMessage(sourceDisplayPath, execution),
                Receipt: execution.Receipt,
                SourcePath: sourceDisplayPath,
                OutputPath: resolvedOutputPath,
                MarkdownPreview: string.Empty,
                PreviewTruncated: false,
                Diagnostics: diagnostics);
        }

        if (!File.Exists(outputFullPath))
        {
            return new WorkspaceDocumentConversionResult(
                Succeeded: false,
                Message: $"markitdown completed without creating '{resolvedOutputPath}'.",
                Receipt: execution.Receipt,
                SourcePath: sourceDisplayPath,
                OutputPath: resolvedOutputPath,
                MarkdownPreview: string.Empty,
                PreviewTruncated: false,
                Diagnostics: diagnostics);
        }

        var markdown = await File.ReadAllTextAsync(outputFullPath).ConfigureAwait(false);
        var preview = TrimPreview(markdown, Math.Clamp(previewCharacters, 200, 12000), out var previewTruncated);

        return new WorkspaceDocumentConversionResult(
            Succeeded: true,
            Message: $"Converted '{sourceDisplayPath}' to '{resolvedOutputPath}'.",
            Receipt: execution.Receipt,
            SourcePath: sourceDisplayPath,
            OutputPath: resolvedOutputPath,
            MarkdownPreview: preview,
            PreviewTruncated: previewTruncated,
            Diagnostics: diagnostics);
    }

    public async Task<WorkspaceSpreadsheetInspectionResult> InspectSpreadsheetFile(string path, int maxRows = 8, int maxColumns = 8, int previewCharacters = 4000, int timeoutSeconds = 300)
    {
        var displayPath = NormalizeWorkspaceDisplayPath(path);
        var execution = await commandExecutionService.InspectSpreadsheetPreview(displayPath, maxRows, maxColumns, timeoutSeconds).ConfigureAwait(false);
        var diagnostics = BuildDiagnostics(execution);
        if (!execution.Succeeded)
        {
            return new WorkspaceSpreadsheetInspectionResult(
                Succeeded: false,
                Message: BuildSpreadsheetFailureMessage(displayPath, execution),
                Receipt: execution.Receipt,
                Path: displayPath,
                Preview: string.Empty,
                PreviewTruncated: false,
                Diagnostics: diagnostics);
        }

        if (string.IsNullOrWhiteSpace(execution.StdoutPreview))
        {
            return new WorkspaceSpreadsheetInspectionResult(
                Succeeded: false,
                Message: $"Spreadsheet '{displayPath}' did not produce any preview text.",
                Receipt: execution.Receipt,
                Path: displayPath,
                Preview: string.Empty,
                PreviewTruncated: execution.StdoutTruncated,
                Diagnostics: diagnostics);
        }

        var preview = TrimPreview(execution.StdoutPreview, Math.Clamp(previewCharacters, 200, 12000), out var previewTruncated);
        return new WorkspaceSpreadsheetInspectionResult(
            Succeeded: true,
            Message: $"Inspected spreadsheet '{displayPath}'.",
            Receipt: execution.Receipt,
            Path: displayPath,
            Preview: preview,
            PreviewTruncated: execution.StdoutTruncated || previewTruncated,
            Diagnostics: diagnostics);
    }

    private string BuildDefaultMarkdownOutputPath(string sourceDisplayPath)
    {
        var normalizedPath = sourceDisplayPath.Replace('\\', '/');
        return workspaceScope.CombineArtifactPath("converted-documents", Path.ChangeExtension(normalizedPath, ".md") ?? "document.md");
    }

    private string NormalizeWorkspaceDisplayPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ".";
        }

        return pathPolicy.ResolveAccessiblePath(path).DisplayPath;
    }

    private string ResolveWorkspacePath(string path)
    {
        return pathPolicy.ResolveAccessiblePath(path).FullPath;
    }

    private static string BuildDiagnostics(WorkspaceCommandExecutionResult execution)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(execution.StdoutPreview))
        {
            parts.Add("Stdout:" + Environment.NewLine + execution.StdoutPreview.Trim());
        }

        if (!string.IsNullOrWhiteSpace(execution.StderrPreview))
        {
            parts.Add("Stderr:" + Environment.NewLine + execution.StderrPreview.Trim());
        }

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    private static string BuildDocumentFailureMessage(string sourceDisplayPath, WorkspaceCommandExecutionResult execution)
    {
        var diagnostic = execution.StderrPreview + Environment.NewLine + execution.StdoutPreview;
        if (diagnostic.Contains("No module named markitdown", StringComparison.OrdinalIgnoreCase))
        {
            return $"Document conversion for '{sourceDisplayPath}' requires the Python package 'markitdown' in the tool host environment.";
        }

        if (execution.Message.Contains("Unable to resolve executable 'python'", StringComparison.OrdinalIgnoreCase))
        {
            return $"Document conversion for '{sourceDisplayPath}' requires Python to be installed in the tool host environment.";
        }

        return execution.Message;
    }

    private static string BuildSpreadsheetFailureMessage(string sourceDisplayPath, WorkspaceCommandExecutionResult execution)
    {
        var diagnostic = execution.StderrPreview + Environment.NewLine + execution.StdoutPreview;
        if (diagnostic.Contains("No module named openpyxl", StringComparison.OrdinalIgnoreCase))
        {
            return $"Spreadsheet inspection for '{sourceDisplayPath}' requires the Python package 'openpyxl' for .xlsx workbooks.";
        }

        if (diagnostic.Contains("No module named xlrd", StringComparison.OrdinalIgnoreCase))
        {
            return $"Spreadsheet inspection for '{sourceDisplayPath}' requires the Python package 'xlrd' for legacy .xls workbooks.";
        }

        if (execution.Message.Contains("Unable to resolve executable 'python'", StringComparison.OrdinalIgnoreCase))
        {
            return $"Spreadsheet inspection for '{sourceDisplayPath}' requires Python to be installed in the tool host environment.";
        }

        return execution.Message;
    }

    private static string TrimPreview(string content, int limit, out bool truncated)
    {
        var normalized = content.ReplaceLineEndings(Environment.NewLine).Trim();
        if (normalized.Length <= limit)
        {
            truncated = false;
            return normalized;
        }

        truncated = true;
        return normalized[..limit] + Environment.NewLine + "[truncated]";
    }
}
