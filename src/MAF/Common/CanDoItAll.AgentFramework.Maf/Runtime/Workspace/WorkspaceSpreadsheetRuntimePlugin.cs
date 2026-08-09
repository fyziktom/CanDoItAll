using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tools.Documents;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class WorkspaceSpreadsheetRuntimePlugin(
    ISpreadsheetDocumentService spreadsheets,
    string workspaceRoot,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
    WorkspaceScopeDescriptor workspaceScope,
    AgentWorkspaceToolAccessSettings accessSettings,
    IExternalTargetPathRegistry externalTargetPathRegistry)
{
    private const string SpreadsheetPreviewOperation = "workspace_spreadsheet_preview";

    private readonly ISpreadsheetDocumentService spreadsheets = spreadsheets ?? throw new ArgumentNullException(nameof(spreadsheets));
    private readonly WorkspacePathResolutionService paths = new(
        workspaceRoot,
        physicalPathPolicyFactory,
        workspaceScope,
        externalTargetPathRegistry);
    private readonly WorkspaceRuntimeFileAccessGuard pathAccess = new(
        workspaceRoot,
        physicalPathPolicyFactory,
        workspaceScope,
        accessSettings);
    private readonly WorkspaceSpreadsheetReceiptWriter receiptWriter = new(workspaceRoot, workspaceScope);

    public WorkspaceSpreadsheetSummaryToolResult InspectWorkbook(string workbookPath)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var workbook = ResolveReadableWorkbook(workbookPath);
        var summary = ExecuteRead(() => spreadsheets.InspectWorkbook(workbook.FullPath));
        var receipt = receiptWriter.Persist(
            "workspace_spreadsheet_summary",
            mutatesWorkspace: false,
            message: $"Inspected workbook '{workbook.RelativePath}'.",
            requestSummary: workbook.RelativePath,
            targetPaths: [workbook.RelativePath],
            artifactReferences: [],
            startedAtUtc);

        return new WorkspaceSpreadsheetSummaryToolResult(
            workbook.RelativePath,
            summary.Worksheets,
            receipt);
    }

    public WorkspaceSpreadsheetPreviewToolResult PreviewWorkbook(
        string workbookPath,
        int maxWorksheets = 2,
        int maxRows = 8,
        int maxColumns = 8)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var workbook = ResolveReadableWorkbook(workbookPath);
        var result = ExecuteRead(() => spreadsheets.PreviewWorkbook(
            new SpreadsheetWorkbookPreviewRequest(
                workbook.FullPath,
                maxWorksheets,
                maxRows,
                maxColumns)));
        var receipt = receiptWriter.Persist(
            SpreadsheetPreviewOperation,
            mutatesWorkspace: false,
            message: $"Previewed {result.Worksheets.Count} of {result.TotalWorksheetCount} worksheet(s) from workbook '{workbook.RelativePath}'.",
            requestSummary: $"{workbook.RelativePath}, maxWorksheets={maxWorksheets}, maxRows={maxRows}, maxColumns={maxColumns}",
            targetPaths: [workbook.RelativePath],
            artifactReferences: [],
            startedAtUtc);

        return new WorkspaceSpreadsheetPreviewToolResult(
            workbook.RelativePath,
            result.TotalWorksheetCount,
            result.Worksheets,
            result.WorksheetsTruncated,
            receipt);
    }

    public WorkspaceSpreadsheetCellToolResult ReadSpreadsheetCell(string workbookPath, string worksheetName, string cellAddress)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var workbook = ResolveReadableWorkbook(workbookPath);
        var requestedWorksheetName = Require(worksheetName, nameof(worksheetName));
        var requestedCellAddress = Require(cellAddress, nameof(cellAddress));
        var result = ExecuteRead(() => spreadsheets.ReadCell(
            workbook.FullPath,
            requestedWorksheetName,
            requestedCellAddress));
        var receipt = receiptWriter.Persist(
            "workspace_read_spreadsheet_cell",
            mutatesWorkspace: false,
            message: $"Read cell '{requestedWorksheetName}!{requestedCellAddress}' from workbook '{workbook.RelativePath}'.",
            requestSummary: $"{workbook.RelativePath}, {requestedWorksheetName}!{requestedCellAddress}",
            targetPaths: [workbook.RelativePath],
            artifactReferences: [],
            startedAtUtc);

        return new WorkspaceSpreadsheetCellToolResult(
            result.Address,
            result.Value,
            receipt);
    }

    public WorkspaceSpreadsheetRangeToolResult ReadSpreadsheetRange(
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
        var result = ExecuteRead(() => spreadsheets.ReadRange(
            workbook.FullPath,
            requestedWorksheetName,
            requestedRangeAddress,
            maxRows,
            maxColumns));
        var receipt = receiptWriter.Persist(
            "workspace_read_spreadsheet_range",
            mutatesWorkspace: false,
            message: $"Read range '{requestedWorksheetName}!{requestedRangeAddress}' from workbook '{workbook.RelativePath}'.",
            requestSummary: $"{workbook.RelativePath}, {requestedWorksheetName}!{requestedRangeAddress}, maxRows={maxRows}, maxColumns={maxColumns}",
            targetPaths: [workbook.RelativePath],
            artifactReferences: [],
            startedAtUtc);

        return new WorkspaceSpreadsheetRangeToolResult(
            workbook.RelativePath,
            result.WorksheetName,
            result.RangeAddress,
            result.Values,
            result.MarkdownTable,
            receipt);
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
        var workbook = ResolveWorkbookPath(
            normalizedWorkbookPath,
            allowMissing: true,
            nameof(workbookPath));
        var requestedOutputPath = string.IsNullOrWhiteSpace(outputWorkbookPath)
            ? requestedWorkbookPath
            : outputWorkbookPath.Trim();
        var normalizedOutputPath = pathAccess.PrepareFileWritePath(requestedOutputPath) ?? requestedOutputPath;
        var output = ResolveWorkbookPath(
            normalizedOutputPath,
            allowMissing: true,
            nameof(outputWorkbookPath));

        SpreadsheetWriteResult result;
        try
        {
            result = spreadsheets.Write(new SpreadsheetWriteRequest(
                workbook.FullPath,
                output.FullPath,
                requestedWorksheetName,
                cellWrites ?? [],
                rangeWrites ?? [],
                createWorkbookIfMissing,
                overwrite));
        }
        catch (SpreadsheetRangeCapacityExceededException exception)
        {
            throw CreateRangeCapacityFailure(exception);
        }
        catch (SpreadsheetWriteInputException exception)
        {
            throw CreateWriteInputFailure(exception);
        }
        catch (SpreadsheetReadInputException exception)
        {
            throw CreateReadInputFailure(exception);
        }
        catch (SpreadsheetWriteConflictException)
        {
            throw AgentToolConflictException.Create(
                "The output workbook already exists and overwrite is false. Choose another outputWorkbookPath or set overwrite to true, then retry.");
        }
        var receipt = receiptWriter.Persist(
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
            Diagnostics: string.Empty,
            Receipt: receipt);
    }

    public WorkspaceSpreadsheetFunctionCatalogResult ListSpreadsheetFunctions(
        string? query = null,
        string? category = null,
        int maxResults = 50)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var functions = SpreadsheetFunctionCatalog.List(query, category, maxResults);
        var receipt = receiptWriter.Persist(
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
            Diagnostics: string.Empty,
            Receipt: receipt);
    }

    private WorkspaceResolvedPath ResolveReadableWorkbook(string workbookPath)
    {
        var normalizedPath = pathAccess.PrepareFileReadPath(Require(workbookPath, nameof(workbookPath))) ?? workbookPath;
        return ResolveWorkbookPath(
            normalizedPath,
            allowMissing: true,
            nameof(workbookPath));
    }

    private string NormalizeWorkbookInputPath(string workbookPath, bool createWorkbookIfMissing)
        => createWorkbookIfMissing && !WorkbookExists(workbookPath)
            ? pathAccess.PrepareFileWritePath(workbookPath) ?? workbookPath
            : pathAccess.PrepareFileReadPath(workbookPath) ?? workbookPath;

    private bool WorkbookExists(string workbookPath)
    {
        var readPath = pathAccess.PrepareFileReadPath(workbookPath) ?? workbookPath;
        var resolved = ResolveWorkbookPath(
            readPath,
            allowMissing: true,
            nameof(workbookPath));
        return File.Exists(resolved.FullPath);
    }

    private WorkspaceResolvedPath ResolveWorkbookPath(
        string path,
        bool allowMissing,
        string argumentName)
    {
        try
        {
            return paths.ResolveFilePath(path, allowMissing);
        }
        catch (WorkspacePathResolutionException exception)
        {
            throw CreatePathInputFailure(argumentName, exception);
        }
    }

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw AgentToolInputValidationException.Create(
                $"Spreadsheet tool argument '{name}' is required. Supply it and retry.")
            : value.Trim();

    private static AgentToolInputValidationException CreateRangeCapacityFailure(
        SpreadsheetRangeCapacityExceededException exception)
    {
        var correction = exception.Dimension switch
        {
            SpreadsheetRangeCapacityDimension.Rows =>
                $"Use a range with at least {exception.SuppliedCount} rows or supply at most {exception.Capacity} rows, then retry.",
            SpreadsheetRangeCapacityDimension.Columns =>
                $"Expand the range to at least {exception.SuppliedCount} columns or reduce values row {exception.ValuesRowNumber} to at most {exception.Capacity} values, then retry.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(exception),
                exception.Dimension,
                "Unknown spreadsheet range dimension.")
        };
        return AgentToolInputValidationException.Create($"{exception.Message} {correction}");
    }

    private static AgentToolInputValidationException CreateWriteInputFailure(
        SpreadsheetWriteInputException exception)
    {
        var safeMessage = exception.Kind switch
        {
            SpreadsheetWriteInputFailureKind.UnsupportedInputWorkbookFormat =>
                "workbookPath must end with .xlsx. Choose an .xlsx input path and retry.",
            SpreadsheetWriteInputFailureKind.UnsupportedOutputWorkbookFormat =>
                "outputWorkbookPath must end with .xlsx. Choose an .xlsx output path and retry.",
            SpreadsheetWriteInputFailureKind.InvalidWorksheetName =>
                "worksheetName is invalid. Use 1-31 characters, omit : \\ / ? * [ ], and do not begin or end with an apostrophe, then retry.",
            SpreadsheetWriteInputFailureKind.MissingCellWrites =>
                "cellWrites is missing. Supply an array or an empty array, then retry.",
            SpreadsheetWriteInputFailureKind.MissingCellWrite =>
                $"cellWrites item {exception.WriteNumber} is null. Supply a cell write object or remove that item, then retry.",
            SpreadsheetWriteInputFailureKind.InvalidCellAddress =>
                $"cellWrites item {exception.WriteNumber} has an invalid cellAddress. Use one A1 cell address such as B3, then retry.",
            SpreadsheetWriteInputFailureKind.MissingRangeWrites =>
                "rangeWrites is missing. Supply an array or an empty array, then retry.",
            SpreadsheetWriteInputFailureKind.MissingRangeWrite =>
                $"rangeWrites item {exception.WriteNumber} is null. Supply a range write object or remove that item, then retry.",
            SpreadsheetWriteInputFailureKind.InvalidRangeAddress =>
                $"rangeWrites item {exception.WriteNumber} has an invalid rangeAddress. Use one A1 range such as A1:B12, then retry.",
            SpreadsheetWriteInputFailureKind.MissingRangeValues =>
                $"rangeWrites item {exception.WriteNumber} has no values array. Supply an array of rows or an empty array, then retry.",
            SpreadsheetWriteInputFailureKind.MissingRangeRow =>
                $"rangeWrites item {exception.WriteNumber} values row {exception.ValuesRowNumber} is null. Supply an array for that row or remove it, then retry.",
            SpreadsheetWriteInputFailureKind.InputWorkbookMissing =>
                "The input workbook does not exist and createWorkbookIfMissing is false. Choose an existing workbook or set createWorkbookIfMissing to true, then retry.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(exception),
                exception.Kind,
                "Unknown spreadsheet write input failure kind.")
        };
        return AgentToolInputValidationException.Create(safeMessage);
    }

    private static T ExecuteRead<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (SpreadsheetReadInputException exception)
        {
            throw CreateReadInputFailure(exception);
        }
    }

    private static AgentToolInputValidationException CreateReadInputFailure(
        SpreadsheetReadInputException exception)
    {
        var safeMessage = exception.Kind switch
        {
            SpreadsheetReadInputFailureKind.WorkbookMissing =>
                "workbookPath does not identify an existing .xlsx workbook. Choose an existing workbook and retry.",
            SpreadsheetReadInputFailureKind.UnsupportedWorkbookFormat =>
                "workbookPath must identify an .xlsx workbook. Choose a supported workbook and retry.",
            SpreadsheetReadInputFailureKind.InvalidWorkbook =>
                "The selected workbook is invalid or corrupt. Choose a valid .xlsx workbook and retry.",
            SpreadsheetReadInputFailureKind.WorksheetNotFound =>
                "worksheetName was not found in the workbook. Use a name returned by workspace_spreadsheet_summary and retry.",
            SpreadsheetReadInputFailureKind.InvalidCellAddress =>
                "cellAddress is invalid. Use one A1 cell address such as B3 and retry.",
            SpreadsheetReadInputFailureKind.InvalidRangeAddress =>
                "rangeAddress is invalid. Use one A1 range such as A1:B12 and retry.",
            SpreadsheetReadInputFailureKind.PreviewLimitOutOfRange or
            SpreadsheetReadInputFailureKind.ReadLimitOutOfRange =>
                CreateReadLimitFailureMessage(exception),
            _ => throw new ArgumentOutOfRangeException(
                nameof(exception),
                exception.Kind,
                "Unknown spreadsheet read input failure kind.")
        };
        return AgentToolInputValidationException.Create(safeMessage);
    }

    private static AgentToolInputValidationException CreatePathInputFailure(
        string argumentName,
        WorkspacePathResolutionException exception)
    {
        var safeMessage = exception.Kind switch
        {
            WorkspacePathResolutionFailureKind.FileRequired =>
                $"{argumentName} identifies a directory, but a workbook file path is required. Choose a file path and retry.",
            WorkspacePathResolutionFailureKind.PathMissing =>
                $"{argumentName} does not identify an existing workbook file. Choose an existing file and retry.",
            WorkspacePathResolutionFailureKind.InvalidPath or
            WorkspacePathResolutionFailureKind.OutsideWorkspace or
            WorkspacePathResolutionFailureKind.ManagedPathAliasMismatch or
            WorkspacePathResolutionFailureKind.ReparsePointTraversal or
            WorkspacePathResolutionFailureKind.ForeignManagedScope =>
                $"{argumentName} is not a valid accessible workspace file path. Choose an allowed workspace path and retry.",
            WorkspacePathResolutionFailureKind.DirectoryRequired =>
                throw new ArgumentOutOfRangeException(
                    nameof(exception),
                    exception.Kind,
                    "A directory-required failure is invalid for workbook file resolution."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(exception),
                exception.Kind,
                "Unknown workspace path resolution failure kind.")
        };
        return AgentToolInputValidationException.Create(safeMessage);
    }

    private static string CreateReadLimitFailureMessage(
        SpreadsheetReadInputException exception)
    {
        var argumentName = exception.LimitKind switch
        {
            SpreadsheetReadLimitKind.MaxWorksheets => "maxWorksheets",
            SpreadsheetReadLimitKind.MaxRows => "maxRows",
            SpreadsheetReadLimitKind.MaxColumns => "maxColumns",
            _ => throw new ArgumentOutOfRangeException(
                nameof(exception),
                exception.LimitKind,
                "Unknown spreadsheet read limit kind.")
        };
        return $"{argumentName} must be between {exception.Minimum} and {exception.Maximum}. Supply a value in that range and retry.";
    }
}

