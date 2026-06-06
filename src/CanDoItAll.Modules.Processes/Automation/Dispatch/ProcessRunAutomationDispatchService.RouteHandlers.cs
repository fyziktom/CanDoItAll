using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private enum ProcessClaimedDispatchRouteHandlerResultKind
    {
        NotHandled,
        DispatchComplete,
        ContinueCandidates
    }

    private readonly record struct ProcessClaimedDispatchRouteHandlerResult(
        ProcessClaimedDispatchRouteHandlerResultKind Kind)
    {
        public static ProcessClaimedDispatchRouteHandlerResult NotHandled { get; } = new(
            ProcessClaimedDispatchRouteHandlerResultKind.NotHandled);

        public static ProcessClaimedDispatchRouteHandlerResult DispatchComplete { get; } = new(
            ProcessClaimedDispatchRouteHandlerResultKind.DispatchComplete);

        public static ProcessClaimedDispatchRouteHandlerResult ContinueCandidates { get; } = new(
            ProcessClaimedDispatchRouteHandlerResultKind.ContinueCandidates);

        public bool Handled => Kind != ProcessClaimedDispatchRouteHandlerResultKind.NotHandled;

        public ProcessClaimedDispatchResult ToClaimedDispatchResult()
        {
            return Kind switch
            {
                ProcessClaimedDispatchRouteHandlerResultKind.DispatchComplete => ProcessClaimedDispatchResult.DispatchComplete,
                ProcessClaimedDispatchRouteHandlerResultKind.ContinueCandidates => ProcessClaimedDispatchResult.ContinueCandidates,
                _ => throw new InvalidOperationException("A route handler result must be handled before it can be converted to a dispatch result.")
            };
        }
    }

    private sealed class ProcessClaimedDispatchRouteContext(
        ProcessClaimedDispatchExecution execution,
        DispatchCandidate candidate)
    {
        public ProcessClaimedDispatchExecution Execution { get; } = execution;

        public DispatchCandidate Candidate { get; private set; } = candidate;

        public DispatchExecutionOutcome? DirectAgentExecutionOutcome { get; private set; }

        public ProcessDispatchRouteSnapshot CreateRouteSnapshot()
        {
            return ProcessDispatchRouteSnapshot.Create(
                Candidate,
                Execution.Trigger,
                Execution.TriggerStepRunId);
        }

        public void UpdateCandidate(DispatchCandidate candidate)
        {
            Candidate = candidate;
            Execution.Candidate = candidate;
        }

        public void SetDirectAgentExecutionOutcome(DispatchExecutionOutcome executionOutcome)
        {
            DirectAgentExecutionOutcome = executionOutcome;
        }

        public DispatchExecutionOutcome GetRequiredDirectAgentExecutionOutcome(ProcessDispatchRouteStage stage)
        {
            return DirectAgentExecutionOutcome ??
                throw new InvalidOperationException($"Route stage {stage} requires a direct-agent execution outcome.");
        }
    }

    private interface IProcessClaimedDispatchRouteHandler
    {
        ProcessDispatchRouteStage Stage { get; }

        Task<ProcessClaimedDispatchRouteHandlerResult> HandleAsync(ProcessClaimedDispatchRouteContext context);
    }

    private sealed class ProcessDispatchRouteHandlerPipeline(IReadOnlyList<IProcessClaimedDispatchRouteHandler> handlers)
    {
        private readonly IReadOnlyList<IProcessClaimedDispatchRouteHandler> handlers = ValidateHandlers(handlers);

        public async Task<ProcessClaimedDispatchResult> ExecuteAsync(ProcessClaimedDispatchRouteContext context)
        {
            foreach (var handler in handlers)
            {
                var result = await handler.HandleAsync(context);
                if (!result.Handled)
                {
                    continue;
                }

                return result.ToClaimedDispatchResult();
            }

            throw new InvalidOperationException("The claimed dispatch route handler pipeline completed without a terminal result.");
        }

        private static IReadOnlyList<IProcessClaimedDispatchRouteHandler> ValidateHandlers(
            IReadOnlyList<IProcessClaimedDispatchRouteHandler> handlers)
        {
            ProcessDispatchRouteOrderAssertion.ThrowIfStageOrderInvalid(handlers.Select(handler => handler.Stage).ToArray());

            return handlers;
        }
    }

    private ProcessDispatchRouteHandlerPipeline CreateClaimedDispatchRouteHandlerPipeline()
    {
        return new ProcessDispatchRouteHandlerPipeline(
        [
            new FreshRecoverySkipRouteHandler(clock, logger),
            new DatabaseRequirementRouteHandler(this),
            new UpstreamMaterializationRouteHandler(this),
            new StrandedArtifactRecoveryRouteHandler(this),
            new SubprocessRouteHandler(this),
            new StartTransitionRouteHandler(this, logger),
            new WorkflowRouteHandler(this, workflowRunCoordinator),
            new DirectAgentExecutionRouteHandler(this),
            new CompetingExecutionGuardRouteHandler(this, logger),
            new RunClosedGuardRouteHandler(this, logger),
            new FinalizerTransitionRouteHandler(this)
        ]);
    }

    private sealed class FreshRecoverySkipRouteHandler(
        IClock clock,
        ILogger<ProcessRunAutomationDispatchService> logger) : IProcessClaimedDispatchRouteHandler
    {
        public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.FreshRecoverySkip;

        public Task<ProcessClaimedDispatchRouteHandlerResult> HandleAsync(ProcessClaimedDispatchRouteContext context)
        {
            var routeSnapshot = context.CreateRouteSnapshot();
            if (!ShouldSkipFreshAutomationDispatch(routeSnapshot, clock.GetUtcNow()))
            {
                return Task.FromResult(ProcessClaimedDispatchRouteHandlerResult.NotHandled);
            }

            logger.LogInformation(
                "Skipping recovery redispatch within the fresh-step grace period for run {RunId}, step {StepRunId}, status {Status}, trigger {Trigger}. Recovery worker will retry if the execution remains stranded.",
                context.Candidate.Run.Id,
                context.Candidate.StepRun.Id,
                context.Candidate.StepRun.Status,
                NormalizeTrigger(context.Execution.Trigger, context.Execution.TriggerStepRunId));

            return Task.FromResult(ProcessClaimedDispatchRouteHandlerResult.DispatchComplete);
        }
    }

    private sealed class DatabaseRequirementRouteHandler(ProcessRunAutomationDispatchService dispatcher)
        : IProcessClaimedDispatchRouteHandler
    {
        public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.DatabaseRequirement;

        public async Task<ProcessClaimedDispatchRouteHandlerResult> HandleAsync(ProcessClaimedDispatchRouteContext context)
        {
            var routeSnapshot = context.CreateRouteSnapshot();
            var databaseRequirementFailure = routeSnapshot.UsesAgentAutomation
                ? dispatcher.ResolveAutomationDatabaseRequirementFailure()
                : null;
            if (ProcessDispatchRoutePlanner.ResolveDatabaseRequirement(
                    routeSnapshot,
                    databaseRequirementFailure is not null).Kind != ProcessDispatchRouteKind.DatabaseRequirement ||
                databaseRequirementFailure is null)
            {
                return ProcessClaimedDispatchRouteHandlerResult.NotHandled;
            }

            await dispatcher.BlockDispatchForDatabaseRequirementAsync(
                context.Candidate,
                databaseRequirementFailure,
                context.Execution.DispatchClaim,
                context.Execution.DispatchCancellationToken);

            return ProcessClaimedDispatchRouteHandlerResult.DispatchComplete;
        }
    }

    private sealed class UpstreamMaterializationRouteHandler(ProcessRunAutomationDispatchService dispatcher)
        : IProcessClaimedDispatchRouteHandler
    {
        public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.UpstreamMaterialization;

        public async Task<ProcessClaimedDispatchRouteHandlerResult> HandleAsync(ProcessClaimedDispatchRouteContext context)
        {
            var materializationRequested = await dispatcher.TryRequestMissingUpstreamArtifactMaterializationAsync(
                context.Candidate,
                context.Execution.DispatchClaim,
                context.Execution.DispatchCancellationToken);
            if (ProcessDispatchRoutePlanner.ResolveUpstreamMaterialization(materializationRequested).Kind != ProcessDispatchRouteKind.UpstreamMaterialization)
            {
                return ProcessClaimedDispatchRouteHandlerResult.NotHandled;
            }

            return ProcessClaimedDispatchRouteHandlerResult.DispatchComplete;
        }
    }

    private sealed class StrandedArtifactRecoveryRouteHandler(ProcessRunAutomationDispatchService dispatcher)
        : IProcessClaimedDispatchRouteHandler
    {
        public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.StrandedArtifactRecovery;

        public async Task<ProcessClaimedDispatchRouteHandlerResult> HandleAsync(ProcessClaimedDispatchRouteContext context)
        {
            var strandedArtifactRecoveryOutcome = await dispatcher.TryRecoverStrandedMissingCompletionArtifactsAsync(
                context.Candidate,
                context.Execution.Trigger,
                context.Execution.DispatchClaim,
                context.Execution.DispatchRenewLeaseAsync,
                context.Execution.DispatchCancellationToken);
            if (ProcessDispatchRoutePlanner.ResolveStrandedRecovery(strandedArtifactRecoveryOutcome is not null).Kind != ProcessDispatchRouteKind.StrandedRecovery ||
                strandedArtifactRecoveryOutcome is null)
            {
                return ProcessClaimedDispatchRouteHandlerResult.NotHandled;
            }

            var finalizedRecoveryCompletion = await dispatcher.FinalizeStepCompletionAsync(
                ProcessDispatchFinalizerContextFactory.ForManagerArtifactRecovery(
                    context.Candidate,
                    strandedArtifactRecoveryOutcome,
                    context.Execution.Trigger,
                    context.Execution.DispatchRenewLeaseAsync),
                context.Execution.DispatchClaim,
                context.Execution.DispatchCancellationToken);
            if (finalizedRecoveryCompletion is not null)
            {
                await dispatcher.ApplyFinalizedStepTransitionAsync(
                    context.Candidate,
                    finalizedRecoveryCompletion,
                    context.Execution.DispatchClaim,
                    context.Execution.DispatchCancellationToken);
            }

            return ProcessClaimedDispatchRouteHandlerResult.DispatchComplete;
        }
    }

    private sealed class SubprocessRouteHandler(ProcessRunAutomationDispatchService dispatcher)
        : IProcessClaimedDispatchRouteHandler
    {
        public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.Subprocess;

        public async Task<ProcessClaimedDispatchRouteHandlerResult> HandleAsync(ProcessClaimedDispatchRouteContext context)
        {
            if (ProcessDispatchRoutePlanner.ResolveSubprocess(context.CreateRouteSnapshot()).Kind != ProcessDispatchRouteKind.Subprocess)
            {
                return ProcessClaimedDispatchRouteHandlerResult.NotHandled;
            }

            await dispatcher.HandleSubprocessDispatchAsync(
                context.Candidate,
                context.Execution.Trigger,
                context.Execution.TriggerStepRunId,
                context.Execution.DispatchClaim,
                context.Execution.DispatchCancellationToken);

            return ProcessClaimedDispatchRouteHandlerResult.DispatchComplete;
        }
    }

    private sealed class StartTransitionRouteHandler(
        ProcessRunAutomationDispatchService dispatcher,
        ILogger<ProcessRunAutomationDispatchService> logger) : IProcessClaimedDispatchRouteHandler
    {
        public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.StartTransition;

        public async Task<ProcessClaimedDispatchRouteHandlerResult> HandleAsync(ProcessClaimedDispatchRouteContext context)
        {
            var routeSnapshot = context.CreateRouteSnapshot();
            if (!routeSnapshot.RequiresStartTransition)
            {
                return ProcessClaimedDispatchRouteHandlerResult.NotHandled;
            }

            var startResult = await dispatcher.TransitionStepWithClaimAsync(
                ProcessDispatchStartTransitionPlanner.BuildStartTransitionRequest(
                    routeSnapshot,
                    context.Candidate.StepRun.ConcurrencyToken,
                    AutomationActor),
                context.Execution.DispatchClaim,
                context.Execution.DispatchCancellationToken);
            if (!startResult.IsFailure)
            {
                return ProcessClaimedDispatchRouteHandlerResult.NotHandled;
            }

            logger.LogInformation(
                "Process step {StepRunId} could not be claimed for automation dispatch on run {RunId}. Errors: {Errors}",
                context.Candidate.StepRun.Id,
                context.Execution.ProcessRunId,
                string.Join(" | ", startResult.Errors.Select(error => error.Message)));
            var refreshedCandidate = await dispatcher.LoadDispatchCandidateAsync(
                context.Execution.ProcessRunId,
                context.Execution.DispatchClaim.StepRunId,
                context.Execution.Trigger,
                context.Execution.DispatchCancellationToken);
            if (refreshedCandidate is null ||
                refreshedCandidate.StepRun.Id != context.Candidate.StepRun.Id ||
                refreshedCandidate.StepRun.Status != ProcessStepRunStatus.InProgress)
            {
                return ProcessClaimedDispatchRouteHandlerResult.ContinueCandidates;
            }

            logger.LogInformation(
                "Continuing process automation dispatch for run {RunId}, step {StepRunId} after reload confirmed the step is already InProgress.",
                refreshedCandidate.Run.Id,
                refreshedCandidate.StepRun.Id);
            context.UpdateCandidate(refreshedCandidate);

            return ProcessClaimedDispatchRouteHandlerResult.NotHandled;
        }
    }

    private sealed class WorkflowRouteHandler(
        ProcessRunAutomationDispatchService dispatcher,
        ProcessWorkflowRunCoordinator workflowRunCoordinator) : IProcessClaimedDispatchRouteHandler
    {
        public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.Workflow;

        public async Task<ProcessClaimedDispatchRouteHandlerResult> HandleAsync(ProcessClaimedDispatchRouteContext context)
        {
            var workflowOutcome = await workflowRunCoordinator.TryRunOrObserveAsync(
                context.Candidate.Run.Id,
                context.Candidate.StepRun.Id,
                NormalizeTrigger(context.Execution.Trigger, context.Execution.TriggerStepRunId),
                context.Execution.DispatchCancellationToken);
            var workflowRoute = ProcessDispatchRoutePlanner.ResolveWorkflow(workflowOutcome.Handled);
            if (workflowRoute.Kind != ProcessDispatchRouteKind.Workflow)
            {
                return ProcessClaimedDispatchRouteHandlerResult.NotHandled;
            }

            await dispatcher.HandleWorkflowExecutionOutcomeAsync(
                context.Candidate,
                workflowOutcome,
                context.Execution.DispatchClaim,
                context.Execution.DispatchCancellationToken);

            return ProcessClaimedDispatchRouteHandlerResult.DispatchComplete;
        }
    }

    private sealed class DirectAgentExecutionRouteHandler(ProcessRunAutomationDispatchService dispatcher)
        : IProcessClaimedDispatchRouteHandler
    {
        public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.DirectAgentExecution;

        public async Task<ProcessClaimedDispatchRouteHandlerResult> HandleAsync(ProcessClaimedDispatchRouteContext context)
        {
            var executionOutcome = await dispatcher.ExecuteUntilSettledAsync(
                context.Candidate,
                context.Execution.Trigger,
                context.Execution.DispatchRenewLeaseAsync,
                context.Execution.DispatchCancellationToken);
            context.Execution.DispatchHeartbeat?.ThrowIfClaimLost();
            context.SetDirectAgentExecutionOutcome(executionOutcome);

            return ProcessClaimedDispatchRouteHandlerResult.NotHandled;
        }
    }

    private sealed class CompetingExecutionGuardRouteHandler(
        ProcessRunAutomationDispatchService dispatcher,
        ILogger<ProcessRunAutomationDispatchService> logger) : IProcessClaimedDispatchRouteHandler
    {
        public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.CompetingExecutionGuard;

        public async Task<ProcessClaimedDispatchRouteHandlerResult> HandleAsync(ProcessClaimedDispatchRouteContext context)
        {
            var executionOutcome = context.GetRequiredDirectAgentExecutionOutcome(Stage);
            var competingExecution = executionOutcome.CompletionStatus is not ProcessStepRunStatus.Completed
                ? await dispatcher.ResolveCompetingActiveAutomationExecutionAsync(
                    context.Candidate,
                    executionOutcome,
                    context.Execution.DispatchCancellationToken)
                : null;
            if (competingExecution is null)
            {
                return ProcessClaimedDispatchRouteHandlerResult.NotHandled;
            }

            logger.LogInformation(
                "Skipping non-successful automation completion transition for run {RunId}, step {StepRunId}, execution run {ExecutionRunId} because execution run {CompetingExecutionRunId} is still active for the same process step.",
                context.Candidate.Run.Id,
                context.Candidate.StepRun.Id,
                executionOutcome.Detail.Run.Id,
                competingExecution.Id);

            return ProcessClaimedDispatchRouteHandlerResult.DispatchComplete;
        }
    }

    private sealed class RunClosedGuardRouteHandler(
        ProcessRunAutomationDispatchService dispatcher,
        ILogger<ProcessRunAutomationDispatchService> logger) : IProcessClaimedDispatchRouteHandler
    {
        public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.RunClosedGuard;

        public async Task<ProcessClaimedDispatchRouteHandlerResult> HandleAsync(ProcessClaimedDispatchRouteContext context)
        {
            _ = context.GetRequiredDirectAgentExecutionOutcome(Stage);
            if (!await dispatcher.IsRunClosedToAutomationAsync(
                    context.Candidate.Run.Id,
                    context.Candidate.StepRun.Id,
                    context.Execution.DispatchCancellationToken))
            {
                return ProcessClaimedDispatchRouteHandlerResult.NotHandled;
            }

            logger.LogInformation(
                "Skipping automation completion projection for run {RunId}, step {StepRunId} because the process run became terminal while agent execution was in flight.",
                context.Candidate.Run.Id,
                context.Candidate.StepRun.Id);

            return ProcessClaimedDispatchRouteHandlerResult.DispatchComplete;
        }
    }

    private sealed class FinalizerTransitionRouteHandler(ProcessRunAutomationDispatchService dispatcher)
        : IProcessClaimedDispatchRouteHandler
    {
        public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.FinalizerTransition;

        public async Task<ProcessClaimedDispatchRouteHandlerResult> HandleAsync(ProcessClaimedDispatchRouteContext context)
        {
            var executionOutcome = context.GetRequiredDirectAgentExecutionOutcome(Stage);
            var finalizedCompletion = await dispatcher.FinalizeStepCompletionAsync(
                ProcessDispatchFinalizerContextFactory.ForDirectAgent(
                    context.Candidate,
                    executionOutcome,
                    context.Execution.Trigger,
                    context.Execution.DispatchRenewLeaseAsync),
                context.Execution.DispatchClaim,
                context.Execution.DispatchCancellationToken);
            context.Execution.DispatchHeartbeat?.ThrowIfClaimLost();
            if (finalizedCompletion is not null)
            {
                await dispatcher.ApplyFinalizedStepTransitionAsync(
                    context.Candidate,
                    finalizedCompletion,
                    context.Execution.DispatchClaim,
                    context.Execution.DispatchCancellationToken);
            }

            return ProcessClaimedDispatchRouteHandlerResult.DispatchComplete;
        }
    }
}

internal static class ProcessDispatchRouteOrderAssertion
{
    public static void ThrowIfStageOrderInvalid(IReadOnlyList<ProcessDispatchRouteStage> actualStageOrder)
    {
        if (ProcessDispatchRoutePipeline.StageOrder.SequenceEqual(actualStageOrder))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Process dispatch route handler order must match the canonical route stage order. Expected: {FormatStageOrder(ProcessDispatchRoutePipeline.StageOrder)}. Actual: {FormatStageOrder(actualStageOrder)}.");
    }

    private static string FormatStageOrder(IReadOnlyList<ProcessDispatchRouteStage> stageOrder)
    {
        return string.Join(" -> ", stageOrder);
    }
}
