namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private ProcessDispatchRouteHandlerPipeline CreateClaimedDispatchRouteHandlerPipeline()
    {
        var stepTransitionService = CreateStepTransitionService();
        var preExecutionGuardHandler = CreatePreExecutionGuardHandler();
        var candidateHydrationService = CreateCandidateHydrationService();
        var runClosureGuardService = CreateRunClosureGuardService();
        var finalizerAdapter = CreateFinalizerAdapter();
        var finalizerApplicationService = CreateFinalizerApplicationService(finalizerAdapter);
        var recoveryRuntimeService = CreateRecoveryRuntimeService();
        var directAgentRuntimeService = CreateDirectAgentRuntimeService();
        var competingExecutionGuardService = CreateCompetingExecutionGuardService();
        var subprocessRuntimeService = CreateSubprocessRuntimeService(
            stepTransitionService,
            finalizerApplicationService);

        return ProcessDispatchRouteHandlerFactory.Create(
            clock,
            logger,
            new ProcessDispatchDatabaseRequirementRouteService(
                CreateDatabaseRequirementResolver(),
                preExecutionGuardHandler,
                stepTransitionService,
                logger),
            new ProcessDispatchUpstreamMaterializationRouteService(
                preExecutionGuardHandler,
                stepTransitionService,
                logger),
            new ProcessDispatchRecoveryRouteService(recoveryRuntimeService, finalizerApplicationService),
            new ProcessDispatchSubprocessRouteService(subprocessRuntimeService),
            new ProcessDispatchStartTransitionRouteService(
                stepTransitionService,
                candidateHydrationService),
            new ProcessDispatchWorkflowRouteService(finalizerApplicationService, workflowRunCoordinator),
            new ProcessDispatchDirectAgentRouteService(directAgentRuntimeService),
            new ProcessDispatchGuardRouteService(competingExecutionGuardService, runClosureGuardService),
            new ProcessDispatchFinalizerRouteService(finalizerApplicationService));
    }

    private ProcessDispatchStepTransitionService CreateStepTransitionService()
        => new(serviceScopeFactory, dbContextFactory, EnsureStepDispatchClaimHeldAsync);

    private ProcessDispatchRunClosureGuardService CreateRunClosureGuardService()
        => new(dbContextFactory);

    private ProcessDispatchRecoveryRuntimeService CreateRecoveryRuntimeService()
        => new(TryRecoverStrandedMissingCompletionArtifactsAsync);

    private ProcessDispatchDirectAgentRuntimeService CreateDirectAgentRuntimeService()
    {
        var executionAdapter = new ProcessDispatchDirectAgentExecutionAdapter(ExecuteUntilSettledAsync);

        return new ProcessDispatchDirectAgentRuntimeService(executionAdapter.ExecuteUntilSettledAsync);
    }

    private ProcessDispatchCompetingExecutionGuardService CreateCompetingExecutionGuardService()
        => new(executionClient, clock, StaleAutomationExecutionRunTimeout);

    private ProcessAutomationDatabaseRequirementResolver CreateDatabaseRequirementResolver()
        => new(databaseProfileRuntimeAccessor, processRuntimeOptions);

    private ProcessDispatchCandidateHydrationService CreateCandidateHydrationService()
    {
        return new ProcessDispatchCandidateHydrationService(
            dbContextFactory,
            workspacePathResolver,
            databaseProfileRuntimeAccessor,
            technicalAgentBridge,
            executionClient,
            clock,
            StaleAutomationExecutionRunTimeout,
            logger);
    }

    private ProcessDispatchFinalizerAdapter CreateFinalizerAdapter()
        => new(FinalizeStepCompletionAsync, ApplyFinalizedStepTransitionAsync);

    private ProcessDispatchFinalizerApplicationService CreateFinalizerApplicationService(ProcessDispatchFinalizerAdapter finalizerAdapter)
        => new(finalizerAdapter);

    private ProcessDispatchSubprocessRuntimeService CreateSubprocessRuntimeService(
        ProcessDispatchStepTransitionService stepTransitionService,
        ProcessDispatchFinalizerApplicationService finalizerApplicationService)
    {
        return new ProcessDispatchSubprocessRuntimeService(
            stepTransitionService,
            finalizerApplicationService,
            dbContextFactory,
            serviceScopeFactory,
            new ProcessSubprocessProjectionPersistenceService(
                dbContextFactory,
                workspacePathResolver,
                databaseProfileRuntimeAccessor,
                clock,
                (claim, token) => EnsureStepDispatchClaimHeldAsync(
                    new ProcessStepDispatchClaim(claim.StepRunId, claim.ClaimToken),
                    token)),
            logger);
    }

    internal async Task FinalizeRecoveredCompletionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome recoveryOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await CreateFinalizerAdapter().FinalizeRecoveredCompletionAsync(
            new ProcessDispatchRecoveredFinalizerInput(
                ProcessDispatchRouteModelAdapters.FromDispatcherCandidate(candidate),
                ProcessDispatchRouteModelAdapters.FromDispatcherExecutionOutcome(recoveryOutcome),
                trigger,
                renewLeaseAsync,
                ProcessDispatchRouteModelAdapters.FromDispatcherClaim(dispatchClaim)),
            cancellationToken);
    }

    internal async Task FinalizeDirectAgentCompletionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await CreateFinalizerAdapter().FinalizeDirectAgentCompletionAsync(
            new ProcessDispatchDirectAgentFinalizerInput(
                ProcessDispatchRouteModelAdapters.FromDispatcherCandidate(candidate),
                ProcessDispatchRouteModelAdapters.FromDispatcherExecutionOutcome(executionOutcome),
                trigger,
                renewLeaseAsync,
                ProcessDispatchRouteModelAdapters.FromDispatcherClaim(dispatchClaim)),
            cancellationToken);
    }
}
