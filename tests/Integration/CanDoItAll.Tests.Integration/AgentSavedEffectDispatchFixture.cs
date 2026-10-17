using System.Reflection;
using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

internal sealed class AgentSavedEffectDispatchFixture : IAsyncDisposable {
    private AgentSavedEffectDispatchFixture(AgentToolAdmissionJournalFixture files, ExecutionRunRecord prepared) {
        Files = files;
        Prepared = prepared;
    }

    internal AgentToolAdmissionJournalFixture Files { get; }
    internal ExecutionRunRecord Prepared { get; }
    internal static ValueTask<bool> AllowRead(AgentToolSessionObservation observation, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(true);
    }

    internal static async Task<AgentSavedEffectDispatchFixture> CreateAsync(int count = 2) {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = source.Detail.Run with { ToolAdmission = source.Detail.Run.ToolAdmission! with {
            SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion,
            Support = AgentToolAdmissionSupport.EffectJournalOnly
        } };
        var files = await AgentToolAdmissionJournalFixture.RestoreRetainedAsync(source.Agent, source.Provider, source.Profile, run);
        try {
            var owner = files.NewJournal();
            await using (var origin = await owner.AcquireRunAsync(files.Session, default)) {
                using var bound = origin.Bind();
                var journal = await owner.BeginEffectSegmentAsync(origin, default);
                var calls = Enumerable.Range(0, count).Select(index => new AgentToolPreparedCall($"saved-call-{index}", Payload(index), true)).ToArray();
                await owner.AdmitBatchAsync(origin, calls[0].Payload.Digest, Envelope(), calls, default);
                await owner.SaveEffectApprovalsAsync(origin, journal.Segments[0].Id, calls.Select((call, index) =>
                    new PendingToolApprovalRecord($"saved-approval-{index}", call.CallId, call.Payload.ToolName,
                        "function", "Saved fixture proposal", call.Payload.ArgumentsJson)).ToArray(), default);
            }
            return new(files, (await files.NewStore().GetExecutionRunAsync(files.Session.ExecutionRunId))!);
        } catch {
            await files.DisposeAsync();
            throw;
        }
    }

    internal static AgentToolProtocolEnvelope Envelope(string json = "{}")
        => AgentToolProtocolEnvelope.Create("saved-effect-fixture", 1, json);

    internal static AgentToolPreparedPayload Payload(int index) {
        var json = System.Text.Json.JsonSerializer.Serialize(new { index });
        return new("saved_effect_fixture_create", 2, AgentToolProtocolEnvelope.ComputeDigest(json), json,
            AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt, Envelope("{\"originalSource\":true}"));
    }

    internal static AgentToolApprovalBinding Binding(ExecutionRunRecord run, int index = 0) {
        var batch = run.ToolAdmission!.Batches[0];
        var proposal = batch.Proposals[index];
        return new(batch.Id, proposal.IntentId, proposal.Payload.SemanticVersion, proposal.Payload.Digest);
    }

    internal static PendingToolApprovalDecision[] Decisions(ExecutionRunRecord run, params bool[] approved)
        => run.PendingApprovals.Select((approval, index) => new PendingToolApprovalDecision(approval.ApprovalId, approved[index]) {
            ToolAdmission = approval.ToolAdmission
        }).ToArray();

    internal static AgentSavedEffectContinuationRequest Request(ExecutionRunRecord run, int selected = 0,
        IReadOnlyList<PendingToolApprovalDecision>? decisions = null)
        => new(run.Id, run.ChatSessionId, run.Revision, run.ToolAdmission!.Revision, Binding(run, selected), decisions ?? []);

    internal static AgentSavedEffectCancellationRequest Cancellation(ExecutionRunRecord run)
        => new(run.Id, run.ChatSessionId, run.Revision, run.ToolAdmission!.Revision);

    internal static void AssertOriginPreserved(AgentToolInvocationSegment expected, AgentToolInvocationSegment actual) {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Ordinal, actual.Ordinal);
        Assert.Equal(expected.FirstBatchOrdinal, actual.FirstBatchOrdinal);
        Assert.Equal(expected.OriginDispatchLeaseId, actual.OriginDispatchLeaseId);
        Assert.Equal(expected.CheckpointKind, actual.CheckpointKind);
        Assert.Equal(expected.ContinuesSegmentId, actual.ContinuesSegmentId);
        Assert.Null(actual.RestartCheckpoint);
        Assert.Null(actual.ApprovalCheckpoint);
        Assert.Equal(expected.PendingApprovals.ToArray(), actual.PendingApprovals.ToArray());
    }

    internal Task<ExecutionRunRecord?> ReadAsync() => Files.NewStore().GetExecutionRunAsync(Prepared.Id);

    internal static void AssertIoCause(Exception? failure, string message) {
        Assert.NotNull(failure);
        for (var current = failure; current is not null; current = current.InnerException) {
            if (current is IOException && current.Message == message) {
                return;
            }
        }
        Assert.Fail("The exact actual file-store fault was not observed.");
    }

    public ValueTask DisposeAsync() => Files.DisposeAsync();

    internal sealed class ReceiptReader(bool committed = false) : IAgentToolReceiptReconciliationProvider {
        internal int Calls { get; private set; }
        internal AgentToolReceiptReconciliationClaim? LastClaim { get; private set; }
        public bool Supports(string toolName) => toolName == Payload(0).ToolName;

        public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
            CancellationToken cancellationToken = default) {
            Calls++;
            LastClaim = claim;
            var admission = await claim.RequireAsync(cancellationToken);
            return committed
                ? new(new("saved-effect-fixture", admission.Binding.IntentId.Value.ToString("D")), Envelope("{\"accepted\":true}"))
                : AgentToolReceiptObservation.NotObserved;
        }
    }
}

public interface ISavedEffectFailureStore : ISandboxWorkspaceStore, ISandboxWorkspaceExecutionRunStore,
    ISandboxWorkspaceExecutionRunMutationStore, ISandboxWorkspaceExecutionRunLeaseStore {
}

public class SavedEffectFailureStore : DispatchProxy {
    internal FileSandboxWorkspaceStore Inner { get; set; } = null!;
    internal int Releases { get; private set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
        ArgumentNullException.ThrowIfNull(targetMethod);
        object? result;
        try {
            result = targetMethod.Invoke(Inner, args);
        } catch (TargetInvocationException failure) when (failure.InnerException is not null) {
            ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
            throw;
        }
        return targetMethod.Name == nameof(ISandboxWorkspaceExecutionRunLeaseStore.AcquireToolDispatchLeaseAsync)
            ? WrapAsync((ValueTask<IAsyncDisposable>)result!)
            : result;
    }

    private async ValueTask<IAsyncDisposable> WrapAsync(ValueTask<IAsyncDisposable> handle)
        => new ReleasedHandle(await handle, () => Releases++);

    private sealed class ReleasedHandle(IAsyncDisposable inner, Action released) : IAsyncDisposable {
        public async ValueTask DisposeAsync() {
            await inner.DisposeAsync();
            released();
            throw new IOException("saved-lease-cleanup");
        }
    }
}
