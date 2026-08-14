using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "UnixRuntimePortability")]
public sealed class ProcessBlockedRunPersistedRecoveryTests
{
    private const string ChildBlockedDiagnosticCode = "process.adapter.subprocess_child_blocked";
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DispatcherOwnerId OwnerId = new("persisted-recovery-test");
    private static readonly ProcessStrategyBindingSnapshot Binding = new(
        new DriverId("driver.persisted-recovery"),
        new StrategyId("strategy.persisted-recovery.execute"),
        "1.0.0",
        "factory.1.0.0",
        "runtime.1",
        "runtime.1",
        Hash("persisted-recovery-binding"),
        []);

    private static string Hash(string value)
        => ProcessPlanHasher.ComputeContentHash(value);

    [Fact]
    public async Task Producer_and_consumer_recovery_history_survives_restarts_and_denies_replay()
    {
        var databaseName = $"process-blocked-recovery-{Guid.NewGuid():N}";
        var databaseRoot = new InMemoryDatabaseRoot();
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var producerStepId = ProcessStepInstanceId.New();
        var producerDefinitionId = ProcessStepDefinitionId.New();
        var consumerStepId = ProcessStepInstanceId.New();
        var consumerDefinitionId = ProcessStepDefinitionId.New();
        var artifactSlotId = ArtifactSlotId.New();
        var sourceResultKey = StrategyResultIdempotencyKey.New();
        var initialState = CreateBlockedState(
            runId,
            planId,
            producerStepId,
            producerDefinitionId,
            consumerStepId,
            consumerDefinitionId,
            artifactSlotId,
            sourceResultKey);
        var plan = CreateSimpleAppPlan(planId, initialState.Steps);
        initialState = initialState with { PlanHash = plan.PlanHash };
        var dispatchQueue = new RecordingDispatchQueue();

        await using (var seedContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(seedContext);
            var seedResult = await store.CommitAsync(new ProcessRuntimeCommitRequest(
                RuntimeCommandId.New(),
                initialState,
                Applied(initialState),
                InitialPlan: plan)
            {
                InitialAssignments = initialState.Steps
                    .Where(step => step.IsExecutable)
                    .Select(step => CreateAssignment(initialState, step, Now))
                    .ToArray()
            });

            Assert.True(seedResult.Succeeded);
        }

        await using (var producerRecoveryContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(producerRecoveryContext);
            var coordinator = CreateCoordinator(
                producerRecoveryContext,
                store,
                dispatchQueue,
                Now.AddMinutes(1));

            var producerRecovery = await coordinator.TryRecoverAsync(runId, "persisted-recovery-test");

            Assert.Equal(ProcessBlockedRunRecoveryOutcome.Recovered, producerRecovery.Outcome);
            Assert.Equal(
                ProcessBlockedRunRecoveryActionKind.UpstreamStepRework,
                producerRecovery.ActionKind);
            var persisted = await store.LoadAsync(runId);
            Assert.NotNull(persisted);
            var producerAction = Assert.Single(persisted.BlockedRecoveryActions);
            Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer, producerAction.Phase);
            Assert.Equal(sourceResultKey, producerAction.SourceResultIdempotencyKey);
            Assert.Equal(producerStepId, producerAction.TargetStepInstanceId);
        }

        await using (var producerExecutionContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(producerExecutionContext);
            var restartedState = await store.LoadAsync(runId);
            Assert.NotNull(restartedState);
            Assert.Equal(
                ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer,
                Assert.Single(restartedState.BlockedRecoveryActions).Phase);

            var producerResult = await ExecuteStepAsync(
                store,
                restartedState,
                producerStepId,
                producerDefinitionId,
                CreateProducerResult(artifactSlotId),
                Now.AddMinutes(2));
            Assert.Equal(
                ProcessRuntimeStepStatus.Completed,
                producerResult.Steps.Single(step => step.StepInstanceId == producerStepId).Status);
            var restoredInput = Assert.Single(producerResult.ConnectedInputArtifacts);
            Assert.Equal(ProcessArtifactInputAvailability.Available, restoredInput.Availability);
            Assert.NotNull(restoredInput.ArtifactId);
            Assert.Equal(Hash("restored-input"), restoredInput.ContentHash);

            var blockedResult = await PersistBlockedRunAsync(
                store,
                producerResult,
                Now.AddMinutes(5));
            Assert.Equal(ProcessRuntimeStatus.Blocked, blockedResult.Status);
        }

