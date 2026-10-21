using System.Runtime.ExceptionServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectTaskPlanProcessReviewRequest(AgentInheritedPlanningReviewRequest Agent, ProcessDecisionReviewRequest Process);

public sealed record ProjectTaskPlanProcessReviewResult(Guid ActionId, AgentInheritedPlanningReviewResult Agent,
    ProcessRuntimeCommitResult Completion, ProcessRuntimeStateSnapshot CurrentState);

public sealed class ProjectTaskPlanProcessReviewService(IAgentFrameworkWorkspaceService workspace,
    IAgentInheritedPlanningWorkspaceService reviews, IProcessRuntimeStateStore states,
    ProcessDecisionReviewCoordinator completion, ProjectProcessLaunchAuthorityService sources,
    ICanonicalRuntimeDatabase database, IAgentExecutionProfileGenerationSource generations, TimeProvider clock) {
    public async Task<ProjectTaskPlanProcessReviewResult> ReviewAsync(ProjectTaskPlanProcessReviewRequest request,
        ProcessPlanningManagerAuthority manager, AgentExecutionOperationId activityOperationId, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Agent);
        ArgumentNullException.ThrowIfNull(request.Process);
        ArgumentNullException.ThrowIfNull(manager);
        request.Process.Validate();
        if (request.Process.Child.ChildId.Value != request.Agent.Expected.ExecutionRunId ||
                request.Process.CompletionKey.Value != request.Agent.ActionId || activityOperationId.Value == Guid.Empty) {
            throw new ArgumentException("Review requires one original child, action/completion identity and activity operation.", nameof(request));
        }
        await manager.RequireCurrentAsync(cancellationToken);
        await using var authority = await sources.AcquireAsync(manager.Original, manager.Original, cancellationToken);
        var original = (await workspace.GetExecutionRunDetailAsync(request.Agent.Expected.ExecutionRunId, cancellationToken)).Run;
        if (original.ToolAdmission?.PlanningDecision is not { Owner: AgentPlanningOwner.ProjectTaskPlan, InheritedOrigin: { } origin } decision) {
            throw new AgentToolAdmissionException("process-review.original-missing", "The selected run has no original Process task-plan decision.");
        }
        _ = ProjectTaskPlanProcessCodec.Target(decision);
        var parent = ProjectTaskPlanProcessCodec.Parent(origin);
        if (parent.Child.Address != request.Process.Child) {
            throw new AgentToolAdmissionException("process-review.child-changed", "Review requires the exact original Process child.");
        }
        await RequireManagerAsync(cancellationToken);
        var intent = ComputeIntent(request.Process, parent, manager.Actor);
        var continuation = AgentInheritedSavedEffectContinuation.ForManagerReview(origin, request.Agent.Expected, request.Agent.Binding,
            async (run, token) => {
                if (run.Id != original.Id ||
                        await completion.RequirePendingAsync(await LoadStateAsync(token), request.Process, token) != parent) {
                    throw new AgentToolAdmissionException("process-review.parent-changed", "The original Process no longer admits this child review.");
                }
            }, RequireManagerAsync);
        try {
            return await reviews.ReviewInheritedPlanningAsync(request.Agent, continuation, intent, activityOperationId,
                async (result, token) => {
                    var state = await LoadStateAsync(token);
                    var completed = await completion.CompleteAsync(state,
                        new(new(request.Agent.ActionId), manager.Actor, new(request.Agent.ActionId.ToString("N")), clock.GetUtcNow()),
                        request.Process, token);
                    return new ProjectTaskPlanProcessReviewResult(request.Agent.ActionId, result, completed, await LoadStateAsync(token));
                }, cancellationToken);
        } catch (AgentToolReceiptReadUnavailableException exception) when (exception.InnerException is ProcessDecisionReviewAdmissionException) {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }

        async Task<ProcessRuntimeStateSnapshot> LoadStateAsync(CancellationToken token)
            => await states.LoadAsync(request.Process.Child.RunId, token)
                ?? throw new InvalidDataException("The original Process runtime state is unavailable.");

        Task RequireManagerAsync(CancellationToken token)
            => manager.RequireCurrentAsync(origin.Profile, database, generations, authority, token);
    }

    internal static AgentToolSemanticDigest ComputeIntent(ProcessDecisionReviewRequest process,
        ProcessDecisionChildMaterialBinding parent, ProcessEventActor actor)
        => AgentToolProtocolEnvelope.ComputeDigest(JsonSerializer.Serialize(new {
            Domain = "process-task-plan-review/v1", Process = process,
            OriginalParent = ProcessDecisionChildMaterialBindingCodec.Serialize(parent), Actor = actor
        }));
}
