using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tools.Documents;
using DocumentCellWrite = CanDoItAll.Tools.Documents.SpreadsheetCellWrite;
using DocumentRangeWrite = CanDoItAll.Tools.Documents.SpreadsheetRangeWrite;
using DocumentWriteRequest = CanDoItAll.Tools.Documents.SpreadsheetWriteRequest;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents;

public sealed class SpreadsheetWorkflowExecutor(
    ISpreadsheetDocumentService documents,
    IWorkspacePathResolutionService paths) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.Spreadsheet;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = WorkflowExecutorJson.Deserialize<WorkflowSpreadsheetExecutorSettings>(context.SettingsJson);
        object result = settings.Operation switch
        {
            WorkflowSpreadsheetOperation.WorkbookSummary => Inspect(settings),
            WorkflowSpreadsheetOperation.ReadCell => ReadCell(settings),
            WorkflowSpreadsheetOperation.ReadRange => ReadRange(settings),
            WorkflowSpreadsheetOperation.RangeToMarkdown => ReadRange(settings),
            WorkflowSpreadsheetOperation.WriteCell => WriteCell(settings),
            WorkflowSpreadsheetOperation.WriteRange => WriteRange(settings),
            WorkflowSpreadsheetOperation.ApplyBatch => WriteBatch(settings),
            _ => throw new InvalidOperationException($"Spreadsheet operation '{settings.Operation}' is not supported.")
        };

        return ValueTask.FromResult(WorkflowExecutorJson.Result(context, result));
    }

    private object Inspect(WorkflowSpreadsheetExecutorSettings settings)
    {
        var workbook = paths.ResolveFilePath(Require(settings.WorkbookPath, nameof(settings.WorkbookPath)), allowMissing: false);
        return documents.InspectWorkbook(workbook.FullPath);
    }

    private object ReadCell(WorkflowSpreadsheetExecutorSettings settings)
    {
        var workbook = paths.ResolveFilePath(Require(settings.WorkbookPath, nameof(settings.WorkbookPath)), allowMissing: false);
        return documents.ReadCell(
            workbook.FullPath,
            Require(settings.WorksheetName, nameof(settings.WorksheetName)),
            Require(settings.CellAddress, nameof(settings.CellAddress)));
    }

    private object ReadRange(WorkflowSpreadsheetExecutorSettings settings)
    {
        var workbook = paths.ResolveFilePath(Require(settings.WorkbookPath, nameof(settings.WorkbookPath)), allowMissing: false);
        return documents.ReadRange(
            workbook.FullPath,
            Require(settings.WorksheetName, nameof(settings.WorksheetName)),
            Require(settings.RangeAddress, nameof(settings.RangeAddress)),
            settings.MaxRows,
            settings.MaxColumns);
    }

    private object WriteCell(WorkflowSpreadsheetExecutorSettings settings)
    {
        var write = settings with
        {
            CellWrites = [new WorkflowSpreadsheetCellWrite(Require(settings.CellAddress, nameof(settings.CellAddress)), settings.Value)]
        };
        return WriteBatch(write);
    }

    private object WriteRange(WorkflowSpreadsheetExecutorSettings settings)
    {
        if (settings.RangeWrites.Count == 0)
        {
            throw new InvalidOperationException("Spreadsheet write-range operation requires at least one range write.");
        }

        return WriteBatch(settings);
    }

    private object WriteBatch(WorkflowSpreadsheetExecutorSettings settings)
    {
        var workbook = paths.ResolveFilePath(Require(settings.WorkbookPath, nameof(settings.WorkbookPath)), settings.CreateWorkbookIfMissing);
        var output = string.IsNullOrWhiteSpace(settings.OutputWorkbookPath)
            ? workbook
            : paths.ResolveFilePath(settings.OutputWorkbookPath, allowMissing: true);
        var result = documents.Write(new DocumentWriteRequest(
            workbook.FullPath,
            output.FullPath,
            Require(settings.WorksheetName, nameof(settings.WorksheetName)),
            settings.CellWrites.Select(item => new DocumentCellWrite(item.CellAddress, item.Value)).ToArray(),
            settings.RangeWrites.Select(item => new DocumentRangeWrite(item.RangeAddress, item.Values)).ToArray(),
            settings.CreateWorkbookIfMissing,
            settings.Overwrite));

        return new
        {
            result.WorkbookPath,
            result.WorksheetName,
            result.CellWriteCount,
            result.RangeWriteCount,
            relativePath = output.RelativePath
        };
    }

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Spreadsheet executor setting '{name}' is required.")
            : value.Trim();
}

