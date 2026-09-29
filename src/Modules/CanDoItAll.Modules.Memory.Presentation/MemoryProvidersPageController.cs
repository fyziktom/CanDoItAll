using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.UI;
using CanDoItAll.Modules.Memory.Services;

namespace CanDoItAll.Modules.Memory.Pages;

public sealed class MemoryProvidersPageController(IMemoryProviderManagementUiService owner) : IMemoryWorkspace, IDisposable {
    private const int SubmissionLimit = 32;
    private readonly List<MemorySubmission> submissions = [];
    private CancellationTokenSource? read;
    private long readVersion;
    private long selectionVersion;
    private bool disposed;
    private string? readError;

    public event Action? Changed;
    public MemoryWorkspaceDraft Draft { get; private set; } = new(null);
    public MemoryProviderManagementSnapshot? Snapshot { get; private set; }
    public string? SelectedProviderId { get; private set; }
    public MemoryProviderManagementProfile? SelectedProvider => Snapshot?.SelectedProvider;
    public MemoryProviderProfileEditorModel Editor => Draft.Profile;
    public MemoryQueryEditorModel QueryEditor => Draft.Query;
    public MemoryFeedbackEditorModel FeedbackEditor => Draft.Feedback;
    public MemoryManualIngestionEditorModel IngestionEditor => Draft.Ingestion;
    public MemoryProviderQueryUiResult? QueryResult => Draft.QueryResult;
    public MemoryProviderOperationUiResult? OperationActionResult => Draft.OperationResult;
    public MemoryProviderFeedbackUiResult? FeedbackActionResult => Draft.FeedbackResult;
    public MemoryProviderManualIngestionUiResult? IngestionResult => Draft.IngestionResult;
    public MemoryProviderEventAcknowledgeUiResult? EventActionResult => Draft.EventResult;
    public IReadOnlyList<MemorySubmission> Submissions => submissions;
    public int ActiveTabIndex { get; set; }
    public bool IsLoading { get; private set; } = true;
    public bool IsBusy => IsLoading || Blocked(Editor.InstanceId.Trim()) || submissions.Any(s => s.Action == MemoryAction.Demo && s.BlocksDispatch);
    public bool IsQueryBusy => Blocked(SelectedProviderId);
    public bool IsFeedbackBusy => Blocked(SelectedProviderId);
    public bool IsOperationBusy => Blocked(SelectedProviderId);
    public bool IsEventBusy => Blocked(SelectedProviderId);
    public bool IsIngestionBusy => Blocked(SelectedProviderId);
    public string? ErrorMessage { get => readError ?? Draft.ErrorMessage; private set => Draft.ErrorMessage = value; }

    public Task RefreshAsync() => ReadAsync();

    public Task SelectProviderAsync(string providerId) {
        if (disposed) {
            return Task.CompletedTask;
        }
        readError = null;
        SelectedProviderId = providerId;
        Draft = new(providerId);
        ErrorMessage = null;
        if (Snapshot is not null) {
            Snapshot = Snapshot with { SelectedProvider = null, Editor = null, Operations = [], Feedback = [], Events = [], ProviderUiSurfaces = [], FailedRegions = [] };
        }
        return ReadAsync();
    }

    public Task ResetDraftAsync() {
        Draft = new(SelectedProviderId);
        return ReadAsync();
    }

    public Task SaveProviderAsync() {
        var input = Editor.Capture();
        try {
            input.InstanceId = MemoryProviderInstanceId.Parse(input.InstanceId).Value;
        } catch (ArgumentException) {
            ErrorMessage = "Provider ID must be a valid, nonempty identifier.";
            Notify();
            return Task.CompletedTask;
        }
        return ExecuteAsync(MemoryAction.Save, input.InstanceId,
            () => owner.SaveProviderAsync(input),
            (draft, receipt, result) => {
                receipt.SavedProviders = [result.InstanceId];
                receipt.Message = $"Saved profile '{result.InstanceId.Value}'. Changing the ID creates or updates that destination; it does not rename the original.";
            });
    }

