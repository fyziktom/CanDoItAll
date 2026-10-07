using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderIngestionUiService(
    ManualMemorySourceIngestionService manualIngestionService,
    IMemoryOperationLedgerStore operationLedgerStore,
    MemoryProviderUiRequestFactory requestFactory,
    MemoryProviderExecutableActionGuard actionGuard,
    ILogger<MemoryProviderIngestionUiService> logger)
{
    public async Task<MemoryProviderManualIngestionUiResult> EnqueueAsync(
        string? selectedProviderInstanceId,
        MemoryManualIngestionEditorModel editor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(editor);
        editor = editor.Capture();
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
        MemoryOperationRecord? operation = null;
        var readFailed = false;
        try {
            operation = await operationLedgerStore.GetAsync(result.OperationId, cancellationToken);
        } catch (Exception exception) {
            readFailed = true;
            logger.LogWarning("Memory ingestion job {JobId}, operation {OperationId} was accepted; its ledger read failed with {ExceptionType}.", result.JobId, result.OperationId.Value, exception.GetType().Name);
        }
        return new MemoryProviderManualIngestionUiResult(
            MemoryProviderActionStatus.Accepted,
            readFailed ? "Source snapshot captured and queued. Its ledger could not be read; do not enqueue it again." : "Source snapshot captured and queued for provider ingestion.",
            result.JobId,
            result.OperationId,
            result.CapturedSnapshotId.Value,
            operation is null ? null : MemoryProviderUiRecordMapper.ToUiRecord(operation)) { SnapshotReadFailed = readFailed };
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
