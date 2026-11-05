using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureTaskEditingSession(ProjectWriteAdmission project, ProjectStructureNode original,
    ProjectStructureTaskSnapshotReader reader, ProjectStructureTaskEditorAccess access,
    ProjectStructureGanttProjectionOptions? schedule, string defaultCurrency) : IPlanningTaskComparisonOwner, IAsyncDisposable {
    private bool retired;
    private PlanningTaskUnavailableReason? unavailableReason;
    private (Guid Witness, ProjectStructureTaskSnapshot Snapshot)? current;
    private Guid? verified;
    private ProjectStructureChangeCursor? hinted;
    internal Action? Disposed { get; set; }
    public ProjectWriteAdmission Project => project;
    public string NodeId => original.Id;
    public ProjectStructureTaskSnapshot Baseline { get; private set; } = default!;
    public bool IsActive => !retired && access.IsActive;
    public PlanningTaskUnavailableReason? UnavailableReason => unavailableReason ?? access.UnavailableReason ?? (IsActive ? null : PlanningTaskUnavailableReason.OpeningRetired);
    public long ChangeRevision { get; private set; }
    public event Action? Changed;
    public Func<bool>? CanAccept { get; set; }
    public Action? Accepted { get; set; }

    public async Task OpenAsync(CancellationToken cancellationToken) {
        access.Retired += AccessRetired;
        await access.OpenAsync(cancellationToken);
        Baseline = await ReadAsync(cancellationToken);
    }

    public ValueTask AuthorizeWriteAsync(CancellationToken cancellationToken) => access.AuthorizeWriteAsync(cancellationToken);
    public ValueTask AuthorizeReadAsync(CancellationToken cancellationToken) => access.AuthorizeReadAsync(cancellationToken);
    public Task<T> ReadPrivateAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken)
        => access.ReadPrivateAsync(read, cancellationToken);
    private void AccessRetired() => Retire(access.UnavailableReason ?? PlanningTaskUnavailableReason.AccessChanged, stopAccess: false);

    private async Task<ProjectStructureTaskSnapshot> ReadAsync(CancellationToken cancellationToken) {
        await AuthorizeReadAsync(cancellationToken);
        var occurrence = original.RecordId ?? throw new ProjectStructureEditConflictException("A canonical Task occurrence is required for this editor.");
        try {
            var snapshot = await reader.ReadAsync(project, original.Id, occurrence, schedule, cancellationToken);
            await AuthorizeReadAsync(cancellationToken);
            return snapshot;
        } catch (ProjectStructureEditConflictException) {
            Retire(PlanningTaskUnavailableReason.OriginalDeletedOrReplaced);
            throw;
        } catch (ProjectWriteAdmissionRejectedException) {
            Retire(PlanningTaskUnavailableReason.ProjectLifetimeChanged);
            throw;
        }
    }

    public async Task<PlanningTaskComparisonCurrent> ReadCurrentAsync(CancellationToken cancellationToken) {
        var snapshot = await ReadAsync(cancellationToken);
        var witness = Guid.NewGuid();
        current = (witness, snapshot);
        verified = null;
        var model = schedule is null ? null : ProjectStructureTaskForm.GanttModel(snapshot);
        return new(witness, ProjectStructureTaskForm.Input(snapshot, schedule is not null, defaultCurrency),
            snapshot.Assignees.CanChangeDirectAssignee, model?.ScheduleReadOnly == true);
    }

    public async Task<bool> VerifyCurrentAsync(Guid witness, CancellationToken cancellationToken) {
        verified = null;
        if (current is not { } expected || expected.Witness != witness) {
            return false;
        }
        var snapshot = await ReadAsync(cancellationToken);
        if (!Same(expected.Snapshot, snapshot)) {
            return false;
        }
        await AuthorizeWriteAsync(cancellationToken);
        if (current?.Witness != witness) {
            return false;
        }
        verified = witness;
        return true;
    }

    public bool AcceptCurrent(Guid witness) {
        if (!IsActive || verified != witness || current is not { } accepted || accepted.Witness != witness || CanAccept?.Invoke() != true || Accepted is null) {
            return false;
        }
        Baseline = accepted.Snapshot;
        verified = null;
        Accepted();
        return true;
    }

    public void ObserveHint(ProjectStructureChangeCursor? cursor) {
        if (IsActive && cursor is not null && cursor != hinted) {
            hinted = cursor;
            ChangeRevision++;
            Changed?.Invoke();
        }
    }

    private static bool Same(ProjectStructureTaskSnapshot expected, ProjectStructureTaskSnapshot actual) =>
        ProjectStructureNodeStateGuard.HasSameEditableContent(expected.Task, actual.Task) && expected.State == actual.State &&
        (expected.Schedule is null || expected.Task.ProgressPercent == actual.Task.ProgressPercent) &&
        ProjectStructureTaskAssigneeSelectionPolicy.HasSameDirectAssignments(expected.Assignees.DirectAssignments, actual.Assignees.DirectAssignments) &&
        (expected.Schedule is null && actual.Schedule is null || expected.Schedule is { } before && actual.Schedule is { } after &&
            before.Tasks.Select(task => (task.Id, task.Start, task.End)).SequenceEqual(after.Tasks.Select(task => (task.Id, task.Start, task.End))) &&
            before.Dependencies.SequenceEqual(after.Dependencies));

    private void Retire(PlanningTaskUnavailableReason reason = PlanningTaskUnavailableReason.AccessChanged, bool stopAccess = true) {
        if (retired) {
            return;
        }
        retired = true;
        unavailableReason = reason;
        if (stopAccess) {
            access.Retire(reason);
        }
        current = null;
        verified = null;
        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync() {
        Retire(PlanningTaskUnavailableReason.OpeningRetired);
        access.Retired -= AccessRetired;
        try {
            await access.DisposeAsync();
        } finally {
            Disposed?.Invoke();
            Disposed = null;
        }
    }
}

public sealed class ProjectStructureTaskEditingSessions(ProjectStructureTaskSnapshotReader snapshots, EditingChangeFeed changes,
    IEditingUiAccess access, ProjectChanges projects, ILogger<EditingObservation> logger, ProjectWriteAdmissionService admissions) {
    private readonly HashSet<ProjectStructureTaskEditingSession> openings = [];

    internal ProjectStructureTaskEditingSession Create(ProjectWriteAdmission project, ProjectStructureNode task, Func<bool> isCurrent,
        string defaultCurrency, ProjectStructureGanttProjectionOptions? schedule = null) {
        var opening = new ProjectStructureTaskEditingSession(project, task, snapshots,
            CreateAccess(project, isCurrent), schedule, defaultCurrency);
        openings.Add(opening);
        opening.Disposed = () => openings.Remove(opening);
        return opening;
    }

    internal ProjectStructureTaskEditorAccess CreateAccess(ProjectWriteAdmission project, Func<bool> isCurrent)
        => new(new(changes, access, new(projects.Scope, EditingOwner.ProjectStructure), logger), admissions, project, isCurrent);

    internal void ObserveHints(ProjectWriteAdmission? project, Func<string, ProjectStructureChangeCursor?> latest) {
        foreach (var opening in openings.Where(opening => opening.Project == project).ToArray()) {
            opening.ObserveHint(latest(opening.NodeId));
        }
    }
}