internal sealed record WorkspaceSpreadsheetSummaryToolResult(
    string WorkbookPath,
    IReadOnlyList<SpreadsheetWorksheetSummary> Worksheets,
    WorkspaceToolReceipt Receipt);

internal sealed record WorkspaceSpreadsheetPreviewToolResult(
    string WorkbookPath,
    int TotalWorksheetCount,
    IReadOnlyList<SpreadsheetWorksheetPreview> Worksheets,
    bool WorksheetsTruncated,
    WorkspaceToolReceipt Receipt)
{
    public bool IsTruncated => WorksheetsTruncated || Worksheets.Any(worksheet => worksheet.IsTruncated);
}

internal sealed record WorkspaceSpreadsheetCellToolResult(
    string Address,
    string Value,
    WorkspaceToolReceipt Receipt);

internal sealed record WorkspaceSpreadsheetRangeToolResult(
    string WorkbookPath,
    string WorksheetName,
    string RangeAddress,
    IReadOnlyList<IReadOnlyList<string>> Values,
    string MarkdownTable,
    WorkspaceToolReceipt Receipt);

internal sealed record WorkspaceSpreadsheetWriteToolResult(
    bool Succeeded,
    string Message,
    string WorkbookPath,
    string WorksheetName,
    int CellWriteCount,
    int RangeWriteCount,
    string Diagnostics,
    WorkspaceToolReceipt Receipt);

internal sealed record WorkspaceSpreadsheetFunctionCatalogResult(
    bool Succeeded,
    string Message,
    IReadOnlyList<SpreadsheetFunctionDescriptor> Functions,
    string Diagnostics,
    WorkspaceToolReceipt Receipt);

internal sealed class WorkspaceSpreadsheetReceiptWriter(
    string workspaceRoot,
    WorkspaceScopeDescriptor workspaceScope)
{
    private const string BoundaryDescription = "Workspace spreadsheet service backed by the document tool boundary. No host process execution.";
    private const string WorkbookContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly string workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly WorkspaceScopeDescriptor workspaceScope = workspaceScope;

    public WorkspaceToolReceipt Persist(
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

        return receipt;
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
