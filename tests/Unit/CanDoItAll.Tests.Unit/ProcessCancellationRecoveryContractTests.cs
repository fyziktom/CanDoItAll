using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessCancellationRecoveryContractTests {
    [Fact]
    public void Positions_support_only_the_fields_owned_by_their_stage() {
        foreach (var position in RecoveryContractFixture.Positions()) {
            position.Validate();
        }
    }

    [Fact]
    public void Invalid_or_cross_stage_cursors_are_refused() {
        ProcessDecisionReference decision = new(new(Guid.NewGuid()), 1);
        ProcessCancellationRecoveryPosition[] invalid = [
            new((ProcessCancellationRecoveryStage)99), new(ProcessCancellationRecoveryStage.Runtime, new ProcessRunId()),
            new(ProcessCancellationRecoveryStage.SharedScope, RecoveryContractFixture.Root),
            new(ProcessCancellationRecoveryStage.Runtime, CurrentDecision: decision),
            new(ProcessCancellationRecoveryStage.Decisions, ChildAfter: new(Guid.NewGuid())),
            new(ProcessCancellationRecoveryStage.Decisions, DecisionAfter: decision.DecisionId, CurrentDecision: decision),
            new(ProcessCancellationRecoveryStage.Decisions, CurrentDecision: decision with { Generation = 0 }),
            new(ProcessCancellationRecoveryStage.Decisions, DecisionAfter: new(Guid.Empty))
        ];
        foreach (var position in invalid) {
            Assert.Throws<InvalidDataException>(position.Validate);
        }
    }

    [Fact]
    public void Claims_require_original_origin_identity_and_a_positive_lease() {
        var valid = RecoveryContractFixture.Claim();
        valid.Validate();
        (valid with { Origin = ProcessCancellationRecoveryOrigin.LegacySeed, BarrierCommandId = null }).Validate();
        ProcessCancellationRecoveryClaim[] invalid = [
            valid with { RootRunId = default }, valid with { LeaseToken = Guid.Empty }, valid with { LeaseGeneration = 0 },
            valid with { ProgressRevision = -1 }, valid with { Cycle = -1 }, valid with { LeaseExpiresAtUtc = default },
            valid with { BarrierCommandId = null }, valid with { Origin = ProcessCancellationRecoveryOrigin.LegacySeed },
            valid with { Pending = (ProcessCancellationRecoveryPending)99 },
            valid with { Position = new(ProcessCancellationRecoveryStage.Completed) }
        ];
        foreach (var claim in invalid) {
            Assert.Throws<InvalidDataException>(claim.Validate);
        }
    }

    [Fact]
    public void Intent_identity_is_bound_to_the_original_system_actor_root_and_action() {
        foreach (var (stage, intent) in RecoveryContractFixture.Intents()) {
            intent.Validate(RecoveryContractFixture.Root, stage);
            Assert.Throws<InvalidDataException>(() => intent.Validate(new(Guid.NewGuid()), stage));
            var changed = intent with { Context = intent.Context with { Actor = new(ProcessEventActorKind.User, new("another-actor")) } };
            Assert.Throws<InvalidDataException>(() => changed.Validate(RecoveryContractFixture.Root, stage));
        }
        var account = (ProcessCancellationAccountChild)RecoveryContractFixture.Intents()[1].Intent;
        Assert.Throws<InvalidDataException>(() => (account with { Request = account.Request with { ActionId = RuntimeCommandId.New() } })
            .Validate(RecoveryContractFixture.Root, ProcessCancellationRecoveryStage.Decisions));
    }

    [Fact]
    public void Intents_cannot_cross_stages_or_fabricate_capture_settlement() {
        foreach (var (stage, intent) in RecoveryContractFixture.Intents()) {
            foreach (var other in Enum.GetValues<ProcessCancellationRecoveryStage>().Where(item => item != stage)) {
                Assert.Throws<InvalidDataException>(() => intent.Validate(RecoveryContractFixture.Root, other));
            }
        }
        var scope = (ProcessCancellationChangeScope)RecoveryContractFixture.Intents()[3].Intent;
        Assert.Throws<InvalidDataException>(() => (scope with { Command = scope.Command with { Kind = ProcessDecisionRunScopeCommandKind.AbortCapture } })
            .Validate(RecoveryContractFixture.Root, ProcessCancellationRecoveryStage.SharedScope));
    }

    [Fact]
    public void Unresolved_intent_survives_release_and_cannot_be_replaced_or_advanced() {
        var intent = RecoveryContractFixture.Intents()[0].Intent;
        var claim = RecoveryContractFixture.Claim() with { Intent = intent };
        new ProcessCancellationRecoveryCheckpoint(claim.Position, intent, ProcessCancellationRecoveryPending.UnresolvedEffects,
            RetryAfter: TimeSpan.FromSeconds(10)).Validate(claim);
        Assert.Throws<InvalidDataException>(() => new ProcessCancellationRecoveryCheckpoint(claim.Position).Validate(claim));
        Assert.Throws<InvalidDataException>(() => new ProcessCancellationRecoveryCheckpoint(claim.Position,
            intent with { Context = intent.Context with { CommandId = RuntimeCommandId.New() } }).Validate(claim));
        Assert.Throws<InvalidDataException>(() => new ProcessCancellationRecoveryCheckpoint(claim.Position with { RunAfter = claim.RootRunId }, intent).Validate(claim));
    }

    [Fact]
    public void Preparing_an_intent_keeps_the_position_and_does_not_claim_a_resolution() {
        var claim = RecoveryContractFixture.Claim();
        var intent = RecoveryContractFixture.Intents()[0].Intent;
        new ProcessCancellationRecoveryCheckpoint(claim.Position, intent).Validate(claim);
        Assert.Throws<InvalidDataException>(() => new ProcessCancellationRecoveryCheckpoint(claim.Position with { RunAfter = claim.RootRunId }, intent).Validate(claim));
        Assert.Throws<InvalidDataException>(() => new ProcessCancellationRecoveryCheckpoint(claim.Position,
            Resolution: new(intent.Context.CommandId, ProcessCancellationIntentOutcome.Committed)).Validate(claim));
    }

    [Fact]
    public void Clearing_requires_the_exact_original_action_and_bounded_delay() {
        var intent = RecoveryContractFixture.Intents()[0].Intent;
        var claim = RecoveryContractFixture.Claim() with { Intent = intent };
        var resolved = new ProcessCancellationRecoveryCheckpoint(claim.Position,
            Resolution: new(intent.Context.CommandId, ProcessCancellationIntentOutcome.RefusedWithoutCommit));
        resolved.Validate(claim);
        Assert.Throws<InvalidDataException>(() => (resolved with { Resolution = new(RuntimeCommandId.New(), ProcessCancellationIntentOutcome.Committed) }).Validate(claim));
        Assert.Throws<InvalidDataException>(() => (resolved with { Resolution = new(intent.Context.CommandId, (ProcessCancellationIntentOutcome)99) }).Validate(claim));
        Assert.Throws<InvalidDataException>(() => (resolved with { RetryAfter = TimeSpan.FromHours(2) }).Validate(claim));
        Assert.Throws<InvalidDataException>(() => (resolved with { RetryAfter = TimeSpan.FromTicks(-1) }).Validate(claim));
    }

    [Fact]
    public void Completion_requires_a_verified_root_intent_and_pending_cycles_restart_explicitly() {
        var claim = RecoveryContractFixture.Claim();
        Assert.Throws<InvalidDataException>(() => new ProcessCancellationRecoveryCheckpoint(new(ProcessCancellationRecoveryStage.Quiescence)).Validate(claim));
        claim = claim with { Position = new(ProcessCancellationRecoveryStage.Quiescence) };
        Assert.Throws<InvalidDataException>(() => new ProcessCancellationRecoveryCheckpoint(new(ProcessCancellationRecoveryStage.Completed)).Validate(claim));
        new ProcessCancellationRecoveryCheckpoint(ProcessCancellationRecoveryPosition.Initial, Pending: ProcessCancellationRecoveryPending.BudgetWork).Validate(claim);
        Assert.Throws<InvalidDataException>(() => new ProcessCancellationRecoveryCheckpoint(ProcessCancellationRecoveryPosition.Initial).Validate(claim));
        var intent = RecoveryContractFixture.Intents()[4].Intent;
        claim = claim with { Intent = intent };
        var completed = new ProcessCancellationRecoveryCheckpoint(new(ProcessCancellationRecoveryStage.Completed),
            Resolution: new(intent.Context.CommandId, ProcessCancellationIntentOutcome.Committed));
        completed.Validate(claim);
        Assert.Throws<InvalidDataException>(() => (completed with { Pending = ProcessCancellationRecoveryPending.ActiveWork }).Validate(claim));
        Assert.Throws<InvalidDataException>(() => (completed with { Resolution = new(intent.Context.CommandId, ProcessCancellationIntentOutcome.RefusedWithoutCommit) }).Validate(claim));
    }
}

