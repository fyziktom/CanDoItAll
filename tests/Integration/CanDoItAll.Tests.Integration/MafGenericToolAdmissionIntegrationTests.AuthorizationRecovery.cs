using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafGenericToolAdmissionIntegrationTests {
    [Theory]
    [InlineData(AgentToolProposalState.Executing, RecoveryDenialKind.Authorization)]
    [InlineData(AgentToolProposalState.Executing, RecoveryDenialKind.ReturnedNone)]
    [InlineData(AgentToolProposalState.Executing, RecoveryDenialKind.ReturnedNotCommitted)]
    [InlineData(AgentToolProposalState.Executing, RecoveryDenialKind.MappedNone)]
    [InlineData(AgentToolProposalState.Executing, RecoveryDenialKind.MappedNotCommitted)]
    [InlineData(AgentToolProposalState.Executing, RecoveryDenialKind.PreDispatch)]
    [InlineData(AgentToolProposalState.Executing, RecoveryDenialKind.CommittedPolicy)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, RecoveryDenialKind.Authorization)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, RecoveryDenialKind.ReturnedNone)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, RecoveryDenialKind.ReturnedNotCommitted)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, RecoveryDenialKind.MappedNone)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, RecoveryDenialKind.MappedNotCommitted)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, RecoveryDenialKind.PreDispatch)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, RecoveryDenialKind.CommittedPolicy)]
    public async Task Current_retry_denial_preserves_the_prior_owner_outcome_and_original_disclosure_evidence(
        AgentToolProposalState priorState, RecoveryDenialKind kind) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        using var client = new ScriptClient(complete: true);
        var effects = new List<string>();
        var runtime = CreateRuntime(client, effects, unsupportedResult: true);
        var denyAuthorization = false;
        ConfigureOwnerRecovery(runtime, () => denyAuthorization);
        var call = RecoveryCall();
        var digest = AgentToolProtocolEnvelope.ComputeDigest("original-owner-recovery-request");
        var evidence = AgentToolProtocolEnvelope.Create("fixture-original-owner-admission", 1, "{\"lifetime\":\"original\"}");
        var first = fixture.NewJournal();
        AgentToolProposalRecord original;
        await using (var lease = await first.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var opened = await OpenAsync(fixture, first, lease, runtime);
            using var active = opened.Context.Bind();
            await opened.Context.AdmitResponseAsync(digest, RecoveryResponse(call), [call], default);
            var batch = Assert.Single((await first.ReadAsync(lease, default)).Batches);
            var proposal = Assert.Single(batch.Proposals);
            var claim = await first.ClaimInvocationAsync(lease, batch.Id, call.CallId, proposal.Payload, default);
            if (priorState == AgentToolProposalState.ReconciliationRequired) {
                await first.CompleteInvocationAsync(claim,
                    AgentToolProtocolEnvelope.Create("fixture-original-owner-result", 1, "{\"effect\":\"already-committed\"}"),
                    AgentToolEffectState.Committed, default, requiresOwnerReconciliation: true, disclosureEvidence: evidence);
            }
            original = Assert.Single((await first.ReadAsync(lease, default)).Batches[0].Proposals);
            Assert.Equal(priorState, original.State);
        }

        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var restartLease = await restarted.AcquireRunAsync(fixture.Session, default);
        using var restartBound = restartLease.Bind();
        var restored = await OpenAsync(fixture, restarted, restartLease, runtime);
        using var restoredBound = restored.Context.Bind();
        Assert.NotNull(await restored.Context.ReplayResponseAsync(digest, default));
        denyAuthorization = kind == RecoveryDenialKind.Authorization;
        var dispatched = 0;
        using var effect = AgentToolInvocationEffectScope.Begin();
        await Assert.ThrowsAnyAsync<Exception>(async () => await restored.Context.InvokeAsync(call, _ => {
            dispatched++;
            var state = kind is RecoveryDenialKind.ReturnedNone or RecoveryDenialKind.MappedNone
                ? AgentToolEffectState.None : AgentToolEffectState.NotCommitted;
            if (kind == RecoveryDenialKind.CommittedPolicy) {
                AgentToolInvocationEffectScope.RecordCommitted("fixture-owner", "current-confirmed-receipt");
                throw new AgentToolPolicyBlockedException(call.Name, ToolInvocationDecisionKind.Deny, AuthorizationPrivateMessage);
            }
            if (kind is RecoveryDenialKind.MappedNone or RecoveryDenialKind.MappedNotCommitted) {
                throw new RecoveryBodyFailure(state);
            }
            if (kind == RecoveryDenialKind.PreDispatch) {
                AgentToolInvocationEffectScope.RecordPreDispatchFailure(new("FixtureCurrentDenial", "Current authority is unavailable."));
                return ValueTask.FromResult<object?>("Current authority is unavailable.");
            }
            return ValueTask.FromResult<object?>(new AgentToolFailureResult(false, "FixtureCurrentDenial", "Current authority is unavailable.", false) {
                EffectState = state
            });
        }, effect, default));

        var saved = Assert.Single((await restarted.ReadAsync(restartLease, default)).Batches[0].Proposals);
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, saved.State);
        Assert.Equal(original with { State = saved.State, DispatchClaimId = saved.DispatchClaimId }, saved);
        Assert.Equal(kind == RecoveryDenialKind.Authorization ? 0 : 1, dispatched);
        Assert.Equal(0, client.Requests);
        Assert.Empty(effects);
        var independent = await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId);
        Assert.Equal(saved, Assert.Single(independent!.ToolAdmission!.Batches[0].Proposals));
    }

    [Fact]
    public async Task Fresh_body_policy_exception_after_recorded_commit_cannot_be_saved_as_not_committed() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        using var client = new ScriptClient(complete: true);
        var runtime = CreateRuntime(client, [], unsupportedResult: true);
        ConfigureOwnerRecovery(runtime, () => false);
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var opened = await OpenAsync(fixture, journal, lease, runtime);
        using var active = opened.Context.Bind();
        var call = RecoveryCall();
        await opened.Context.AdmitResponseAsync(AgentToolProtocolEnvelope.ComputeDigest("fresh-committed-denial"),
            RecoveryResponse(call), [call], default);
        using var effect = AgentToolInvocationEffectScope.Begin();
        await Assert.ThrowsAsync<AgentToolPolicyBlockedException>(async () => await opened.Context.InvokeAsync(call, _ => {
            AgentToolInvocationEffectScope.RecordCommitted("fixture-owner", "confirmed-effect");
            throw new AgentToolPolicyBlockedException(call.Name, ToolInvocationDecisionKind.Deny, AuthorizationPrivateMessage);
        }, effect, default));
        var saved = Assert.Single((await journal.ReadAsync(lease, default)).Batches[0].Proposals);
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, saved.State);
        Assert.Equal(AgentToolEffectState.Unknown, saved.EffectState);
        Assert.Null(saved.Result);
        Assert.NotNull(effect.CommittedEffect);
        Assert.Equal(0, client.Requests);
    }

    [Theory]
    [InlineData((int)ExistingRunDetailCommitStage.JournalPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.RunPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.WorkspaceIndexPersisted)]
    public async Task Lost_completion_acknowledgment_keeps_the_saved_outcome_and_replays_without_another_effect(
        int stageValue) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        using var client = new ScriptClient(complete: true);
        var runtime = CreateRuntime(client, [], unsupportedResult: true);
        ConfigureOwnerRecovery(runtime, () => false);
        var loseAcknowledgment = false;
        var faultObserved = false;
        var store = fixture.NewStore(current => {
            if (loseAcknowledgment && current == (ExistingRunDetailCommitStage)stageValue) {
                loseAcknowledgment = false;
                faultObserved = true;
                throw new IOException("Injected completion acknowledgment loss.");
            }
        });
        var journal = fixture.NewJournal(store);
        var call = RecoveryCall();
        var digest = AgentToolProtocolEnvelope.ComputeDigest("lost-completion-acknowledgment");
        var evidence = AgentToolProtocolEnvelope.Create("fixture-original-owner-admission", 1, "{\"original\":true}");
        var dispatched = 0;
        AgentToolProposalRecord original;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var opened = await OpenAsync(fixture, journal, lease, runtime);
            using var active = opened.Context.Bind();
            await opened.Context.AdmitResponseAsync(digest, RecoveryResponse(call), [call], default);
            using var effect = AgentToolInvocationEffectScope.Begin();
            await Assert.ThrowsAsync<IOException>(async () => await opened.Context.InvokeAsync(call, _ => {
                dispatched++;
                AgentToolInvocationEffectScope.RecordCommitted("fixture-owner", "original-effect");
                AgentToolInvocationEffectScope.RecordDisclosureEvidence(evidence);
                loseAcknowledgment = true;
                return ValueTask.FromResult<object?>("saved-owner-result");
            }, effect, default));
            original = Assert.Single((await journal.ReadAsync(lease, default)).Batches[0].Proposals);
            Assert.True(faultObserved);
            Assert.Equal(AgentToolProposalState.Completed, original.State);
            Assert.Equal(AgentToolEffectState.Committed, original.EffectState);
            Assert.Equal(evidence, original.DisclosureEvidence);
        }

        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var restartLease = await restarted.AcquireRunAsync(fixture.Session, default);
        using var restartBound = restartLease.Bind();
        var restored = await OpenAsync(fixture, restarted, restartLease, runtime);
        using var restoredBound = restored.Context.Bind();
        Assert.NotNull(await restored.Context.ReplayResponseAsync(digest, default));
        using var restoredEffect = AgentToolInvocationEffectScope.Begin();
        var result = await restored.Context.InvokeAsync(call, _ => {
            dispatched++;
            return ValueTask.FromResult<object?>("duplicate-must-not-run");
        }, restoredEffect, default);
        Assert.Equal("saved-owner-result", result);
        Assert.Equal(original, Assert.Single((await restarted.ReadAsync(restartLease, default)).Batches[0].Proposals));
        Assert.Equal(1, dispatched);
        Assert.Equal(0, client.Requests);
    }

    private static FunctionCallContent RecoveryCall() => new("original-owner-call", "generic_fixture_write", new Dictionary<string, object?>());

    private static AgentToolProtocolEnvelope RecoveryResponse(FunctionCallContent call)
        => MafToolProtocolCodec.Encode(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])));

    private static void ConfigureOwnerRecovery(Runtime runtime, Func<bool> denyAuthorization) {
        runtime.Capabilities.RuntimeToolMetadata.Add(new("owner-recovery-fixture", "generic_fixture_write",
            AgentRuntimeToolOperationKind.Mutation, false) {
            PrepareAdmission = input => new("generic_fixture_write", 1, MafToolProtocolCodec.Digest(input),
                MafToolProtocolCodec.Canonicalize(input), AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt),
            AuthorizeAdmissionAsync = (_, _) => denyAuthorization()
                ? throw new AgentToolAdmissionException("fixture.owner-denied", AuthorizationPrivateMessage)
                : ValueTask.FromResult<IAsyncDisposable>(new RecoveryScope()),
            AuthorizeResultDisclosureAsync = (_, _) => ValueTask.FromResult<IAsyncDisposable?>(null)
        });
    }

    public enum RecoveryDenialKind { Authorization, ReturnedNone, ReturnedNotCommitted, MappedNone, MappedNotCommitted, PreDispatch, CommittedPolicy }

    private sealed class RecoveryScope : IAsyncDisposable {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecoveryBodyFailure(AgentToolEffectState state) : Exception(AuthorizationPrivateMessage), IAgentToolFailureEffectEvidence {
        public string ErrorCode => "FixtureCurrentDenial";
        public string SafeMessage => "Current authority is unavailable.";
        public bool IsSafeToExpose => true;
        public bool CanRetryWithCorrectedInput => false;
        public AgentToolEffectState EffectState => state;
    }
}
