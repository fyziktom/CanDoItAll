using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tools.Documents;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class WorkspaceSpreadsheetRuntimePlugin(
    ISpreadsheetDocumentService spreadsheets,
    string workspaceRoot,
    WorkspaceScopeDescriptor workspaceScope,
    AgentWorkspaceToolAccessSettings accessSettings)
{
    private const string SpreadsheetPreviewOperation = "workspace_spreadsheet_preview";

    private readonly ISpreadsheetDocumentService spreadsheets = spreadsheets ?? throw new ArgumentNullException(nameof(spreadsheets));
    private readonly WorkspacePathResolutionService paths = new(workspaceRoot, workspaceScope);
    private readonly WorkspaceSpreadsheetRuntimePathAccess pathAccess = new(workspaceRoot, workspaceScope, accessSettings);
    private readonly WorkspaceSpreadsheetReceiptWriter receiptWriter = new(workspaceRoot, workspaceScope);

    public SpreadsheetWorkbookSummary InspectWorkbook(string workbookPath)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var workbook = ResolveReadableWorkbook(workbookPath);
        var summary = spreadsheets.InspectWorkbook(workbook.FullPath);
        receiptWriter.Persist(
            "workspace_spreadsheet_summary",
            mutatesWorkspace: false,
            message: $"Inspected workbook '{workbook.RelativePath}'.",
            requestSummary: workbook.RelativePath,
            targetPaths: [workbook.RelativePath],
            artifactReferences: [],
            startedAtUtc);

        return summary with
        {
            WorkbookPath = workbook.RelativePath
        };
    }

    public SpreadsheetWorkbookPreviewResult PreviewWorkbook(
        string workbookPath,
        int maxWorksheets = 2,
        int maxRows = 8,
        int maxColumns = 8)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var workbook = ResolveReadableWorkbook(workbookPath);
        var result = spreadsheets.PreviewWorkbook(new SpreadsheetWorkbookPreviewRequest(
            workbook.FullPath,
            maxWorksheets,
            maxRows,
            maxColumns));
        receiptWriter.Persist(
            SpreadsheetPreviewOperation,
            mutatesWorkspace: false,
            message: $"Previewed {result.Worksheets.Count} of {result.TotalWorksheetCount} worksheet(s) from workbook '{workbook.RelativePath}'.",
            requestSummary: $"{workbook.RelativePath}, maxWorksheets={maxWorksheets}, maxRows={maxRows}, maxColumns={maxColumns}",
            targetPaths: [workbook.RelativePath],
            artifactReferences: [],
            startedAtUtc);

        return result with
        {
            WorkbookPath = workbook.RelativePath
        };
    }

    public SpreadsheetCellValue ReadSpreadsheetCell(string workbookPath, string worksheetName, string cellAddress)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var workbook = ResolveReadableWorkbook(workbookPath);
        var requestedWorksheetName = Require(worksheetName, nameof(worksheetName));
        var requestedCellAddress = Require(cellAddress, nameof(cellAddress));
        var result = spreadsheets.ReadCell(
            workbook.FullPath,
            requestedWorksheetName,
            requestedCellAddress);
        receiptWriter.Persist(
            "workspace_read_spreadsheet_cell",
            mutatesWorkspace: false,
            message: $"Read cell '{requestedWorksheetName}!{requestedCellAddress}' from workbook '{workbook.RelativePath}'.",
            requestSummary: $"{workbook.RelativePath}, {requestedWorksheetName}!{requestedCellAddress}",
            targetPaths: [workbook.RelativePath],
            artifactReferences: [],
            startedAtUtc);

        return result;
    }

    public SpreadsheetRangeReadResult ReadSpreadsheetRange(
        string workbookPath,
        string worksheetName,
        string rangeAddress,
        int maxRows = 100,
        int maxColumns = 40)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var workbook = ResolveReadableWorkbook(workbookPath);
        var requestedWorksheetName = Require(worksheetName, nameof(worksheetName));
        var requestedRangeAddress = Require(rangeAddress, nameof(rangeAddress));
        var result = spreadsheets.ReadRange(
            workbook.FullPath,
            requestedWorksheetName,
            requestedRangeAddress,
            maxRows,
            maxColumns);
        receiptWriter.Persist(
            "workspace_read_spreadsheet_range",
            mutatesWorkspace: false,
            message: $"Read range '{requestedWorksheetName}!{requestedRangeAddress}' from workbook '{workbook.RelativePath}'.",
            requestSummary: $"{workbook.RelativePath}, {requestedWorksheetName}!{requestedRangeAddress}, maxRows={maxRows}, maxColumns={maxColumns}",
            targetPaths: [workbook.RelativePath],
            artifactReferences: [],
            startedAtUtc);

        return result with
        {
            WorkbookPath = workbook.RelativePath
        };
    }

    public WorkspaceSpreadsheetWriteToolResult WriteSpreadsheetWorkbook(
        string workbookPath,
        string worksheetName,
        string? outputWorkbookPath = null,
        SpreadsheetCellWrite[]? cellWrites = null,
        SpreadsheetRangeWrite[]? rangeWrites = null,
        bool createWorkbookIfMissing = true,
        bool overwrite = false)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var requestedWorkbookPath = Require(workbookPath, nameof(workbookPath));
        var requestedWorksheetName = Require(worksheetName, nameof(worksheetName));
        var normalizedWorkbookPath = NormalizeWorkbookInputPath(requestedWorkbookPath, createWorkbookIfMissing);
        var workbook = paths.ResolveFilePath(normalizedWorkbookPath, allowMissing: createWorkbookIfMissing);
        var requestedOutputPath = string.IsNullOrWhiteSpace(outputWorkbookPath)
            ? requestedWorkbookPath
            : outputWorkbookPath.Trim();
        var normalizedOutputPath = pathAccess.PrepareFileWritePath(requestedOutputPath) ?? requestedOutputPath;
        var output = paths.ResolveFilePath(normalizedOutputPath, allowMissing: true);

        var result = spreadsheets.Write(new SpreadsheetWriteRequest(
            workbook.FullPath,
            output.FullPath,
            requestedWorksheetName,
            cellWrites ?? [],
            rangeWrites ?? [],
            createWorkbookIfMissing,
            overwrite));
        receiptWriter.Persist(
            "workspace_write_spreadsheet",
            mutatesWorkspace: true,
            message: $"Wrote workbook '{output.RelativePath}' worksheet '{result.WorksheetName}'.",
            requestSummary: $"{output.RelativePath}, worksheet={result.WorksheetName}, cellWrites={result.CellWriteCount}, rangeWrites={result.RangeWriteCount}",
            targetPaths: [output.RelativePath],
            artifactReferences: [WorkspaceSpreadsheetReceiptWriter.CreateWorkbookArtifact(output.RelativePath, "workspace_write_spreadsheet target")],
            startedAtUtc);

        return new WorkspaceSpreadsheetWriteToolResult(
            Succeeded: true,
            Message: $"Wrote workbook '{output.RelativePath}' worksheet '{result.WorksheetName}'.",
            WorkbookPath: output.RelativePath,
            WorksheetName: result.WorksheetName,
            CellWriteCount: result.CellWriteCount,
            RangeWriteCount: result.RangeWriteCount,
            Diagnostics: string.Empty);
    }

    public WorkspaceSpreadsheetFunctionCatalogResult ListSpreadsheetFunctions(
        string? query = null,
        string? category = null,
        int maxResults = 50)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var functions = SpreadsheetFunctionCatalog.List(query, category, maxResults);
        receiptWriter.Persist(
            "workspace_spreadsheet_function_catalog",
            mutatesWorkspace: false,
            message: $"Returned {functions.Count:N0} spreadsheet function descriptor(s).",
            requestSummary: $"query={query ?? string.Empty}, category={category ?? string.Empty}, maxResults={maxResults}",
            targetPaths: [],
            artifactReferences: [],
            startedAtUtc);

        return new WorkspaceSpreadsheetFunctionCatalogResult(
            Succeeded: true,
            Message: $"Returned {functions.Count:N0} spreadsheet function descriptor(s).",
            Functions: functions,
            Diagnostics: string.Empty);
    }

    private WorkspaceResolvedPath ResolveReadableWorkbook(string workbookPath)
    {
        var normalizedPath = pathAccess.PrepareFileReadPath(Require(workbookPath, nameof(workbookPath))) ?? workbookPath;
        return paths.ResolveFilePath(normalizedPath, allowMissing: false);
    }

    private string NormalizeWorkbookInputPath(string workbookPath, bool createWorkbookIfMissing)
        => createWorkbookIfMissing && !WorkbookExists(workbookPath)
            ? pathAccess.PrepareFileWritePath(workbookPath) ?? workbookPath
            : pathAccess.PrepareFileReadPath(workbookPath) ?? workbookPath;

    private bool WorkbookExists(string workbookPath)
    {
        try
        {
            var readPath = pathAccess.PrepareFileReadPath(workbookPath) ?? workbookPath;
            var resolved = paths.ResolveFilePath(readPath, allowMissing: true);
            return File.Exists(resolved.FullPath);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Spreadsheet tool argument '{name}' is required.")
            : value.Trim();
}

internal sealed record WorkspaceSpreadsheetWriteToolResult(
    bool Succeeded,
    string Message,
    string WorkbookPath,
    string WorksheetName,
    int CellWriteCount,
    int RangeWriteCount,
    string Diagnostics);

internal sealed record WorkspaceSpreadsheetFunctionCatalogResult(
    bool Succeeded,
    string Message,
    IReadOnlyList<SpreadsheetFunctionDescriptor> Functions,
    string Diagnostics);

internal sealed class WorkspaceSpreadsheetReceiptWriter(
    string workspaceRoot,
    WorkspaceScopeDescriptor workspaceScope)
{
    private const string BoundaryDescription = "Workspace spreadsheet service backed by the document tool boundary. No host process execution.";
    private const string WorkbookContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly string workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly WorkspaceScopeDescriptor workspaceScope = workspaceScope;

    public void Persist(
        string operation,
        bool mutatesWorkspace,
        string message,
        string requestSummary,
        IReadOnlyList<string> targetPaths,
        IReadOnlyList<WorkspaceArtifactReference> artifactReferences,
        DateTimeOffset startedAtUtc)
    {
        var receipt = new WorkspaceToolReceipt(
            Operation: operation,
            MutatesWorkspace: mutatesWorkspace,
            Boundary: BoundaryDescription,
            Outcome: "Succeeded",
            Message: message,
            ReceiptRelativePath: BuildReceiptRelativePath(operation),
            TargetPaths: targetPaths,
            ArtifactReferences: artifactReferences,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow)
        {
            ExecutionRunId = WorkspaceExecutionAuditContext.Current?.ExecutionRunId
        };

        WorkspaceExecutionAuditTrailWriter.PersistReceipt(
            workspaceRoot,
            workspaceScope,
            receipt,
            toolFamily: "workspace-file",
            toolName: operation,
            riskClass: mutatesWorkspace ? "MutatingWorkspace" : "ReadOnlyWorkspace",
            approvalMode: "NotRequired",
            isolationGuarantee: BoundaryDescription,
            requestSummary: requestSummary,
            workingDirectory: ".",
            exitSummary: $"Succeeded: {message}");
    }

    public static WorkspaceArtifactReference CreateWorkbookArtifact(string relativePath, string summary)
        => new(
            Zone: "workspace-file",
            RelativePath: relativePath,
            DisplayName: Path.GetFileName(relativePath),
            ContentType: WorkbookContentType,
            Summary: summary);

    private string BuildReceiptRelativePath(string operation)
        => workspaceScope.CombineArtifactPath(
            "tool-receipts",
            DateTime.UtcNow.ToString("yyyyMMdd"),
            $"{DateTime.UtcNow:HHmmssfff}-{operation}-{Guid.NewGuid():N}.json");
}

internal sealed class WorkspaceSpreadsheetRuntimePathAccess(
    string workspaceRoot,
    WorkspaceScopeDescriptor workspaceScope,
    AgentWorkspaceToolAccessSettings accessSettings)
{
    private readonly string workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly string workspaceRootWithSeparator = EnsureTrailingSeparator(Path.GetFullPath(workspaceRoot));
    private readonly WorkspaceScopeDescriptor workspaceScope = workspaceScope;
    private readonly AgentWorkspaceToolAccessSettings accessSettings = AgentWorkspaceToolAccessMetadata.Normalize(accessSettings);

    public string? PrepareFileReadPath(string? path)
    {
        EnsureFileReadAllowed(path);
        return NormalizeRecoverableCurrentRunArtifactPath(NormalizeAllowedExternalPathForWorkspaceTools(path));
    }

    public string? PrepareFileWritePath(string? path)
    {
        EnsureFileWriteAllowed(path);
        return NormalizeAllowedExternalPathForWorkspaceTools(path);
    }

    private void EnsureFileReadAllowed(string? path)
    {
        if (!accessSettings.CanReadFiles && !accessSettings.CanWriteFiles)
        {
            throw new InvalidOperationException("This agent is not allowed to read workspace files.");
        }

        EnsureExternalAliasAllowed(path, requireWrite: false);
    }

    private void EnsureFileWriteAllowed(string? path)
    {
        if (!accessSettings.CanWriteFiles)
        {
            throw new InvalidOperationException("This agent is not allowed to write workspace files.");
        }

        EnsureExternalAliasAllowed(path, requireWrite: true);
    }

    private void EnsureExternalAliasAllowed(string? path, bool requireWrite)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            return;
        }

        if (IsManagedWorkspaceAbsolutePath(path))
        {
            return;
        }

        var readOnlyAliases = ResolveReadOnlyExternalTargetAliases();
        if (requireWrite &&
            IsExternalTargetAliasAllowed(normalizedAlias, readOnlyAliases))
        {
            throw new InvalidOperationException(
                $"External workspace path '{normalizedAlias}' is read-only for this run.");
        }

        var allowedAliases = ResolveAllowedExternalTargetAliases();
        var isAllowedForRead = IsExternalTargetAliasAllowed(normalizedAlias, allowedAliases) ||
                               IsExternalTargetAliasAllowed(normalizedAlias, readOnlyAliases);
        if (!isAllowedForRead)
        {
            throw new InvalidOperationException(
                $"External workspace path '{normalizedAlias}' is not in this agent's allowed external workspace roots.");
        }

        if (requireWrite &&
            !IsExternalTargetAliasAllowed(normalizedAlias, allowedAliases))
        {
            throw new InvalidOperationException(
                $"External workspace path '{normalizedAlias}' is read-only for this run.");
        }
    }

    private string? NormalizeAllowedExternalPathForWorkspaceTools(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            IsManagedWorkspaceAbsolutePath(path))
        {
            return path;
        }

        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
        return string.IsNullOrWhiteSpace(normalizedAlias)
            ? path
            : normalizedAlias;
    }

    private string? NormalizeRecoverableCurrentRunArtifactPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var auditScope = WorkspaceExecutionAuditContext.Current;
        var currentRunId = auditScope?.ProcessRunId;
        var currentWorkspaceScope = auditScope?.ContextWorkspaceScope ?? workspaceScope;
        return WorkspaceProcessRunArtifactPath.TryBuildRecoverableCurrentRunPath(
            path,
            currentRunId,
            currentWorkspaceScope,
            out var currentRunPath)
            ? currentRunPath
            : path;
    }

    private IReadOnlyList<string> ResolveAllowedExternalTargetAliases()
    {
        var auditScope = WorkspaceExecutionAuditContext.Current;
        if (auditScope is not null &&
            (auditScope.AllowedExternalTargetAliases.Count > 0 ||
             auditScope.ReadOnlyExternalTargetAliases.Count > 0))
        {
            return auditScope.AllowedExternalTargetAliases
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return accessSettings.AllowedExternalTargetAliases
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveReadOnlyExternalTargetAliases()
    {
        return WorkspaceExecutionAuditContext.Current?.ReadOnlyExternalTargetAliases
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    private static bool IsExternalTargetAliasAllowed(
        string normalizedAlias,
        IReadOnlyList<string> allowedAliases)
    {
        return AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(
            normalizedAlias,
            allowedAliases);
    }

    private bool IsManagedWorkspaceAbsolutePath(string path)
    {
        try
        {
            if (!Path.IsPathRooted(path))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(path);
            return string.Equals(fullPath, workspaceRoot, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(workspaceRootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
