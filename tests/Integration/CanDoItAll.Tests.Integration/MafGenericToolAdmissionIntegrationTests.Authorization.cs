using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafGenericToolAdmissionIntegrationTests {
    [Theory]
    [InlineData(AuthorizationFailureKind.OwnerDenial, false)]
    [InlineData(AuthorizationFailureKind.OwnerDenial, true)]
    [InlineData(AuthorizationFailureKind.PolicyDenial, false)]
    [InlineData(AuthorizationFailureKind.PolicyDenial, true)]
    [InlineData(AuthorizationFailureKind.AccessDenial, false)]
    [InlineData(AuthorizationFailureKind.AccessDenial, true)]
    public async Task Explicit_authorization_denial_is_durable_before_dispatch_and_replays_without_a_second_attempt(
        AuthorizationFailureKind kind, bool streaming) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var effects = new List<string>();
        var authorizationAttempts = 0;
        AgentToolProposalRecord? original = null;
        for (var attempt = 0; attempt < 2; attempt++) {
            var journal = fixture.NewJournal(fixture.NewStore());
            await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
            using var bound = lease.Bind();
            using var client = new AuthorizationClient(denyRequests: attempt != 0);
            var runtime = CreateRuntime(client, effects, unsupportedResult: true);
            runtime.Capabilities.RuntimeToolMetadata.Add(new("authorization-fixture", "generic_fixture_write",
                AgentRuntimeToolOperationKind.Mutation, false) {
                AuthorizeAdmissionAsync = (_, _) => {
                    authorizationAttempts++;
                    throw AuthorizationFailure(kind);
                },
                AuthorizeResultDisclosureAsync = (_, _) => ValueTask.FromResult<IAsyncDisposable?>(null)
            });
            var opened = await OpenAsync(fixture, journal, lease, runtime);
            using var active = opened.Context.Bind();
            Assert.Equal("finished", (await RunAsync(runtime.Agent, opened.Session, opened.Input, streaming)).Text);
            var proposal = Assert.Single((await journal.ReadAsync(lease, default)).Batches.SelectMany(batch => batch.Proposals));
            Assert.Equal(AgentToolProposalState.Completed, proposal.State);
            Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
            var checkpoint = Assert.IsType<AgentToolProtocolEnvelope>(proposal.Result);
            Assert.Contains("ToolPolicyDenied", checkpoint.PayloadJson, StringComparison.Ordinal);
            Assert.DoesNotContain(AuthorizationPrivateMessage, checkpoint.PayloadJson, StringComparison.Ordinal);
            Assert.Null(proposal.DisclosureEvidence);
            Assert.Equal(attempt == 0 ? 2 : 0, client.Requests);
            if (original is null) {
                original = proposal;
            } else {
                Assert.Equal(original, proposal);
            }
        }
        Assert.Equal(1, authorizationAttempts);
        Assert.Empty(effects);
    }

    [Theory]
    [InlineData(AuthorizationFailureKind.Unexpected, false)]
    [InlineData(AuthorizationFailureKind.Unexpected, true)]
    [InlineData(AuthorizationFailureKind.Cancellation, false)]
    [InlineData(AuthorizationFailureKind.Cancellation, true)]
    [InlineData(AuthorizationFailureKind.CommittedDenial, false)]
    [InlineData(AuthorizationFailureKind.CommittedDenial, true)]
    public async Task Unexpected_authorization_failure_or_conflicting_committed_evidence_remains_explicitly_unresolved(
        AuthorizationFailureKind kind, bool streaming) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var effects = new List<string>();
        using var client = new AuthorizationClient(denyRequests: false);
        var runtime = CreateRuntime(client, effects, unsupportedResult: true);
        runtime.Capabilities.RuntimeToolMetadata.Add(new("authorization-fixture", "generic_fixture_write",
            AgentRuntimeToolOperationKind.Mutation, false) {
            AuthorizeAdmissionAsync = (_, _) => {
                if (kind == AuthorizationFailureKind.CommittedDenial) {
                    AgentToolInvocationEffectScope.RecordCommitted("fixture-owner", "already-committed-effect");
                }
                throw AuthorizationFailure(kind);
            },
            AuthorizeResultDisclosureAsync = (_, _) => ValueTask.FromResult<IAsyncDisposable?>(null)
        });
        var journal = fixture.NewJournal();
        AgentToolProposalRecord original;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var opened = await OpenAsync(fixture, journal, lease, runtime);
            using var active = opened.Context.Bind();
            await Assert.ThrowsAnyAsync<Exception>(() => RunAsync(runtime.Agent, opened.Session, opened.Input, streaming));
            original = Assert.Single((await journal.ReadAsync(lease, default)).Batches.SelectMany(batch => batch.Proposals));
            Assert.Equal(AgentToolProposalState.ReconciliationRequired, original.State);
            Assert.Equal(AgentToolEffectState.Unknown, original.EffectState);
            Assert.Null(original.Result);
            Assert.Null(original.DisclosureEvidence);
        }
        Assert.Equal(1, client.Requests);
        Assert.Empty(effects);
        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var restartLease = await restarted.AcquireRunAsync(fixture.Session, default);
        using var restartBound = restartLease.Bind();
        using var never = new AuthorizationClient(denyRequests: true);
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
            OpenAsync(fixture, restarted, restartLease, CreateRuntime(never, effects, unsupportedResult: true)));
        Assert.Equal("tool-admission.reconciliation-required", denied.Code);
        Assert.Equal(original, Assert.Single((await restarted.ReadAsync(restartLease, default)).Batches.SelectMany(batch => batch.Proposals)));
        Assert.Equal(0, never.Requests);
        Assert.Empty(effects);
    }

    private const string AuthorizationPrivateMessage = "Private authorization detail must not be copied into the durable tool result.";

    private static Exception AuthorizationFailure(AuthorizationFailureKind kind) => kind switch {
        AuthorizationFailureKind.OwnerDenial => new AgentToolAdmissionException("fixture.owner-denied", AuthorizationPrivateMessage),
        AuthorizationFailureKind.PolicyDenial or AuthorizationFailureKind.CommittedDenial =>
            new AgentToolPolicyBlockedException("generic_fixture_write", ToolInvocationDecisionKind.Deny, AuthorizationPrivateMessage),
        AuthorizationFailureKind.AccessDenial => new UnauthorizedAccessException(AuthorizationPrivateMessage),
        AuthorizationFailureKind.Unexpected => new InvalidOperationException(AuthorizationPrivateMessage),
        AuthorizationFailureKind.Cancellation => new OperationCanceledException(AuthorizationPrivateMessage),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public enum AuthorizationFailureKind { OwnerDenial, PolicyDenial, AccessDenial, Unexpected, Cancellation, CommittedDenial }

    private sealed class AuthorizationClient(bool denyRequests) : IChatClient {
        internal int Requests { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            Assert.False(denyRequests, "The durable response must replay without another provider call.");
            Requests++;
            Assert.InRange(Requests, 1, 2);
            return Task.FromResult(new ChatResponse(Requests == 1
                ? new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("authorization", "generic_fixture_write", new Dictionary<string, object?>())])
                : new ChatMessage(ChatRole.Assistant, "finished")) { ResponseId = $"authorization-fixture-{Requests}" });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates()) {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() {
        }
    }
}
