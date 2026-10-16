using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.SchedulerPlanner.UI;

namespace CanDoItAll.Modules.SchedulerPlanner.Presentation;

public sealed class SchedulerWorkspace : ISchedulerWorkspace, IDisposable {
    private const string CalendarDateFormat = "yyyy-MM-dd";
    private const SchedulerCalendarView DefaultCalendarView = SchedulerCalendarView.Week;
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
    private CalendarPosition? calendarStatePosition;
    private CalendarPosition calendarPosition;
    private CanvasCalendarSurface? calendarDefaultsSource;
    private DateOnly? calendarDefaultDate;
    private string historySearch = string.Empty;
    private SchedulerPlanRunDispatchStatus? historyStatus;
    private SchedulerPlanTargetKind? historyKind;

    private readonly record struct CalendarPosition(SchedulerCalendarView? View, DateOnly? Date);

    public event Action? Changed;
    // Raised after a user intent changed addressable state; the routed page records it in the URL.
    public event Action<SchedulerNavigationKind>? Navigated;
    public SchedulerWorkspaceData? Data { get; private set; }
    public SchedulerReadStatus ReadStatus { get; private set; }
    public string Error { get; private set; } = string.Empty;
    public long ViewGeneration { get; private set; }
    public SchedulerTab Tab {
        get => tab;
        set {
            if (SetTab(value)) {
                Navigate(SchedulerNavigationKind.Tab);
            }
        }
    }
    public SchedulerHistoryQuery HistoryQuery { get; } = new();
    public SchedulerScheduleFilters ScheduleFilters { get; private set; } = new();
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

    // The addressable state; drafts, the target picker and confirmations are not part of it.
    public SchedulerRoute Route => new(tab, EditingPlanId, calendarPosition.View, calendarPosition.Date,
        ScheduleFilters.Search, ScheduleFilters.TargetKind, ScheduleFilters.State, historySearch, historyStatus, historyKind);

    public Task InitializeAsync() => InitializeAsync(new SchedulerRoute());

    public Task InitializeAsync(SchedulerRoute route) {
        AdoptRoute(route);
        var editor = route.PlanId is { } id ? OpenEditorAsync(id, announce: false) : Task.CompletedTask;
        return Task.WhenAll(RefreshAsync(), LoadDefaultAsync(NewDraft), editor);
    }

    // Applies an address (a link, Back or Forward) without raising navigation: the page already owns it.
    // State a tab does not show stays as it is when the address leaves it out.
    public Task ApplyRouteAsync(SchedulerRoute route) {
        if (disposed) {
            return Task.CompletedTask;
        }
        var historyChanged = AdoptRoute(route);
        var editor = Task.CompletedTask;
        if (route.PlanId != EditingPlanId) {
            if (route.PlanId is { } id) {
                editor = OpenEditorAsync(id, announce: false);
            } else {
                CloseEditor();
            }
        }
        Notify();
        return Task.WhenAll(editor, historyChanged ? RefreshAsync() : Task.CompletedTask);
    }

    private bool AdoptRoute(SchedulerRoute route) {
        SetTab(route.Tab);
        if (route.Tab == SchedulerTab.Calendar) {
            calendarPosition = new(route.CalendarView == DefaultCalendarView ? null : route.CalendarView, route.CalendarDate);
            if (Data is { } data) {
                RestoreCalendarState(data.CalendarSurface);
            }
        }
        if (route.Tab == SchedulerTab.Schedules) {
            ScheduleFilters = route.ScheduleFilters;
        }
        var search = route.HistorySearch.Trim();
        var historyChanged = search != historySearch || route.HistoryStatus != historyStatus || route.HistoryKind != historyKind;
        historySearch = search;
        historyStatus = route.HistoryStatus;
        historyKind = route.HistoryKind;
        if (historyChanged) {
            HistoryQuery.Search = search;
            HistoryQuery.Status = route.HistoryStatus;
            HistoryQuery.TargetKind = route.HistoryKind;
        }
        return historyChanged;
    }

