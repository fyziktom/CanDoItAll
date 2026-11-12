using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectTaskPlanProcessReconciliationRequest(AgentPlanningDecisionExpectation Expected,
    ProcessDecisionReviewRequest Process) {
    public void Validate() {
        ArgumentNullException.ThrowIfNull(Expected);
        ArgumentNullException.ThrowIfNull(Process);
        Process.Validate();
        if (Process.Child.ChildId.Value != Expected.ExecutionRunId) {
            throw new ArgumentException("Accounting requires the exact displayed Process child.");
        }
    }
}

public sealed record ProjectTaskPlanProcessReconciliationResult(Guid ActionId, ExecutionRunRecord Agent,
    ProcessRuntimeCommitResult Completion, ProcessRuntimeStateSnapshot CurrentState);

public sealed class ProjectTaskPlanProcessReconciliationService(AgentInheritedPlanningRunReader runs,
    IProcessRuntimeStateStore states, ProcessDecisionReviewCoordinator completion, ProjectProcessLaunchAuthorityService sources,
    ICanonicalRuntimeDatabase database, IAgentExecutionProfileGenerationSource generations, TimeProvider clock) {
    private const string ChangedCode = "process-accounting.child-changed";
    private const string PendingCode = "process-accounting.child-pending";

    public async Task<ProjectTaskPlanProcessReconciliationResult> ReconcileAsync(ProjectTaskPlanProcessReconciliationRequest request,
        ProcessPlanningManagerAuthority manager, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(manager);
        request.Validate();
        await manager.RequireCurrentAsync(cancellationToken);
        await using var authority = await sources.AcquireAsync(manager.Original, manager.Original, cancellationToken);
        var run = (await runs.ReadAsync(request.Expected.ExecutionRunId, cancellationToken)).Run;
        if (run.ToolAdmission?.PlanningDecision is not { Owner: AgentPlanningOwner.ProjectTaskPlan, InheritedOrigin: { } origin }) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var original = ProjectTaskPlanProcessCodec.Parent(origin);
        if (original.Child.Address != request.Process.Child || AgentPlanningDecisionExpectation.From(run) != request.Expected) {
            throw new AgentToolAdmissionException(ChangedCode, "Accounting requires the exact displayed original child and revision.");
        }
        if (ProjectTaskPlanProcessOutcome.Read(run, original).Outcome is not ProcessDecisionChildResult.Closed) {
            throw new AgentToolAdmissionException(PendingCode, "The original child still has unresolved work, approval or effects.");
        }
        await manager.RequireCurrentAsync(origin.Profile, database, generations, authority, cancellationToken);
        var action = request.Process.CompletionKey.Value;
        var result = await completion.CompleteAsync(await LoadStateAsync(),
            new(new(action), manager.Actor, new(action.ToString("N")), clock.GetUtcNow()), request.Process, cancellationToken);
        var currentState = await LoadStateAsync();
        var current = (await runs.ReadAsync(run.Id, cancellationToken)).Run;
        if (AgentPlanningDecisionExpectation.From(current) != request.Expected) {
            throw new AgentToolAdmissionException(ChangedCode, "The original child changed while accounting was reconciled; reload its current state.");
        }
        await manager.RequireCurrentAsync(origin.Profile, database, generations, authority, cancellationToken);
        return new(action, current, result, currentState);

        async Task<ProcessRuntimeStateSnapshot> LoadStateAsync()
            => await states.LoadAsync(request.Process.Child.RunId, cancellationToken)
                ?? throw new InvalidDataException("The original Process runtime state is unavailable.");
    }
}
