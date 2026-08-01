using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.AgentFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessPersistenceStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Commit_writes_runtime_state_events_outbox_ledger_and_idempotency_atomically()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: true);

        var result = await unitOfWork.CommitAsync(request);

        Assert.Equal(ProcessRuntimeTransitionOutcome.Applied, result.Outcome);
        Assert.Equal(1, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(1, await dbContext.RuntimeSteps.CountAsync());
        Assert.Equal(1, await dbContext.RuntimeEvents.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        Assert.Equal(1, await dbContext.ArtifactLedgerEvents.CountAsync());
        Assert.Equal(1, await dbContext.IdempotencyKeys.CountAsync());

        var loaded = await unitOfWork.LoadAsync(request.Mutation.State.RunId);
        Assert.NotNull(loaded);
        Assert.Equal(ProcessRuntimeStatus.Completed, loaded.Status);
        Assert.Contains(loaded.AvailableArtifactSlots, slot => slot == RequiredArtifactSlotId);
        var loadedStep = Assert.Single(loaded.Steps);
        Assert.Contains(loadedStep.ProducedArtifactSlots, slot => slot == RequiredArtifactSlotId);
        Assert.Contains(loadedStep.RequiredRuntimeToolNames, toolName => toolName == "runtime-tool");
        var loadedInputArtifact = Assert.Single(loaded.ConnectedInputArtifacts);
        Assert.Equal(ProcessArtifactInputAvailability.Available, loadedInputArtifact.Availability);
        Assert.Equal(RequiredArtifactSlotId, loadedInputArtifact.RequiredSlotId);
    }

    [Fact]
    public async Task Initial_commit_writes_plan_and_runtime_mutation_together()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var plan = NewInitialPlan();
        var request = NewCommitRequest(
            includeArtifactLedger: true,
            initialPlan: plan);

        var result = await unitOfWork.CommitAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(1, await dbContext.InstancePlans.CountAsync());
        Assert.Equal(1, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(1, await dbContext.RuntimeEvents.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        Assert.Equal(1, await dbContext.ArtifactLedgerEvents.CountAsync());
        Assert.Equal(1, await dbContext.IdempotencyKeys.CountAsync());
        var persistedPlan = await new EfProcessInstancePlanStore(dbContext).LoadAsync(plan.Header.PlanId);
        Assert.NotNull(persistedPlan);
        Assert.Equal(plan.PlanHash, persistedPlan.PlanHash);
    }

    [Fact]
    public async Task Initial_commit_reuses_existing_plan_with_matching_identity_and_hash()
    {
        await using var dbContext = CreateDbContext();
        var plan = NewInitialPlan();
        await new EfProcessInstancePlanStore(dbContext).PersistAsync(plan);
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(
            includeArtifactLedger: false,
            initialPlan: plan);

        var result = await unitOfWork.CommitAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(1, await dbContext.InstancePlans.CountAsync());
        Assert.Equal(1, await dbContext.RuntimeStates.CountAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Initial_commit_rejects_plan_identity_or_hash_mismatch_before_writes(
        bool mismatchPlanId)
    {
        await using var dbContext = CreateDbContext();
        var plan = NewInitialPlan();
        var request = NewCommitRequest(
            includeArtifactLedger: true,
            initialPlan: plan);
        var mismatchedState = request.Mutation.State with
        {
            PlanId = mismatchPlanId
                ? ProcessInstancePlanId.New()
                : request.Mutation.State.PlanId,
            PlanHash = mismatchPlanId
                ? request.Mutation.State.PlanHash
                : "sha256:different-runtime-plan"
        };
        request = request with
        {
            Mutation = request.Mutation with
            {
                State = mismatchedState
            }
        };
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.CommitAsync(request));

        Assert.Equal(0, await dbContext.InstancePlans.CountAsync());
        Assert.Equal(0, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(0, await dbContext.RuntimeEvents.CountAsync());
        Assert.Equal(0, await dbContext.OutboxMessages.CountAsync());
        Assert.Equal(0, await dbContext.ArtifactLedgerEvents.CountAsync());
        Assert.Equal(0, await dbContext.IdempotencyKeys.CountAsync());
    }

    [Fact]
    public async Task Initial_commit_rejects_conflicting_existing_plan_without_runtime_writes()
    {
        await using var dbContext = CreateDbContext();
        var persistedPlan = NewInitialPlan();
        await new EfProcessInstancePlanStore(dbContext).PersistAsync(persistedPlan);
        var conflictingPlan = persistedPlan with
        {
            PlanHash = "sha256:conflicting-plan"
        };
        var request = NewCommitRequest(
            includeArtifactLedger: false,
            initialPlan: conflictingPlan);
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.CommitAsync(request));

        Assert.Equal(1, await dbContext.InstancePlans.CountAsync());
        Assert.Equal(persistedPlan.PlanHash, (await dbContext.InstancePlans.SingleAsync()).PlanHash);
        Assert.Equal(0, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(0, await dbContext.RuntimeEvents.CountAsync());
        Assert.Equal(0, await dbContext.OutboxMessages.CountAsync());
        Assert.Equal(0, await dbContext.IdempotencyKeys.CountAsync());
    }

    [Fact]
    public async Task Commit_round_trips_strategy_result_diagnostics_and_artifact_lineage()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: true);
        var stepId = request.Mutation.State.AppliedResults.Single().StepInstanceId;
        var idempotencyKey = StrategyResultIdempotencyKey.New();
        var relatedChildRunId = ProcessRunId.New();
        var executionSafetyAttestation =
            ProcessExecutionSafetyAttestation.FailedBeforeRecordedSideEffects(
                new ProcessExecutionRunId(
                    new Guid("ebc4e0ee-451d-4ed9-b703-8252c2fb0540")),
                request.Mutation.State.RunId,
                stepId,
                new ProcessExecutionExecutorId(
                    new Guid("e9ebf32f-47cf-4d61-9020-17e171dcbe7b")),
                "sha256:" + new string('c', 64));
        var receipt = new StrategyResultReceipt(
            stepId,
            new StrategyId("strategy.test"),
            idempotencyKey,
            StrategyOutcome.NeedsManager,
            ProcessRuntimeStepStatus.Blocked,
            "hash:blocked-result",
            [
                new StrategyResultDiagnosticReceipt(
                    "process.runtime.test_blocked",
                    StrategyDiagnosticSensitivity.Normal,
                    "hash:diagnostic",
                    "Unit test blocked.",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
                {
                    RelatedChildRunId = relatedChildRunId,
                    ExecutionSafetyAttestation = executionSafetyAttestation
                }
            ],
            [
                new StrategyResultArtifactReceipt(
                    RequiredArtifactSlotId,
                    new ArtifactInstanceId(new Guid("9facff93-8f8b-4736-921e-916de95df35f")),
                    "hash:artifact")
            ],
            new ProcessRecoveryDecisionReceipt(
                ProcessFailureCategory.MissingArtifact,
                ProcessRecoveryDecisionKind.ManagerRequired,
                "process.runtime.test_blocked",
                "unit-test-policy",
                "Unit test recovery decision.")
            {
                RouteKind = ProcessRecoveryRouteKind.UpstreamStepRework,
                ResponsibleStepInstanceId = stepId,
                RelatedChildRunId = relatedChildRunId
            })
        {
            UserSafeSummary = "Persisted runtime recovery summary.",
            ExecutionRunId = executionSafetyAttestation.ExecutionRunId
        };
        var state = request.Mutation.State with
        {
            AppliedResults = [receipt]
        };
        var mutation = request.Mutation with
        {
            State = state
        };

        await unitOfWork.CommitAsync(request with { Mutation = mutation });

        var persistedReceipt = await dbContext.StrategyResultReceipts.SingleAsync();
        Assert.Equal("Persisted runtime recovery summary.", persistedReceipt.UserSafeSummary);
        using var diagnosticsDocument = JsonDocument.Parse(persistedReceipt.DiagnosticsJson);
        Assert.Equal(JsonValueKind.Array, diagnosticsDocument.RootElement.ValueKind);
        Assert.Equal(
            relatedChildRunId.Value,
            diagnosticsDocument.RootElement[0].GetProperty("relatedChildRunId").GetGuid());
        Assert.Equal(
            executionSafetyAttestation.ExecutionRunId.Value,
            diagnosticsDocument.RootElement[0].GetProperty("resultExecutionRunId").GetGuid());
        var persistedAttestation = diagnosticsDocument.RootElement[0]
            .GetProperty("executionSafetyAttestation");
        Assert.Equal(
            ProcessExecutionSafetyAttestor.AgentFrameworkExecutionLedger.ToString(),
            persistedAttestation.GetProperty("attestor").GetString());
        Assert.Equal(
            executionSafetyAttestation.DurableEvidenceDigest,
            persistedAttestation.GetProperty("durableEvidenceDigest").GetString());
        Assert.Equal(
            executionSafetyAttestation.EvidenceHash,
            persistedAttestation.GetProperty("evidenceHash").GetString());
        using var recoveryDecisionDocument = JsonDocument.Parse(persistedReceipt.RecoveryDecisionJson!);
        Assert.Equal(
            relatedChildRunId.Value,
            recoveryDecisionDocument.RootElement.GetProperty("relatedChildRunId").GetGuid());

        var loaded = await unitOfWork.LoadAsync(state.RunId);
        Assert.NotNull(loaded);
        var loadedReceipt = Assert.Single(loaded.AppliedResults);
        Assert.Equal("Persisted runtime recovery summary.", loadedReceipt.UserSafeSummary);
        var loadedDiagnostic = Assert.Single(loadedReceipt.Diagnostics);
        Assert.Equal("process.runtime.test_blocked", loadedDiagnostic.Code);
        Assert.Equal(relatedChildRunId, loadedDiagnostic.RelatedChildRunId);
        Assert.Equal(executionSafetyAttestation, loadedDiagnostic.ExecutionSafetyAttestation);
        Assert.Equal(executionSafetyAttestation.ExecutionRunId, loadedReceipt.ExecutionRunId);
        Assert.Equal(RequiredArtifactSlotId, Assert.Single(loadedReceipt.ProducedArtifacts).SlotId);
        Assert.NotNull(loadedReceipt.RecoveryDecision);
        Assert.Equal(ProcessFailureCategory.MissingArtifact, loadedReceipt.RecoveryDecision.FailureCategory);
        Assert.Equal(ProcessRecoveryRouteKind.UpstreamStepRework, loadedReceipt.RecoveryDecision.RouteKind);
        Assert.Equal(stepId, loadedReceipt.RecoveryDecision.ResponsibleStepInstanceId);
        Assert.Equal(relatedChildRunId, loadedReceipt.RecoveryDecision.RelatedChildRunId);
    }

    [Fact]
    public async Task Commit_round_trips_applied_sequence_and_blocked_recovery_actions_exactly()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: false);
        var sourceReceipt = Assert.Single(request.Mutation.State.AppliedResults) with
        {
            AppliedSequence = 7
        };
        var sourceStepId = sourceReceipt.StepInstanceId;
        var relatedChildRunId = ProcessRunId.New();
        var relatedChildUpdatedAtUtc = Now.AddMinutes(-1);
        var recoveryAction = new ProcessRuntimeBlockedRecoveryActionReceipt(
            sourceReceipt.IdempotencyKey,
            sourceStepId,
            sourceStepId,
            "sha256:missing-summary",
            ProcessRecoveryRouteKind.ChildRunPropagation,
            ProcessRuntimeBlockedRecoveryPhase.CompletedChildConsumer,
            Now)
        {
            RelatedChildRunId = relatedChildRunId,
            RelatedChildUpdatedAtUtc = relatedChildUpdatedAtUtc
        };
        var state = request.Mutation.State with
        {
            AppliedResults = [sourceReceipt],
            BlockedRecoveryActions = [recoveryAction]
        };

        await unitOfWork.CommitAsync(request with
        {
            Mutation = request.Mutation with
            {
                State = state
            }
        });
        dbContext.ChangeTracker.Clear();

        var loaded = await unitOfWork.LoadAsync(state.RunId);

        Assert.NotNull(loaded);
        Assert.Equal(7, Assert.Single(loaded.AppliedResults).AppliedSequence);
        Assert.Equal(recoveryAction, Assert.Single(loaded.BlockedRecoveryActions));
        var persistedState = await dbContext.RuntimeStates.SingleAsync();
        using var actionDocument = JsonDocument.Parse(persistedState.BlockedRecoveryActionsJson);
        var persistedAction = actionDocument.RootElement[0];
        Assert.Equal(
            relatedChildRunId.Value,
            persistedAction.GetProperty("relatedChildRunId").GetGuid());
        Assert.Equal(
            relatedChildUpdatedAtUtc,
            persistedAction.GetProperty("relatedChildUpdatedAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task Commit_rejects_invalid_completed_child_recovery_action_before_writing_state()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: false);
        var sourceReceipt = Assert.Single(request.Mutation.State.AppliedResults);
        var invalidAction = new ProcessRuntimeBlockedRecoveryActionReceipt(
            sourceReceipt.IdempotencyKey,
            sourceReceipt.StepInstanceId,
            sourceReceipt.StepInstanceId,
            "sha256:invalid-child-recovery",
            ProcessRecoveryRouteKind.ChildRunPropagation,
            ProcessRuntimeBlockedRecoveryPhase.CompletedChildConsumer,
            Now);
        var state = request.Mutation.State with
        {
            BlockedRecoveryActions = [invalidAction]
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.CommitAsync(request with
            {
                Mutation = request.Mutation with
                {
                    State = state
                }
            }));

        Assert.Contains("invalid entry", exception.Message, StringComparison.Ordinal);
        Assert.Empty(dbContext.RuntimeStates);
    }

    [Fact]
    public async Task Load_orders_receipts_by_applied_sequence_despite_reversed_persisted_order_and_nonchronological_keys()
    {
        var databaseName = $"process-persistence-restart-{Guid.NewGuid():N}";
        var databaseRoot = new InMemoryDatabaseRoot();
        var request = NewCommitRequest(includeArtifactLedger: false);
        var stepId = Assert.Single(request.Mutation.State.Steps).StepInstanceId;
        var laterReceiptKey = new StrategyResultIdempotencyKey(
            new Guid("00000000-0000-0000-0000-000000000002"));
        var earlierReceiptKey = new StrategyResultIdempotencyKey(
            new Guid("ffffffff-ffff-ffff-ffff-fffffffffff1"));
        var laterReceipt = NewResultReceipt(stepId, laterReceiptKey, appliedSequence: 2);
        var state = request.Mutation.State with
        {
            AppliedResults = [laterReceipt]
        };

        await using (var writeContext = CreateDbContext(databaseName, databaseRoot))
        {
            var writeUnitOfWork = new EfProcessRuntimeUnitOfWork(writeContext);
            await writeUnitOfWork.CommitAsync(request with
            {
                Mutation = request.Mutation with
                {
                    State = state
                }
            });

            writeContext.StrategyResultReceipts.Add(new ProcessStrategyResultReceiptEntity
            {
                RunId = state.RunId.Value,
                StepInstanceId = stepId.Value,
                StrategyId = "strategy.test",
                IdempotencyKey = earlierReceiptKey.Value,
                Outcome = StrategyOutcome.Succeeded.ToString(),
                AppliedStepStatus = ProcessRuntimeStepStatus.Completed,
                ResultHash = "hash:result-1",
                AppliedSequence = 1,
                DiagnosticsJson = "[]",
                ProducedArtifactsJson = "[]"
            });
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = CreateDbContext(databaseName, databaseRoot);
        var readUnitOfWork = new EfProcessRuntimeUnitOfWork(readContext);
        var loaded = await readUnitOfWork.LoadAsync(state.RunId);

        Assert.NotNull(loaded);
        Assert.Equal(
            [1L, 2L],
            loaded.AppliedResults.Select(receipt => receipt.AppliedSequence));
        Assert.Equal(
            [earlierReceiptKey, laterReceiptKey],
            loaded.AppliedResults.Select(receipt => receipt.IdempotencyKey));
    }

    [Fact]
    public async Task Load_rejects_malformed_blocked_recovery_action_ledger()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: false);

        await unitOfWork.CommitAsync(request);

        var persistedState = await dbContext.RuntimeStates.SingleAsync();
        persistedState.BlockedRecoveryActionsJson =
            $$"""
            [
              {
                "sourceResultIdempotencyKey": "{{Guid.NewGuid():D}}",
                "sourceBlockedStepInstanceId": "{{Guid.NewGuid():D}}",
                "targetStepInstanceId": "{{Guid.NewGuid():D}}",
                "diagnosticFingerprint": "sha256:invalid-route",
                "recoveryRouteKind": "None",
                "phase": "CurrentStep",
                "appliedAtUtc": "{{Now:O}}"
              }
            ]
            """;
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.LoadAsync(request.Mutation.State.RunId));

        Assert.Contains("invalid entry", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Load_rejects_nonpositive_persisted_receipt_sequence()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: false);

        await unitOfWork.CommitAsync(request);

        var persistedReceipt = await dbContext.StrategyResultReceipts.SingleAsync();
        persistedReceipt.AppliedSequence = 0;
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.LoadAsync(request.Mutation.State.RunId));

        Assert.Contains("positive and unique", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Load_accepts_legacy_diagnostics_array_without_user_safe_summary()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: false);

        await unitOfWork.CommitAsync(request);

        var persistedReceipt = await dbContext.StrategyResultReceipts.SingleAsync();
        Assert.Null(persistedReceipt.UserSafeSummary);
        persistedReceipt.DiagnosticsJson =
            """
            [
              {
                "code": "process.runtime.legacy_diagnostic",
                "sensitivity": "Normal",
                "evidenceHash": "hash:legacy-diagnostic",
                "safeSummary": "Legacy diagnostic summary.",
                "retrySafety": "UnsafeToRetry",
                "idempotency": "Idempotent"
              }
            ]
            """;
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var loaded = await unitOfWork.LoadAsync(request.Mutation.State.RunId);

        Assert.NotNull(loaded);
        var loadedReceipt = Assert.Single(loaded.AppliedResults);
        Assert.Equal(string.Empty, loadedReceipt.UserSafeSummary);
        Assert.Equal("process.runtime.legacy_diagnostic", Assert.Single(loadedReceipt.Diagnostics).Code);
    }

    [Fact]
    public async Task Load_fails_closed_on_partial_execution_safety_attestation()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: false);

        await unitOfWork.CommitAsync(request);

        var persistedReceipt = await dbContext.StrategyResultReceipts.SingleAsync();
        persistedReceipt.DiagnosticsJson =
            """
            [
              {
                "code": "process.adapter.agent_transient_execution_before_side_effects",
                "sensitivity": "Normal",
                "evidenceHash": "sha256:stable-diagnostic",
                "safeSummary": "Durable execution detail proved no recorded side effects.",
                "retrySafety": "SafeToRetry",
                "idempotency": "Idempotent",
                "executionSafetyAttestation": {
                  "kind": "FailedBeforeRecordedSideEffects",
                  "attestor": "AgentFrameworkExecutionLedger",
                  "schemaVersion": 1,
                  "executionRunId": "ebc4e0ee-451d-4ed9-b703-8252c2fb0540"
                }
              }
            ]
            """;
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var loaded = await unitOfWork.LoadAsync(request.Mutation.State.RunId);

        Assert.NotNull(loaded);
        var diagnostic = Assert.Single(Assert.Single(loaded.AppliedResults).Diagnostics);
        Assert.Null(diagnostic.ExecutionSafetyAttestation);
    }

    [Fact]
    public async Task Commit_round_trips_generic_artifact_payload_schema_metadata()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: false);
        var step = Assert.Single(request.Mutation.State.Steps);
        var descriptor = new ProcessArtifactSlotDescriptor(
            RequiredArtifactSlotId,
            "architecture:solution-context",
            "architecture",
            "solution-context",
            "Solution context",
            "Decision",
            "artifacts/process-runs/test/steps/architecture.md",
            ProcessArtifactMaterializationMode.AgentWritten)
        {
            PayloadSchema = "example.solution-context/v1"
        };
        var state = request.Mutation.State with
        {
            Steps =
            [
                step with
                {
                    ArtifactDescriptors = [descriptor]
                }
            ]
        };
        var mutation = request.Mutation with
        {
            State = state
        };

        await unitOfWork.CommitAsync(request with { Mutation = mutation });

        var loaded = await unitOfWork.LoadAsync(state.RunId);

        Assert.NotNull(loaded);
        var loadedDescriptor = Assert.Single(Assert.Single(loaded.Steps).ArtifactDescriptors);
        Assert.Equal("example.solution-context/v1", loadedDescriptor.PayloadSchema);
    }

    [Fact]
    public async Task Commit_rejects_broken_event_outbox_atomicity_before_writing_rows()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: false);
        var brokenOutbox = new ProcessOutboxMessage(
            RuntimeOutboxMessageId.New(),
            RuntimeEventId.New(),
            ProcessOutboxSubscriberKind.RuntimeProjection,
            "hash:broken");
        var brokenMutation = request.Mutation with
        {
            OutboxMessages = [brokenOutbox]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.CommitAsync(request with { Mutation = brokenMutation }));

        Assert.Equal(0, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(0, await dbContext.RuntimeEvents.CountAsync());
        Assert.Equal(0, await dbContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Duplicate_command_returns_existing_result_without_second_event_append()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: false);

        await unitOfWork.CommitAsync(request);
        var duplicate = await unitOfWork.CommitAsync(request);

        Assert.Equal(ProcessRuntimeTransitionOutcome.Applied, duplicate.Outcome);
        Assert.Equal(1, await dbContext.RuntimeEvents.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        Assert.Equal(1, await dbContext.IdempotencyKeys.CountAsync());
    }

    [Fact]
    public async Task Stale_original_state_rejects_commit_without_overwriting_current_state()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var initial = NewCommitRequest(includeArtifactLedger: false);

        await unitOfWork.CommitAsync(initial);
        var original = await unitOfWork.LoadAsync(initial.Mutation.State.RunId);
        Assert.NotNull(original);

        var concurrent = NewCommitRequest(
            includeArtifactLedger: false,
            runId: initial.Mutation.State.RunId,
            rootRunId: initial.Mutation.State.RootRunId,
            commandId: RuntimeCommandId.New(),
            eventType: ProcessRuntimeEventTypes.StepCompleted,
            updatedAtUtc: Now.AddMinutes(1));
        await unitOfWork.CommitAsync(ContinueFrom(concurrent, original));

        var stale = NewCommitRequest(
            includeArtifactLedger: false,
            runId: initial.Mutation.State.RunId,
            rootRunId: initial.Mutation.State.RootRunId,
            commandId: RuntimeCommandId.New(),
            eventType: ProcessRuntimeEventTypes.StepFailed,
            updatedAtUtc: Now.AddMinutes(2));

        await Assert.ThrowsAsync<ProcessRuntimeOptimisticConcurrencyException>(() =>
            unitOfWork.CommitAsync(ContinueFrom(stale, original)));

        Assert.Equal(2, await dbContext.RuntimeEvents.CountAsync());
        var current = await unitOfWork.LoadAsync(initial.Mutation.State.RunId);
        Assert.NotNull(current);
        Assert.Equal(Now.AddMinutes(1), current.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(ProcessRuntimeStatus.CancelRequested)]
    [InlineData(ProcessRuntimeStatus.Cancelled)]
    public async Task Commit_rejects_child_creation_beneath_stopping_or_cancelled_root_without_writes(
        ProcessRuntimeStatus rootStatus)
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(
            dbContext,
            new FixedTimeProvider(Now));
        var rootRunId = ProcessRunId.New();
        var parentStepId = ProcessStepInstanceId.New();
        var rootState = NewParentState(
            rootRunId,
            parentStepId,
            rootStatus,
            ProcessRuntimeStepStatus.Cancelled,
            hasActiveClaim: false);
        var rootRequestTemplate = NewCommitRequest(
            includeArtifactLedger: false,
            runId: rootRunId,
            rootRunId: rootRunId,
            eventType: rootStatus == ProcessRuntimeStatus.CancelRequested
                ? ProcessRuntimeEventTypes.ProcessRunCancelRequested
                : ProcessRuntimeEventTypes.ProcessRunCancelled);
        var rootRequest = rootRequestTemplate with
        {
            OriginalState = rootState,
            Mutation = rootRequestTemplate.Mutation with
            {
                State = rootState
            }
        };
        await unitOfWork.CommitAsync(rootRequest);

        var childRunId = ProcessRunId.New();
        var childPlan = NewInitialPlan();
        var childRequest = NewCommitRequest(
            includeArtifactLedger: false,
            runId: childRunId,
            rootRunId: rootRunId,
            initialPlan: childPlan);
        var childAssignment = Assert.Single(childRequest.InitialAssignments!) with
        {
            LaunchVariables = ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                rootRunId,
                parentStepId)
        };
        childRequest = childRequest with
        {
            ParentStepPrecondition = new ProcessRuntimeParentStepReference(rootRunId, parentStepId),
            InitialAssignments = [childAssignment]
        };

        var result = await unitOfWork.CommitAsync(childRequest);

        Assert.Equal(ProcessRuntimeTransitionOutcome.Rejected, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "Runtime.ParentRootNotLaunchable");
        Assert.Equal(1, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(1, await dbContext.RuntimeEvents.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        Assert.Equal(1, await dbContext.IdempotencyKeys.CountAsync());
        Assert.Equal(0, await dbContext.InstancePlans.CountAsync());
        Assert.Null(await unitOfWork.LoadAsync(childRunId));
    }

    [Fact]
    public async Task Commit_rejects_new_descendant_without_typed_parent_step_precondition_without_writes()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(
            dbContext,
            new FixedTimeProvider(Now));
        var rootRunId = ProcessRunId.New();
        var parentStepId = ProcessStepInstanceId.New();
        var rootState = NewParentState(
            rootRunId,
            parentStepId,
            ProcessRuntimeStatus.Active,
            ProcessRuntimeStepStatus.Running,
            hasActiveClaim: true);
        var rootRequestTemplate = NewCommitRequest(
            includeArtifactLedger: false,
            runId: rootRunId,
            rootRunId: rootRunId,
            eventType: ProcessRuntimeEventTypes.ProcessRunActivated);
        await unitOfWork.CommitAsync(rootRequestTemplate with
        {
            OriginalState = rootState,
            Mutation = rootRequestTemplate.Mutation with
            {
                State = rootState
            }
        });

        var childRunId = ProcessRunId.New();
        var childPlan = NewInitialPlan();
        var childRequest = NewCommitRequest(
            includeArtifactLedger: false,
            runId: childRunId,
            rootRunId: rootRunId,
            initialPlan: childPlan);

        var result = await unitOfWork.CommitAsync(childRequest);

        Assert.Equal(ProcessRuntimeTransitionOutcome.Rejected, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "Runtime.ParentStepPreconditionRequired");
        Assert.Equal(1, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(1, await dbContext.RuntimeEvents.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        Assert.Equal(1, await dbContext.IdempotencyKeys.CountAsync());
        Assert.Equal(0, await dbContext.InstancePlans.CountAsync());
        Assert.Null(await unitOfWork.LoadAsync(childRunId));
    }

    [Fact]
    public async Task Commit_rejects_child_creation_when_parent_step_has_no_active_running_claim()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(
            dbContext,
            new FixedTimeProvider(Now));
        var rootRunId = ProcessRunId.New();
        var parentStepId = ProcessStepInstanceId.New();
        var rootState = NewParentState(
            rootRunId,
            parentStepId,
            ProcessRuntimeStatus.Active,
            ProcessRuntimeStepStatus.Ready,
            hasActiveClaim: false);
        var rootRequestTemplate = NewCommitRequest(
            includeArtifactLedger: false,
            runId: rootRunId,
            rootRunId: rootRunId,
            eventType: ProcessRuntimeEventTypes.ProcessRunActivated);
        await unitOfWork.CommitAsync(rootRequestTemplate with
        {
            OriginalState = rootState,
            Mutation = rootRequestTemplate.Mutation with
            {
                State = rootState
            }
        });

        var childRunId = ProcessRunId.New();
        var childPlan = NewInitialPlan();
        var childRequest = NewCommitRequest(
            includeArtifactLedger: false,
            runId: childRunId,
            rootRunId: rootRunId,
            initialPlan: childPlan);
        var childAssignment = Assert.Single(childRequest.InitialAssignments!) with
        {
            LaunchVariables = ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                rootRunId,
                parentStepId)
        };
        childRequest = childRequest with
        {
            ParentStepPrecondition = new ProcessRuntimeParentStepReference(rootRunId, parentStepId),
            InitialAssignments = [childAssignment]
        };

        var result = await unitOfWork.CommitAsync(childRequest);

        Assert.Equal(ProcessRuntimeTransitionOutcome.Rejected, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "Runtime.ParentStepNotRunning");
        Assert.Equal(1, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(0, await dbContext.InstancePlans.CountAsync());
        Assert.Null(await unitOfWork.LoadAsync(childRunId));
    }

    [Fact]
    public async Task Commit_rejects_child_creation_when_parent_claim_lease_is_expired_without_writes()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(
            dbContext,
            new FixedTimeProvider(Now));
        var rootRunId = ProcessRunId.New();
        var parentStepId = ProcessStepInstanceId.New();
        var rootState = NewParentState(
            rootRunId,
            parentStepId,
            ProcessRuntimeStatus.Active,
            ProcessRuntimeStepStatus.Running,
            hasActiveClaim: true,
            claimExpiresAtUtc: Now);
        var rootRequestTemplate = NewCommitRequest(
            includeArtifactLedger: false,
            runId: rootRunId,
            rootRunId: rootRunId,
            eventType: ProcessRuntimeEventTypes.ProcessRunActivated);
        await unitOfWork.CommitAsync(rootRequestTemplate with
        {
            OriginalState = rootState,
            Mutation = rootRequestTemplate.Mutation with
            {
                State = rootState
            }
        });

        var childRunId = ProcessRunId.New();
        var childPlan = NewInitialPlan();
        var childRequest = NewCommitRequest(
            includeArtifactLedger: false,
            runId: childRunId,
            rootRunId: rootRunId,
            initialPlan: childPlan) with
        {
            ParentStepPrecondition = new ProcessRuntimeParentStepReference(rootRunId, parentStepId)
        };

        var result = await unitOfWork.CommitAsync(childRequest);

        Assert.Equal(ProcessRuntimeTransitionOutcome.Rejected, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "Runtime.ParentStepNotRunning");
        Assert.Equal(1, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(0, await dbContext.InstancePlans.CountAsync());
        Assert.Null(await unitOfWork.LoadAsync(childRunId));
    }

    [Fact]
    public async Task Commit_rejects_child_creation_without_atomic_assignments_even_when_parent_claim_is_valid()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(
            dbContext,
            new FixedTimeProvider(Now));
        var rootRunId = ProcessRunId.New();
        var parentStepId = ProcessStepInstanceId.New();
        var rootState = NewParentState(
            rootRunId,
            parentStepId,
            ProcessRuntimeStatus.Active,
            ProcessRuntimeStepStatus.Running,
            hasActiveClaim: true);
        var rootRequestTemplate = NewCommitRequest(
            includeArtifactLedger: false,
            runId: rootRunId,
            rootRunId: rootRunId,
            eventType: ProcessRuntimeEventTypes.ProcessRunActivated);
        await unitOfWork.CommitAsync(rootRequestTemplate with
        {
            OriginalState = rootState,
            Mutation = rootRequestTemplate.Mutation with
            {
                State = rootState
            }
        });

        var childRunId = ProcessRunId.New();
        var childPlan = NewInitialPlan();
        var childRequest = NewCommitRequest(
            includeArtifactLedger: false,
            runId: childRunId,
            rootRunId: rootRunId,
            initialPlan: childPlan) with
        {
            ParentStepPrecondition = new ProcessRuntimeParentStepReference(
                rootRunId,
                parentStepId),
            InitialAssignments = null
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.CommitAsync(childRequest));

        Assert.Contains("atomically", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(0, await dbContext.InstancePlans.CountAsync());
        Assert.Equal(0, await dbContext.RuntimeStepAssignments.CountAsync());
        Assert.Null(await unitOfWork.LoadAsync(childRunId));
    }

    [Fact]
    public async Task Commit_allows_child_creation_when_parent_step_is_running_with_active_claim()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(
            dbContext,
            new FixedTimeProvider(Now));
        var rootRunId = ProcessRunId.New();
        var parentStepId = ProcessStepInstanceId.New();
        var rootState = NewParentState(
            rootRunId,
            parentStepId,
            ProcessRuntimeStatus.Active,
            ProcessRuntimeStepStatus.Running,
            hasActiveClaim: true);
        var rootRequestTemplate = NewCommitRequest(
            includeArtifactLedger: false,
            runId: rootRunId,
            rootRunId: rootRunId,
            eventType: ProcessRuntimeEventTypes.ProcessRunActivated);
        await unitOfWork.CommitAsync(rootRequestTemplate with
        {
            OriginalState = rootState,
            Mutation = rootRequestTemplate.Mutation with
            {
                State = rootState
            }
        });

        var childRunId = ProcessRunId.New();
        var childPlan = NewInitialPlan();
        var childRequest = NewCommitRequest(
            includeArtifactLedger: false,
            runId: childRunId,
            rootRunId: rootRunId,
            initialPlan: childPlan);
        var childAssignment = Assert.Single(childRequest.InitialAssignments!) with
        {
            LaunchVariables = ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                rootRunId,
                parentStepId)
        };
        childRequest = childRequest with
        {
            ParentStepPrecondition = new ProcessRuntimeParentStepReference(rootRunId, parentStepId),
            InitialAssignments = [childAssignment]
        };

        var result = await unitOfWork.CommitAsync(childRequest);

        Assert.True(result.Succeeded);
        Assert.Equal(2, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(1, await dbContext.InstancePlans.CountAsync());
        Assert.Single(await new EfProcessRuntimeStepAssignmentStore(dbContext).LoadByRunAsync(childRunId));
        Assert.NotNull(await new EfProcessInstancePlanStore(dbContext).LoadAsync(childPlan.Header.PlanId));
        Assert.NotNull(await unitOfWork.LoadAsync(childRunId));
    }

    [Fact]
    public async Task Replay_store_reads_global_and_root_sequences_in_order()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var first = NewCommitRequest(includeArtifactLedger: false);
        var second = NewCommitRequest(
            includeArtifactLedger: false,
            runId: first.Mutation.State.RunId,
            rootRunId: first.Mutation.State.RootRunId,
            commandId: RuntimeCommandId.New(),
            eventType: ProcessRuntimeEventTypes.StepCompleted);

        await unitOfWork.CommitAsync(first);
        var original = await unitOfWork.LoadAsync(first.Mutation.State.RunId);
        Assert.NotNull(original);
        await unitOfWork.CommitAsync(ContinueFrom(second, original));

        var replayStore = new EfProcessRuntimeEventStore(dbContext);
        var globalEvents = await replayStore.ReadAfterGlobalSequenceAsync(0, 10);
        var rootEvents = await replayStore.ReadByRootRunAsync(first.Mutation.State.RootRunId, 0, 10);

        Assert.Equal([1, 2], globalEvents.Select(runtimeEvent => runtimeEvent.GlobalSequence));
        Assert.Equal([1, 2], rootEvents.Select(runtimeEvent => runtimeEvent.RootSequence));
        Assert.All(globalEvents, runtimeEvent => Assert.Equal(first.Mutation.State.RootRunId, runtimeEvent.Envelope.RootRunId));
    }

    [Fact]
    public async Task Runtime_event_store_assigns_contiguous_sequences_within_append_batch()
    {
        await using var dbContext = CreateDbContext();
        var eventStore = new EfProcessRuntimeEventStore(dbContext);
        var runId = ProcessRunId.New();

        await eventStore.AppendAsync(
            [
                NewEvent(runId, runId, ProcessRuntimeEventTypes.ProcessRunActivated, Now),
                NewEvent(runId, runId, ProcessRuntimeEventTypes.StepCompleted, Now.AddSeconds(1))
            ]);

        var events = await eventStore.ReadByRootRunAsync(runId, 0, 10);

        Assert.Equal([1, 2], events.Select(runtimeEvent => runtimeEvent.GlobalSequence));
        Assert.Equal([1, 2], events.Select(runtimeEvent => runtimeEvent.RootSequence));
    }

    [Fact]
    public async Task Outbox_store_claims_retries_and_marks_delivery_explicitly()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: false);
        await unitOfWork.CommitAsync(request);

        var outboxStore = new EfProcessOutboxStore(dbContext);
        var lockId = new ProcessOutboxLockId("worker-1");
        var claimed = await outboxStore.ClaimPendingAsync(Now, 1, lockId);

        Assert.Single(claimed);
        Assert.Equal(1, claimed[0].AttemptCount);

        await outboxStore.MarkFailedAsync(claimed[0].MessageId, lockId, "Transient", Now.AddMinutes(1));
        Assert.Empty(await outboxStore.ClaimPendingAsync(Now, 1, new ProcessOutboxLockId("worker-2")));

        var retried = await outboxStore.ClaimPendingAsync(Now.AddMinutes(1), 1, new ProcessOutboxLockId("worker-2"));
        Assert.Single(retried);
        Assert.Equal(2, retried[0].AttemptCount);

        await outboxStore.MarkDeliveredAsync(retried[0].MessageId, new ProcessOutboxLockId("worker-2"), Now.AddMinutes(2));
        Assert.Empty(await outboxStore.ClaimPendingAsync(Now.AddMinutes(3), 1, new ProcessOutboxLockId("worker-3")));
    }

    [Fact]
    public async Task Projection_store_upserts_snapshots_offsets_and_dead_letters()
    {
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var projectorName = new ProcessProjectorName("runtime.live");
        var projectionKey = new ProcessProjectionKey("run:alpha");
        var shardKey = new ProcessProjectionShardKey("root:alpha");
        var deadLetter = new ProcessProjectionDeadLetter(
            ProcessProjectionDeadLetterId.New(),
            projectorName,
            shardKey,
            RuntimeEventId.New(),
            7,
            "SchemaMismatch",
            "diag-7",
            "manual-review",
            Now);

        await projectionStore.UpsertSnapshotAsync(new ProcessProjectionSnapshot(
            projectorName,
            projectionKey,
            ProcessContractVersions.RuntimeProjectionV1,
            """{"status":"Active"}""",
            "hash:projection-1",
            Now));
        await projectionStore.UpsertSnapshotAsync(new ProcessProjectionSnapshot(
            projectorName,
            projectionKey,
            ProcessContractVersions.RuntimeProjectionV1,
            """{"status":"Completed"}""",
            "hash:projection-2",
            Now.AddMinutes(1)));
        await projectionStore.SaveOffsetAsync(new ProcessProjectorOffset(projectorName, shardKey, 7, Now));
        await projectionStore.SaveOffsetAsync(new ProcessProjectorOffset(projectorName, shardKey, 5, Now.AddMinutes(1)));
        await projectionStore.WriteDeadLetterAsync(deadLetter);

        var snapshot = await projectionStore.LoadSnapshotAsync(projectorName, projectionKey);
        var offset = await projectionStore.LoadOffsetAsync(projectorName, shardKey);
        var deadLetters = await projectionStore.ReadDeadLettersAsync(projectorName, shardKey, 10);

        Assert.NotNull(snapshot);
        Assert.Equal("hash:projection-2", snapshot.PayloadHash);
        Assert.NotNull(offset);
        Assert.Equal(7, offset.GlobalSequence);
        Assert.Single(deadLetters);
        Assert.Equal(deadLetter.DeadLetterId, deadLetters[0].DeadLetterId);
    }

    [Fact]
    public async Task Instance_plan_store_round_trips_strategy_binding_for_dispatch()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessInstancePlanStore(dbContext);
        var stepId = ProcessStepInstanceId.New();
        var stepDefinitionId = ProcessStepDefinitionId.New();
        var binding = new ProcessStrategyBindingSnapshot(
            new DriverId("driver.persistence-test"),
            new StrategyId("strategy.persistence-test.execute"),
            "1.0.0",
            "factory.1.0.0",
            RuntimeSchemaVersion,
            RuntimeSchemaVersion,
            "sha256:binding",
            [new StrategyBindingInput(new StrategyBindingInputKey("operation"), "sha256:operation")]);
        var plan = NewDispatchablePlan(stepId, stepDefinitionId, binding);

        await store.PersistAsync(plan);
        var loaded = await store.LoadAsync(plan.Header.PlanId);

        Assert.NotNull(loaded);
        var loadedStep = Assert.Single(loaded.Steps);
        Assert.Equal(stepId, loadedStep.StepInstanceId);
        Assert.NotNull(loadedStep.ExecutionStrategyBinding);
        Assert.Equal(binding.StrategyId, loadedStep.ExecutionStrategyBinding.StrategyId);
        var state = new ProcessRuntimeStateSnapshot(
            ProcessRunId.New(),
            ProcessRunId.New(),
            loaded.Header.PlanId,
            loaded.PlanHash,
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    stepId,
                    stepDefinitionId,
                    ProcessRuntimeStepStatus.Ready,
                    true,
                    0,
                    new HashSet<ProcessStepInstanceId>(),
                    new HashSet<ArtifactSlotId>(),
                    null,
                    null)
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now);

        var readyWork = new ProcessRuntimeScheduler().CalculateReadyWork(state, loaded, Now);

        var workItem = Assert.Single(readyWork);
        Assert.Equal(stepId, workItem.StepInstanceId);
        Assert.Equal(binding.StrategyId, workItem.StrategyBinding.StrategyId);
    }

    [Fact]
    public async Task Runtime_step_assignment_store_round_trips_launch_variables_for_execution_metadata()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRuntimeStepAssignmentStore(dbContext);
        var runId = ProcessRunId.New();
        var stepId = ProcessStepInstanceId.New();
        var producedSlotId = ArtifactSlotId.New();
        var requiredSlotId = ArtifactSlotId.New();
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var assignment = new ProcessRuntimeStepAssignment(
            runId,
            ProcessInstancePlanId.New(),
            stepId,
            "implement-blazor-change",
            "blazor-engineer",
            "lead-engineer",
            "Blazor engineer",
            ProcessLaunchExecutorKinds.Workflow,
            Guid.NewGuid().ToString("D"),
            "Blazor engineer",
            "Execute the step.",
            "sha256:readiness",
            "Resolved from test.",
            [producedSlotId],
            [requiredSlotId],
            [ProcessOperationContractNames.MutateProductTarget, ProcessOperationContractNames.ReadProjectStructure],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ProjectId"] = Guid.NewGuid().ToString("D"),
                ["RepositoryRoot"] = @"C:\programovani\dotnet\output"
            },
            new ProcessRuntimeBranchGate("validate-blazor-runtime", "repair-required"),
            Now)
        {
            WorkflowBinding = new ProcessWorkflowExecutorBinding(
                new ProcessWorkflowId(workflowId),
                new ProcessWorkflowVersionId(workflowVersionId),
                ProcessWorkflowOutputMappingKind.ProcessStepOutcome),
            CapabilityScope = new ProcessCapabilityScope
            {
                Directives =
                [
                    new ProcessCapabilityScopeDirective
                    {
                        Kind = ProcessCapabilityScopeDirectiveKind.AllowOnly,
                        Target = new ProcessCapabilityScopeTarget
                        {
                            Kind = ProcessCapabilityScopeTargetKind.RuntimeToolProviderKey,
                            Value = "management.provider"
                        },
                        Reason = "Management-only step."
                    }
                ],
                InstructionFragments =
                [
                    new ProcessScopedInstructionFragment
                    {
                        Key = "management-only",
                        Title = "Management-only scope",
                        Content = "Do not implement product changes."
                    }
                ]
            }
        };

        await SeedAssignmentsAsync(dbContext, assignment);
        var persisted = await dbContext.RuntimeStepAssignments.SingleAsync(entity =>
            entity.RunId == runId.Value && entity.StepInstanceId == stepId.Value);
        Assert.Equal((int)ProcessWorkflowOutputMappingKind.ProcessStepOutcome, persisted.WorkflowOutputMapping);
        var loaded = await store.LoadAsync(runId, stepId);

        Assert.NotNull(loaded);
        Assert.Equal("blazor-engineer", loaded.RoleKey);
        Assert.Equal("lead-engineer", loaded.RoleResourceKey);
        Assert.Equal("Blazor engineer", loaded.RoleDisplayName);
        Assert.Equal(ProcessOperationContractNames.ExternalProductTargetMutable, loaded.OperationTargetScope);
        Assert.Contains(ProcessOperationContractNames.MutateProductTarget, loaded.AllowedOperations);
        Assert.True(loaded.LaunchVariables.TryGetValue("RepositoryRoot", out var repositoryRoot));
        Assert.Equal(@"C:\programovani\dotnet\output", repositoryRoot);
        Assert.Equal("repair-required", loaded.BranchGate?.RequiredOutcomeKey);
        Assert.Equal(workflowId, loaded.WorkflowBinding?.WorkflowId.Value);
        Assert.Equal(workflowVersionId, loaded.WorkflowBinding?.WorkflowVersionId?.Value);
        Assert.Equal(ProcessWorkflowOutputMappingKind.ProcessStepOutcome, loaded.WorkflowBinding?.OutputMapping);
        var directive = Assert.Single(loaded.CapabilityScope.Directives);
        Assert.Equal(ProcessCapabilityScopeDirectiveKind.AllowOnly, directive.Kind);
        Assert.Equal(ProcessCapabilityScopeTargetKind.RuntimeToolProviderKey, directive.Target.Kind);
        Assert.Equal("management.provider", directive.Target.Value);
        Assert.Equal("Do not implement product changes.", Assert.Single(loaded.CapabilityScope.InstructionFragments).Content);
    }

    [Fact]
    public async Task Runtime_step_assignment_store_finds_launch_variables_by_key_value_pairs()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRuntimeStepAssignmentStore(dbContext);
        var targetRunId = ProcessRunId.New();
        var decoyRunId = ProcessRunId.New();
        var target = NewAssignment(
            targetRunId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ProjectId"] = "project-1",
                ["ParentProcessRunId"] = "parent-1",
                ["Optional"] = string.Empty
            });
        var decoy = NewAssignment(
            decoyRunId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ProjectId"] = "project-1",
                ["Other"] = "parent-1",
                ["Optional"] = string.Empty
            },
            Now.AddSeconds(1));

        await SeedAssignmentsAsync(dbContext, target, decoy);
        var found = await store.FindByLaunchVariablesAsync(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ProjectId"] = "project-1",
                ["ParentProcessRunId"] = "parent-1",
                ["Optional"] = string.Empty
            });

        var assignment = Assert.Single(found);
        Assert.Equal(targetRunId, assignment.RunId);
    }

    [Fact]
    public async Task Runtime_step_assignment_store_bounded_search_detects_distinct_run_overflow()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRuntimeStepAssignmentStore(dbContext);
        var parentVariables = ProcessRuntimeLaunchVariables.CreateParentStepLookup(
            ProcessRunId.New(),
            ProcessStepInstanceId.New());
        var assignments = Enumerable
            .Range(0, 3)
            .Select(index => NewAssignment(
                ProcessRunId.New(),
                parentVariables,
                Now.AddSeconds(index)))
            .ToArray();
        await SeedAssignmentsAsync(dbContext, assignments);

        var exceeded = await store.FindByLaunchVariablesBoundedAsync(
            parentVariables,
            maximumDistinctRunCount: 2);
        var withinLimit = await store.FindByLaunchVariablesBoundedAsync(
            parentVariables,
            maximumDistinctRunCount: 3);

        Assert.True(exceeded.LimitExceeded);
        Assert.Empty(exceeded.Assignments);
        Assert.False(withinLimit.LimitExceeded);
        Assert.Equal(
            assignments.Select(assignment => assignment.RunId).OrderBy(runId => runId.Value),
            withinLimit.Assignments.Select(assignment => assignment.RunId).OrderBy(runId => runId.Value));
    }

    [Fact]
    public async Task Initial_runtime_commit_rejects_missing_assignments_without_tracker_leak()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var plan = NewInitialPlan();
        var request = NewCommitRequest(
            includeArtifactLedger: false,
            initialPlan: plan) with
        {
            InitialAssignments = null
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.CommitAsync(request));

        Assert.Contains("atomically", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(dbContext.ChangeTracker.Entries());
        await dbContext.SaveChangesAsync();
        Assert.Equal(0, await dbContext.InstancePlans.CountAsync());
        Assert.Equal(0, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(0, await dbContext.RuntimeStepAssignments.CountAsync());
    }

    [Fact]
    public async Task Initial_runtime_commit_rejects_state_step_definition_mismatch_without_writes()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var plan = NewInitialPlan();
        var request = NewCommitRequest(
            includeArtifactLedger: false,
            initialPlan: plan);
        var stateStep = Assert.Single(request.OriginalState.Steps);
        var malformedState = request.OriginalState with
        {
            Steps =
            [
                stateStep with
                {
                    StepDefinitionId = ProcessStepDefinitionId.New()
                }
            ]
        };
        request = request with
        {
            OriginalState = malformedState,
            Mutation = request.Mutation with
            {
                State = malformedState
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.CommitAsync(request));

        Assert.Contains("map exactly", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(dbContext.ChangeTracker.Entries());
        Assert.Equal(0, await dbContext.InstancePlans.CountAsync());
        Assert.Equal(0, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(0, await dbContext.RuntimeStepAssignments.CountAsync());
    }

    [Fact]
    public async Task Initial_runtime_commit_persists_assignments_atomically_and_store_rejects_late_insertion()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var plan = NewInitialPlan();
        var request = NewCommitRequest(
            includeArtifactLedger: false,
            initialPlan: plan);
        var step = Assert.Single(request.Mutation.State.Steps);
        var assignment = NewAssignment(
            request.Mutation.State.RunId,
            new Dictionary<string, string>(StringComparer.Ordinal))
            with
            {
                PlanId = request.Mutation.State.PlanId,
                StepInstanceId = step.StepInstanceId,
                StepKey = Assert.Single(plan.Steps).StepKey
            };

        var committed = await unitOfWork.CommitAsync(request with
        {
            InitialAssignments = [assignment]
        });

        Assert.True(committed.Succeeded);
        var persisted = await dbContext.RuntimeStepAssignments
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(assignment.RunId.Value, persisted.RunId);
        Assert.Equal(assignment.StepInstanceId.Value, persisted.StepInstanceId);

        var assignmentStore = new EfProcessRuntimeStepAssignmentStore(dbContext);
        var lateAssignment = assignment with
        {
            StepInstanceId = ProcessStepInstanceId.New(),
            StepKey = "late-step"
        };
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await assignmentStore.SaveAsync([lateAssignment]));

        Assert.Contains(
            "atomically",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Single(dbContext.RuntimeStepAssignments);
    }

    [Fact]
    public void PostgreSql_blocked_recovery_child_link_query_translates()
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=test;Password=test")
            .Options;
        using var dbContext = new ProcessPersistenceDbContext(options);
        var parentRunId = ProcessRunId.New();
        var parentStepId = ProcessStepInstanceId.New();
        var sql = BlockedRecoveryChildLineageQuery
            .Compose(dbContext.RuntimeStepAssignments, parentRunId, parentStepId)
            .ToQueryString();

        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MAX", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AssignmentImmutableMutation.ParentRunChanged)]
    [InlineData(AssignmentImmutableMutation.ParentRunRemoved)]
    [InlineData(AssignmentImmutableMutation.ParentRunAdded)]
    [InlineData(AssignmentImmutableMutation.ParentStepChanged)]
    [InlineData(AssignmentImmutableMutation.ParentStepRemoved)]
    [InlineData(AssignmentImmutableMutation.ParentStepAdded)]
    [InlineData(AssignmentImmutableMutation.PlanChanged)]
    [InlineData(AssignmentImmutableMutation.StepKeyChanged)]
    [InlineData(AssignmentImmutableMutation.CreatedAtChanged)]
    public async Task Runtime_step_assignment_store_rejects_immutable_lineage_mutation(
        AssignmentImmutableMutation mutation)
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRuntimeStepAssignmentStore(dbContext);
        var parentRunId = ProcessRunId.New();
        var parentStepId = ProcessStepInstanceId.New();
        var initialVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.ToString(),
            [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStepId.ToString(),
            ["Mutable"] = "before"
        };
        var nextVariables = new Dictionary<string, string>(
            initialVariables,
            StringComparer.Ordinal);
        var nextPlanId = ProcessInstancePlanId.New();
        var nextStepKey = "test-step";
        var nextCreatedAtUtc = Now;
        switch (mutation)
        {
            case AssignmentImmutableMutation.ParentRunChanged:
                nextVariables[ProcessRuntimeLaunchVariables.ParentProcessRunId] =
                    ProcessRunId.New().ToString();
                break;
            case AssignmentImmutableMutation.ParentRunRemoved:
                nextVariables.Remove(ProcessRuntimeLaunchVariables.ParentProcessRunId);
                break;
            case AssignmentImmutableMutation.ParentRunAdded:
                initialVariables.Remove(ProcessRuntimeLaunchVariables.ParentProcessRunId);
                break;
            case AssignmentImmutableMutation.ParentStepChanged:
                nextVariables[ProcessRuntimeLaunchVariables.ParentProcessStepId] =
                    ProcessStepInstanceId.New().ToString();
                break;
            case AssignmentImmutableMutation.ParentStepRemoved:
                nextVariables.Remove(ProcessRuntimeLaunchVariables.ParentProcessStepId);
                break;
            case AssignmentImmutableMutation.ParentStepAdded:
                initialVariables.Remove(ProcessRuntimeLaunchVariables.ParentProcessStepId);
                break;
            case AssignmentImmutableMutation.PlanChanged:
                nextPlanId = ProcessInstancePlanId.New();
                break;
            case AssignmentImmutableMutation.StepKeyChanged:
                nextStepKey = "changed-step";
                break;
            case AssignmentImmutableMutation.CreatedAtChanged:
                nextCreatedAtUtc = Now.AddSeconds(1);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unsupported assignment immutability mutation.");
        }

        var initial = NewAssignment(
            ProcessRunId.New(),
            initialVariables,
            Now);
        if (mutation != AssignmentImmutableMutation.PlanChanged)
        {
            nextPlanId = initial.PlanId;
        }

        var updated = initial with
        {
            PlanId = nextPlanId,
            StepKey = nextStepKey,
            LaunchVariables = nextVariables,
            CreatedAtUtc = nextCreatedAtUtc
        };
        await SeedAssignmentsAsync(dbContext, initial);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.SaveAsync([updated]));

        Assert.Contains("immutable", exception.Message, StringComparison.OrdinalIgnoreCase);
        var persisted = await store.LoadAsync(initial.RunId, initial.StepInstanceId);
        Assert.NotNull(persisted);
        Assert.Equal(initial.CreatedAtUtc, persisted.CreatedAtUtc);
        Assert.Equal(
            initial.LaunchVariables
                .OrderBy(item => item.Key, StringComparer.Ordinal),
            persisted.LaunchVariables
                .OrderBy(item => item.Key, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Runtime_step_assignment_store_rejects_batch_before_mutating_any_tracked_assignment()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRuntimeStepAssignmentStore(dbContext);
        var initial = NewAssignment(
            ProcessRunId.New(),
            new Dictionary<string, string>(StringComparer.Ordinal));
        await SeedAssignmentsAsync(dbContext, initial);
        var updated = initial with
        {
            Prompt = "This update must not leak from a rejected batch."
        };
        var missing = NewAssignment(
            ProcessRunId.New(),
            new Dictionary<string, string>(StringComparer.Ordinal));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.SaveAsync([updated, missing]));

        Assert.Empty(dbContext.ChangeTracker.Entries());
        await dbContext.SaveChangesAsync();
        var persisted = await store.LoadAsync(initial.RunId, initial.StepInstanceId);
        Assert.NotNull(persisted);
        Assert.NotEqual(updated.Prompt, persisted.Prompt);
        Assert.Equal(initial.Prompt, persisted.Prompt);
        Assert.Null(await store.LoadAsync(missing.RunId, missing.StepInstanceId));
    }

    [Fact]
    public async Task Runtime_step_assignment_store_rejects_non_prompt_contract_update()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRuntimeStepAssignmentStore(dbContext);
        var initial = NewAssignment(
            ProcessRunId.New(),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] =
                    ProcessRunId.New().ToString(),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] =
                    ProcessStepInstanceId.New().ToString(),
                ["Mutable"] = "before"
            });
        await SeedAssignmentsAsync(dbContext, initial);
        var updated = initial with
        {
            Prompt = "Updated repair prompt.",
            LaunchVariables = initial.LaunchVariables
                .ToDictionary(
                    item => item.Key,
                    item => item.Key == "Mutable" ? "after" : item.Value,
                    StringComparer.Ordinal)
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.SaveAsync([updated]));

        var persisted = await store.LoadAsync(initial.RunId, initial.StepInstanceId);
        Assert.NotNull(persisted);
        Assert.Contains("only change", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(initial.Prompt, persisted.Prompt);
        Assert.Equal("before", persisted.LaunchVariables["Mutable"]);
    }

    [Fact]
    public async Task Runtime_step_assignment_store_allows_prompt_and_executor_readiness_repair()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRuntimeStepAssignmentStore(dbContext);
        var initial = NewAssignment(
            ProcessRunId.New(),
            new Dictionary<string, string>(StringComparer.Ordinal));
        await SeedAssignmentsAsync(dbContext, initial);
        var updated = initial with
        {
            Prompt = "Updated repair prompt.",
            ExecutorKind = ProcessLaunchExecutorKinds.Agent,
            ExecutorId = Guid.NewGuid().ToString("D"),
            ExecutorDisplayName = "Replacement executor",
            ReadinessHash = "sha256:replacement-readiness",
            AssignmentReason = "Reassigned after readiness validation."
        };

        await store.SaveAsync([updated]);

        var persisted = await store.LoadAsync(initial.RunId, initial.StepInstanceId);
        Assert.NotNull(persisted);
        Assert.Equal(updated.Prompt, persisted.Prompt);
        Assert.Equal(updated.ExecutorKind, persisted.ExecutorKind);
        Assert.Equal(updated.ExecutorId, persisted.ExecutorId);
        Assert.Equal(updated.ExecutorDisplayName, persisted.ExecutorDisplayName);
        Assert.Equal(updated.ReadinessHash, persisted.ReadinessHash);
        Assert.Equal(updated.AssignmentReason, persisted.AssignmentReason);
    }

    [Fact]
    public void Persistence_model_declares_required_unique_constraints()
    {
        using var dbContext = CreateDbContext();

        AssertHasUniqueConstraint<ProcessRuntimeIdempotencyEntity>(
            dbContext,
            nameof(ProcessRuntimeIdempotencyEntity.RunId),
            nameof(ProcessRuntimeIdempotencyEntity.CommandId));
        AssertHasUniqueConstraint<ProcessDispatchClaimEntity>(
            dbContext,
            nameof(ProcessDispatchClaimEntity.StepInstanceId),
            nameof(ProcessDispatchClaimEntity.ClaimToken));
        AssertHasUniqueConstraint<ProcessStrategyResultReceiptEntity>(
            dbContext,
            nameof(ProcessStrategyResultReceiptEntity.StepInstanceId),
            nameof(ProcessStrategyResultReceiptEntity.StrategyId),
            nameof(ProcessStrategyResultReceiptEntity.IdempotencyKey));
        AssertHasUniqueConstraint<ProcessRuntimeEventEntity>(
            dbContext,
            nameof(ProcessRuntimeEventEntity.EventId));
        AssertHasUniqueConstraint<ProcessRuntimeEventEntity>(
            dbContext,
            nameof(ProcessRuntimeEventEntity.RootRunId),
            nameof(ProcessRuntimeEventEntity.RootSequence));
        AssertHasUniqueConstraint<ProcessOutboxMessageEntity>(
            dbContext,
            nameof(ProcessOutboxMessageEntity.EventId),
            nameof(ProcessOutboxMessageEntity.SubscriberKind));
        AssertHasUniqueConstraint<ProcessArtifactLedgerEventEntity>(
            dbContext,
            nameof(ProcessArtifactLedgerEventEntity.SlotId),
            nameof(ProcessArtifactLedgerEventEntity.LedgerEventId));
    }

    private static ProcessPersistenceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase($"process-persistence-{Guid.NewGuid():N}")
            .Options;
        return new ProcessPersistenceDbContext(options);
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

    private static async Task SeedAssignmentsAsync(
        ProcessPersistenceDbContext dbContext,
        params ProcessRuntimeStepAssignment[] assignments)
    {
        dbContext.RuntimeStepAssignments.AddRange(
            assignments.Select(EfProcessRuntimeStepAssignmentStore.ToEntity));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }

    private static void AssertHasUniqueConstraint<TEntity>(
        ProcessPersistenceDbContext dbContext,
        params string[] propertyNames)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped.");

        var keyPropertyNames = entityType.GetKeys()
            .Select(key => key.Properties.Select(property => property.Name).ToArray());
        var indexPropertyNames = entityType.GetIndexes()
            .Where(index => index.IsUnique)
            .Select(index => index.Properties.Select(property => property.Name).ToArray());
        var hasConstraint = keyPropertyNames
            .Concat(indexPropertyNames)
            .Any(actualPropertyNames => actualPropertyNames.SequenceEqual(propertyNames));

        Assert.True(
            hasConstraint,
            $"{typeof(TEntity).Name} must declare a unique constraint on {string.Join(", ", propertyNames)}.");
    }

    private static ProcessRuntimeCommitRequest NewCommitRequest(
        bool includeArtifactLedger,
        ProcessRunId? runId = null,
        ProcessRunId? rootRunId = null,
        RuntimeCommandId? commandId = null,
        ProcessEventType? eventType = null,
        DateTimeOffset? updatedAtUtc = null,
        ProcessInstancePlan? initialPlan = null)
    {
        var actualRunId = runId ?? ProcessRunId.New();
        var actualRootRunId = rootRunId ?? actualRunId;
        var actualUpdatedAtUtc = updatedAtUtc ?? Now;
        var state = NewState(
            actualRunId,
            actualRootRunId,
            actualUpdatedAtUtc,
            initialPlan);
        var runtimeEvent = NewEvent(
            actualRunId,
            actualRootRunId,
            eventType ?? ProcessRuntimeEventTypes.ProcessRunCompleted,
            actualUpdatedAtUtc);
        var outbox = new ProcessOutboxMessage(
            RuntimeOutboxMessageId.New(),
            runtimeEvent.EventId,
            ProcessOutboxSubscriberKind.RuntimeProjection,
            runtimeEvent.PayloadHash);
        IReadOnlyList<ProcessArtifactLedgerEvent> ledgerEvents = includeArtifactLedger
            ? [
                new ProcessArtifactLedgerEvent(
                    ArtifactLedgerEventId.New(),
                    runtimeEvent.EventId,
                    RequiredArtifactSlotId,
                    ArtifactInstanceId.New(),
                    "hash:artifact")
            ]
            : [];
        var mutation = new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Applied,
            state,
            [runtimeEvent],
            [outbox],
            ledgerEvents,
            []);

        var request = new ProcessRuntimeCommitRequest(
            commandId ?? RuntimeCommandId.New(),
            state,
            mutation,
            InitialPlan: initialPlan);
        if (initialPlan is null)
        {
            return request;
        }

        return request with
        {
            InitialAssignments = initialPlan.Steps
                .Where(step => step.IsExecutable)
                .Select(step => NewAssignment(
                        actualRunId,
                        new Dictionary<string, string>(StringComparer.Ordinal),
                        actualUpdatedAtUtc)
                    with
                    {
                        PlanId = initialPlan.Header.PlanId,
                        StepInstanceId = step.StepInstanceId,
                        StepKey = step.StepKey
                    })
                .ToArray()
        };
    }

    private static ProcessRuntimeStateSnapshot NewState(
        ProcessRunId runId,
        ProcessRunId rootRunId,
        DateTimeOffset updatedAtUtc,
        ProcessInstancePlan? plan = null)
    {
        var planStep = plan?.Steps.Single();
        var stepId = planStep?.StepInstanceId ?? ProcessStepInstanceId.New();
        var stepDefinitionId =
            planStep?.StepDefinitionId ?? ProcessStepDefinitionId.New();
        return new ProcessRuntimeStateSnapshot(
            rootRunId,
            runId,
            plan?.Header.PlanId ?? ProcessInstancePlanId.New(),
            plan?.PlanHash ?? "hash:plan",
            ProcessRuntimeStatus.Completed,
            [
                new ProcessRuntimeStepState(
                    stepId,
                    stepDefinitionId,
                    ProcessRuntimeStepStatus.Completed,
                    true,
                    1,
                    new HashSet<ProcessStepInstanceId>(),
                    new HashSet<ArtifactSlotId> { RequiredArtifactSlotId },
                    null,
                    StrategyResultIdempotencyKey.New())
                {
                    ProducedArtifactSlots = new HashSet<ArtifactSlotId> { RequiredArtifactSlotId },
                    RequiredRuntimeToolNames = ["runtime-tool"]
                }
            ],
            [],
            [
                new StrategyResultReceipt(
                    stepId,
                    new StrategyId("strategy.test"),
                    StrategyResultIdempotencyKey.New(),
                    StrategyOutcome.Succeeded,
                    ProcessRuntimeStepStatus.Completed,
                    "hash:result")
            ],
            new HashSet<ArtifactSlotId> { RequiredArtifactSlotId },
            updatedAtUtc)
        {
            ConnectedInputArtifacts =
            [
                new ProcessRuntimeInputArtifactReceipt(
                    stepId,
                    RequiredArtifactSlotId,
                    ProcessArtifactInputAvailability.Available,
                    ProducerStepInstanceId: null,
                    ArtifactId: ArtifactInstanceId.New(),
                    ContentHash: "hash:artifact",
                    ConnectionHash: "hash:connected-input")
            ]
        };
    }

    private static ProcessRuntimeCommitRequest ContinueFrom(
        ProcessRuntimeCommitRequest request,
        ProcessRuntimeStateSnapshot originalState)
    {
        return request with
        {
            OriginalState = originalState,
            Mutation = request.Mutation with
            {
                State = request.Mutation.State with
                {
                    PlanId = originalState.PlanId,
                    PlanHash = originalState.PlanHash
                }
            }
        };
    }

    private static StrategyResultReceipt NewResultReceipt(
        ProcessStepInstanceId stepId,
        StrategyResultIdempotencyKey idempotencyKey,
        long appliedSequence)
    {
        return new StrategyResultReceipt(
            stepId,
            new StrategyId("strategy.test"),
            idempotencyKey,
            StrategyOutcome.Succeeded,
            ProcessRuntimeStepStatus.Completed,
            $"hash:result-{appliedSequence}")
        {
            AppliedSequence = appliedSequence
        };
    }

    private static ProcessRuntimeStateSnapshot NewParentState(
        ProcessRunId runId,
        ProcessStepInstanceId stepId,
        ProcessRuntimeStatus status,
        ProcessRuntimeStepStatus stepStatus,
        bool hasActiveClaim,
        DateTimeOffset? claimExpiresAtUtc = null)
    {
        var claimToken = hasActiveClaim ? DispatchClaimToken.New() : (DispatchClaimToken?)null;
        IReadOnlyList<DispatchClaimState> claims = claimToken is { } activeClaimToken
            ? [
                new DispatchClaimState(
                    activeClaimToken,
                    stepId,
                    new DispatcherOwnerId("unit-test"),
                    DispatchClaimStatus.Claimed,
                    1,
                    Now,
                    claimExpiresAtUtc ?? Now.AddMinutes(5),
                    null,
                    null)
            ]
            : [];
        return new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            ProcessInstancePlanId.New(),
            "hash:parent-plan",
            status,
            [
                new ProcessRuntimeStepState(
                    stepId,
                    ProcessStepDefinitionId.New(),
                    stepStatus,
                    true,
                    1,
                    new HashSet<ProcessStepInstanceId>(),
                    new HashSet<ArtifactSlotId>(),
                    claimToken,
                    null)
            ],
            claims,
            [],
            new HashSet<ArtifactSlotId>(),
            Now);
    }

    private static ProcessRuntimeEventEnvelope NewEvent(
        ProcessRunId runId,
        ProcessRunId rootRunId,
        ProcessEventType eventType,
        DateTimeOffset occurredAtUtc)
    {
        return new ProcessRuntimeEventEnvelope(
            RuntimeEventId.New(),
            rootRunId,
            runId,
            new ProcessCorrelationId("corr-1"),
            null,
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId("system")),
            ProcessContractVersions.RuntimeEventEnvelopeV1,
            ProcessEventSensitivity.Normal,
            occurredAtUtc,
            eventType,
            "hash:event");
    }

    public enum AssignmentImmutableMutation
    {
        ParentRunChanged,
        ParentRunRemoved,
        ParentRunAdded,
        ParentStepChanged,
        ParentStepRemoved,
        ParentStepAdded,
        PlanChanged,
        StepKeyChanged,
        CreatedAtChanged
    }

    private static ProcessRuntimeStepAssignment NewAssignment(
        ProcessRunId runId,
        IReadOnlyDictionary<string, string> launchVariables,
        DateTimeOffset? createdAtUtc = null)
    {
        return new ProcessRuntimeStepAssignment(
            runId,
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "test-step",
            "test-role",
            "test-role",
            "Test role",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            "Test executor",
            "Execute the step.",
            "sha256:readiness",
            "Resolved from test.",
            [],
            [],
            [ProcessOperationContractNames.ReadProjectStructure],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            launchVariables,
            null,
            createdAtUtc ?? Now);
    }

    private static ProcessInstancePlan NewDispatchablePlan(
        ProcessStepInstanceId stepId,
        ProcessStepDefinitionId stepDefinitionId,
        ProcessStrategyBindingSnapshot binding,
        ProcessInstancePlanId? planId = null,
        string planHash = "sha256:plan")
    {
        var actualPlanId = planId ?? ProcessInstancePlanId.New();
        return new ProcessInstancePlan(
            new ProcessInstancePlanHeader(
                actualPlanId,
                actualPlanId,
                null,
                null,
                "processes.instance-plan.v1",
                Now,
                0),
            new ResolvedProcessDefinitionSnapshot(
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                "sha256:definition",
                RuntimeSchemaVersion,
                RuntimeSchemaVersion,
                [],
                [],
                []),
            new DriverStackSnapshot(
                [
                    new ResolvedDriverSnapshot(
                        binding.DriverId,
                        "1.0.0",
                        ProcessDriverLayer.Scenario,
                        RuntimeSchemaVersion,
                        RuntimeSchemaVersion,
                        new HashSet<CapabilityTag> { new("test") })
                ]),
            new StrategyBindingSet([binding], [], [], []),
            [
                new StepInstancePlan(
                    stepId,
                    stepDefinitionId,
                    "execute-test",
                    ProcessStepKind.Activity,
                    true,
                    false,
                    binding)
            ],
            new ArtifactPlan([], []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan("sha256:manager", null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(false, "sha256:projection"),
            new SecurityPlan("sha256:governance", []),
            planHash);
    }

    private static ProcessInstancePlan NewInitialPlan()
    {
        var binding = new ProcessStrategyBindingSnapshot(
            new DriverId("driver.persistence-test"),
            new StrategyId("strategy.persistence-test.execute"),
            "1.0.0",
            "factory.1.0.0",
            RuntimeSchemaVersion,
            RuntimeSchemaVersion,
            "sha256:binding",
            []);
        return NewDispatchablePlan(
            ProcessStepInstanceId.New(),
            ProcessStepDefinitionId.New(),
            binding);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private const string RuntimeSchemaVersion = "runtime/1.0";
    private static readonly ArtifactSlotId RequiredArtifactSlotId = ArtifactSlotId.New();
}
