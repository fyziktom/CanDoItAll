using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafGenericToolAdmissionIntegrationTests {
    private const string RecoveryReadTool = "generic_fixture_read";
    private const string RecoveryReadFailureCode = "fixture.record-not-found";
    private const string RecoveryReadFailureMessage = "The requested record was not found.";

    [Theory]
    [InlineData(AgentToolProposalState.Executing, false)]
    [InlineData(AgentToolProposalState.Executing, true)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, false)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, true)]
    public async Task Authorized_read_retry_can_checkpoint_a_known_no_effect_failure_without_replacing_original_identity(
        AgentToolProposalState priorState, bool mapped) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        using var client = new ScriptClient(complete: true);
        var runtime = CreateRuntime(client, []);
        var disclosures = new List<AgentToolResultDisclosure>();
        ConfigureReadRecovery(runtime, () => false, disclosures);
        var call = new FunctionCallContent("original-read-call", RecoveryReadTool, new Dictionary<string, object?>());
        var digest = AgentToolProtocolEnvelope.ComputeDigest("original-read-request");
        var original = await SaveUncertainReadAsync(fixture, runtime, call, digest, priorState);
        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var lease = await restarted.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var opened = await OpenAsync(fixture, restarted, lease, runtime);
        using var active = opened.Context.Bind();
        Assert.NotNull(await opened.Context.ReplayResponseAsync(digest, default));
        var dispatched = 0;
        using var effect = AgentToolInvocationEffectScope.Begin();
        var result = await opened.Context.InvokeAsync(call, _ => {
            dispatched++;
            if (mapped) {
                throw new ExpectedReadFailure();
            }
            return ValueTask.FromResult<object?>(ReadFailureResult());
        }, effect, default);

        Assert.Equal(ReadFailureResult(), Assert.IsType<AgentToolFailureResult>(result));
        var journal = await restarted.ReadAsync(lease, default);
        var saved = Assert.Single(Assert.Single(journal.Batches).Proposals);
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
        Assert.Equal(AgentToolEffectState.None, saved.EffectState);
        Assert.NotNull(saved.Result);
        Assert.Equal(original with {
            State = saved.State, EffectState = saved.EffectState, Result = saved.Result, DispatchClaimId = saved.DispatchClaimId
        }, saved);
        Assert.False(journal.HasUnresolvedEffects);
        var independent = await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId);
        Assert.Equal(saved, Assert.Single(Assert.Single(independent!.ToolAdmission!.Batches).Proposals));

        using var replayEffect = AgentToolInvocationEffectScope.Begin();
        var replayed = await opened.Context.InvokeAsync(call, _ => {
            dispatched++;
            throw new InvalidOperationException("Completed read must replay its saved result.");
        }, replayEffect, default);
        Assert.Equal(result, replayed);
        Assert.Equal(1, dispatched);
        Assert.Equal(0, client.Requests);
        Assert.True(Assert.Single(disclosures).IsTypedFailure);
        Assert.Equal(saved, Assert.Single(Assert.Single((await restarted.ReadAsync(lease, default)).Batches).Proposals));
    }

    [Theory]
    [InlineData(AgentToolProposalState.Executing, ReadRetryDenial.Authorization)]
    [InlineData(AgentToolProposalState.Executing, ReadRetryDenial.ReturnedNotCommitted)]
    [InlineData(AgentToolProposalState.Executing, ReadRetryDenial.ReturnedUnknown)]
    [InlineData(AgentToolProposalState.Executing, ReadRetryDenial.ReturnedCommitted)]
    [InlineData(AgentToolProposalState.Executing, ReadRetryDenial.MappedNotCommitted)]
    [InlineData(AgentToolProposalState.Executing, ReadRetryDenial.PreDispatch)]
    [InlineData(AgentToolProposalState.Executing, ReadRetryDenial.CommittedPolicy)]
    [InlineData(AgentToolProposalState.Executing, ReadRetryDenial.BodyPolicy)]
    [InlineData(AgentToolProposalState.Executing, ReadRetryDenial.Infrastructure)]
    [InlineData(AgentToolProposalState.Executing, ReadRetryDenial.Cancellation)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, ReadRetryDenial.Authorization)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, ReadRetryDenial.ReturnedNotCommitted)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, ReadRetryDenial.ReturnedUnknown)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, ReadRetryDenial.ReturnedCommitted)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, ReadRetryDenial.MappedNotCommitted)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, ReadRetryDenial.PreDispatch)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, ReadRetryDenial.CommittedPolicy)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, ReadRetryDenial.BodyPolicy)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, ReadRetryDenial.Infrastructure)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, ReadRetryDenial.Cancellation)]
    public async Task Read_retry_denial_or_unproven_failure_keeps_the_original_journal_uncertain(
        AgentToolProposalState priorState, ReadRetryDenial denial) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        using var client = new ScriptClient(complete: true);
        var runtime = CreateRuntime(client, []);
        var denyAuthorization = false;
        ConfigureReadRecovery(runtime, () => denyAuthorization, []);
        var call = new FunctionCallContent("uncertain-read-call", RecoveryReadTool, new Dictionary<string, object?>());
        var digest = AgentToolProtocolEnvelope.ComputeDigest("uncertain-read-request");
        var original = await SaveUncertainReadAsync(fixture, runtime, call, digest, priorState);
        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var lease = await restarted.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var opened = await OpenAsync(fixture, restarted, lease, runtime);
        using var active = opened.Context.Bind();
        Assert.NotNull(await opened.Context.ReplayResponseAsync(digest, default));
        denyAuthorization = denial == ReadRetryDenial.Authorization;
        var dispatched = 0;
        using var effect = AgentToolInvocationEffectScope.Begin();
        await Assert.ThrowsAnyAsync<Exception>(async () => await opened.Context.InvokeAsync(call, _ => {
            dispatched++;
            switch (denial) {
                case ReadRetryDenial.ReturnedUnknown:
                    return ValueTask.FromResult<object?>(ReadFailureResult() with { EffectState = AgentToolEffectState.Unknown });
                case ReadRetryDenial.ReturnedCommitted:
                    return ValueTask.FromResult<object?>(ReadFailureResult() with { EffectState = AgentToolEffectState.Committed });
                case ReadRetryDenial.MappedNotCommitted:
                    throw new RecoveryBodyFailure(AgentToolEffectState.NotCommitted);
                case ReadRetryDenial.PreDispatch:
                    AgentToolInvocationEffectScope.RecordPreDispatchFailure(new("FixtureCurrentDenial", "Current authority is unavailable."));
                    return ValueTask.FromResult<object?>(ReadFailureResult());
                case ReadRetryDenial.CommittedPolicy:
                    AgentToolInvocationEffectScope.RecordCommitted("fixture-owner", "confirmed-effect");
                    throw new AgentToolPolicyBlockedException(call.Name, ToolInvocationDecisionKind.Deny, AuthorizationPrivateMessage);
                case ReadRetryDenial.BodyPolicy:
                    throw new AgentToolPolicyBlockedException(call.Name, ToolInvocationDecisionKind.Deny, AuthorizationPrivateMessage);
                case ReadRetryDenial.Infrastructure:
                    throw new IOException(AuthorizationPrivateMessage);
                case ReadRetryDenial.Cancellation:
                    throw new OperationCanceledException(AuthorizationPrivateMessage);
                default:
                    return ValueTask.FromResult<object?>(ReadFailureResult() with { EffectState = AgentToolEffectState.NotCommitted });
            }
        }, effect, default));

        var saved = Assert.Single(Assert.Single((await restarted.ReadAsync(lease, default)).Batches).Proposals);
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, saved.State);
        Assert.Equal(original with { State = saved.State, DispatchClaimId = saved.DispatchClaimId }, saved);
        Assert.Equal(denial == ReadRetryDenial.Authorization ? 0 : 1, dispatched);
        Assert.Equal(0, client.Requests);
        Assert.Equal(saved, Assert.Single(Assert.Single((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!
            .ToolAdmission!.Batches).Proposals));
    }

    [Theory]
    [InlineData(AgentToolProposalState.Executing)]
    [InlineData(AgentToolProposalState.ReconciliationRequired)]
    public async Task Read_label_cannot_bypass_a_reconcile_before_retry_contract(AgentToolProposalState priorState) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        using var client = new ScriptClient(complete: true);
        var runtime = CreateRuntime(client, []);
        ConfigureReadRecovery(runtime, () => false, [], AgentToolProposalRecovery.ReconcileBeforeRetry);
        var call = new FunctionCallContent("non-retryable-read-call", RecoveryReadTool, new Dictionary<string, object?>());
        var digest = AgentToolProtocolEnvelope.ComputeDigest("non-retryable-read-request");
        var original = await SaveUncertainReadAsync(fixture, runtime, call, digest, priorState);
        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var lease = await restarted.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();

        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => OpenAsync(fixture, restarted, lease, runtime));

        Assert.Equal("tool-admission.reconciliation-required", denied.Code);
        Assert.Equal(original, Assert.Single(Assert.Single((await restarted.ReadAsync(lease, default)).Batches).Proposals));
        Assert.Equal(0, client.Requests);
    }

    private static async Task<AgentToolProposalRecord> SaveUncertainReadAsync(AgentToolAdmissionJournalFixture fixture,
        Runtime runtime, FunctionCallContent call, AgentToolSemanticDigest digest, AgentToolProposalState priorState) {
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var opened = await OpenAsync(fixture, journal, lease, runtime);
        using var active = opened.Context.Bind();
        await opened.Context.AdmitResponseAsync(digest, RecoveryResponse(call), [call], default);
        var batch = Assert.Single((await journal.ReadAsync(lease, default)).Batches);
        var proposal = Assert.Single(batch.Proposals);
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, call.CallId, proposal.Payload, default);
        if (priorState == AgentToolProposalState.ReconciliationRequired) {
            await journal.PreserveUncertainInvocationAsync(claim, default);
        }
        var original = Assert.Single(Assert.Single((await journal.ReadAsync(lease, default)).Batches).Proposals);
        Assert.Equal(priorState, original.State);
        Assert.Equal(AgentToolEffectState.Unknown, original.EffectState);
        Assert.Null(original.Result);
        return original;
    }

    private static void ConfigureReadRecovery(Runtime runtime, Func<bool> denyAuthorization,
        List<AgentToolResultDisclosure> disclosures, AgentToolProposalRecovery recovery = AgentToolProposalRecovery.RevalidateAndRead) {
        runtime.Capabilities.RuntimeToolMetadata.Add(new("read-recovery-fixture", RecoveryReadTool,
            AgentRuntimeToolOperationKind.Read, false) {
            PrepareAdmission = input => new(RecoveryReadTool, 1, MafToolProtocolCodec.Digest(input),
                MafToolProtocolCodec.Canonicalize(input), AgentToolProposalEffect.Read, recovery),
            AuthorizeAdmissionAsync = (_, _) => denyAuthorization()
                ? throw new AgentToolAdmissionException("fixture.read-denied", AuthorizationPrivateMessage)
                : ValueTask.FromResult<IAsyncDisposable>(new RecoveryScope()),
            AuthorizeResultDisclosureAsync = (disclosure, _) => {
                disclosures.Add(disclosure);
                return ValueTask.FromResult<IAsyncDisposable?>(null);
            }
        });
    }

    private static AgentToolFailureResult ReadFailureResult() => new(false, RecoveryReadFailureCode, RecoveryReadFailureMessage, true) {
        EffectState = AgentToolEffectState.None
    };

    public enum ReadRetryDenial { Authorization, ReturnedNotCommitted, ReturnedUnknown, ReturnedCommitted, MappedNotCommitted, PreDispatch, CommittedPolicy, BodyPolicy, Infrastructure, Cancellation }

    private sealed class ExpectedReadFailure() : Exception(RecoveryReadFailureMessage), IAgentToolFailureEffectEvidence {
        public string ErrorCode => RecoveryReadFailureCode;
        public string SafeMessage => Message;
        public bool IsSafeToExpose => true;
        public bool CanRetryWithCorrectedInput => true;
        public AgentToolEffectState EffectState => AgentToolEffectState.None;
    }
}
