using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderManagementUiService(
    MemoryProviderSnapshotReader snapshotReader,
    MemoryProviderProfileUiService profileService,
    MemoryProviderQueryUiService queryService,
    MemoryProviderLedgerActionUiService ledgerActionService,
    MemoryProviderIngestionUiService ingestionService) : IMemoryProviderManagementUiService
{
    public Task<MemoryProviderManagementSnapshot> GetSnapshotAsync(
        string? selectedProviderInstanceId = null,
        CancellationToken cancellationToken = default) =>
        snapshotReader.GetSnapshotAsync(selectedProviderInstanceId, cancellationToken);

    public Task<MemoryProviderProfile> SaveProviderAsync(
        MemoryProviderProfileEditorModel editor,
        CancellationToken cancellationToken = default) =>
        profileService.SaveAsync(editor, cancellationToken);

    public Task<IReadOnlyList<MemoryProviderProfile>> CreateDemoProvidersAsync(
        CancellationToken cancellationToken = default) =>
        profileService.CreateDemoProvidersAsync(cancellationToken);

    public Task<MemoryProviderQueryUiResult> RunQueryAsync(
        string? selectedProviderInstanceId,
        MemoryQueryEditorModel editor,
        CancellationToken cancellationToken = default) =>
        queryService.RunAsync(selectedProviderInstanceId, editor, cancellationToken);

    public Task<MemoryProviderOperationUiResult> RefreshOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default) =>
        ledgerActionService.RefreshOperationAsync(operationId, cancellationToken);

    public Task<MemoryProviderOperationUiResult> CancelOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default) =>
        ledgerActionService.CancelOperationAsync(operationId, cancellationToken);

    public Task<MemoryProviderFeedbackUiResult> SubmitFeedbackAsync(
        string? selectedProviderInstanceId,
        MemoryFeedbackEditorModel editor,
        CancellationToken cancellationToken = default) =>
        ledgerActionService.SubmitFeedbackAsync(selectedProviderInstanceId, editor, cancellationToken);

    public Task<MemoryProviderManualIngestionUiResult> EnqueueManualIngestionAsync(
        string? selectedProviderInstanceId,
        MemoryManualIngestionEditorModel editor,
        CancellationToken cancellationToken = default) =>
        ingestionService.EnqueueAsync(selectedProviderInstanceId, editor, cancellationToken);

    public Task<MemoryProviderEventAcknowledgeUiResult> AcknowledgeEventAsync(
        string? selectedProviderInstanceId,
        string providerEventId,
        bool accepted,
        CancellationToken cancellationToken = default) =>
        ledgerActionService.AcknowledgeEventAsync(
            selectedProviderInstanceId,
            providerEventId,
            accepted,
            cancellationToken);
}
