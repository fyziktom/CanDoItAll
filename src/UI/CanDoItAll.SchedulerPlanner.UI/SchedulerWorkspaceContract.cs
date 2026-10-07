using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.SchedulerPlanner;

namespace CanDoItAll.SchedulerPlanner.UI;

public enum SchedulerTab { Calendar, Schedules, NewSchedule, History }
public enum SchedulerReadStatus { Initial, Loading, Ready, Stale, Unavailable }
public enum SchedulerMutationStatus { Pending, Refused, Committed, CommittedWithWarning, Unknown }

public sealed record SchedulerMutationReceipt(
    SchedulerMutationKind Kind, SchedulerMutationStatus Status, Guid? PlanId,
    SchedulerMutationStage Stage, string Message, SchedulerDraftValues? SavedValues = null);

public sealed record SchedulerDraftValues {
    public Guid? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public SchedulerPlanTargetKind TargetKind { get; init; } = SchedulerPlanTargetKind.Workflow;
    public Guid TargetId { get; init; }
    public Guid? TargetVersionId { get; init; }
    public string CronExpression { get; init; } = "0 0 9 ? * MON-FRI";
    public string TimeZoneId { get; init; } = "UTC";
    public SchedulerPlanMisfirePolicy MisfirePolicy { get; init; } = SchedulerPlanMisfirePolicy.FireOnceNow;
    public bool IsEnabled { get; init; } = true;
    public DateTimeOffset? StartAtUtc { get; init; }
    public DateTimeOffset? EndAtUtc { get; init; }
    public string InputJson { get; init; } = "{}";
}

public enum SchedulerDraftField { Id, Name, Description, TargetKind, TargetId, TargetVersionId, CronExpression, TimeZoneId, MisfirePolicy, IsEnabled, StartAtUtc, EndAtUtc, InputJson }

public sealed class SchedulerDraft(SchedulerDraftValues values) {
    public Guid Origin { get; } = Guid.NewGuid();
    private SchedulerDraftValues values = values;
    private readonly Dictionary<SchedulerDraftField, long> revisions = [];
    public long Revision { get; private set; }
    public long LastChanged(SchedulerDraftField field) => revisions.GetValueOrDefault(field);
    public SchedulerDraftValues Values {
        get => values;
        set {
            if (values == value) {
                return;
            }
            Revision++;
            if (values.Id != value.Id) {
                revisions[SchedulerDraftField.Id] = Revision;
            }
            if (values.Name != value.Name) {
                revisions[SchedulerDraftField.Name] = Revision;
            }
            if (values.Description != value.Description) {
                revisions[SchedulerDraftField.Description] = Revision;
            }
            if (values.TargetKind != value.TargetKind) {
                revisions[SchedulerDraftField.TargetKind] = Revision;
            }
            if (values.TargetId != value.TargetId) {
                revisions[SchedulerDraftField.TargetId] = Revision;
            }
            if (values.TargetVersionId != value.TargetVersionId) {
                revisions[SchedulerDraftField.TargetVersionId] = Revision;
            }
            if (values.CronExpression != value.CronExpression) {
                revisions[SchedulerDraftField.CronExpression] = Revision;
            }
            if (values.TimeZoneId != value.TimeZoneId) {
                revisions[SchedulerDraftField.TimeZoneId] = Revision;
            }
            if (values.MisfirePolicy != value.MisfirePolicy) {
                revisions[SchedulerDraftField.MisfirePolicy] = Revision;
            }
            if (values.IsEnabled != value.IsEnabled) {
                revisions[SchedulerDraftField.IsEnabled] = Revision;
            }
            if (values.StartAtUtc != value.StartAtUtc) {
                revisions[SchedulerDraftField.StartAtUtc] = Revision;
            }
            if (values.EndAtUtc != value.EndAtUtc) {
                revisions[SchedulerDraftField.EndAtUtc] = Revision;
            }
            if (values.InputJson != value.InputJson) {
                revisions[SchedulerDraftField.InputJson] = Revision;
            }
            values = value;
        }
    }
    public SchedulerDraftValues Baseline { get; set; } = values;
    public bool HasUnappliedInput { get; set; }
    public bool IsDirty => Values != Baseline || HasUnappliedInput;
    public void MarkInputEdited() {
        revisions[SchedulerDraftField.InputJson] = ++Revision;
        HasUnappliedInput = true;
    }
    public SchedulerMutationReceipt? Receipt { get; set; }
    public bool IsReviewing { get; set; }
    public bool IsLocked => Receipt?.Status is SchedulerMutationStatus.Pending or SchedulerMutationStatus.Unknown;
    public string Error { get; set; } = string.Empty;
    public SchedulerReadStatus SchemaStatus { get; set; }
    public SchedulerWorkflowInputSchema? Schema { get; set; }
    public Dictionary<string, string> InputValues { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, SchedulerInputOptions> Options { get; } = new(StringComparer.Ordinal);
    public IReadOnlyList<SchedulerWorkflowInputValidationIssue> Issues { get; set; } = [];
}

public sealed record SchedulerInputOptions(SchedulerReadStatus Status,
    IReadOnlyList<WorkflowInputParameterOption> Values, string? Error = null);

public sealed record SchedulerWorkspaceData(
    IReadOnlyList<SchedulerPlanSummary> Plans, IReadOnlyList<SchedulerPlanRunSummary> History,
    IReadOnlyList<SchedulerTargetOption> TargetOptions, CanvasCalendarSurface CalendarSurface);

public interface ISchedulerWorkspace {
    event Action? Changed;
    SchedulerWorkspaceData? Data { get; }
    SchedulerReadStatus ReadStatus { get; }
    string Error { get; }
    SchedulerTab Tab { get; set; }
    long ViewGeneration { get; }
    SchedulerHistoryQuery HistoryQuery { get; }
    SchedulerDraft NewDraft { get; }
    SchedulerDraft? EditDraft { get; }
    SchedulerDraft? PickerDraft { get; }
    SchedulerPlanSummary? DeletePlan { get; }
    Guid? SelectedCalendarPlanId { get; }
    bool EditorLoading { get; }
    Guid? EditingPlanId { get; }
    IReadOnlyList<SchedulerDraft> RetainedDrafts { get; }
    IReadOnlyDictionary<Guid, SchedulerMutationReceipt> PlanReceipts { get; }
    bool IsPlanLocked(Guid id);
    bool IsPlanReviewing(Guid id);
    string DescribeCron(SchedulerDraftValues values);
    Task RefreshAsync();
    Task ResetAsync();
    Task EditAsync(Guid id);
    void ResumeDraft(SchedulerDraft draft);
    void CloseEdit();
    void OpenPicker(SchedulerDraft draft);
    void ClosePicker();
    Task SelectTargetAsync(SchedulerTargetOption target);
    void SetValues(SchedulerDraft draft, SchedulerDraftValues values);
    Task SetRawInputAsync(SchedulerDraft draft, string value);
    Task SetInputAsync(SchedulerDraft draft, WorkflowInputParameterDescriptor parameter, string value);
    Task SaveAsync(SchedulerDraft draft);
    Task ToggleAsync(SchedulerPlanSummary plan);
    void AskDelete(SchedulerPlanSummary plan);
    void CloseDelete();
    Task DeleteAsync();
    Task ReviewUnknownAsync(SchedulerDraft draft, Guid exactPlanId);
    Task ReviewPlanAsync(Guid id);
    void AcknowledgeReceipt(Guid id);
    void SelectCalendar(CanvasCalendarEvent? item);
    void SetCalendarState(CanvasCalendarStateChangedEventArgs state);
}
