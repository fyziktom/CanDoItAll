using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderIngestionUiService(
    ManualMemorySourceIngestionService manualIngestionService,
    IMemoryOperationLedgerStore operationLedgerStore,
    MemoryProviderUiRequestFactory requestFactory,
    MemoryProviderExecutableActionGuard actionGuard)
{
    public async Task<MemoryProviderManualIngestionUiResult> EnqueueAsync(
        string? selectedProviderInstanceId,
        MemoryManualIngestionEditorModel editor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(editor);
        await actionGuard.EnsureProviderCanExecuteAsync(
            selectedProviderInstanceId,
            MemoryCapabilityIds.IngestionSnapshot,
            cancellationToken);

        var result = await manualIngestionService.EnqueueAsync(
            new ManualMemorySourceIngestionRequest(
                MemoryProviderInstanceId.Parse(selectedProviderInstanceId!),
                ManualMemorySourcePayload.Text(
                    MemoryProviderUiText.Normalize(editor.Title, nameof(editor.Title)),
                    MemoryProviderUiText.Normalize(editor.ContentText, nameof(editor.ContentText)),
                    MemoryProviderUiText.Normalize(editor.SourceCategory, nameof(editor.SourceCategory)),
                    SplitTags(editor.Tags)),
                RequestedBy: "memory-ui",
                requestFactory.CreateRequester(),
                requestFactory.CreateRetentionPolicy()),
            cancellationToken);
        var operation = await operationLedgerStore.GetAsync(result.OperationId, cancellationToken);
        return new MemoryProviderManualIngestionUiResult(
            MemoryOperationHandlerStatus.Accepted,
            "Source snapshot captured and queued for provider ingestion.",
            result.JobId,
            result.OperationId,
            result.CapturedSnapshotId.Value,
            operation is null ? null : MemoryProviderUiRecordMapper.ToUiRecord(operation));
    }

    private static IReadOnlyList<string> SplitTags(string tags) =>
        string.IsNullOrWhiteSpace(tags)
            ? []
            : tags
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
}
