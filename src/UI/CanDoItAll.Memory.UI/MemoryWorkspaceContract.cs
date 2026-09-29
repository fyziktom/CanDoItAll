using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.Memory.Services;

namespace CanDoItAll.Memory.UI;

public interface IMemoryWorkspace {
    event Action? Changed;
    MemoryWorkspaceDraft Draft { get; }
    MemoryProviderManagementSnapshot? Snapshot { get; }
    MemoryProviderManagementProfile? SelectedProvider { get; }
    string? SelectedProviderId { get; }
    MemoryProviderProfileEditorModel Editor { get; }
    MemoryQueryEditorModel QueryEditor { get; }
    MemoryFeedbackEditorModel FeedbackEditor { get; }
    MemoryManualIngestionEditorModel IngestionEditor { get; }
    MemoryProviderQueryUiResult? QueryResult { get; }
    MemoryProviderOperationUiResult? OperationActionResult { get; }
    MemoryProviderFeedbackUiResult? FeedbackActionResult { get; }
    MemoryProviderManualIngestionUiResult? IngestionResult { get; }
    MemoryProviderEventAcknowledgeUiResult? EventActionResult { get; }
    IReadOnlyList<MemorySubmission> Submissions { get; }
    int ActiveTabIndex { get; set; }
    bool IsLoading { get; }
    bool IsBusy { get; }
    bool IsQueryBusy { get; }
    bool IsFeedbackBusy { get; }
    bool IsOperationBusy { get; }
    bool IsEventBusy { get; }
    bool IsIngestionBusy { get; }
    string? ErrorMessage { get; }
    Task RefreshAsync();
    Task SelectProviderAsync(string providerId);
    Task ResetDraftAsync();
    Task SaveProviderAsync();
    Task AddDemoProvidersAsync();
    Task RunQueryAsync();
    Task SubmitFeedbackAsync();
    Task EnqueueManualIngestionAsync();
    Task RefreshOperationAsync(MemoryOperationId operationId);
    Task CancelOperationAsync(MemoryOperationId operationId);
    Task AcknowledgeEventAsync(MemoryProviderEventId eventId);
    Task ReviewAsync(MemorySubmission submission);
}

public sealed class MemoryWorkspaceDraft(string? providerId) {
    public Guid Origin { get; } = Guid.NewGuid();
    public string? ProviderId { get; } = providerId;
    public MemoryProviderProfileEditorModel Profile { get; set; } = new();
    public MemoryQueryEditorModel Query { get; } = new();
    public MemoryFeedbackEditorModel Feedback { get; } = new();
    public MemoryManualIngestionEditorModel Ingestion { get; } = new();
    public bool IsInitialized { get; set; }
    public string? ErrorMessage { get; set; }
    public MemoryProviderQueryUiResult? QueryResult { get; set; }
    public MemorySubmission? QuerySubmission { get; set; }
    public long? AutoFeedbackContextVersion { get; set; }
    public MemoryProviderOperationUiResult? OperationResult { get; set; }
    public MemoryProviderFeedbackUiResult? FeedbackResult { get; set; }
    public MemoryProviderManualIngestionUiResult? IngestionResult { get; set; }
    public MemoryProviderEventAcknowledgeUiResult? EventResult { get; set; }
}

public enum MemoryAction { Save, Demo, Query, Status, Cancel, Feedback, Ingestion, Acknowledge }
public enum MemoryEffectState { Pending, Refused, Observed, ReadbackWarning, Unknown, Reviewed }

public sealed class MemorySubmission(Guid origin, string? providerId, MemoryAction action) {
    public Guid Id { get; } = Guid.NewGuid();
    public Guid Origin { get; } = origin;
    public string? ProviderId { get; } = providerId;
    public MemoryProviderRevision? ProviderRevision { get; init; }
    public MemoryAction Action { get; } = action;
    public MemoryEffectState State { get; set; } = MemoryEffectState.Pending;
    public string Message { get; set; } = "Waiting for the owner result.";
    public bool IsReviewing { get; set; }
    public MemoryOperationId? OperationId { get; set; }
    public IReadOnlyList<MemoryProviderInstanceId> SavedProviders { get; set; } = [];
    public MemoryProviderQueryUiResult? QueryResult { get; set; }
    public MemoryProviderOperationUiResult? OperationResult { get; set; }
    public MemoryProviderFeedbackUiResult? FeedbackResult { get; set; }
    public MemoryProviderManualIngestionUiResult? IngestionResult { get; set; }
    public MemoryProviderEventAcknowledgeUiResult? EventResult { get; set; }
    public bool BlocksDispatch => State is MemoryEffectState.Pending or MemoryEffectState.Unknown;
}
