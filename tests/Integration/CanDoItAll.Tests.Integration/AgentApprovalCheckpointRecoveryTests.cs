using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentApprovalCheckpointRecoveryTests {
    public enum Damage { None, Missing, InvalidJson, NullPayload, MissingApprovals, ChangedSession }

    [Theory]
    [InlineData(Damage.None)]
    [InlineData(Damage.Missing)]
    [InlineData(Damage.InvalidJson)]
    [InlineData(Damage.NullPayload)]
    [InlineData(Damage.MissingApprovals)]
    [InlineData(Damage.ChangedSession)]
    public async Task Restarted_bridge_reports_checkpoint_damage_without_changing_consent_or_history(Damage damage) {
        await using var fixture = await RestoreAsync();
        var run = fixture.Detail.Run;
        ExecutionWorkflowCheckpointRecord checkpoint;
        using (var bridge = new WorkflowBackedAgentExecutionCheckpointBridge(fixture.Store, fixture.WorkspaceRoot)) {
            checkpoint = (await bridge.CapturePendingApprovalCheckpointAsync(run))!;
        }
        var payloadName = $"{Uri.EscapeDataString(checkpoint.WorkflowSessionId)}_{Uri.EscapeDataString(checkpoint.WorkflowCheckpointId)}.json";
        var path = Assert.Single(Directory.EnumerateFiles(fixture.WorkspaceRoot, payloadName, SearchOption.AllDirectories));
        if (damage == Damage.Missing) {
            File.Delete(path);
        } else if (damage is Damage.InvalidJson or Damage.NullPayload or Damage.MissingApprovals) {
            await File.WriteAllTextAsync(path, damage switch {
                Damage.InvalidJson => "{PRIVATE_CORRUPT_CHECKPOINT",
                Damage.NullPayload => "null",
                _ => "{}"
            });
        }
        var before = await fixture.NewStore().GetExecutionRunDetailAsync(run.Id);
        using var restarted = new WorkflowBackedAgentExecutionCheckpointBridge(fixture.NewStore(), fixture.WorkspaceRoot);
        if (damage == Damage.None) {
            Assert.Equal(JsonSerializer.Serialize(checkpoint), JsonSerializer.Serialize(await restarted.ValidatePendingApprovalResumeAsync(run)));
        } else {
            var failure = await Assert.ThrowsAsync<AgentApprovalCheckpointUnavailableException>(() =>
                restarted.ValidatePendingApprovalResumeAsync(damage == Damage.ChangedSession ? run with { RuntimeSessionKey = "different-session" } : run));
            Assert.Equal(damage switch {
                Damage.Missing => AgentApprovalCheckpointFailure.Missing,
                Damage.ChangedSession => AgentApprovalCheckpointFailure.Incompatible,
                _ => AgentApprovalCheckpointFailure.Corrupt
            }, failure.Failure);
            Assert.Contains("No approval was applied", failure.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(fixture.WorkspaceRoot, failure.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE_CORRUPT_CHECKPOINT", failure.Message, StringComparison.Ordinal);
        }
        var after = await fixture.NewStore().GetExecutionRunDetailAsync(run.Id);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        Assert.All(after!.Checkpoints, item => Assert.Null(item.ResumedAtUtc));
        Assert.Empty(after.ToolReceipts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Atomic_writer_preserves_the_first_cancellation_or_continuation_across_independent_stores(bool continueFirst) {
        await using var fixture = await RestoreAsync();
        var original = fixture.Detail.Run;
        var first = (ISandboxWorkspaceExecutionRunMutationStore)fixture.NewStore();
        var second = (ISandboxWorkspaceExecutionRunMutationStore)fixture.NewStore();
        var saved = await first.UpdateExecutionRunDetailAsync(original.Id, current => continueFirst
            ? BeginContinuation(current, original)
            : AgentPendingApprovalCancellation.Apply(current, original, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.UpdateExecutionRunDetailAsync(original.Id, current => continueFirst
            ? AgentPendingApprovalCancellation.Apply(current, original, DateTimeOffset.UtcNow)
            : BeginContinuation(current, original)));
        var persisted = await fixture.NewStore().GetExecutionRunDetailAsync(original.Id);
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(persisted));
        Assert.Equal(continueFirst ? ExecutionState.Running : ExecutionState.Failed, persisted!.Run.State);
        Assert.Equal(continueFirst ? null : RunOutcome.Cancelled, persisted.Run.Outcome);
        Assert.Empty(persisted.Run.PendingApprovals);
        Assert.Empty(persisted.ToolReceipts);
        if (!continueFirst) {
            var repeated = await second.UpdateExecutionRunDetailAsync(original.Id,
                current => AgentPendingApprovalCancellation.Apply(current, original, DateTimeOffset.UtcNow));
            Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(repeated));
            Assert.Single(repeated.ExecutionLog);
        }
    }

    private static ExecutionRunDetail BeginContinuation(ExecutionRunDetail current, ExecutionRunRecord expected) {
        if (current.Run.State != ExecutionState.WaitingOnTool || !ExecutionRunStateTransitions.PendingApprovalStateMatches(expected, current.Run)) {
            throw new InvalidOperationException("The selected approval state is no longer current.");
        }
        var decisions = expected.PendingApprovals.Select(item => new PendingToolApprovalDecision(item.ApprovalId, true) {
            ToolAdmission = item.ToolAdmission
        }).ToArray();
        return current with { Run = ExecutionRunStateTransitions.CreateContinuationStartRun(current.Run, true, false, DateTimeOffset.UtcNow) with {
            ToolAdmission = AgentToolJournalTransitions.ApplyDecisions(current.Run.ToolAdmission!, expected.PendingApprovals, decisions, false)
        } };
    }

    private static async Task<AgentToolAdmissionJournalFixture> RestoreAsync() {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Maf120", "Durable", "journal-ChatCompletions-False-pending.json");
        var saved = JsonSerializer.Deserialize<RetainedFixture>(await File.ReadAllTextAsync(path), JsonSerializerOptions.Web)!;
        Assert.Equal("1.20.0.0", saved.MafVersion);
        return await AgentToolAdmissionJournalFixture.RestoreRetainedAsync(saved.Agent, saved.Provider, saved.Profile, saved.Detail);
    }

    private sealed record RetainedFixture(string MafVersion, AgentDefinition Agent, ProviderProfile Provider,
        AgentToolProfileBinding Profile, ExecutionRunRecord Detail);
}
