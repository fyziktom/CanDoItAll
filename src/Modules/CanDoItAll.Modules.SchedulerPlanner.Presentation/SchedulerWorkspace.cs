using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.SchedulerPlanner.UI;

namespace CanDoItAll.Modules.SchedulerPlanner.Presentation;

public sealed class SchedulerWorkspace : ISchedulerWorkspace, IDisposable {
    private readonly ISchedulerWorkspaceOwner owner;
    private readonly SchedulerMutations mutations;
    public SchedulerWorkspace(ISchedulerWorkspaceOwner owner) {
        this.owner = owner;
        mutations = new(owner, RefreshAsync, Notify, message => {
            Error = message;
            Notify();
        });
    }
    private readonly SchedulerReadLane reads = new();
    private readonly SchedulerReadLane defaults = new();
    private readonly SchedulerReadLane editors = new();
    private readonly List<SchedulerDraft> drafts = [];
    private readonly Dictionary<Guid, SchedulerInputSession> inputs = [];
    private SchedulerTab tab;
    private bool disposed;
    private CanvasCalendarStateChangedEventArgs? calendarState;

    public event Action? Changed;
    public SchedulerWorkspaceData? Data { get; private set; }
    public SchedulerReadStatus ReadStatus { get; private set; }
    public string Error { get; private set; } = string.Empty;
    public long ViewGeneration { get; private set; }
    public SchedulerTab Tab {
        get => tab;
        set {
            if (tab == value) {
                return;
            }
            tab = value;
            ViewGeneration++;
            editors.Dispose();
            EditorLoading = false;
            Notify();
        }
    }
    public SchedulerHistoryQuery HistoryQuery { get; } = new();
    public SchedulerDraft NewDraft { get; private set; } = new(new());
    public SchedulerDraft? EditDraft { get; private set; }
    public SchedulerDraft? PickerDraft { get; private set; }
    public SchedulerPlanSummary? DeletePlan { get; private set; }
    public Guid? SelectedCalendarPlanId { get; private set; }
    public bool EditorLoading { get; private set; }
    public bool DefaultLoading { get; private set; }
    public Guid? EditingPlanId { get; private set; }
    public IReadOnlyList<SchedulerDraft> RetainedDrafts => drafts;
    public IReadOnlyDictionary<Guid, SchedulerMutationReceipt> PlanReceipts => mutations.Receipts;

    public Task InitializeAsync() => Task.WhenAll(RefreshAsync(), LoadDefaultAsync(NewDraft));

    public bool IsPlanLocked(Guid id) => mutations.IsPlanLocked(id);

    public string DescribeCron(SchedulerDraftValues values) {
        try {
            return owner.DescribeCron(values.CronExpression, values.TimeZoneId);
        } catch (Exception exception) {
            return exception.Message;
        }
    }

    public async Task RefreshAsync() {
        if (disposed) {
            return;
        }
        using var read = reads.Begin();
        var query = new SchedulerHistoryQuery {
            Search = HistoryQuery.Search, Status = HistoryQuery.Status, TargetKind = HistoryQuery.TargetKind,
            FromUtc = HistoryQuery.FromUtc, ToUtc = HistoryQuery.ToUtc, Take = HistoryQuery.Take
        };
        ReadStatus = SchedulerReadStatus.Loading;
        Notify();
        try {
            var data = await owner.ReadAsync(query, read.Token);
            if (!read.IsCurrent) {
                return;
            }
            RestoreCalendarState(data.CalendarSurface);
            Data = data;
            ReadStatus = SchedulerReadStatus.Ready;
            Error = string.Empty;
        } catch (OperationCanceledException) when (read.Token.IsCancellationRequested) {
        } catch (Exception exception) {
            if (read.IsCurrent) {
                Error = exception.Message;
                ReadStatus = Data is null ? SchedulerReadStatus.Unavailable : SchedulerReadStatus.Stale;
            }
        } finally {
            Notify();
        }
    }

    public Task ResetAsync() {
        if (!Retain(NewDraft)) {
            return Task.CompletedTask;
        }
        defaults.Dispose();
        NewDraft = new(new());
        ViewGeneration++;
        Notify();
        return LoadDefaultAsync(NewDraft);
    }

    private async Task LoadDefaultAsync(SchedulerDraft draft) {
        using var read = defaults.Begin();
        DefaultLoading = true;
        var before = draft.Values;
        var revision = draft.Revision;
        try {
            var loaded = await owner.DefaultAsync(read.Token);
            if (read.IsCurrent && ReferenceEquals(draft, NewDraft) && draft.Values == before && draft.Revision == revision) {
                draft.Values = loaded;
                draft.Baseline = loaded;
                await Input(draft).LoadAsync();
            }
        } catch (OperationCanceledException) when (read.Token.IsCancellationRequested) {
        } catch (Exception exception) {
            if (read.IsCurrent && ReferenceEquals(draft, NewDraft)) {
                draft.Error = exception.Message;
            }
        } finally {
            if (read.IsCurrent) {
                DefaultLoading = false;
            }
            Notify();
        }
    }

    public async Task EditAsync(Guid id) {
        if (disposed || !Retain(EditDraft)) {
            return;
        }
        using var read = editors.Begin();
        ViewGeneration++;
        EditDraft = null;
        EditingPlanId = id;
        EditorLoading = true;
        Notify();
        var retained = drafts.FirstOrDefault(draft => draft.Values.Id == id);
        if (retained is not null) {
            EditDraft = retained;
            EditorLoading = false;
            Notify();
            return;
        }
        try {
            var values = await owner.EditorAsync(id, read.Token);
            if (read.IsCurrent) {
                EditDraft = new(values);
                await Input(EditDraft).LoadAsync();
            }
        } catch (OperationCanceledException) when (read.Token.IsCancellationRequested) {
        } catch (Exception exception) {
            if (read.IsCurrent) {
                Error = exception.Message;
            }
        } finally {
            if (read.IsCurrent) {
                EditorLoading = false;
            }
            Notify();
        }
    }

