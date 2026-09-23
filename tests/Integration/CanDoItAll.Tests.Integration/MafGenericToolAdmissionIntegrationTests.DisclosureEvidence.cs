using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafGenericToolAdmissionIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cached_owner_evidence_survives_restart_and_denial_without_entering_the_tool_result(bool legacyWithoutEvidence) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var effects = new List<string>();
        using var client = new ScriptClient(complete: true);
        var runtime = CreateRuntime(client, effects);
        var originalEvidence = AgentToolProtocolEnvelope.Create("fixture-original-admissions", 1,
            JsonSerializer.Serialize(new { profile = Guid.NewGuid(), targets = new[] { Guid.NewGuid(), Guid.NewGuid() } }));
        var currentReadAllowed = true;
        var observed = new List<AgentToolProtocolEnvelope>();
        runtime.Capabilities.RuntimeToolMetadata.Add(new("fixture-provider", "generic_fixture_read",
            AgentRuntimeToolOperationKind.Read, false) {
            AuthorizeResultDisclosureAsync = (disclosure, _) => {
                if (disclosure.Evidence is not { } evidence) {
                    throw new AgentToolAdmissionException("tool-admission.disclosure-authorization-unavailable",
                        "The original owner admission is absent; the saved result cannot be restamped.");
                }
                Assert.Equal(originalEvidence, evidence);
                observed.Add(evidence);
                if (!currentReadAllowed) {
                    throw new AgentToolAdmissionException("tool-admission.disclosure-denied", "The original target is no longer readable.");
                }
                Assert.Equal("public-original-result", disclosure.Result.GetString());
                return ValueTask.FromResult<IAsyncDisposable?>(null);
            }
        });
        var call = new FunctionCallContent("read", "generic_fixture_read", new Dictionary<string, object?>());
        var digest = AgentToolProtocolEnvelope.ComputeDigest("owner-evidence-request");
        var journal = fixture.NewJournal();
        AgentToolProposalRecord original;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var opened = await OpenAsync(fixture, journal, lease, runtime);
            using var active = opened.Context.Bind();
            await opened.Context.AdmitResponseAsync(digest, AgentToolAdmissionJournalFixture.Envelope(), [call], default);
            using var effect = AgentToolInvocationEffectScope.Begin();
            var returned = await opened.Context.InvokeAsync(call, _ => {
                effects.Add("original");
                if (!legacyWithoutEvidence) {
                    AgentToolInvocationEffectScope.RecordDisclosureEvidence(originalEvidence);
                }
                return ValueTask.FromResult<object?>("public-original-result");
            }, effect, default);
            Assert.Equal("public-original-result", returned);
            original = Assert.Single((await journal.ReadAsync(lease, default)).Batches[0].Proposals);
            Assert.Equal(legacyWithoutEvidence ? null : originalEvidence, original.DisclosureEvidence);
            Assert.DoesNotContain(originalEvidence.PayloadJson, original.Result!.PayloadJson, StringComparison.Ordinal);
            Assert.Equal(AgentToolProposalState.Completed, original.State);
        }

        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var restartLease = await restarted.AcquireRunAsync(fixture.Session, default);
        using var restartBound = restartLease.Bind();
        currentReadAllowed = false;
        var denial = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => OpenAsync(fixture, restarted, restartLease, runtime));
        Assert.Equal(legacyWithoutEvidence ? "tool-admission.disclosure-authorization-unavailable" : "tool-admission.disclosure-denied", denial.Code);
        Assert.Equal(original, Assert.Single((await restarted.ReadAsync(restartLease, default)).Batches[0].Proposals));

        currentReadAllowed = true;
        if (legacyWithoutEvidence) {
            var stillUnavailable = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => OpenAsync(fixture, restarted, restartLease, runtime));
            Assert.Equal("tool-admission.disclosure-authorization-unavailable", stillUnavailable.Code);
            Assert.Empty(observed);
        } else {
            var restored = await OpenAsync(fixture, restarted, restartLease, runtime);
            using var active = restored.Context.Bind();
            Assert.NotNull(await restored.Context.ReplayResponseAsync(digest, default));
            using var effect = AgentToolInvocationEffectScope.Begin();
            var replayed = await restored.Context.InvokeAsync(call, _ => {
                effects.Add("duplicate");
                return ValueTask.FromResult<object?>("must-not-run");
            }, effect, default);
            Assert.Equal("public-original-result", replayed);
            Assert.True(observed.Count >= 3);
        }
        Assert.Equal(original, Assert.Single((await restarted.ReadAsync(restartLease, default)).Batches[0].Proposals));
        Assert.Equal(["original"], effects);
        Assert.Equal(0, client.Requests);
    }

    [Fact]
    public async Task Explicit_owner_uncertainty_keeps_disclosure_evidence_with_the_original_returned_checkpoint() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var effects = new List<string>();
        using var client = new ScriptClient(complete: true);
        var runtime = CreateRuntime(client, effects, unsupportedResult: true);
        runtime.Capabilities.ToolPolicies = new([new("generic_fixture_write", ToolInvocationClassification.Mutation,
            false, true, ToolCapabilitySideEffectKind.InternalStateMutation, ToolCapabilityOperationRequirementKind.None,
            [], [], true, false, false, false, ToolCapabilityBrowserProofRole.None, ToolCapabilityIdempotencyDescriptor.StateChanging)]);
        var evidence = AgentToolProtocolEnvelope.Create("fixture-original-admission", 1, "{\"originalLifetime\":\"retained\"}");
        var result = CreateObservedResult(OwnerObservationResult.WorkflowPending);
        var call = new FunctionCallContent("write", "generic_fixture_write", new Dictionary<string, object?>());
        var journal = fixture.NewJournal();
        AgentToolProposalRecord original;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var opened = await OpenAsync(fixture, journal, lease, runtime);
            using var active = opened.Context.Bind();
            await opened.Context.AdmitResponseAsync(AgentToolProtocolEnvelope.ComputeDigest("uncertain-owner-evidence"),
                AgentToolAdmissionJournalFixture.Envelope(), [call], default);
            using var effect = AgentToolInvocationEffectScope.Begin();
            await opened.Context.InvokeAsync(call, _ => {
                effects.Add("original");
                AgentToolInvocationEffectScope.RecordDisclosureEvidence(evidence);
                return ValueTask.FromResult<object?>(result);
            }, effect, default);
            original = Assert.Single((await journal.ReadAsync(lease, default)).Batches[0].Proposals);
            Assert.Equal(evidence, original.DisclosureEvidence);
            Assert.NotNull(original.Result);
            Assert.Equal(AgentToolProposalState.ReconciliationRequired, original.State);
            Assert.Equal(AgentToolEffectState.Unknown, original.EffectState);
        }

        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var restartLease = await restarted.AcquireRunAsync(fixture.Session, default);
        using var restartBound = restartLease.Bind();
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => OpenAsync(fixture, restarted, restartLease, runtime));
        Assert.Equal("tool-admission.reconciliation-required", denied.Code);
        Assert.Equal(original, Assert.Single((await restarted.ReadAsync(restartLease, default)).Batches[0].Proposals));
        Assert.Equal(["original"], effects);
        Assert.Equal(0, client.Requests);
    }
}
