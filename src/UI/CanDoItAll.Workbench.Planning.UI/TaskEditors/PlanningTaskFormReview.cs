using CanDoItAll.Editing;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Workbench.Planning.UI;

public sealed record TaskDeliveryInput(string Start, string End, string Duration);
public sealed record TaskEffortInput(string Amount, string Unit);
public sealed record TaskMoneyInput(string Amount, string Currency);

public sealed class PlanningTaskFormReview {
    private readonly PlanningTaskEditorInput local;

    public PlanningTaskFormReview(PlanningTaskEditorInput original, PlanningTaskEditorInput local,
        PlanningTaskComparisonCurrent current) {
        this.local = local;
        Current = current;
        if (original is PlanningTaskEditorInput.Structure b && local is PlanningTaskEditorInput.Structure l &&
            current.Input is PlanningTaskEditorInput.Structure r) {
            Title = new(b.Title, l.Title, r.Title, StringComparer.Ordinal);
            Subtitle = new(b.Subtitle, l.Subtitle, r.Subtitle, StringComparer.Ordinal);
            Notes = TextMerge.Compare(b.Notes, l.Notes, r.Notes).Field;
            Due = new(b.Due, l.Due, r.Due, TaskInputComparers.Date);
            Initialize(b.Estimate, l.Estimate, r.Estimate, b.Execution, l.Execution, r.Execution,
                b.Assignee, l.Assignee, r.Assignee);
        } else if (original is PlanningTaskEditorInput.Gantt bg && local is PlanningTaskEditorInput.Gantt lg &&
            current.Input is PlanningTaskEditorInput.Gantt rg) {
            Title = new(bg.Title, lg.Title, rg.Title, StringComparer.Ordinal);
            Delivery = new(new(bg.Start, bg.End, bg.Duration), new(lg.Start, lg.End, lg.Duration),
                new(rg.Start, rg.End, rg.Duration), TaskInputComparers.Delivery);
            Progress = new(bg.Progress, lg.Progress, rg.Progress, TaskInputComparers.Number);
            Initialize(bg.Estimate, lg.Estimate, rg.Estimate, bg.Execution, lg.Execution, rg.Execution,
                bg.Assignee, lg.Assignee, rg.Assignee);
        } else {
            throw new ArgumentException("A Task comparison requires the same editor shape for original, draft and current values.");
        }
    }

    public PlanningTaskComparisonCurrent Current { get; }
    public MergeField<string> Title { get; }
    public MergeField<string>? Subtitle { get; }
    public MergeField<string>? Notes { get; }
    public MergeField<string>? Due { get; }
    public MergeField<TaskDeliveryInput>? Delivery { get; }
    public MergeField<string>? Progress { get; }
    public MergeField<TaskEffortInput> Effort { get; private set; } = default!;
    public MergeField<TaskMoneyInput> Money { get; private set; } = default!;
    public MergeField<TaskExecutionInputText> Execution { get; private set; } = default!;
    public MergeField<ProjectStructureTaskResourceSelection?> Assignee { get; private set; } = default!;
    public long Revision { get; private set; }
    public bool CanEditCost => ProjectTaskExecutionStatePolicy.AllowsAuthoritativeRepricing(Execution.Current.State);
    public bool CanApply => Title.IsResolved && (Subtitle?.IsResolved ?? true) && (Notes?.IsResolved ?? true) &&
        (Due?.IsResolved ?? true) && (Delivery?.IsResolved ?? true) && (Progress?.IsResolved ?? true) &&
        Effort.IsResolved && Money.IsResolved && Execution.IsResolved && Assignee.IsResolved &&
        (CanEditCost || TaskInputComparers.Money.Equals(Money.Result, Money.Current)) &&
        (Current.CanChangeDirectAssignee || Assignee.Result == Assignee.Current) &&
        (!Current.ScheduleReadOnly || Delivery?.Result == Delivery?.Current);

    public void RecordInput() => Revision++;

