using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessLaunchAtomicCommitTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Launch_passes_compiled_plan_to_initial_runtime_commit_without_separate_plan_write()
    {
        var planStore = new TrackingPlanStore();
        var unitOfWork = new RejectingUnitOfWork();
        var service = new ProcessLaunchApplicationService(
            new ProcessTemplatePackLoader(),
            new TestClock(),
            new TestDriverCatalogProvider(),
            new AllStepsExecutorResolver(),
            planStore,
            unitOfWork,
            stateStore: null!,
            assignmentStore: null!,
            artifactInitializer: null!,
            new GenericProcessStepBriefBuilder(),
            dispatchQueue: null!,
            projectionCatchupService: null!,
            new LaunchVariableTemplateResolver());

        var result = await service.LaunchAsync(new ProcessLaunchRequest(
            DefinitionKey: "dotnet-runtime-command-writeback",
            ProcessDefinitionId: null,
            LiveRunProfileKey: null,
            ProjectId: null,
            ProjectNodeId: null,
            RequestedBy: "unit-test",
            Variables: new Dictionary<string, string>(StringComparer.Ordinal),
            RunReadiness: false,
            Execute: false));

        Assert.Equal(ProcessLaunchStage.Failed, result.Stage);
        Assert.Null(result.RunId);
        Assert.Equal(0, planStore.PersistCount);
        var commit = Assert.IsType<ProcessRuntimeCommitRequest>(unitOfWork.Request);
        Assert.NotNull(commit.InitialPlan);
        Assert.Equal(result.LaunchPlanId, commit.InitialPlan.Header.PlanId);
        Assert.Equal(commit.Mutation.State.PlanId, commit.InitialPlan.Header.PlanId);
        Assert.Equal(commit.Mutation.State.PlanHash, commit.InitialPlan.PlanHash);
    }

    [Fact]
    public async Task Launch_cancels_durable_run_when_artifact_initialization_fails()
    {
        var runtimeStore = new StatefulRuntimeStore();
        var dispatchQueue = new RecordingDispatchQueue();
        var service = NewOperationalService(
            runtimeStore,
            new ThrowingArtifactInitializer(),
            dispatchQueue);

        var result = await service.LaunchAsync(NewLaunchRequest(execute: true));

        Assert.Equal(ProcessLaunchStage.Failed, result.Stage);
        Assert.NotNull(result.RunId);
        Assert.NotNull(runtimeStore.State);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, runtimeStore.State.Status);
        Assert.Empty(dispatchQueue.Requests);
        Assert.Contains(
            runtimeStore.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunCancelled);
        Assert.Contains(
            result.Warnings,
            diagnostic => diagnostic.Contains("artifact root", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Launch_cancels_and_does_not_dispatch_when_activation_is_rejected()
    {
        var runtimeStore = new StatefulRuntimeStore(ProcessRuntimeEventTypes.ProcessRunActivated);
        var dispatchQueue = new RecordingDispatchQueue();
        var service = NewOperationalService(
            runtimeStore,
            new NoOpArtifactInitializer(),
            dispatchQueue);

        var result = await service.LaunchAsync(NewLaunchRequest(execute: true));

        Assert.Equal(ProcessLaunchStage.Failed, result.Stage);
        Assert.NotNull(runtimeStore.State);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, runtimeStore.State.Status);
        Assert.Empty(dispatchQueue.Requests);
        Assert.Contains(
            result.Warnings,
            diagnostic => diagnostic.Contains("activation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Launch_selects_and_compiles_generic_simple_app_live_profile()
    {
        var executorResolver = new AllStepsExecutorResolver();
        var service = new ProcessLaunchApplicationService(
            new ProcessTemplatePackLoader(),
            new TestClock(),
            new TestDriverCatalogProvider(),
            executorResolver,
            new TrackingPlanStore(),
            new RejectingUnitOfWork(),
            stateStore: null!,
            assignmentStore: null!,
            artifactInitializer: null!,
            new GenericProcessStepBriefBuilder(),
            dispatchQueue: null!,
            projectionCatchupService: null!,
            new LaunchVariableTemplateResolver());

        var result = await service.LaunchAsync(new ProcessLaunchRequest(
            DefinitionKey: null,
            ProcessDefinitionId: null,
            LiveRunProfileKey: "generic-simple-local-app",
            ProjectId: null,
            ProjectNodeId: null,
            RequestedBy: "unit-test",
            Variables: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["AppTopic"] = "Tetris",
                ["ApplicationKind"] = "UI",
                ["TechnologyStack"] = ".NET",
                ["ProductRoot"] = ".",
                ["AcceptanceCriteria"] = "The application builds and the declared game loop is testable."
            },
            RunReadiness: false,
            Execute: false));

        Assert.Equal("generic-simple-local-app", result.LaunchPlan.LiveRunProfileKey);
        Assert.Equal("simple-app-delivery", result.LaunchPlan.DefinitionKey);
        Assert.Equal(9, result.LaunchPlan.Steps.Count);
        Assert.False(string.IsNullOrWhiteSpace(result.LaunchPlan.PlanHash));
        var resolutionRequest = Assert.IsType<ProcessLaunchExecutorResolutionRequest>(
            executorResolver.LastRequest);
        Assert.Equal(
            "generic-simple-local-app",
            resolutionRequest.LiveRunProfile?.Key);
        Assert.Equal(
            "simple-app-delivery",
            resolutionRequest.Definition.Key);
    }

    private static ProcessLaunchApplicationService NewOperationalService(
        StatefulRuntimeStore runtimeStore,
        IProcessLaunchArtifactInitializer artifactInitializer,
        RecordingDispatchQueue dispatchQueue)
    {
        return new ProcessLaunchApplicationService(
            new ProcessTemplatePackLoader(),
            new TestClock(),
            new TestDriverCatalogProvider(),
            new AllStepsExecutorResolver(),
            new TrackingPlanStore(),
            runtimeStore,
            runtimeStore,
            assignmentStore: null!,
            artifactInitializer,
            new GenericProcessStepBriefBuilder(),
            dispatchQueue,
            projectionCatchupService: null!,
            new LaunchVariableTemplateResolver());
    }

    private static ProcessLaunchRequest NewLaunchRequest(bool execute)
    {
        return new ProcessLaunchRequest(
            DefinitionKey: "dotnet-runtime-command-writeback",
            ProcessDefinitionId: null,
            LiveRunProfileKey: null,
            ProjectId: null,
            ProjectNodeId: null,
            RequestedBy: "unit-test",
            Variables: new Dictionary<string, string>(StringComparer.Ordinal),
            RunReadiness: false,
            Execute: execute);
    }

    private sealed class TestClock : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TestDriverCatalogProvider : IProcessLaunchDriverCatalogProvider
    {
        private static readonly StrategyId ExecutionStrategyId =
            new("strategy.atomic-launch.execute");
        private static readonly IReadOnlySet<CapabilityTag> Capabilities =
            new HashSet<CapabilityTag>
            {
                new("capability.atomic-launch.execution")
            };

        public ValueTask<ProcessLaunchDriverCatalog> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var descriptor = new ProcessDriverDescriptor(
                new DriverId("driver.atomic-launch"),
                "Atomic launch test driver",
                "1.0.0",
                "runtime/1.0",
                "runtime/1.0",
                ProcessDriverLayer.Platform,
                Capabilities,
                [],
                [],
                [],
                [
                    new ProcessStrategyDescriptor(
                        ExecutionStrategyId,
                        "1.0.0",
                        ProcessStrategyKind.StepExecution,
                        Capabilities)
                ]);
            var catalog = new ProcessDriverCatalog(
                [new ProcessDriverPackage(descriptor, [], [], [], [], [], [])]);
            return ValueTask.FromResult(new ProcessLaunchDriverCatalog(
                catalog,
                ExecutionStrategyId,
                Capabilities));
        }
    }

    private sealed class AllStepsExecutorResolver : IProcessLaunchExecutorResolver
    {
        public ProcessLaunchExecutorResolutionRequest? LastRequest { get; private set; }

        public ValueTask<ProcessLaunchExecutorResolution> ResolveAsync(
            ProcessLaunchExecutorResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            var templateSteps = request.Definition.Steps.ToDictionary(
                step => step.Key,
                StringComparer.OrdinalIgnoreCase);
            var bindings = request.Plan.Steps
                .Where(step => step.IsExecutable)
                .Select(step =>
                {
                    templateSteps.TryGetValue(step.StepKey, out var templateStep);
                    var roleKey = templateStep?.RoleAssignments
                        .OrderBy(assignment => assignment.FallbackOrder)
                        .Select(assignment => assignment.RoleKey)
                        .FirstOrDefault(role => !string.IsNullOrWhiteSpace(role))
                        ?? "unit-test-role";
                    return new ProcessLaunchExecutorBinding(
                        step.StepKey,
                        roleKey,
                        ProcessLaunchExecutorKinds.Agent,
                        "unit-test-executor",
                        "Unit test executor",
                        "sha256:unit-test-readiness",
                        "Resolved by the focused launch test.");
                })
                .ToArray();
            return ValueTask.FromResult(new ProcessLaunchExecutorResolution(bindings, []));
        }
    }

    private sealed class TrackingPlanStore : IProcessInstancePlanStore
    {
        public int PersistCount { get; private set; }

        public ValueTask<PersistedProcessInstancePlan> PersistAsync(
            ProcessInstancePlan plan,
            CancellationToken cancellationToken = default)
        {
            PersistCount++;
            return ValueTask.FromResult(new PersistedProcessInstancePlan(
                plan.Header.PlanId,
                plan.PlanHash));
        }

        public ValueTask<ProcessInstancePlan?> LoadAsync(
            ProcessInstancePlanId planId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<ProcessInstancePlan?>(null);
    }

    private sealed class RejectingUnitOfWork : IProcessRuntimeUnitOfWork
    {
        public ProcessRuntimeCommitRequest? Request { get; private set; }

        public Task<ProcessRuntimeCommitResult> CommitAsync(
            ProcessRuntimeCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(
                ProcessRuntimeMutation.Rejected(
                    request.Mutation.State,
                    "Runtime.TestRejected",
                    "The focused test stops after the initial commit request.")));
        }
    }

    private sealed class StatefulRuntimeStore(
        ProcessEventType? rejectedEventType = null) :
        IProcessRuntimeUnitOfWork,
        IProcessRuntimeStateStore
    {
        public ProcessRuntimeStateSnapshot? State { get; private set; }

        public List<ProcessRuntimeCommitRequest> Requests { get; } = [];

        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                State?.RunId == runId
                    ? State
                    : null);
        }

        public Task<ProcessRuntimeCommitResult> CommitAsync(
            ProcessRuntimeCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (rejectedEventType is { } rejected &&
                request.Mutation.Events.Any(runtimeEvent => runtimeEvent.EventType == rejected))
            {
                var current = State ?? request.OriginalState;
                return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(
                    ProcessRuntimeMutation.Rejected(
                        current,
                        "Runtime.TestLifecycleRejected",
                        $"Lifecycle event '{rejected}' was rejected by the focused test.")));
            }

            if (request.Mutation.Outcome == ProcessRuntimeTransitionOutcome.Applied)
            {
                State = request.Mutation.State;
            }

            return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(request.Mutation));
        }
    }

    private sealed class NoOpArtifactInitializer : IProcessLaunchArtifactInitializer
    {
        public Task InitializeAsync(
            ProcessLaunchArtifactInitializationRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingArtifactInitializer : IProcessLaunchArtifactInitializer
    {
        public Task InitializeAsync(
            ProcessLaunchArtifactInitializationRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new IOException("Focused artifact initialization failure.");
        }
    }

    private sealed class RecordingDispatchQueue : IProcessRuntimeDispatchQueue
    {
        public List<ProcessRuntimeDispatchQueueRequest> Requests { get; } = [];

        public ValueTask EnqueueAsync(
            ProcessRuntimeDispatchQueueRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.CompletedTask;
        }

        public void EnqueueOrDefer(ProcessRuntimeDispatchQueueRequest request)
        {
            Requests.Add(request);
        }
    }
}
