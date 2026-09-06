using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentCapabilityAssignmentTests {
    [Fact]
    public void Assignment_submission_preserves_expected_revision_and_unrelated_fields() {
        var draft = Draft();
        var capabilityId = Guid.NewGuid();
        var attempt = new AgentCapabilityAssignmentAttempt(draft, capabilityId);
        Assert.Equal(draft.Id, attempt.AgentId);
        Assert.Equal(draft.ExpectedUpdatedAtUtc, attempt.ExpectedUpdatedAtUtc);
        Assert.Empty(attempt.Before);
        Assert.Contains(capabilityId, attempt.Desired);
        Assert.Equal(draft.Name, attempt.CreateRequest().Name);
        Assert.Equal(draft.Instructions, attempt.CreateRequest().Instructions);
        Assert.Empty(draft.SelectedCapabilityIds);
    }

    [Fact]
    public void Assignment_submission_owns_mutable_descendants_and_each_retry_request() {
        var draft = Draft();
        draft.Tags = ["original"];
        draft.WorkspaceToolAccess.AllowedStorageCatalogIds = [Guid.NewGuid()];
        var attempt = new AgentCapabilityAssignmentAttempt(draft, Guid.NewGuid());
        var first = attempt.CreateRequest();
        draft.Tags.Clear();
        first.Tags.Clear();
        first.WorkspaceToolAccess.AllowedStorageCatalogIds.Clear();
        first.SelectedCapabilityIds.Clear();
        var retry = attempt.CreateRequest();
        Assert.Equal(["original"], retry.Tags);
        Assert.Single(retry.WorkspaceToolAccess.AllowedStorageCatalogIds);
        Assert.Single(retry.SelectedCapabilityIds);
        Assert.Equal(attempt.ExpectedUpdatedAtUtc, retry.ExpectedUpdatedAtUtc);
    }

    [Fact]
    public async Task Unconfirmed_assignment_blocks_blind_replay_and_survives_target_reentry() {
        var commands = new Commands();
        var reads = new Reads();
        var operations = new AgentCapabilityOperations(commands, reads);
        var draft = Draft();
        var result = await operations.AssignAsync(draft, Guid.NewGuid());
        Assert.Equal(AgentCapabilityOperationStatus.Unconfirmed, result!.Status);
        Assert.Null(await operations.AssignAsync(draft, Guid.NewGuid()));
        Assert.Null(operations.Find(Guid.NewGuid()));
        Assert.Equal(result, operations.Find(draft.Id));
        Assert.Equal(1, commands.Calls);
    }

    [Fact]
    public async Task Verification_finds_desired_set_and_reconciles_without_save() {
        var commands = new Commands();
        var reads = new Reads();
        var operations = new AgentCapabilityOperations(commands, reads);
        var draft = Draft();
        var state = await operations.AssignAsync(draft, Guid.NewGuid());
        reads.Current = commands.Last!.CreateRequest();
        reads.Current.ExpectedUpdatedAtUtc = draft.ExpectedUpdatedAtUtc!.Value.AddTicks(1);
        var verified = await operations.VerifyAsync(state!.AgentId, state.AttemptId);
        Assert.Equal(AgentCapabilityOperationStatus.DesiredStateSatisfied, verified!.Status);
        Assert.True(operations.CompleteReconciliation(state.AgentId, state.AttemptId));
        Assert.Null(operations.Find(state.AgentId));
        Assert.Equal(1, commands.Calls);
    }

    [Fact]
    public async Task Exact_before_state_allows_only_deliberate_same_submission_retry() {
        var commands = new Commands();
        var draft = Draft();
        var reads = new Reads { Current = draft };
        var operations = new AgentCapabilityOperations(commands, reads);
        var first = await operations.AssignAsync(draft, Guid.NewGuid());
        var original = commands.Last;
        var verified = await operations.VerifyAsync(first!.AgentId, first.AttemptId);
        Assert.True(verified!.CanRetry);
        Assert.Equal(1, commands.Calls);
        Assert.Null(await operations.AssignAsync(draft, Guid.NewGuid()));
        commands.Outcome = AgentCapabilityOperationStatus.Committed;
        var retried = await operations.RetryAsync(first.AgentId, first.AttemptId);
        Assert.Equal(AgentCapabilityOperationStatus.Committed, retried!.Status);
        Assert.Same(original, commands.Last);
        Assert.Equal(2, commands.Calls);
        Assert.Null(await operations.RetryAsync(first.AgentId, first.AttemptId));
    }

    [Fact]
    public async Task Intervening_authoritative_set_requires_explicit_adoption() {
        var commands = new Commands();
        var draft = Draft();
        var current = Draft(draft.Id);
        current.ExpectedUpdatedAtUtc = draft.ExpectedUpdatedAtUtc!.Value.AddTicks(1);
        current.SelectedCapabilityIds = [Guid.NewGuid()];
        var operations = new AgentCapabilityOperations(commands, new Reads { Current = current });
        var state = await operations.AssignAsync(draft, Guid.NewGuid());
        var result = await operations.VerifyAsync(state!.AgentId, state.AttemptId);
        Assert.Equal(AgentCapabilityOperationStatus.Superseded, result!.Status);
        Assert.False(operations.CompleteReconciliation(state.AgentId, state.AttemptId));
        Assert.Null(await operations.AssignAsync(draft, Guid.NewGuid()));
        Assert.True(operations.CompleteReconciliation(state.AgentId, state.AttemptId, adoptCurrent: true));
        Assert.Null(operations.Find(state.AgentId));
    }

    [Fact]
    public async Task Failed_canonical_read_remains_unconfirmed() {
        var operations = new AgentCapabilityOperations(new Commands(), new Reads { Failure = true });
        var state = await operations.AssignAsync(Draft(), Guid.NewGuid());
        var result = await operations.VerifyAsync(state!.AgentId, state.AttemptId);
        Assert.Equal(AgentCapabilityOperationStatus.Unconfirmed, result!.Status);
        Assert.False(result.IsActive);
        Assert.False(result.CanRetry);
    }

    [Fact]
    public void Wrong_identity_or_older_revision_is_insufficient_evidence() {
        var draft = Draft();
        var attempt = new AgentCapabilityAssignmentAttempt(draft, Guid.NewGuid());
        Assert.Equal(AgentCapabilityOperationStatus.Unconfirmed, attempt.Classify(Draft()));
        var current = attempt.CreateRequest();
        current.ExpectedUpdatedAtUtc = draft.ExpectedUpdatedAtUtc!.Value.AddTicks(-1);
        Assert.Equal(AgentCapabilityOperationStatus.Unconfirmed, attempt.Classify(current));
    }

    [Fact]
    public async Task Validation_rejection_allows_corrected_new_intent() {
        var commands = new Commands { Outcome = AgentCapabilityOperationStatus.Rejected };
        var operations = new AgentCapabilityOperations(commands, new Reads());
        var draft = Draft();
        await operations.AssignAsync(draft, Guid.NewGuid());
        Assert.Null(operations.Find(draft.Id));
        draft.Name = "Corrected";
        commands.Outcome = AgentCapabilityOperationStatus.Committed;
        Assert.NotNull(await operations.AssignAsync(draft, Guid.NewGuid()));
        Assert.Equal(2, commands.Calls);
    }

    [Fact]
    public async Task Two_rapid_intents_start_one_write_while_B_can_run_independently() {
        var pending = new TaskCompletionSource<AgentCapabilityOperationStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commands = new Commands { Pending = pending.Task };
        var operations = new AgentCapabilityOperations(commands, new Reads());
        var draft = Draft();
        var first = operations.AssignAsync(draft, Guid.NewGuid());
        Assert.Null(await operations.AssignAsync(draft, Guid.NewGuid()));
        var beta = Draft();
        var second = operations.AssignAsync(beta, Guid.NewGuid());
        Assert.True(operations.Find(draft.Id)!.IsActive);
        Assert.True(operations.Find(beta.Id)!.IsActive);
        Assert.Equal(2, commands.Calls);
        pending.SetResult(AgentCapabilityOperationStatus.Committed);
        await Task.WhenAll(first, second);
        Assert.False(operations.Find(draft.Id)!.IsActive);
        Assert.False(operations.Find(beta.Id)!.IsActive);
    }

    [Fact]
    public async Task Stale_completion_cannot_remove_a_newer_attempt() {
        var commands = new Commands { Outcome = AgentCapabilityOperationStatus.Committed };
        var operations = new AgentCapabilityOperations(commands, new Reads());
        var draft = Draft();
        var first = (await operations.AssignAsync(draft, Guid.NewGuid()))!;
        Assert.True(operations.CompleteReconciliation(first.AgentId, first.AttemptId));
        var second = await operations.AssignAsync(draft, Guid.NewGuid());
        Assert.False(operations.CompleteReconciliation(first.AgentId, first.AttemptId));
        Assert.Equal(second, operations.Find(draft.Id));
    }

    private static AgentEditorModel Draft(Guid? id = null) => new() {
        Id = id ?? Guid.NewGuid(), ExpectedUpdatedAtUtc = DateTimeOffset.UnixEpoch,
        Name = "Assignment fixture", Instructions = "Preserve unrelated settings."
    };

    private sealed class Commands : IAgentCapabilityCommands {
        public int Calls { get; private set; }
        public AgentCapabilityAssignmentAttempt? Last { get; private set; }
        public AgentCapabilityOperationStatus Outcome { get; set; } = AgentCapabilityOperationStatus.Unconfirmed;
        public Task<AgentCapabilityOperationStatus>? Pending { get; set; }
        public Task<CapabilityVerificationOutcome> DiagnoseAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unexpected diagnostic in assignment test.");
        public Task<AgentCapabilityOperationStatus> AssignAsync(AgentCapabilityAssignmentAttempt attempt, CancellationToken cancellationToken = default) {
            Calls++;
            Last = attempt;
            return Pending ?? Task.FromResult(Outcome);
        }
    }

    private sealed class Reads : IAgentCapabilitiesReads {
        public AgentEditorModel Current { get; set; } = new();
        public bool Failure { get; set; }
        public Task<AgentEditorModel> ReadEditorAsync(Guid agentId, CancellationToken cancellationToken = default)
            => Failure ? Task.FromException<AgentEditorModel>(new IOException("Fixture unavailable")) : Task.FromResult(Current);
        public Task<AgentCapabilitiesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