    public string CopyResults() {
        List<string> lines = [$"Title: {Result(Title)}"];
        if (Subtitle is { } subtitle) {
            lines.Add($"Status lane: {Result(subtitle)}");
        }
        if (Notes is { } notes) {
            lines.Add($"Notes: {Result(notes)}");
        }
        if (Due is { } due) {
            lines.Add($"Due (UTC): {Result(due)}");
        }
        if (Delivery is { } delivery) {
            var interval = Result(delivery);
            lines.Add($"Delivery (UTC): Start: {interval.Start}; End: {interval.End}; Hours: {interval.Duration}");
        }
        if (Progress is { } progress) {
            lines.Add($"Progress (%; -1 means untracked): {Result(progress)}");
        }
        var effort = Result(Effort);
        var money = Result(Money);
        var execution = Result(Execution);
        var assignee = Result(Assignee);
        lines.Add($"Pure effort: {effort.Amount} {effort.Unit}");
        lines.Add($"Expected cost: {(string.IsNullOrEmpty(money.Amount) ? "Not set" : money.Amount)} {money.Currency}");
        lines.Add($"Execution (UTC): {execution.State}; Start: {execution.Started}; End: {execution.Ended}");
        lines.Add($"Direct assignee: {(assignee is null ? "No single direct assignee" : $"{assignee.Kind}; {assignee.ResourceId:D}")}");
        return string.Join(Environment.NewLine, lines);
    }

    private static T Result<T>(MergeField<T> field) => field.IsResolved ? field.Result : field.Local;

    private void Initialize(TaskEstimateInputText? b, TaskEstimateInputText? l, TaskEstimateInputText? r,
        TaskExecutionInputText? be, TaskExecutionInputText? le, TaskExecutionInputText? re,
        ProjectStructureTaskResourceSelection? ba, ProjectStructureTaskResourceSelection? la, ProjectStructureTaskResourceSelection? ra) {
        if (b is null || l is null || r is null || be is null || le is null || re is null) {
            throw new ArgumentException("The mounted Task estimate and execution controls are required for comparison.");
        }
        Effort = new(new(b.Effort, b.Unit), new(l.Effort, l.Unit), new(r.Effort, r.Unit), TaskInputComparers.Effort);
        Money = new(new(b.Cost, b.Currency), new(l.Cost, l.Currency), new(r.Cost, r.Currency), TaskInputComparers.Money);
        Execution = new(be, le, re, TaskInputComparers.Execution);
        Assignee = new(ba, la, ra);
    }

    public PlanningTaskEditorInput Candidate() {
        if (!CanApply) {
            throw new InvalidOperationException("Resolve every Task field and preserve any protected saved assignment or schedule before applying.");
        }
        var estimate = new TaskEstimateInputText(Effort.Result.Amount, Effort.Result.Unit, Money.Result.Amount, Money.Result.Currency);
        return local switch {
            PlanningTaskEditorInput.Structure input => input with {
                Title = Title.Result, Subtitle = Subtitle!.Result, Notes = Notes!.Result, Due = Due!.Result,
                Estimate = estimate, Execution = Execution.Result, Assignee = Assignee.Result,
                SelectedResource = input.Attachment ?? (Current.CanChangeDirectAssignee ? Assignee.Result : null)
            },
            PlanningTaskEditorInput.Gantt input => input with {
                Title = Title.Result, Start = Delivery!.Result.Start, End = Delivery.Result.End, Duration = Delivery.Result.Duration,
                Progress = Progress!.Result, Estimate = estimate, Execution = Execution.Result, Assignee = Assignee.Result,
                SelectedResource = input.Attachment ?? (Current.CanChangeDirectAssignee ? Assignee.Result : null)
            },
            _ => throw new InvalidOperationException("The Task editor shape is unsupported.")
        };
    }

    public void PreserveChoices(PlanningTaskFormReview previous) {
        Keep(previous.Title, Title);
        Keep(previous.Subtitle, Subtitle);
        Keep(previous.Notes, Notes);
        Keep(previous.Due, Due);
        Keep(previous.Delivery, Delivery);
        Keep(previous.Progress, Progress);
        Keep(previous.Effort, Effort);
        Keep(previous.Money, Money);
        Keep(previous.Execution, Execution);
        Keep(previous.Assignee, Assignee);
    }

    private static void Keep<T>(MergeField<T>? old, MergeField<T>? next) {
        if (old is null || next is null || !Equals(old.Original, next.Original) || !Equals(old.Local, next.Local) ||
            !Equals(old.Current, next.Current)) {
            return;
        }
        switch (old.Choice) {
            case MergeChoice.Local:
                next.UseLocal();
                break;
            case MergeChoice.Current:
                next.UseCurrent();
                break;
            case MergeChoice.Custom:
                next.SetResult(old.Result);
                break;
        }
    }
}
