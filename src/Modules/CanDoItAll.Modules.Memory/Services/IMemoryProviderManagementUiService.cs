using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.Memory.Services;

public interface IMemoryProviderManagementUiService
{
    Task<MemoryProviderManagementSnapshot> GetSnapshotAsync(
        string? selectedProviderInstanceId = null,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderProfile> SaveProviderAsync(
        MemoryProviderProfileEditorModel editor,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryProviderProfile>> CreateDemoProvidersAsync(CancellationToken cancellationToken = default);

    Task<MemoryProviderQueryUiResult> RunQueryAsync(
        string? selectedProviderInstanceId,
        MemoryQueryEditorModel editor,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderOperationUiResult> RefreshOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderOperationUiResult> CancelOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderFeedbackUiResult> SubmitFeedbackAsync(
        string? selectedProviderInstanceId,
        MemoryFeedbackEditorModel editor,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderManualIngestionUiResult> EnqueueManualIngestionAsync(
        string? selectedProviderInstanceId,
        MemoryManualIngestionEditorModel editor,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderEventAcknowledgeUiResult> AcknowledgeEventAsync(
        string? selectedProviderInstanceId,
        string providerEventId,
        bool accepted,
        CancellationToken cancellationToken = default);
}