    public void ResumeDraft(SchedulerDraft draft) {
        if (!drafts.Contains(draft) || !Retain(EditDraft)) {
            return;
        }
        editors.Dispose();
        EditDraft = draft;
        EditingPlanId = draft.Values.Id;
        EditorLoading = false;
        ViewGeneration++;
        Notify();
    }

    public void CloseEdit() {
        if (!Retain(EditDraft)) {
            return;
        }
        editors.Dispose();
        EditDraft = null;
        EditingPlanId = null;
        PickerDraft = null;
        EditorLoading = false;
        ViewGeneration++;
        Notify();
    }

    public void OpenPicker(SchedulerDraft draft) {
        PickerDraft = draft;
        ViewGeneration++;
        Notify();
    }
    public void ClosePicker() {
        PickerDraft = null;
        ViewGeneration++;
        Notify();
    }
    public async Task SelectTargetAsync(SchedulerTargetOption target) {
        if (PickerDraft is not { } draft) {
            return;
        }
        SetValues(draft, draft.Values with { TargetKind = target.Kind, TargetId = target.Id, TargetVersionId = target.VersionId });
        ClosePicker();
        await Input(draft).LoadAsync();
    }
    public void SetValues(SchedulerDraft draft, SchedulerDraftValues values) {
        draft.Values = values;
        Notify();
    }
    public Task SetRawInputAsync(SchedulerDraft draft, string value) => Input(draft).RawAsync(value);
    public Task SetInputAsync(SchedulerDraft draft, WorkflowInputParameterDescriptor parameter, string value) => Input(draft).InputAsync(parameter, value);

    public Task SaveAsync(SchedulerDraft draft) => mutations.SaveAsync(draft);
    public Task ReviewUnknownAsync(SchedulerDraft draft, Guid exactPlanId) => mutations.ReviewUnknownAsync(draft, exactPlanId);
    public Task ReviewPlanAsync(Guid id) => mutations.ReviewPlanAsync(id);
    public void AcknowledgeReceipt(Guid id) => mutations.Acknowledge(id);

    public Task ToggleAsync(SchedulerPlanSummary plan) => mutations.ToggleAsync(plan);
    public void AskDelete(SchedulerPlanSummary plan) {
        DeletePlan = plan;
        ViewGeneration++;
        Notify();
    }
    public void CloseDelete() {
        DeletePlan = null;
        ViewGeneration++;
        Notify();
    }
    public async Task DeleteAsync() {
        if (DeletePlan is not { } plan) {
            return;
        }
        var generation = ViewGeneration;
        await mutations.DeleteAsync(plan.Id);
        if (generation == ViewGeneration && mutations.Receipts.GetValueOrDefault(plan.Id)?.Status is SchedulerMutationStatus.Committed or SchedulerMutationStatus.CommittedWithWarning) {
            CloseDelete();
        }
    }
    public void SelectCalendar(CanvasCalendarEvent? item) {
        SelectedCalendarPlanId = item is { ReadOnly: false } && Guid.TryParse(item.RepositoryId, out var id)
            && Data?.Plans.Any(plan => plan.Id == id) == true ? id : null;
        Notify();
    }
    public void SetCalendarState(CanvasCalendarStateChangedEventArgs state) {
        calendarState = state;
        if (Data is { } data) {
            RestoreCalendarState(data.CalendarSurface);
        }
    }
    private void RestoreCalendarState(CanvasCalendarSurface surface) {
        if (calendarState is not { } state) {
            return;
        }
        surface.InitialView = state.View;
        surface.SelectedDate = state.SelectedDate;
        surface.ViewStateJson = state.StateJson;
        surface.SelectedEventId = state.SelectedEventId ?? string.Empty;
        surface.Timezone = state.Timezone;
    }
    private SchedulerInputSession Input(SchedulerDraft draft) {
        if (!inputs.TryGetValue(draft.Origin, out var input)) {
            inputs[draft.Origin] = input = new(draft, owner, Notify);
        }
        return input;
    }
    private bool Retain(SchedulerDraft? draft) {
        foreach (var clean in drafts.Where(item => !item.IsDirty && !item.IsLocked && item != EditDraft).ToArray()) {
            drafts.Remove(clean);
            if (inputs.Remove(clean.Origin, out var input)) {
                input.Dispose();
            }
        }
        if (draft is null || drafts.Contains(draft)) {
            return true;
        }
        if (!draft.IsDirty && !draft.IsLocked) {
            if (inputs.Remove(draft.Origin, out var input)) {
                input.Dispose();
            }
            return true;
        }
        if (drafts.Count >= 16) {
            Error = "Review or save retained drafts before opening another one (limit 16).";
            Notify();
            return false;
        }
        drafts.Add(draft);
        return true;
    }
    private void Notify() {
        if (!disposed) {
            Changed?.Invoke();
        }
    }
    public void Dispose() {
        disposed = true;
        mutations.Dispose();
        ViewGeneration++;
        reads.Dispose();
        defaults.Dispose();
        editors.Dispose();
        foreach (var input in inputs.Values) {
            input.Dispose();
        }
        Changed = null;
    }


}
