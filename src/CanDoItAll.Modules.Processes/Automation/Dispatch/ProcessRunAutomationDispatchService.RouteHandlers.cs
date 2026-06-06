namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private ProcessDispatchRouteHandlerPipeline CreateClaimedDispatchRouteHandlerPipeline()
    {
        var stepTransitionService = CreateStepTransitionService();
        var preExecutionGuardHandler = CreatePreExecutionGuardHandler();
        var candidateHydrationService = CreateCandidateHydrationService();
        var runClosureGuardService = CreateRunClosureGuardService();
        var finalizerApplicationService = CreateFinalizerApplicationService();
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
            new ProcessDispatchRecoveryRouteService(this),
            new ProcessDispatchSubprocessRouteService(subprocessRuntimeService),
            new ProcessDispatchStartTransitionRouteService(
                stepTransitionService,
                candidateHydrationService),
            new ProcessDispatchWorkflowRouteService(this, workflowRunCoordinator),
            new ProcessDispatchDirectAgentRouteService(this),
            new ProcessDispatchGuardRouteService(this, runClosureGuardService),
            new ProcessDispatchFinalizerRouteService(this));
    }

    private ProcessDispatchStepTransitionService CreateStepTransitionService()
        => new(serviceScopeFactory, dbContextFactory, EnsureStepDispatchClaimHeldAsync);

    private ProcessDispatchRunClosureGuardService CreateRunClosureGuardService()
        => new(dbContextFactory);

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

    private ProcessDispatchFinalizerApplicationService CreateFinalizerApplicationService()
        => new(FinalizeStepCompletionAsync, ApplyFinalizedStepTransitionAsync);

    private ProcessDispatchSubprocessRuntimeService CreateSubprocessRuntimeService(
        ProcessDispatchStepTransitionService stepTransitionService,
        ProcessDispatchFinalizerApplicationService finalizerApplicationService)
    {
        return new ProcessDispatchSubprocessRuntimeService(
            stepTransitionService,
            finalizerApplicationService,
            dbContextFactory,
            serviceScopeFactory,
            workspacePathResolver,
            databaseProfileRuntimeAccessor,
            clock,
            EnsureStepDispatchClaimHeldAsync,
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
        await CreateFinalizerApplicationService().FinalizeRecoveredCompletionAsync(
            candidate,
            recoveryOutcome,
            trigger,
            renewLeaseAsync,
            dispatchClaim,
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
        await CreateFinalizerApplicationService().FinalizeDirectAgentCompletionAsync(
            candidate,
            executionOutcome,
            trigger,
            renewLeaseAsync,
            dispatchClaim,
            cancellationToken);
    }
}