        await using (var consumerRecoveryContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(consumerRecoveryContext);
            var restartedState = await store.LoadAsync(runId);
            Assert.NotNull(restartedState);
            Assert.Equal(ProcessRuntimeStatus.Blocked, restartedState.Status);
            Assert.Equal(
                ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer,
                Assert.Single(restartedState.BlockedRecoveryActions).Phase);
            Assert.Equal(
                ProcessArtifactInputAvailability.Available,
                Assert.Single(restartedState.ConnectedInputArtifacts).Availability);
            var coordinator = CreateCoordinator(
                consumerRecoveryContext,
                store,
                dispatchQueue,
                Now.AddMinutes(6));

            var consumerRecovery = await coordinator.TryRecoverAsync(runId, "persisted-recovery-test");

            Assert.Equal(ProcessBlockedRunRecoveryOutcome.Recovered, consumerRecovery.Outcome);
            Assert.Equal(
                ProcessBlockedRunRecoveryActionKind.CurrentStepRework,
                consumerRecovery.ActionKind);
            var persisted = await store.LoadAsync(runId);
            Assert.NotNull(persisted);
            Assert.Collection(
                persisted.BlockedRecoveryActions,
                action => Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer, action.Phase),
                action => Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.RestoredConsumer, action.Phase));
            Assert.All(
                persisted.BlockedRecoveryActions,
                action => Assert.Equal(sourceResultKey, action.SourceResultIdempotencyKey));
        }

        await using (var replaySetupContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(replaySetupContext);
            var restartedState = await store.LoadAsync(runId);
            Assert.NotNull(restartedState);
            Assert.Equal(2, restartedState.BlockedRecoveryActions.Count);

            var unavailableState = restartedState with
            {
                AvailableArtifactSlots = restartedState.AvailableArtifactSlots
                    .Where(slotId => slotId != artifactSlotId)
                    .ToHashSet(),
                ConnectedInputArtifacts = restartedState.ConnectedInputArtifacts
                    .Select(receipt => receipt.RequiredSlotId == artifactSlotId
                        ? receipt with
                        {
                            Availability = ProcessArtifactInputAvailability.Expected,
                            ArtifactId = null,
                            ContentHash = string.Empty,
                            ConnectionHash = Hash("recurrent-missing-input")
                        }
                        : receipt)
                    .ToArray(),
                UpdatedAtUtc = Now.AddMinutes(7)
            };
            var unavailableCommit = await store.CommitAsync(new ProcessRuntimeCommitRequest(
                RuntimeCommandId.New(),
                restartedState,
                Applied(unavailableState)));
            Assert.True(unavailableCommit.Succeeded);

            var recurrentResult = await ExecuteStepAsync(
                store,
                unavailableCommit.State,
                consumerStepId,
                consumerDefinitionId,
                CreateMissingInputResult(artifactSlotId),
                Now.AddMinutes(8));
            var recurrentReceipt = ProcessRuntimeBlockedRecoveryAuthorizationRules
                .FindLatestBlockedManagerRequiredReceipt(recurrentResult, consumerStepId);
            Assert.NotNull(recurrentReceipt);
            Assert.NotEqual(sourceResultKey, recurrentReceipt.IdempotencyKey);
            var initialReceipt = recurrentResult.AppliedResults.Single(
                receipt => receipt.IdempotencyKey == sourceResultKey);
            Assert.Equal(
                initialReceipt.RecoveryDecision!.DiagnosticFingerprint,
                recurrentReceipt.RecoveryDecision!.DiagnosticFingerprint);

            var blockedResult = await PersistBlockedRunAsync(
                store,
                recurrentResult,
                Now.AddMinutes(11));
            Assert.Equal(ProcessRuntimeStatus.Blocked, blockedResult.Status);
            Assert.Equal(2, blockedResult.BlockedRecoveryActions.Count);
        }

        await using (var replayContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(replayContext);
            var restartedState = await store.LoadAsync(runId);
            Assert.NotNull(restartedState);
            Assert.Equal(2, restartedState.BlockedRecoveryActions.Count);
            var coordinator = CreateCoordinator(
                replayContext,
                store,
                dispatchQueue,
                Now.AddMinutes(12));

            var replay = await coordinator.TryRecoverAsync(runId, "persisted-recovery-test");

            Assert.Equal(ProcessBlockedRunRecoveryOutcome.RequiresAttention, replay.Outcome);
            Assert.Contains(
                replay.Diagnostics,
                diagnostic => diagnostic.Contains("budget", StringComparison.OrdinalIgnoreCase));
            var persisted = await store.LoadAsync(runId);
            Assert.NotNull(persisted);
            Assert.Collection(
                persisted.BlockedRecoveryActions,
                action => Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer, action.Phase),
                action => Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.RestoredConsumer, action.Phase));
            Assert.Equal(2, dispatchQueue.Requests.Count);
        }
    }

    [Fact]
    public async Task Completed_child_recovery_action_survives_context_recreation_and_denies_replay()
    {
        var databaseName = $"process-completed-child-recovery-{Guid.NewGuid():N}";
        var databaseRoot = new InMemoryDatabaseRoot();
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var parentStepId = ProcessStepInstanceId.New();
        var parentStepDefinitionId = ProcessStepDefinitionId.New();
        var sourceResultKey = StrategyResultIdempotencyKey.New();
        var childUpdatedAtUtc = Now.AddMinutes(1);
        var blockedParentState = CreateCompletedChildBlockedState(
            parentRunId,
            planId,
            parentStepId,
            parentStepDefinitionId,
            childRunId,
            sourceResultKey,
            Now.AddMinutes(2));
        var plan = CreateSimpleAppPlan(planId, blockedParentState.Steps);
        blockedParentState = blockedParentState with { PlanHash = plan.PlanHash };
        var claimToken = DispatchClaimToken.New();
        var launchableParentState = blockedParentState with
        {
            Status = ProcessRuntimeStatus.Active,
            Steps = blockedParentState.Steps
                .Select(step => step with
                {
                    Status = ProcessRuntimeStepStatus.Running,
                    ActiveClaimToken = claimToken
                })
                .ToArray(),
            Claims =
            [
                new DispatchClaimState(
                    claimToken,
                    parentStepId,
                    OwnerId,
                    DispatchClaimStatus.Claimed,
                    1,
                    Now,
                    Now.AddMinutes(10),
                    null,
                    null)
            ],
            AppliedResults = [],
            UpdatedAtUtc = Now
        };
        var parentAssignment = CreateAssignment(
            blockedParentState,
            blockedParentState.Steps.Single(step => step.StepInstanceId == parentStepId),
            Now);
        var childStepId = ProcessStepInstanceId.New();
        var childPlanStep = new ProcessRuntimeStepState(
            childStepId,
            ProcessStepDefinitionId.New(),
            ProcessRuntimeStepStatus.Completed,
            IsExecutable: true,
            AttemptNumber: 1,
            DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
            RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
            ActiveClaimToken: null,
            CompletedResultKey: StrategyResultIdempotencyKey.New());
        var childPlan = CreateSimpleAppPlan(
            ProcessInstancePlanId.New(),
            [childPlanStep]);
        var completedChildState = CreateChildState(
            parentRunId,
            childRunId,
            childPlan,
            ProcessRuntimeStatus.Completed,
            childUpdatedAtUtc);
        var childAssignment = parentAssignment with
        {
            RunId = childRunId,
            PlanId = childPlan.Header.PlanId,
            StepInstanceId = childStepId,
            StepKey = Assert.Single(childPlan.Steps).StepKey,
            LaunchVariables = ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                parentRunId,
                parentStepId),
            CreatedAtUtc = childUpdatedAtUtc.AddMinutes(-1)
        };
        var dispatchQueue = new RecordingDispatchQueue();

        await using (var seedContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(
                seedContext,
                new FixedTimeProvider(Now));
            var parentSeed = await store.CommitAsync(new ProcessRuntimeCommitRequest(
                RuntimeCommandId.New(),
                launchableParentState,
                Applied(launchableParentState),
                InitialPlan: plan)
            {
                InitialAssignments = [parentAssignment]
            });
            Assert.True(parentSeed.Succeeded);

            var childSeed = await store.CommitAsync(new ProcessRuntimeCommitRequest(
                RuntimeCommandId.New(),
                completedChildState,
                Applied(completedChildState),
                ParentStepPrecondition: new ProcessRuntimeParentStepReference(
                    parentRunId,
                    parentStepId),
                InitialPlan: childPlan)
            {
                InitialAssignments = [childAssignment]
            });
            Assert.True(childSeed.Succeeded);

            var blockedSeed = await store.CommitAsync(new ProcessRuntimeCommitRequest(
                RuntimeCommandId.New(),
                launchableParentState,
                Applied(blockedParentState)));
            Assert.True(blockedSeed.Succeeded);
        }

        await using (var recoveryContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(recoveryContext);
            var assignmentStore = new EfProcessRuntimeStepAssignmentStore(recoveryContext);
            var coordinator = CreateCoordinator(
                recoveryContext,
                store,
                dispatchQueue,
                Now.AddMinutes(3),
                assignmentStore);

            var recovery = await coordinator.TryRecoverAsync(
                parentRunId,
                "persisted-child-recovery-test");

            Assert.Equal(ProcessBlockedRunRecoveryOutcome.Recovered, recovery.Outcome);
            Assert.Equal(
                ProcessBlockedRunRecoveryPolicy.CompletedChildConsumerRework,
                recovery.Policy);
            var persisted = await store.LoadAsync(parentRunId);
            Assert.NotNull(persisted);
            var action = Assert.Single(persisted.BlockedRecoveryActions);
            Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.CompletedChildConsumer, action.Phase);
            Assert.Equal(childRunId, action.RelatedChildRunId);
            Assert.Equal(childUpdatedAtUtc, action.RelatedChildUpdatedAtUtc);

            var replayState = await PersistBlockedRunAsync(
                store,
                persisted,
                Now.AddMinutes(4),
                parentStepId);
            Assert.Equal(ProcessRuntimeStatus.Blocked, replayState.Status);
        }

        await using (var replayContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(replayContext);
            var assignmentStore = new EfProcessRuntimeStepAssignmentStore(replayContext);
            var persisted = await store.LoadAsync(parentRunId);
            Assert.NotNull(persisted);
            var action = Assert.Single(persisted.BlockedRecoveryActions);
            Assert.Equal(childRunId, action.RelatedChildRunId);
            Assert.Equal(childUpdatedAtUtc, action.RelatedChildUpdatedAtUtc);
            var childEvidence = await store.LoadAsync(childRunId);
            Assert.NotNull(childEvidence);
            Assert.Equal(ProcessRuntimeStatus.Completed, childEvidence.Status);
            Assert.Equal(childUpdatedAtUtc, childEvidence.UpdatedAtUtc);
            var linkedAssignments = await assignmentStore.FindByLaunchVariablesAsync(
                ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                    parentRunId,
                    parentStepId));
            Assert.Contains(
                linkedAssignments,
                assignment => assignment.RunId == childRunId);
            var coordinator = CreateCoordinator(
                replayContext,
                store,
                dispatchQueue,
                Now.AddMinutes(5),
                assignmentStore);

            var replay = await coordinator.TryRecoverAsync(
                parentRunId,
                "persisted-child-recovery-test");

            Assert.Equal(ProcessBlockedRunRecoveryOutcome.RequiresAttention, replay.Outcome);
            Assert.Contains(
                replay.Diagnostics,
                diagnostic => diagnostic.Contains("already applied", StringComparison.OrdinalIgnoreCase));
            Assert.Single(dispatchQueue.Requests);
        }
    }

    [Theory]
    [InlineData(ChildLineageRaceMutation.SiblingReactivated)]
    [InlineData(ChildLineageRaceMutation.NewerChildLinked)]
    [InlineData(ChildLineageRaceMutation.LinkedChildStateMissing)]
    [InlineData(ChildLineageRaceMutation.SiblingLinkChanged)]
    public async Task Completed_child_recovery_rejects_lineage_change_before_runtime_commit(
        ChildLineageRaceMutation mutation)
    {
        var databaseName = $"process-child-lineage-race-{Guid.NewGuid():N}";
        var databaseRoot = new InMemoryDatabaseRoot();
        var scenario = await SeedChildLineageScenarioAsync(databaseName, databaseRoot);
        var dispatchQueue = new RecordingDispatchQueue();

        await using var recoveryContext = CreateDbContext(databaseName, databaseRoot);
        var store = new EfProcessRuntimeUnitOfWork(recoveryContext);
        var assignmentStore = new EfProcessRuntimeStepAssignmentStore(recoveryContext);
        var interceptingUnitOfWork = new InterceptingRuntimeUnitOfWork(
            store,
            async (request, cancellationToken) =>
            {
                Assert.NotNull(
                    request.BlockedRecoveryAuthorization?.ExpectedChildLineageEvidence);
                await ApplyChildLineageRaceMutationAsync(
                    recoveryContext,
                    assignmentStore,
                    scenario,
                    mutation,
                    cancellationToken);
            });
        var coordinator = CreateCoordinator(
            recoveryContext,
            store,
            dispatchQueue,
            Now.AddMinutes(4),
            assignmentStore,
            interceptingUnitOfWork);

        var recovery = await coordinator.TryRecoverAsync(
            scenario.ParentRunId,
            "persisted-child-lineage-race-test");

        Assert.True(interceptingUnitOfWork.Intercepted);
        Assert.Equal(ProcessBlockedRunRecoveryOutcome.RequiresAttention, recovery.Outcome);
        Assert.NotEmpty(recovery.Diagnostics);
        Assert.Empty(dispatchQueue.Requests);
        var persistedParent = await store.LoadAsync(scenario.ParentRunId);
        Assert.NotNull(persistedParent);
        Assert.Equal(ProcessRuntimeStatus.Blocked, persistedParent.Status);
        Assert.Empty(persistedParent.BlockedRecoveryActions);
    }

    private static async Task<ChildLineageScenario> SeedChildLineageScenarioAsync(
        string databaseName,
        InMemoryDatabaseRoot databaseRoot)
    {
        var parentRunId = ProcessRunId.New();
        var relatedChildRunId = ProcessRunId.New();
        var siblingRunId = ProcessRunId.New();
        var newerChildRunId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var parentStepId = ProcessStepInstanceId.New();
        var parentStepDefinitionId = ProcessStepDefinitionId.New();
        var initialBlockedParentState = CreateCompletedChildBlockedState(
            parentRunId,
            planId,
            parentStepId,
            parentStepDefinitionId,
            relatedChildRunId,
            StrategyResultIdempotencyKey.New(),
            Now.AddMinutes(2));
        var alternateParentStepId = ProcessStepInstanceId.New();
        var alternateParentStep = new ProcessRuntimeStepState(
            alternateParentStepId,
            ProcessStepDefinitionId.New(),
            ProcessRuntimeStepStatus.Completed,
            IsExecutable: true,
            AttemptNumber: 1,
            DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
            RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
            ActiveClaimToken: null,
            CompletedResultKey: null);
        var blockedParentState = initialBlockedParentState with
        {
            Steps = initialBlockedParentState.Steps
                .Append(alternateParentStep)
                .ToArray()
        };
        var plan = CreateSimpleAppPlan(planId, blockedParentState.Steps);
        blockedParentState = blockedParentState with { PlanHash = plan.PlanHash };
        var claimToken = DispatchClaimToken.New();
        var alternateClaimToken = DispatchClaimToken.New();
        var launchableParentState = blockedParentState with
        {
            Status = ProcessRuntimeStatus.Active,
            Steps = blockedParentState.Steps
                .Select(step => step with
                {
                    Status = ProcessRuntimeStepStatus.Running,
                    ActiveClaimToken = step.StepInstanceId == parentStepId
                        ? claimToken
                        : alternateClaimToken
                })
                .ToArray(),
            Claims =
            [
                new DispatchClaimState(
                    claimToken,
                    parentStepId,
                    OwnerId,
                    DispatchClaimStatus.Claimed,
                    1,
                    Now,
                    Now.AddMinutes(10),
                    null,
                    null),
                new DispatchClaimState(
                    alternateClaimToken,
                    alternateParentStepId,
                    OwnerId,
                    DispatchClaimStatus.Claimed,
                    1,
                    Now,
                    Now.AddMinutes(10),
                    null,
                    null)
            ],
            AppliedResults = [],
            UpdatedAtUtc = Now
        };
        var parentAssignment = CreateAssignment(
            blockedParentState,
            blockedParentState.Steps.Single(step => step.StepInstanceId == parentStepId),
            Now);
        var alternateParentAssignment = CreateAssignment(
            blockedParentState,
            alternateParentStep,
            Now);
        var relatedChild = CreateLinkedChildSeed(
            parentAssignment,
            parentRunId,
            parentStepId,
            relatedChildRunId,
            ProcessRuntimeStatus.Completed,
            Now.AddMinutes(1),
            Now.AddMinutes(1));
        var sibling = CreateLinkedChildSeed(
            parentAssignment,
            parentRunId,
            parentStepId,
            siblingRunId,
            ProcessRuntimeStatus.Failed,
            Now.AddSeconds(45),
            Now.AddSeconds(30));
        var newerChild = CreateLinkedChildSeed(
            alternateParentAssignment,
            parentRunId,
            alternateParentStepId,
            newerChildRunId,
            ProcessRuntimeStatus.Completed,
            Now.AddMinutes(1).AddSeconds(30),
            Now.AddMinutes(2));

        await using var seedContext = CreateDbContext(databaseName, databaseRoot);
        var store = new EfProcessRuntimeUnitOfWork(
            seedContext,
            new FixedTimeProvider(Now));
        var parentSeed = await store.CommitAsync(new ProcessRuntimeCommitRequest(
            RuntimeCommandId.New(),
            launchableParentState,
            Applied(launchableParentState),
            InitialPlan: plan)
        {
            InitialAssignments = [parentAssignment, alternateParentAssignment]
        });
        Assert.True(parentSeed.Succeeded);

        foreach (var child in new[]
                 {
                     relatedChild,
                     sibling,
                     newerChild
                 })
        {
            var childRequest = new ProcessRuntimeCommitRequest(
                RuntimeCommandId.New(),
                child.State,
                Applied(child.State),
                ParentStepPrecondition: child.ParentStepPrecondition,
                InitialPlan: child.Plan)
            {
                InitialAssignments = [child.Assignment]
            };
            var childSeed = await store.CommitAsync(childRequest);
            Assert.True(childSeed.Succeeded);
        }

        var blockedSeed = await store.CommitAsync(new ProcessRuntimeCommitRequest(
            RuntimeCommandId.New(),
            launchableParentState,
            Applied(blockedParentState)));
        Assert.True(blockedSeed.Succeeded);

        return new ChildLineageScenario(
            parentRunId,
            parentStepId,
            siblingRunId,
            newerChild.Assignment);
    }

    private static ProcessRuntimeStateSnapshot CreateChildState(
        ProcessRunId rootRunId,
        ProcessRunId runId,
        ProcessInstancePlan plan,
        ProcessRuntimeStatus status,
        DateTimeOffset updatedAtUtc)
    {
        var stepStatus = status switch
        {
            ProcessRuntimeStatus.Completed => ProcessRuntimeStepStatus.Completed,
            ProcessRuntimeStatus.Failed => ProcessRuntimeStepStatus.Failed,
            ProcessRuntimeStatus.Cancelled => ProcessRuntimeStepStatus.Cancelled,
            ProcessRuntimeStatus.Blocked => ProcessRuntimeStepStatus.Blocked,
            _ => ProcessRuntimeStepStatus.Ready
        };
        return new ProcessRuntimeStateSnapshot(
            rootRunId,
            runId,
            plan.Header.PlanId,
            plan.PlanHash,
            status,
            plan.Steps
                .Select(step => new ProcessRuntimeStepState(
                    step.StepInstanceId,
                    step.StepDefinitionId,
                    stepStatus,
                    step.IsExecutable,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: stepStatus == ProcessRuntimeStepStatus.Completed
                        ? StrategyResultIdempotencyKey.New()
                        : null))
                .ToArray(),
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            updatedAtUtc);
    }

    private static ProcessRuntimeStepAssignment CreateLinkedChildAssignment(
        ProcessRuntimeStepAssignment parentAssignment,
        ProcessRunId parentRunId,
        ProcessStepInstanceId parentStepId,
        ProcessRunId childRunId,
        DateTimeOffset createdAtUtc)
    {
        var childStepId = ProcessStepInstanceId.New();
        return parentAssignment with
        {
            RunId = childRunId,
            StepInstanceId = childStepId,
            StepKey = $"child-{childStepId.Value:N}",
            LaunchVariables = ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                parentRunId,
                parentStepId),
            CreatedAtUtc = createdAtUtc
        };
    }

    private static LinkedChildSeed CreateLinkedChildSeed(
        ProcessRuntimeStepAssignment parentAssignment,
        ProcessRunId parentRunId,
        ProcessStepInstanceId parentStepId,
        ProcessRunId childRunId,
        ProcessRuntimeStatus status,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset linkedAtUtc)
    {
        var assignment = CreateLinkedChildAssignment(
            parentAssignment,
            parentRunId,
            parentStepId,
            childRunId,
            linkedAtUtc);
        var planStep = new ProcessRuntimeStepState(
            assignment.StepInstanceId,
            ProcessStepDefinitionId.New(),
            ProcessRuntimeStepStatus.Planned,
            IsExecutable: true,
            AttemptNumber: 0,
            DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
            RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
            ActiveClaimToken: null,
            CompletedResultKey: null);
        var plan = CreateSimpleAppPlan(
            ProcessInstancePlanId.New(),
            [planStep]);
        assignment = assignment with
        {
            PlanId = plan.Header.PlanId,
            StepKey = Assert.Single(plan.Steps).StepKey
        };
        return new LinkedChildSeed(
            plan,
            CreateChildState(
                parentRunId,
                childRunId,
                plan,
                status,
                updatedAtUtc),
            assignment,
            new ProcessRuntimeParentStepReference(parentRunId, parentStepId));
    }

    private static async Task ApplyChildLineageRaceMutationAsync(
        ProcessPersistenceDbContext dbContext,
        EfProcessRuntimeStepAssignmentStore assignmentStore,
        ChildLineageScenario scenario,
        ChildLineageRaceMutation mutation,
        CancellationToken cancellationToken)
    {
        switch (mutation)
        {
            case ChildLineageRaceMutation.SiblingReactivated:
            {
                var siblingState = await dbContext.RuntimeStates.SingleAsync(
                    state => state.RunId == scenario.SiblingRunId.Value,
                    cancellationToken);
                siblingState.Status = ProcessRuntimeStatus.Active;
                siblingState.UpdatedAtUtc = Now.AddMinutes(3);
                siblingState.ConcurrencyToken = Guid.NewGuid();
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            case ChildLineageRaceMutation.NewerChildLinked:
            {
                var newerChildAssignment = await dbContext.RuntimeStepAssignments.SingleAsync(
                    assignment => assignment.RunId == scenario.NewerChildAssignment.RunId.Value,
                    cancellationToken);
                newerChildAssignment.LaunchVariablesJson =
                    EfProcessRuntimeStepAssignmentStore.ToEntity(
                        scenario.NewerChildAssignment with
                        {
                            LaunchVariables = ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                                scenario.ParentRunId,
                                scenario.ParentStepId)
                        })
                    .LaunchVariablesJson;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            case ChildLineageRaceMutation.LinkedChildStateMissing:
            {
                var siblingState = await dbContext.RuntimeStates.SingleAsync(
                    state => state.RunId == scenario.SiblingRunId.Value,
                    cancellationToken);
                dbContext.RuntimeStates.Remove(siblingState);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            case ChildLineageRaceMutation.SiblingLinkChanged:
            {
                var siblingAssignment = await dbContext.RuntimeStepAssignments.SingleAsync(
                    assignment => assignment.RunId == scenario.SiblingRunId.Value,
                    cancellationToken);
                siblingAssignment.LaunchVariablesJson = "{}";
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unsupported child-lineage race mutation.");
        }
    }

    private static async Task<ProcessRuntimeStateSnapshot> ExecuteStepAsync(
        EfProcessRuntimeUnitOfWork store,
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepId,
        ProcessStepDefinitionId stepDefinitionId,
        StrategyResultEnvelope result,
        DateTimeOffset startedAtUtc)
    {
        var engine = new ProcessRuntimeEngine(store);
        var claimToken = DispatchClaimToken.New();
        var workItem = new DispatchWorkItem(
            state.RunId,
            stepId,
            stepDefinitionId,
            Binding,
            attemptNumber: 1);
        var claim = await engine.CreateClaimAsync(
            state,
            Context(startedAtUtc),
            new CreateDispatchClaimCommand(
                workItem,
                OwnerId,
                claimToken,
                startedAtUtc.AddMinutes(10)));
        Assert.True(claim.Succeeded);
        var running = await engine.MarkClaimRunningAsync(
            claim.State,
            Context(startedAtUtc.AddMinutes(1)),
            stepId,
            claimToken);
        Assert.True(running.Succeeded);
        var completed = await engine.SubmitStrategyResultAsync(
            running.State,
            Context(startedAtUtc.AddMinutes(2)),
            new SubmitStrategyResultCommand(
                stepId,
                OwnerId,
                claimToken,
                StrategyResultIdempotencyKey.New(),
                result));
        Assert.True(completed.Succeeded);
        return completed.State;
    }

    private static async Task<ProcessRuntimeStateSnapshot> PersistBlockedRunAsync(
        EfProcessRuntimeUnitOfWork store,
        ProcessRuntimeStateSnapshot state,
        DateTimeOffset occurredAtUtc,
        ProcessStepInstanceId? blockedStepId = null)
    {
        var blockedState = state with
        {
            Status = ProcessRuntimeStatus.Blocked,
            Steps = blockedStepId is null
                ? state.Steps
                : state.Steps
                    .Select(step => step.StepInstanceId == blockedStepId
                        ? step with
                        {
                            Status = ProcessRuntimeStepStatus.Blocked,
                            ActiveClaimToken = null,
                            CompletedResultKey = null
                        }
                        : step)
                    .ToArray(),
            UpdatedAtUtc = occurredAtUtc
        };
        var runtimeEvent = new ProcessRuntimeEventEnvelope(
            RuntimeEventId.New(),
            blockedState.RootRunId,
            blockedState.RunId,
            new ProcessCorrelationId($"persisted-recovery-{Guid.NewGuid():N}"),
            CausationId: null,
            new ProcessEventActor(
                ProcessEventActorKind.System,
                new ProcessActorId("persisted-recovery-test")),
            ProcessContractVersions.RuntimeEventEnvelopeV1,
            ProcessEventSensitivity.Normal,
            occurredAtUtc,
            ProcessRuntimeEventTypes.ProcessRunBlocked,
            Hash("persisted-recovery-blocked"));
        var mutation = new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Applied,
            blockedState,
            [runtimeEvent],
            [
                new ProcessOutboxMessage(
                    RuntimeOutboxMessageId.New(),
                    runtimeEvent.EventId,
                    ProcessOutboxSubscriberKind.RuntimeProjection,
                    runtimeEvent.PayloadHash)
            ],
            [],
            []);
        var commit = await store.CommitAsync(new ProcessRuntimeCommitRequest(
            RuntimeCommandId.New(),
            state,
            mutation));
        Assert.True(commit.Succeeded);
        return commit.State;
    }

    private static ProcessBlockedRunRecoveryCoordinator CreateCoordinator(
        ProcessPersistenceDbContext dbContext,
        EfProcessRuntimeUnitOfWork store,
        RecordingDispatchQueue dispatchQueue,
        DateTimeOffset now,
        IProcessRuntimeStepAssignmentStore? persistedAssignmentStore = null,
        IProcessRuntimeUnitOfWork? runtimeUnitOfWork = null)
    {
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(now);
        var assignmentStore = persistedAssignmentStore ?? new RecoveryAssignmentStore(store);
        var operatorService = new ProcessRuntimeOperatorApplicationService(
            clock,
            store,
            store,
            assignmentStore,
            runtimeUnitOfWork ?? store,
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                EmptyRuntimeEventReplayStore.Instance,
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);
        return new ProcessBlockedRunRecoveryCoordinator(
            store,
            new EfProcessInstancePlanStore(dbContext),
            assignmentStore,
            new ProcessBlockedRunRecoveryCommandExecutor(operatorService),
            new ProcessBlockedRunRecoveryPolicyCatalog());
    }

    private static ProcessRuntimeStateSnapshot CreateBlockedState(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId producerStepId,
        ProcessStepDefinitionId producerDefinitionId,
        ProcessStepInstanceId consumerStepId,
        ProcessStepDefinitionId consumerDefinitionId,
        ArtifactSlotId artifactSlotId,
        StrategyResultIdempotencyKey sourceResultKey)
    {
        var producerResultKey = StrategyResultIdempotencyKey.New();
        var diagnostic = new StrategyResultDiagnosticReceipt(
            ProcessRuntimeDiagnosticCodes.MissingRequiredInputArtifact,
            StrategyDiagnosticSensitivity.Normal,
            Hash("missing-input"),
            "Required input artifact is missing.",
            RestrictedEvidenceReference: null,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
        var recoveryDecision = ProcessRecoveryClassifier.Default.ClassifyBlocked(
            new ProcessRecoveryClassificationInput(
                consumerStepId,
                ProcessFailureCategory.MissingArtifact,
                diagnostic.Code,
                ProcessRecoveryRouteKind.UpstreamStepRework,
                producerStepId,
                [diagnostic],
                []));
        return new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            Hash("persisted-recovery-plan"),
            ProcessRuntimeStatus.Blocked,
            [
                new ProcessRuntimeStepState(
                    producerStepId,
                    producerDefinitionId,
                    ProcessRuntimeStepStatus.Completed,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: producerResultKey)
                {
                    ProducedArtifactSlots = new HashSet<ArtifactSlotId> { artifactSlotId }
                },
                new ProcessRuntimeStepState(
                    consumerStepId,
                    consumerDefinitionId,
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId> { producerStepId },
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId> { artifactSlotId },
                    ActiveClaimToken: null,
                    CompletedResultKey: null)
            ],
            [],
            [
                new StrategyResultReceipt(
                    producerStepId,
                    Binding.StrategyId,
                    producerResultKey,
                    StrategyOutcome.Succeeded,
                    ProcessRuntimeStepStatus.Completed,
                    Hash("initial-producer-result"))
                {
                    AppliedSequence = 1
                },
                new StrategyResultReceipt(
                    consumerStepId,
                    Binding.StrategyId,
                    sourceResultKey,
                    StrategyOutcome.NeedsManager,
                    ProcessRuntimeStepStatus.Blocked,
                    Hash("missing-input-result"),
                    [diagnostic],
                    recoveryDecision: recoveryDecision)
                {
                    AppliedSequence = 2
                }
            ],
            new HashSet<ArtifactSlotId>(),
            Now)
        {
            ConnectedInputArtifacts =
            [
                new ProcessRuntimeInputArtifactReceipt(
                    consumerStepId,
                    artifactSlotId,
                    ProcessArtifactInputAvailability.Expected,
                    producerStepId,
                    ArtifactId: null,
                    ContentHash: string.Empty,
                    ConnectionHash: Hash("producer-to-consumer"))
            ]
        };
    }

    private static ProcessRuntimeStateSnapshot CreateCompletedChildBlockedState(
        ProcessRunId parentRunId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId parentStepId,
        ProcessStepDefinitionId parentStepDefinitionId,
        ProcessRunId childRunId,
        StrategyResultIdempotencyKey sourceResultKey,
        DateTimeOffset updatedAtUtc)
    {
        var diagnostic = new StrategyResultDiagnosticReceipt(
            ChildBlockedDiagnosticCode,
            StrategyDiagnosticSensitivity.Normal,
            Hash("completed-child-blocked-parent"),
            "The parent is waiting on a linked child run.",
            RestrictedEvidenceReference: null,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent)
        {
            RelatedChildRunId = childRunId
        };
        var recoveryDecision = ProcessRecoveryClassifier.Default.ClassifyBlocked(
            new ProcessRecoveryClassificationInput(
                parentStepId,
                ProcessFailureCategory.ChildRunBlocked,
                diagnostic.Code,
                ProcessRecoveryRouteKind.ChildRunPropagation,
                parentStepId,
                [diagnostic],
                []));
        return new ProcessRuntimeStateSnapshot(
            parentRunId,
            parentRunId,
            planId,
            Hash("persisted-recovery-plan"),
            ProcessRuntimeStatus.Blocked,
            [
                new ProcessRuntimeStepState(
                    parentStepId,
                    parentStepDefinitionId,
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: null)
            ],
            [],
            [
                new StrategyResultReceipt(
                    parentStepId,
                    Binding.StrategyId,
                    sourceResultKey,
                    StrategyOutcome.NeedsManager,
                    ProcessRuntimeStepStatus.Blocked,
                    Hash("completed-child-blocked-result"),
                    [diagnostic],
                    recoveryDecision: recoveryDecision)
                {
                    AppliedSequence = 1
                }
            ],
            new HashSet<ArtifactSlotId>(),
            updatedAtUtc);
    }

    private static ProcessInstancePlan CreateSimpleAppPlan(
        ProcessInstancePlanId planId,
        IReadOnlyList<ProcessRuntimeStepState> runtimeSteps)
    {
        var plan = new ProcessInstancePlan(
            new ProcessInstancePlanHeader(
                planId,
                planId,
                ParentPlanId: null,
                ParentStepId: null,
                "processes.instance-plan.v1",
                Now,
                HierarchyDepth: 0),
            new ResolvedProcessDefinitionSnapshot(
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                Hash("persisted-recovery-definition"),
                "template/1",
                "template/1",
                [],
                [
                    new ResolvedTemplateComponentSnapshot(
                        TemplateComponentId.New(),
                        "simple-app-delivery",
                        "1.0.0",
                        Hash("simple-app-template"))
                ],
                []),
            new DriverStackSnapshot([]),
            new StrategyBindingSet([Binding], [], [], []),
            runtimeSteps
                .Select(step => new StepInstancePlan(
                    step.StepInstanceId,
                    step.StepDefinitionId,
                    $"step-{step.StepInstanceId.Value:N}",
                    ProcessStepKind.Activity,
                    step.IsExecutable,
                    StartsSubprocess: false,
                    Binding))
                .ToArray(),
            new ArtifactPlan([], []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan(Hash("manager-policy"), null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(true, Hash("monitoring")),
            new SecurityPlan(Hash("security"), []),
            string.Empty);
        return plan with
        {
            PlanHash = ProcessPlanHasher.Compute(plan)
        };
    }

    private static StrategyResultEnvelope CreateProducerResult(ArtifactSlotId artifactSlotId)
    {
        return new StrategyResultEnvelope(
            Binding.StrategyId,
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.Succeeded,
            [
                new ProducedArtifactRef(
                    ArtifactInstanceId.New(),
                    artifactSlotId,
                    Hash("restored-input"))
            ],
            [],
            [],
            [],
            Hash("restored-producer-result"));
    }

    private static StrategyResultEnvelope CreateMissingInputResult(ArtifactSlotId artifactSlotId)
    {
        return new StrategyResultEnvelope(
            Binding.StrategyId,
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.NeedsManager,
            [],
            [new RequestedArtifactRef(artifactSlotId, Hash("requested-input"))],
            [
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode(ProcessRuntimeDiagnosticCodes.MissingRequiredInputArtifact),
                    StrategyDiagnosticSensitivity.Normal,
                    Hash("missing-input"),
                    "Required input artifact is missing.",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode(ProcessRuntimeDiagnosticCodes.MissingRequiredInputArtifact),
                    Hash("missing-input"),
                    "Required input artifact is missing.")
            ],
            Hash("recurrent-missing-input-result"));
    }

    private static ProcessRuntimeMutation Applied(ProcessRuntimeStateSnapshot state)
    {
        return new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Applied,
            state,
            [],
            [],
            [],
            []);
    }

    private static RuntimeCommandContext Context(DateTimeOffset occurredAtUtc)
    {
        return new RuntimeCommandContext(
            RuntimeCommandId.New(),
            new ProcessEventActor(
                ProcessEventActorKind.System,
                new ProcessActorId("persisted-recovery-test")),
            new ProcessCorrelationId($"persisted-recovery-{Guid.NewGuid():N}"),
            occurredAtUtc);
    }

    private static ProcessPersistenceDbContext CreateDbContext(
        string databaseName,
        InMemoryDatabaseRoot databaseRoot)
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot)
            .Options;
        return new ProcessPersistenceDbContext(options);
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step,
        DateTimeOffset? createdAtUtc = null)
    {
        return new ProcessRuntimeStepAssignment(
            state.RunId,
            state.PlanId,
            step.StepInstanceId,
            $"step-{step.StepInstanceId.Value:N}",
            "recovery-worker",
            "recovery-worker",
            "Recovery worker",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            "Recovery worker",
            "Recover the missing managed process artifact.",
            Hash("readiness"),
            "Persisted recovery test assignment.",
            step.ProducedArtifactSlots.ToArray(),
            step.RequiredArtifactSlots.ToArray(),
            [ProcessOperationContractNames.WriteManagedProcessArtifacts],
            ProcessOperationContractNames.ManagedProcessArtifactsOnly,
            new Dictionary<string, string>(StringComparer.Ordinal),
            BranchGate: null,
            createdAtUtc ?? Now);
    }

    public enum ChildLineageRaceMutation
    {
        SiblingReactivated,
        NewerChildLinked,
        LinkedChildStateMissing,
        SiblingLinkChanged
    }

    private sealed record ChildLineageScenario(
        ProcessRunId ParentRunId,
        ProcessStepInstanceId ParentStepId,
        ProcessRunId SiblingRunId,
        ProcessRuntimeStepAssignment NewerChildAssignment);

    private sealed record LinkedChildSeed(
        ProcessInstancePlan Plan,
        ProcessRuntimeStateSnapshot State,
        ProcessRuntimeStepAssignment Assignment,
        ProcessRuntimeParentStepReference ParentStepPrecondition);

    private sealed class InterceptingRuntimeUnitOfWork(
        IProcessRuntimeUnitOfWork inner,
        Func<ProcessRuntimeCommitRequest, CancellationToken, Task> beforeCommit)
        : IProcessRuntimeUnitOfWork
    {
        private int intercepted;

        public bool Intercepted => intercepted != 0;

        public async Task<ProcessRuntimeCommitResult> CommitAsync(
            ProcessRuntimeCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref intercepted, 1) == 0)
            {
                await beforeCommit(request, cancellationToken).ConfigureAwait(false);
            }

            return await inner.CommitAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class FixedProcessProjectionClock(DateTimeOffset utcNow) : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
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

    private sealed class RecoveryAssignmentStore(
        IProcessRuntimeStateStore stateStore) : IProcessRuntimeStepAssignmentStore
    {
        public ValueTask SaveAsync(
            IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public async ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
        {
            var state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false);
            return state is null
                ? []
                : state.Steps.Select(step => CreateAssignment(state, step)).ToArray();
        }

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(
            IReadOnlyDictionary<string, string> requiredVariables,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>([]);
        }

        public async ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
            ProcessRunId runId,
            ProcessStepInstanceId stepInstanceId,
            CancellationToken cancellationToken = default)
        {
            var assignments = await LoadByRunAsync(runId, cancellationToken).ConfigureAwait(false);
            return assignments.FirstOrDefault(assignment =>
                assignment.StepInstanceId == stepInstanceId);
        }
    }

    private sealed class EmptyRuntimeEventReplayStore : IProcessRuntimeEventReplayStore
    {
        public static EmptyRuntimeEventReplayStore Instance { get; } = new();

        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadAfterGlobalSequenceAsync(
            long globalSequenceExclusive,
            int take,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProcessStoredRuntimeEvent>>([]);
        }

        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadByRootRunAsync(
            ProcessRunId rootRunId,
            long rootSequenceExclusive,
            int take,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProcessStoredRuntimeEvent>>([]);
        }
    }
}
