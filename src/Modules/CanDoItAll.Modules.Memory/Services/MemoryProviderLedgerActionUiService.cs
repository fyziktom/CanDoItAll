using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderLedgerActionUiService(
    IMemoryOperationHandler operationHandler,
    MemoryProviderUiRequestFactory requestFactory,
    MemoryProviderExecutableActionGuard actionGuard)
{
    public async Task<MemoryProviderOperationUiResult> RefreshOperationAsync(
        string operationId,
        CancellationToken cancellationToken)
    {
        var parsedOperationId = MemoryProviderUiRequestFactory.ParseOperationId(operationId);
        await actionGuard.EnsureOperationCanExecuteAsync(
            parsedOperationId,
            MemoryCapabilityIds.OperationStatus,
            cancellationToken);
        var request = MemoryOperationRequestBuilder.Status(
            requestFactory.CreateCaller("memory.ui.operation.status"),
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.OperationStatus),
            new MemoryOperationStatusRequest(parsedOperationId),
            requestFactory.CreateRetentionPolicy());
        var result = await operationHandler.GetStatusAsync(request, cancellationToken);
        return new MemoryProviderOperationUiResult(
            result.Status,
            result.Diagnostic,
            result.Output is null ? null : MemoryProviderUiRecordMapper.ToUiRecord(result.Output));
    }

    public async Task<MemoryProviderOperationUiResult> CancelOperationAsync(
        string operationId,
        CancellationToken cancellationToken)
    {
        MemoryProviderExecutableActionGuard.RejectOperationCancellation();
        var request = MemoryOperationRequestBuilder.Cancellation(
            requestFactory.CreateCaller("memory.ui.operation.cancel"),
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.OperationStatus),
            new MemoryOperationCancellationRequest(
                MemoryProviderUiRequestFactory.ParseOperationId(operationId),
                "User cancelled operation from Memory UI."),
            requestFactory.CreateRetentionPolicy());
        var result = await operationHandler.CancelAsync(request, cancellationToken);
        return new MemoryProviderOperationUiResult(
            result.Status,
            result.Diagnostic,
            result.Output is null ? null : MemoryProviderUiRecordMapper.ToUiRecord(result.Output));
    }

    public async Task<MemoryProviderFeedbackUiResult> SubmitFeedbackAsync(
        string? selectedProviderInstanceId,
        MemoryFeedbackEditorModel editor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var requiredCapability = editor.Stage is MemoryFeedbackStage.ContextUsed or MemoryFeedbackStage.ImmediateToolResult
            ? MemoryCapabilityIds.FeedbackImmediate
            : MemoryCapabilityIds.FeedbackDelayed;
        await actionGuard.EnsureProviderCanExecuteAsync(
            selectedProviderInstanceId,
            requiredCapability,
            cancellationToken);
        var request = new MemoryOperationHandlerRequest<MemoryFeedbackOperationRequest>(
            requestFactory.CreateCaller("memory.ui.feedback.submit"),
            requestFactory.CreateSelectionPolicy(selectedProviderInstanceId, requiredCapability),
            MemoryOperationKind.Feedback,
            SourceSnapshotIds: [],
            requestFactory.CreateRetentionPolicy(),
            new MemoryFeedbackOperationRequest(
                new MemoryFeedbackRequest(
                    MemoryProviderUiRequestFactory.ParseContextPackId(editor.ContextPackId),
                    editor.Outcome,
                    string.IsNullOrWhiteSpace(editor.Comment) ? null : editor.Comment.Trim(),
                    EconomicImpact: null),
                editor.Stage,
                "Feedback was submitted without a persisted context delivery record."));
        var result = await operationHandler.SubmitFeedbackAsync(request, cancellationToken);
        return new MemoryProviderFeedbackUiResult(
            result.Status,
            result.Diagnostic,
            result.Output is null ? null : MemoryProviderUiRecordMapper.ToUiRecord(result.Output));
    }

    public async Task<MemoryProviderEventAcknowledgeUiResult> AcknowledgeEventAsync(
        string? selectedProviderInstanceId,
        string providerEventId,
        bool accepted,
        CancellationToken cancellationToken)
    {
        await actionGuard.EnsureProviderCanExecuteAsync(
            selectedProviderInstanceId,
            MemoryCapabilityIds.EventsProviderPush,
            cancellationToken);
        var eventId = MemoryProviderUiRequestFactory.ParseProviderEventId(providerEventId);
        var request = MemoryOperationRequestBuilder.EventAcknowledge(
            requestFactory.CreateCaller("memory.ui.event.acknowledge"),
            requestFactory.CreateSelectionPolicy(selectedProviderInstanceId, MemoryCapabilityIds.EventsProviderPush),
            new MemoryEventAcknowledgeRequest(
                eventId,
                accepted,
                accepted ? "Accepted from Memory UI." : "Rejected from Memory UI."),
            requestFactory.CreateRetentionPolicy());
        var result = await operationHandler.AcknowledgeEventAsync(request, cancellationToken);
        return new MemoryProviderEventAcknowledgeUiResult(result.Status, result.Diagnostic, eventId);
    }
}
