using CanDoItAll.Memory.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderManagementUiService(
    MemoryUiProfileOrigin origin,
    MemoryProviderSnapshotReader snapshotReader,
    MemoryProviderProfileUiService profileService,
    MemoryProviderQueryUiService queryService,
    MemoryProviderLedgerActionUiService ledgerActionService,
    MemoryProviderIngestionUiService ingestionService,
    ILogger<MemoryProviderManagementUiService> logger) : IMemoryProviderManagementUiService
{
    public bool IsCurrent => origin.IsCurrent;

    public Task<MemoryProviderManagementSnapshot> GetSnapshotAsync(
        string? selectedProviderInstanceId = null,
        CancellationToken cancellationToken = default) =>
        ObserveAsync(() => snapshotReader.GetSnapshotAsync(selectedProviderInstanceId, cancellationToken));

    public Task<MemoryProviderProfile> SaveProviderAsync(
        MemoryProviderProfileEditorModel editor,
        CancellationToken cancellationToken = default) =>
        ObserveAsync(() => profileService.SaveAsync(editor, cancellationToken));

    public Task<IReadOnlyList<MemoryProviderProfile>> CreateDemoProvidersAsync(
        CancellationToken cancellationToken = default) =>
        ObserveAsync(() => profileService.CreateDemoProvidersAsync(cancellationToken));

    public Task<MemoryProviderQueryUiResult> RunQueryAsync(
        string? selectedProviderInstanceId,
        MemoryQueryEditorModel editor,
        CancellationToken cancellationToken = default) =>
        ObserveAsync(() => queryService.RunAsync(selectedProviderInstanceId, editor, cancellationToken));

    public Task<MemoryProviderOperationUiResult> RefreshOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default) =>
        ObserveAsync(() => ledgerActionService.RefreshOperationAsync(operationId, cancellationToken));

    public Task<MemoryProviderOperationUiResult> CancelOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default) =>
        ObserveAsync(() => ledgerActionService.CancelOperationAsync(operationId, cancellationToken));

    public Task<MemoryProviderFeedbackUiResult> SubmitFeedbackAsync(
        string? selectedProviderInstanceId,
        MemoryFeedbackEditorModel editor,
        CancellationToken cancellationToken = default) =>
        ObserveAsync(() => ledgerActionService.SubmitFeedbackAsync(selectedProviderInstanceId, editor, cancellationToken));

    public Task<MemoryProviderManualIngestionUiResult> EnqueueManualIngestionAsync(
        string? selectedProviderInstanceId,
        MemoryManualIngestionEditorModel editor,
        CancellationToken cancellationToken = default) =>
        ObserveAsync(() => ingestionService.EnqueueAsync(selectedProviderInstanceId, editor, cancellationToken));

    public Task<MemoryProviderEventAcknowledgeUiResult> AcknowledgeEventAsync(
        string? selectedProviderInstanceId,
        string providerEventId,
        bool accepted,
        CancellationToken cancellationToken = default) =>
        ObserveAsync(() => ledgerActionService.AcknowledgeEventAsync(
            selectedProviderInstanceId,
            providerEventId,
            accepted,
            cancellationToken));
    private async Task<T> ObserveAsync<T>(Func<Task<T>> action, [CallerMemberName] string actionName = "") {
        origin.RequireCurrent();
        try {
            return await action();
        } catch (MemoryActionRefusedException) {
            logger.LogDebug("Memory action {Action} was refused before dispatch.", actionName);
            throw;
        } catch (Exception exception) {
            logger.LogWarning("Memory action {Action} did not return a final result; exception type {ExceptionType}. Review the recorded outcome before retrying.", actionName, exception.GetType().Name);
            throw;
        }
    }
}
