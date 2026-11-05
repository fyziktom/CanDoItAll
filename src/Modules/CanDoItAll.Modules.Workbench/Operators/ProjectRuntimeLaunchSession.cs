using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectRuntimeLaunchSession(ProjectWriteAdmission admission, ProjectStructureNode node,
    ProjectStructureRuntimeLaunchPlan plan, ProjectStructureRuntimeLaunchMode mode, Func<bool> current,
    ProjectWorkbenchService workbench, IProjectStructureRuntimeLauncher launcher,
    Func<CancellationToken, ValueTask> authorizeRead) {
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
            async ValueTask AuthorizeLaunchAsync(CancellationToken token) {
                if (!current()) {
                    throw new UnauthorizedAccessException("The original runtime opening has retired.");
                }
                await authorizeRead(token);
                await workbench.RequireContentCurrentAsync(admission, node, token);
                await authorizeRead(token);
                if (!current()) {
                    throw new UnauthorizedAccessException("The original runtime opening changed during admission.");
                }
            }
            await AuthorizeLaunchAsync(cancellationToken);
            return Result = await launcher.LaunchReviewedAsync(new(admission, node.RecordId), node,
                Plan with { AuthorizeLaunch = AuthorizeLaunchAsync }, mode, approval, cancellationToken);
        } catch (Exception failure) when (failure is ProjectWriteAdmissionRejectedException or ProjectStructureEditConflictException or UnauthorizedAccessException) {
            return Result = new(false, "The original project or runtime configuration changed. Review a new launch.");
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return Result = new(false, "The launch was canceled without an accepted session result. Inspect the original runtime before retrying.");
        }
    }
}
