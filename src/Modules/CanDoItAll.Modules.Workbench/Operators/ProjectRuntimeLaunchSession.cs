using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectRuntimeLaunchSession(ProjectWriteAdmission admission, ProjectStructureNode node,
    ProjectStructureRuntimeLaunchPlan plan, ProjectStructureRuntimeLaunchMode mode, Func<bool> current,
    ProjectWorkbenchService workbench, IProjectStructureRuntimeLauncher launcher) {
    public Guid Id { get; } = Guid.NewGuid();
    public ProjectStructureRuntimeLaunchPlan Plan { get; } = plan.Capture();
    public ProjectStructureRuntimeLaunchResult? Result { get; private set; }
    private int submitted;

    public async Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(ProjectStructureRuntimeLaunchApproval approval,
        CancellationToken cancellationToken) {
        if (Interlocked.Exchange(ref submitted, 1) != 0) {
            return Result ?? new(false, "This original launch is already being submitted.");
        }
        if (!current()) {
            return Result = new(false, "The original launch opening is retired. Review a new operation.");
        }
        try {
            await workbench.RequireContentCurrentAsync(admission, node, cancellationToken);
            if (!current()) {
                return Result = new(false, "The original launch authority or selection changed before admission.");
            }
            return Result = await launcher.LaunchReviewedAsync(new(admission, node.RecordId), node, Plan, mode, approval, cancellationToken);
        } catch (Exception failure) when (failure is ProjectWriteAdmissionRejectedException or ProjectStructureEditConflictException) {
            return Result = new(false, "The original project or runtime configuration changed. Review a new launch.");
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return Result = new(false, "The launch was canceled without an accepted session result. Inspect the original runtime before retrying.");
        }
    }
}