    public bool IsPlanLocked(Guid id) => mutations.IsPlanLocked(id);
    public bool IsPlanReviewing(Guid id) => mutations.IsPlanReviewing(id);

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
            Search = historySearch, Status = historyStatus, TargetKind = historyKind,
            FromUtc = HistoryQuery.FromUtc, ToUtc = HistoryQuery.ToUtc, Take = HistoryQuery.Take
        };
        ReadStatus = SchedulerReadStatus.Loading;
        Notify();
        try {
            var data = await owner.ReadAsync(query, read.Token);
            if (!read.IsCurrent) {
                return;
            }
            CaptureCalendarDefaults(data.CalendarSurface);
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

    // Commits the history search draft: the committed query is the one every read uses.
    public Task SearchHistoryAsync() {
        if (disposed) {
            return Task.CompletedTask;
        }
        var search = HistoryQuery.Search?.Trim() ?? string.Empty;
        var changed = search != historySearch || HistoryQuery.Status != historyStatus || HistoryQuery.TargetKind != historyKind;
        historySearch = search;
        historyStatus = HistoryQuery.Status;
        historyKind = HistoryQuery.TargetKind;
        if (changed) {
            Navigate(SchedulerNavigationKind.HistorySearch);
        }
        return RefreshAsync();
    }

    public void SetScheduleFilters(SchedulerScheduleFilters filters) {
        ArgumentNullException.ThrowIfNull(filters);
        if (disposed || ScheduleFilters == filters) {
            return;
        }
        ScheduleFilters = filters;
        Notify();
        Navigate(SchedulerNavigationKind.ScheduleFilters);
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

    public Task EditAsync(Guid id) => OpenEditorAsync(id, announce: true);

    private async Task OpenEditorAsync(Guid id, bool announce) {
        if (disposed || !Retain(EditDraft)) {
            return;
        }
        using var read = editors.Begin();
        ViewGeneration++;
        EditDraft = null;
        EditingPlanId = id;
        EditorLoading = true;
        Notify();
        if (announce) {
            Navigate(SchedulerNavigationKind.OpenEditor);
        }
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
        var previous = EditingPlanId;
        editors.Dispose();
        EditDraft = draft;
        EditingPlanId = draft.Values.Id;
        EditorLoading = false;
        ViewGeneration++;
        Notify();
        if (EditingPlanId != previous) {
            Navigate(EditingPlanId is null ? SchedulerNavigationKind.CloseEditor : SchedulerNavigationKind.OpenEditor);
        }
    }

    public void CloseEdit() {
        var addressed = EditingPlanId is not null;
        if (CloseEditor() && addressed) {
            Navigate(SchedulerNavigationKind.CloseEditor);
        }
    }

    private bool CloseEditor() {
        if (!Retain(EditDraft)) {
            return false;
        }
        editors.Dispose();
        EditDraft = null;
        EditingPlanId = null;
        PickerDraft = null;
        EditorLoading = false;
        ViewGeneration++;
        Notify();
        return true;
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
            if (EditingPlanId == plan.Id) {
                // The deleted plan's editor has no record left to show.
                DiscardEditor();
                Navigate(SchedulerNavigationKind.EditorRemoved);
            }
        }
    }
    private void DiscardEditor() {
        editors.Dispose();
        if (EditDraft is { } draft) {
            drafts.Remove(draft);
            if (inputs.Remove(draft.Origin, out var input)) {
                input.Dispose();
            }
        }
        EditDraft = null;
        EditingPlanId = null;
        PickerDraft = null;
        EditorLoading = false;
        ViewGeneration++;
        Notify();
    }
    public void SelectCalendar(CanvasCalendarEvent? item) {
        SelectedCalendarPlanId = item is { ReadOnly: false } && Guid.TryParse(item.RepositoryId, out var id)
            && Data?.Plans.Any(plan => plan.Id == id) == true ? id : null;
        Notify();
    }

    // A moved view or anchor date is a navigation; a selection inside the shown range is not.
    public void SetCalendarState(CanvasCalendarStateChangedEventArgs state) {
        calendarState = state;
        var reported = ReadPosition(state);
        calendarStatePosition = reported;
        var moved = !SameCalendarPosition(reported, calendarPosition);
        if (moved) {
            calendarPosition = new(
                reported.View == DefaultCalendarView ? null : reported.View,
                reported.Date == calendarDefaultDate ? null : reported.Date);
        }
        if (Data is { } data) {
            RestoreCalendarState(data.CalendarSurface);
        }
        if (moved) {
            Navigate(SchedulerNavigationKind.Calendar);
        }
    }

    private CalendarPosition ReadPosition(CanvasCalendarStateChangedEventArgs state) {
        var view = ParseCalendarView(state.View) ?? calendarPosition.View ?? DefaultCalendarView;
        var anchor = ReadStateText(state.StateJson, "anchorDateKey");
        var date = ParseCalendarDate(anchor) ?? ParseCalendarDate(state.SelectedDate) ?? calendarPosition.Date ?? calendarDefaultDate;
        return new(view, date);
    }

    private bool SameCalendarPosition(CalendarPosition left, CalendarPosition right)
        => (left.View ?? DefaultCalendarView) == (right.View ?? DefaultCalendarView)
           && (left.Date ?? calendarDefaultDate) == (right.Date ?? calendarDefaultDate);

    // The service creates a new surface for every read; its view and date are the calendar defaults.
    private void CaptureCalendarDefaults(CanvasCalendarSurface surface) {
        if (ReferenceEquals(surface, calendarDefaultsSource)) {
            return;
        }
        calendarDefaultsSource = surface;
        calendarDefaultDate = ParseCalendarDate(surface.SelectedDate);
    }

    private void RestoreCalendarState(CanvasCalendarSurface surface) {
        if (calendarState is { } state) {
            surface.SelectedEventId = state.SelectedEventId ?? string.Empty;
            surface.Timezone = state.Timezone;
            if (calendarStatePosition is { } shown && SameCalendarPosition(shown, calendarPosition)) {
                surface.InitialView = state.View;
                surface.SelectedDate = state.SelectedDate;
                surface.ViewStateJson = state.StateJson;
                return;
            }
        }
        if (calendarState is null && calendarPosition == default) {
            return;
        }
        // The address moved the calendar (a link, Back or Forward); its position wins over the remembered state.
        var view = FormatCalendarView(calendarPosition.View ?? DefaultCalendarView);
        var date = (calendarPosition.Date ?? calendarDefaultDate)?.ToString(CalendarDateFormat, CultureInfo.InvariantCulture) ?? surface.SelectedDate;
        surface.InitialView = view;
        surface.SelectedDate = date;
        surface.ViewStateJson = RewriteStatePosition(calendarState?.StateJson ?? surface.ViewStateJson, view, date);
    }

    // The remembered calendar state names its own view and date, which would override the address.
    private static string RewriteStatePosition(string? stateJson, string view, string date) {
        if (string.IsNullOrWhiteSpace(stateJson)) {
            return string.Empty;
        }
        try {
            if (JsonNode.Parse(stateJson) is not JsonObject state) {
                return string.Empty;
            }
            state["preferredView"] = view;
            state["selectedDate"] = date;
            state["anchorDateKey"] = date;
            state.Remove("selectedDateKey");
            return state.ToJsonString();
        } catch (JsonException) {
            return string.Empty;
        }
    }

    private static string? ReadStateText(string? stateJson, string property) {
        if (string.IsNullOrWhiteSpace(stateJson)) {
            return null;
        }
        try {
            return JsonNode.Parse(stateJson) is JsonObject state && state[property] is JsonValue value && value.TryGetValue<string>(out var text)
                ? text : null;
        } catch (JsonException) {
            return null;
        }
    }

    private static DateOnly? ParseCalendarDate(string? value)
        => DateOnly.TryParseExact(value?.Trim(), CalendarDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static SchedulerCalendarView? ParseCalendarView(string? value) => value?.Trim().ToLowerInvariant() switch {
        "day" => SchedulerCalendarView.Day,
        "week" => SchedulerCalendarView.Week,
        "month" => SchedulerCalendarView.Month,
        "year" => SchedulerCalendarView.Year,
        "list" => SchedulerCalendarView.List,
        _ => null
    };

    private static string FormatCalendarView(SchedulerCalendarView view) => view switch {
        SchedulerCalendarView.Day => "day",
        SchedulerCalendarView.Week => "week",
        SchedulerCalendarView.Month => "month",
        SchedulerCalendarView.Year => "year",
        SchedulerCalendarView.List => "list",
        _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The calendar view has no calendar name.")
    };

    private bool SetTab(SchedulerTab value) {
        if (tab == value) {
            return false;
        }
        tab = value;
        ViewGeneration++;
        editors.Dispose();
        if (EditorLoading && EditDraft is null) {
            // The editor that had not opened yet is retired with its read.
            EditingPlanId = null;
        }
        EditorLoading = false;
        Notify();
        return true;
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
    private void Navigate(SchedulerNavigationKind kind) {
        if (!disposed) {
            Navigated?.Invoke(kind);
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
        Navigated = null;
    }
}