    public Task AddDemoProvidersAsync() => ExecuteAsync(MemoryAction.Demo, null,
        () => owner.CreateDemoProvidersAsync(),
        (draft, receipt, result) => {
            receipt.SavedProviders = result.Select(p => p.InstanceId).ToArray();
            receipt.Message = $"Demo action returned {result.Count} saved profiles. Existing demo IDs were retained.";
        });

    public Task RunQueryAsync() {
        var input = QueryEditor.Capture();
        var provider = SelectedProviderId;
        return ExecuteAsync(MemoryAction.Query, provider, () => owner.RunQueryAsync(provider, input),
            (draft, receipt, result) => {
                receipt.QueryResult = result;
                receipt.OperationId = result.Operation?.OperationId ?? result.AcceptedOperation?.OperationId;
                receipt.Message = result.Diagnostic;
                draft.QueryResult = result;
                if (result.ContextPack is { } pack && string.IsNullOrWhiteSpace(draft.Feedback.ContextPackId)) {
                    draft.Feedback.ContextPackId = pack.ContextPackId.Value.ToString("D");
                }
            });
    }

    public Task SubmitFeedbackAsync() {
        var input = FeedbackEditor.Capture();
        var provider = SelectedProviderId;
        return ExecuteAsync(MemoryAction.Feedback, provider, () => owner.SubmitFeedbackAsync(provider, input),
            (draft, receipt, result) => {
                receipt.FeedbackResult = result;
                receipt.Message = result.Diagnostic;
                draft.FeedbackResult = result;
            });
    }

    public Task EnqueueManualIngestionAsync() {
        var input = IngestionEditor.Capture();
        var provider = SelectedProviderId;
        return ExecuteAsync(MemoryAction.Ingestion, provider, () => owner.EnqueueManualIngestionAsync(provider, input),
            (draft, receipt, result) => {
                receipt.IngestionResult = result;
                receipt.OperationId = result.OperationId;
                receipt.Message = result.Diagnostic;
                draft.IngestionResult = result;
            });
    }

    public Task RefreshOperationAsync(MemoryOperationId operationId) => OperationAsync(operationId, MemoryAction.Status);
    public Task CancelOperationAsync(MemoryOperationId operationId) => OperationAsync(operationId, MemoryAction.Cancel);

    private Task OperationAsync(MemoryOperationId operationId, MemoryAction action) {
        var provider = SelectedProviderId;
        if (Snapshot?.Operations.Any(o => o.OperationId == operationId && o.ProviderInstanceId.Value == provider) != true) {
            ErrorMessage = "The operation does not belong to the current provider snapshot. Refresh before continuing.";
            Notify();
            return Task.CompletedTask;
        }
        return ExecuteAsync(action, provider,
            () => action == MemoryAction.Status ? owner.RefreshOperationAsync(operationId.Value.ToString("D")) : owner.CancelOperationAsync(operationId.Value.ToString("D")),
            (draft, receipt, result) => {
                receipt.OperationResult = result;
                receipt.OperationId = operationId;
                receipt.Message = result.Diagnostic;
                draft.OperationResult = result;
            }, operationId);
    }

    public Task AcknowledgeEventAsync(MemoryProviderEventId eventId) {
        var provider = SelectedProviderId;
        if (Snapshot?.Events.Any(e => e.ProviderEventId == eventId && e.ProviderInstanceId.Value == provider) != true) {
            ErrorMessage = "The event does not belong to the current provider snapshot. Refresh before continuing.";
            Notify();
            return Task.CompletedTask;
        }
        return ExecuteAsync(MemoryAction.Acknowledge, provider,
            () => owner.AcknowledgeEventAsync(provider, eventId.Value.ToString("D"), true),
            (draft, receipt, result) => {
                receipt.EventResult = result;
                receipt.Message = result.Diagnostic;
                draft.EventResult = result;
            });
    }