internal static class RecoveryContractFixture {
    internal static readonly ProcessRunId Root = new(Guid.NewGuid());
    internal static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    internal static ProcessCancellationRecoveryClaim Claim() => new(Root, ProcessCancellationRecoveryOrigin.LiveBarrier,
        RuntimeCommandId.New(), Guid.NewGuid(), 1, 0, Now.AddMinutes(1), 0, ProcessCancellationRecoveryPosition.Initial, null,
        ProcessCancellationRecoveryPending.None);

    internal static ProcessCancellationRecoveryPosition[] Positions() => [
        ProcessCancellationRecoveryPosition.Initial, new(ProcessCancellationRecoveryStage.Runtime, Root),
        new(ProcessCancellationRecoveryStage.Decisions, DecisionAfter: new(Guid.NewGuid()), CurrentDecision: new(new(Guid.NewGuid()), 2), ChildAfter: new(Guid.NewGuid())),
        new(ProcessCancellationRecoveryStage.SharedScope), new(ProcessCancellationRecoveryStage.Quiescence), new(ProcessCancellationRecoveryStage.Completed)
    ];

    internal static (ProcessCancellationRecoveryStage Stage, ProcessCancellationRecoveryIntent Intent)[] Intents() {
        var context = ProcessCancellationRecoveryIntent.CreateContext(Root, Now);
        ProcessStepInstanceId step = new(Guid.NewGuid());
        ProcessDecisionExpectation decision = new(new(new(Guid.NewGuid()), 2), 7);
        ProcessDecisionBudgetExpectation budget = new(step, new(Guid.NewGuid()), 9);
        return [
            (ProcessCancellationRecoveryStage.Runtime, new ProcessCancellationCancelRun(Root, context)),
            (ProcessCancellationRecoveryStage.Decisions, new ProcessCancellationAccountChild(Root, context,
                new(new(Root, Root, step, decision.Reference, new(Guid.NewGuid())), decision, budget, ProcessRuntimeStepStatus.Cancelled, context.CommandId))),
            (ProcessCancellationRecoveryStage.Decisions, new ProcessCancellationFinalizeDecision(Root, context,
                new(step, decision, budget, ProcessRuntimeStepStatus.Failed, context.CommandId))),
            (ProcessCancellationRecoveryStage.SharedScope, new ProcessCancellationChangeScope(Root, context,
                new(new(new(Guid.NewGuid()), 3), ProcessDecisionRunScopeCommandKind.Cancel))),
            (ProcessCancellationRecoveryStage.Quiescence, new ProcessCancellationFinalizeRoot(Root, context))
        ];
    }
}
