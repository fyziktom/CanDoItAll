using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentPendingApprovalCancellationTests {
    [Theory]
    [InlineData("ChatCompletions", false)]
    [InlineData("ChatCompletions", true)]
    [InlineData("Responses", false)]
    [InlineData("Responses", true)]
    [InlineData("Ollama", false)]
    [InlineData("Ollama", true)]
    public void Cancellation_rejects_genuine_pending_proposals_without_rewriting_native_history(string provider, bool streaming) {
        var original = Pending(provider, streaming);
        var now = original.Run.UpdatedAtUtc.AddMinutes(1);

        var cancelled = AgentPendingApprovalCancellation.Apply(original, original.Run, now);

        Assert.Equal(RunOutcome.Cancelled, cancelled.Run.Outcome);
        Assert.Equal(ExecutionState.Failed, cancelled.Run.State);
        Assert.Equal(now, cancelled.Run.CompletedAtUtc);
        Assert.Empty(cancelled.Run.PendingApprovals);
        Assert.Null(cancelled.Run.SerializedSessionStateJson);
        Assert.Equal(original.Run.ToolAdmission!.Segments, cancelled.Run.ToolAdmission!.Segments);
        Assert.Equal(original.ToolReceipts, cancelled.ToolReceipts);
        Assert.Equal(original.Checkpoints, cancelled.Checkpoints);
        Assert.Equal(original.ChatSession!.Messages, cancelled.ChatSession!.Messages);
        Assert.All(cancelled.Run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals), proposal => {
            Assert.Equal(AgentToolProposalState.Cancelled, proposal.State);
            Assert.Equal(ExecutionApprovalStatus.Rejected, proposal.ApprovalStatus);
            Assert.Equal(AgentToolCancellationDisposition.NotDispatched, proposal.Cancellation!.Disposition);
            Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
            Assert.Null(proposal.Result);
        });
        Assert.Equal(original.Run.PendingApprovals.Count, cancelled.Approvals.Count);
        Assert.All(cancelled.Approvals, approval => {
            Assert.Equal(ExecutionApprovalStatus.Rejected, approval.Status);
            Assert.Equal(AgentPendingApprovalCancellation.Phase, approval.DecisionSourceKind);
            Assert.Equal(now, approval.DecidedAtUtc);
        });
        Assert.Single(cancelled.ExecutionLog);
        Assert.Same(cancelled, AgentPendingApprovalCancellation.Apply(cancelled, cancelled.Run, now.AddMinutes(1)));
        Assert.Equal(original.Run.PendingApprovals.Count, original.Run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals)
            .Count(proposal => proposal.ApprovalStatus == ExecutionApprovalStatus.Pending));
    }

    [Theory]
    [InlineData(ExecutionState.Running)]
    [InlineData(ExecutionState.Completed)]
    [InlineData(ExecutionState.Failed)]
    public void A_continuation_or_terminal_transition_that_already_won_is_not_overwritten(ExecutionState state) {
        var original = Pending();
        var current = original with { Run = original.Run with { State = state, PendingApprovals = [] } };
        Assert.Throws<InvalidOperationException>(() => AgentPendingApprovalCancellation.Apply(current, original.Run, DateTimeOffset.UtcNow));
        Assert.Equal(state, current.Run.State);
    }

    [Fact]
    public void Changed_pending_arguments_cannot_be_cancelled_using_a_stale_selection() {
        var original = Pending();
        var changed = original with { Run = original.Run with {
            PendingApprovals = original.Run.PendingApprovals.Select(item => item with { ArgumentsJson = "{}" }).ToArray()
        } };
        Assert.Throws<InvalidOperationException>(() => AgentPendingApprovalCancellation.Apply(changed, original.Run, DateTimeOffset.UtcNow));
        Assert.Null(changed.Run.Outcome);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Changed_run_identity_or_authority_is_rejected(bool identity) {
        var original = Pending();
        var changed = original with { Run = identity
            ? original.Run with { Id = Guid.NewGuid() }
            : original.Run with { MetadataJson = "{}" } };
        Assert.Throws<InvalidOperationException>(() => AgentPendingApprovalCancellation.Apply(changed, original.Run, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Process_owned_or_legacy_runs_require_their_owner_cancellation_path() {
        var original = Pending();
        foreach (var run in new[] { original.Run with { ProcessRunId = Guid.NewGuid().ToString("D") }, original.Run with { ToolAdmission = null } }) {
            Assert.Throws<InvalidOperationException>(() => AgentPendingApprovalCancellation.Apply(original with { Run = run }, run, DateTimeOffset.UtcNow));
        }
    }

    private static ExecutionRunDetail Pending(string provider = "ChatCompletions", bool streaming = false) {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Maf120", "Durable", $"journal-{provider}-{streaming}-pending.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var run = document.RootElement.GetProperty("detail").Deserialize<ExecutionRunRecord>(JsonSerializerOptions.Web)!;
        Assert.Equal(ExecutionState.WaitingOnTool, run.State);
        var message = new ChatMessageRecord(Guid.NewGuid(), ChatMessageRole.User, "Preserve the original request.", run.CreatedAtUtc, 5);
        var session = new ChatSessionRecord(run.ChatSessionId!.Value, run.AgentId, run.Title, run.CreatedAtUtc, run.UpdatedAtUtc,
            [message], LatestExecutionRunId: run.Id);
        return new(run, session, [], []);
    }
}