    private async Task ExecuteAsync<T>(MemoryAction action, string? provider, Func<Task<T>> dispatch,
        Action<MemoryWorkspaceDraft, MemorySubmission, T> observe, MemoryOperationId? operationId = null) {
        if (disposed || !owner.IsCurrent) {
            ErrorMessage = "The database profile changed. Open a new Memory workspace before continuing.";
            Notify();
            return;
        }
        if (Blocked(provider) || submissions.Any(s => s.Action == MemoryAction.Demo && s.BlocksDispatch) ||
            (action == MemoryAction.Demo && (Blocked(MemoryDemoProviderIds.Business) || Blocked(MemoryDemoProviderIds.Programming)))) {
            ErrorMessage = "This destination has a pending or unresolved action. Review its outcome before another dispatch.";
            Notify();
            return;
        }
        if (action is not (MemoryAction.Save or MemoryAction.Demo) && SelectedProvider is null) {
            ErrorMessage = "Select an existing provider before running this action.";
            Notify();
            return;
        }
        if (submissions.Count >= SubmissionLimit) {
            var completed = submissions.FirstOrDefault(s => !s.BlocksDispatch && !s.IsReviewing);
            if (completed is null) {
                ErrorMessage = "The workspace has 32 unresolved actions. Resolve their outcomes before continuing.";
                Notify();
                return;
            }
            submissions.Remove(completed);
        }
        var draft = Draft;
        var selectedVersion = selectionVersion;
        var receipt = new MemorySubmission(draft.Origin, provider, action) { OperationId = operationId };
        submissions.Add(receipt);
        ErrorMessage = null;
        Notify();
        try {
            var result = await dispatch();
            var current = Current(draft) && selectedVersion == selectionVersion;
            observe(current ? draft : new MemoryWorkspaceDraft(provider), receipt, result);
            receipt.State = receipt.IngestionResult?.SnapshotReadFailed == true ? MemoryEffectState.ReadbackWarning : MemoryEffectState.Observed;
            if (current && !await ReadAsync()) {
                receipt.State = MemoryEffectState.ReadbackWarning;
                receipt.Message += " The action returned; the snapshot could not be refreshed. Refresh reads only and never repeats this action.";
            }
        } catch (MemoryActionRefusedException exception) {
            receipt.State = MemoryEffectState.Refused;
            receipt.Message = exception.Message;
            if (Current(draft) && selectedVersion == selectionVersion) {
                ErrorMessage = exception.Message;
            }
        } catch (MemoryDemoInterruptedException exception) {
            receipt.SavedProviders = exception.Saved.Select(p => p.InstanceId).ToArray();
            receipt.State = MemoryEffectState.Unknown;
            receipt.Message = exception.Message;
        } catch (Exception) {
            receipt.State = MemoryEffectState.Unknown;
            receipt.Message = "No final owner result was received. The action may have taken effect. Review exact stored identity; do not replay it automatically.";
        } finally {
            Notify();
        }
    }

    public async Task ReviewAsync(MemorySubmission receipt) {
        if (disposed || !owner.IsCurrent || !submissions.Contains(receipt) || receipt.State != MemoryEffectState.Unknown || receipt.IsReviewing) {
            return;
        }
        receipt.IsReviewing = true;
        Notify();
        try {
            var snapshot = await owner.GetSnapshotAsync(receipt.ProviderId);
            if (disposed || !owner.IsCurrent || receipt.State != MemoryEffectState.Unknown) {
                return;
            }
            var observed = receipt.Action switch {
                MemoryAction.Save => snapshot.SelectedProvider?.InstanceId.Value == receipt.ProviderId,
                MemoryAction.Demo => true,
                _ => receipt.OperationId is { } id && !snapshot.FailedRegions.Contains(MemoryReadRegion.Operations) && snapshot.Operations.Any(o => o.OperationId == id && o.ProviderInstanceId.Value == receipt.ProviderId)
            };
            if (receipt.Action == MemoryAction.Demo) {
                receipt.SavedProviders = snapshot.Providers.Where(p => p.InstanceId.Value is MemoryDemoProviderIds.Business or MemoryDemoProviderIds.Programming).Select(p => p.InstanceId).ToArray();
            }
            receipt.State = observed ? MemoryEffectState.Reviewed : MemoryEffectState.Unknown;
            receipt.Message = observed
                ? "Exact stored identity was observed. This is a read-only observation, not proof that the earlier command completed. Inspect current values before another action."
                : "No exact stored identity could be verified. The outcome remains unresolved; no action was replayed.";
        } catch (Exception) {
            if (!disposed && owner.IsCurrent && receipt.State == MemoryEffectState.Unknown) {
                receipt.Message = "The exact-identity review failed. The outcome remains unresolved; no action was replayed.";
            }
        } finally {
            receipt.IsReviewing = false;
            Notify();
        }
    }

