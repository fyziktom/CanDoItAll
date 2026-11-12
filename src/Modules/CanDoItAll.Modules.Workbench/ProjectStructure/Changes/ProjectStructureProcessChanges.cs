using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureProcessChanges(IProcessRuntimeStateStore runs, ProjectWriteAdmissionService projects,
    ProjectStructureChangeFeed feed) {
    private static readonly HashSet<ProcessEventType> StructureEvents = [
        ProcessRuntimeEventTypes.ProcessRunCreated,
        ProcessRuntimeEventTypes.ProcessRunActivated,
        ProcessRuntimeEventTypes.ProcessRunReactivated,
        ProcessRuntimeEventTypes.ProcessRunBlocked,
        ProcessRuntimeEventTypes.ProcessRunCompleted,
        ProcessRuntimeEventTypes.ProcessRunFailed,
        ProcessRuntimeEventTypes.ProcessRunCancelled,
        ProcessRuntimeEventTypes.StepRunning,
        ProcessRuntimeEventTypes.StepBlocked,
        ProcessRuntimeEventTypes.StepCompleted,
        ProcessRuntimeEventTypes.StepFailed
    ];

    public async Task PublishAsync(ProcessStoredRuntimeEvent projected, CancellationToken cancellationToken) {
        var runtimeEvent = projected.Envelope;
        if (runtimeEvent.RunId != runtimeEvent.RootRunId || !StructureEvents.Contains(runtimeEvent.EventType)) {
            return;
        }

        var run = await runs.LoadAsync(runtimeEvent.RunId, cancellationToken);
        if (run?.ProjectAdmission is not { } project || project.DatabaseProfileId != projects.DatabaseProfileId) {
            return;
        }
        var expected = new ProjectWriteAdmission(project.DatabaseProfileId, project.ProjectId, project.LifetimeId);
        if (await projects.CaptureAsync(project.ProjectId, cancellationToken) != expected) {
            return;
        }

        var runId = runtimeEvent.RunId.Value;
        var kind = runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunCreated
            ? ProjectStructureChangeKind.Created : ProjectStructureChangeKind.Updated;
        feed.Publish(project.DatabaseProfileId, project.ProjectId, runId, "Process",
            [new(ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId), $"Process run {runId.ToString("N")[..8]}", kind)]);
    }
}
