using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderQueryUiService(
    IMemoryOperationHandler operationHandler,
    MemoryProviderUiRequestFactory requestFactory,
    MemoryProviderExecutableActionGuard actionGuard)
{
    public async Task<MemoryProviderQueryUiResult> RunAsync(
        string? selectedProviderInstanceId,
        MemoryQueryEditorModel editor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var requiredCapability = editor.UseAsyncQuery
            ? MemoryCapabilityIds.ContextQueryAsync
            : MemoryCapabilityIds.ContextQuerySync;
        await actionGuard.EnsureProviderCanExecuteAsync(
            selectedProviderInstanceId,
            requiredCapability,
            cancellationToken);
        var request = MemoryOperationRequestBuilder.Query(
            requestFactory.CreateCaller("memory.ui.query"),
            requestFactory.CreateSelectionPolicy(selectedProviderInstanceId, requiredCapability),
            new MemoryContextQueryRequest(
                MemoryProviderUiText.Normalize(editor.Query, nameof(editor.Query)),
                [requiredCapability],
                CreateSourceProvenance(editor)),
            requestFactory.CreateRetentionPolicy());
        var result = await operationHandler.ExecuteQueryAsync(request, cancellationToken);

        return new MemoryProviderQueryUiResult(
            result.Status,
            result.Diagnostic,
            result.OperationRecord is null ? null : MemoryProviderUiRecordMapper.ToUiRecord(result.OperationRecord),
            result.Output,
            result.AcceptedOperation,
            result.FeedbackHandle,
            result.DriverDispatchAttempted);
    }

    private static MemorySourceProvenance CreateSourceProvenance(MemoryQueryEditorModel editor)
    {
        var sourceRecordIds = string.IsNullOrWhiteSpace(editor.SourceRecordId)
            ? Array.Empty<string>()
            : new[] { editor.SourceRecordId.Trim() };
        var citations = string.IsNullOrWhiteSpace(editor.Citation)
            ? Array.Empty<string>()
            : new[] { editor.Citation.Trim() };

        return new MemorySourceProvenance(
            MemorySourceSnapshotId.Parse("memory-ui.query"),
            string.IsNullOrWhiteSpace(editor.SourceModule) ? null : editor.SourceModule.Trim(),
            sourceRecordIds,
            citations);
    }
}