    private async Task<bool> ReadAsync() {
        if (disposed) {
            return false;
        }
        read?.Cancel();
        var lane = new CancellationTokenSource();
        read = lane;
        var version = ++readVersion;
        var draft = Draft;
        IsLoading = true;
        Notify();
        bool IsCurrentRead() => Current(draft) && version == readVersion;
        try {
            var next = await owner.GetSnapshotAsync(SelectedProviderId, lane.Token);
            if (!IsCurrentRead()) {
                return false;
            }
            if (SelectedProviderId is null && next.SelectedProvider is not null) {
                if (!draft.IsInitialized) {
                    SelectedProviderId = next.SelectedProvider.InstanceId.Value;
                } else {
                    next = next with { SelectedProvider = null, SelectedRevision = null, Editor = null, Operations = [], Feedback = [], Events = [], ProviderUiSurfaces = [] };
                }
            }
            if (Snapshot?.SelectedProvider?.InstanceId == next.SelectedProvider?.InstanceId) {
                next = next with {
                    Operations = next.FailedRegions.Contains(MemoryReadRegion.Operations) ? Snapshot?.Operations ?? [] : next.Operations,
                    Feedback = next.FailedRegions.Contains(MemoryReadRegion.Feedback) ? Snapshot?.Feedback ?? [] : next.Feedback,
                    Events = next.FailedRegions.Contains(MemoryReadRegion.Events) ? Snapshot?.Events ?? [] : next.Events
                };
            }
            if (Snapshot?.SelectedRevision != next.SelectedRevision) {
                selectionVersion++;
            }
            Snapshot = next;
            if (!draft.IsInitialized) {
                draft.Profile = next.Editor?.Capture() ?? new();
                draft.IsInitialized = true;
            }
            readError = next.FailedRegions.Count > 0 ? "Some ledgers could not be read. Their retained rows are stale; refresh to retry reads." : null;
            if (SelectedProviderId is not null && next.SelectedProvider is null) {
                readError = "The selected provider is missing. Its draft is retained; no other provider was selected automatically.";
            }
            return next.FailedRegions.Count == 0;
        } catch (Exception) {
            if (IsCurrentRead()) {
                readError = Snapshot is null ? "Memory provider state is unavailable. Refresh to retry the read." : "Memory provider state could not be refreshed. Displayed records are stale; your drafts are retained.";
            }
            return false;
        } finally {
            if (version == readVersion && !disposed) {
                IsLoading = false;
                if (!owner.IsCurrent) {
                    Snapshot = null;
                    readError = "The database profile changed. Open a new Memory workspace before continuing.";
                }
                Notify();
            }
            if (ReferenceEquals(read, lane)) {
                read = null;
            }
            lane.Dispose();
        }
    }

    private bool Blocked(string? provider) => submissions.Any(s => s.BlocksDispatch && string.Equals(s.ProviderId, provider, StringComparison.Ordinal));
    private bool Current(MemoryWorkspaceDraft draft) => !disposed && owner.IsCurrent && ReferenceEquals(Draft, draft);
    private void Notify() {
        if (!disposed) {
            Changed?.Invoke();
        }
    }
    public void Dispose() {
        disposed = true;
        readVersion++;
        read?.Cancel();
        Changed = null;
    }
}
