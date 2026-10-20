using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class AgentInheritedPlanningWorkspaceExecution(ISandboxWorkspaceExecutionRunStore runs,
    ISandboxWorkspaceExecutionRunLeaseStore leases, AgentToolAdmissionJournal journal,
    AgentInheritedPlanningCoordinator coordinator) {
    internal async Task<AgentInheritedPlanningWorkspaceResult> ExecuteAsync(AgentInheritedPlanningWorkspaceRequest request,
        Func<ExecutionRunRequest, ExecutionRunRecord?, CancellationToken, Task<AgentExecutionStartupAggregate>> prepare,
        Func<AgentExecutionStartupAggregate, ExecutionRunRequest, AgentInheritedPlanningExecution, CancellationToken, Task<ExecutionRunResult>> complete,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Context);
        ArgumentNullException.ThrowIfNull(request.ContextScope);
        ArgumentNullException.ThrowIfNull(request.ParentBinding);
        ArgumentNullException.ThrowIfNull(request.RequireOriginalParent);
        ArgumentNullException.ThrowIfNull(request.BindAsync);
        if (request.AgentId == Guid.Empty || request.ActivityOperationId.Value == Guid.Empty || !Enum.IsDefined(request.Owner)) {
            throw new ArgumentException("Inherited planning requires an exact Agent, activity and registered owner.", nameof(request));
        }
        AgentExecutionStartupAggregate? startup = null;
        var executionRequest = new ExecutionRunRequest(request.AgentId, request.OriginalInput, request.ActivityOperationId, Context: request.Context);
        var admitted = await new AgentInheritedPlanningRunAdmission(runs, leases, journal).AdmitAsync(request.ExecutionRunId,
            async (run, token) => {
                if (run.AgentId != request.AgentId || run.ToolAdmission?.PlanningDecision?.Owner != request.Owner ||
                    AgentPlanningRunMetadata.ReadScope(run) != request.ContextScope ||
                    run.ToolAdmission.PlanningDecision.InheritedOrigin?.ParentBinding != request.ParentBinding) {
                    throw new AgentToolAdmissionException("planning.inherited-original-changed", "The original child belongs to a different Agent, owner or workspace.");
                }
                await request.RequireOriginalParent(run, token);
            }, async token => {
                startup = await prepare(executionRequest, null, token);
                return new(startup.Run with { Id = request.ExecutionRunId }, request.OriginalInput, request.Owner,
                    coordinator.CaptureRuntimeContract(request.Owner, startup.Agent, startup.Provider), request.ContextScope,
                    request.ParentBinding, request.BindAsync);
            }, cancellationToken);
        var original = admitted.Run;
        await coordinator.RequireCurrentReadAsync(original, cancellationToken);
        var decision = original.ToolAdmission!.PlanningDecision!;
        if (original.State != ExecutionState.Preparing || original.Outcome is not null || decision.Work.Open is not null ||
            decision.Current.State != AgentPlanningGenerationState.Active || decision.Current.Steps.Length != 0 ||
            original.ToolAdmission.HasUnresolvedEffects) {
            return new(original, null);
        }
        executionRequest = RestoreRequest(original, request.ActivityOperationId);
        startup = startup is null ? await prepare(executionRequest, original, cancellationToken) : startup with { Run = original };
        var result = await coordinator.RunWithinLifecycleAsync(startup.Agent, startup.Provider, AgentPlanningDecisionExpectation.From(original),
            (execution, token) => complete(startup with { Run = execution.Run }, executionRequest, execution, token), cancellationToken);
        var saved = await runs.GetExecutionRunAsync(original.Id, cancellationToken)
            ?? throw new InvalidDataException("The completed inherited execution is unavailable.");
        await coordinator.RequireCurrentReadAsync(saved, cancellationToken);
        return new(saved, result);
    }

    private static ExecutionRunRequest RestoreRequest(ExecutionRunRecord original, AgentExecutionOperationId activity)
        => new(original.AgentId, original.ToolAdmission!.BackgroundInput!.Content, activity, Context: ExecutionInvocationContext.Empty with {
            SourceKind = original.SourceKind, SourceId = original.SourceId, CorrelationId = original.CorrelationId,
            CausationId = original.CausationId, RequestedBy = original.RequestedBy, RequestedByKind = original.RequestedByKind,
            MetadataJson = original.MetadataJson, ProcessRunId = original.ProcessRunId, ProcessStepId = original.ProcessStepId,
            SchedulerRunId = original.SchedulerRunId, MessageId = original.MessageId, HistoryCaller = original.HistoryCaller
        });
}
