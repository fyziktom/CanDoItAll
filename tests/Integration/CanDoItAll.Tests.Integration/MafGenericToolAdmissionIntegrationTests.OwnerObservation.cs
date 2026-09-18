using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafGenericToolAdmissionIntegrationTests {
    [Theory]
    [InlineData(OwnerObservationResult.WorkflowPending, true)]
    [InlineData(OwnerObservationResult.WorkflowRecovered, false)]
    [InlineData(OwnerObservationResult.ProcessPending, true)]
    [InlineData(OwnerObservationResult.ProcessStarted, false)]
    [InlineData(OwnerObservationResult.OrdinaryAcknowledgedMutation, false)]
    public async Task Explicit_owner_uncertainty_preserves_the_returned_checkpoint_and_blocks_restart_redispatch(
        OwnerObservationResult resultKind, bool requiresReconciliation) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var effects = new List<string>();
        using var client = new ScriptClient(complete: true);
        var runtime = CreateRuntime(client, effects, unsupportedResult: true);
        runtime.Capabilities.ToolPolicies = new([new("generic_fixture_write", ToolInvocationClassification.Mutation,
            false, true, ToolCapabilitySideEffectKind.InternalStateMutation, ToolCapabilityOperationRequirementKind.None,
            [], [], true, false, false, false, ToolCapabilityBrowserProofRole.None, ToolCapabilityIdempotencyDescriptor.StateChanging)]);
        runtime.Capabilities.RuntimeToolMetadata.Add(new("fixture-provider", "generic_fixture_write",
            AgentRuntimeToolOperationKind.Mutation, false) {
            AuthorizeResultDisclosureAsync = (_, _) => ValueTask.FromResult<IAsyncDisposable?>(null)
        });
        var result = CreateObservedResult(resultKind);
        var expected = JsonSerializer.SerializeToElement(result, MafToolProtocolCodec.SerializationOptions);
        var call = new FunctionCallContent("effect", "generic_fixture_write", new Dictionary<string, object?>());
        var requestDigest = AgentToolProtocolEnvelope.ComputeDigest("owner-observation-request");
        var journal = fixture.NewJournal();
        AgentToolProtocolEnvelope checkpoint;
        AgentToolBusinessIntentId intent;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var opened = await OpenAsync(fixture, journal, lease, runtime);
            using var active = opened.Context.Bind();
            await opened.Context.AdmitResponseAsync(requestDigest, AgentToolAdmissionJournalFixture.Envelope(), [call], default);
            using var effectScope = AgentToolInvocationEffectScope.Begin();
            var returned = await opened.Context.InvokeAsync(call, _ => {
                effects.Add("original");
                return ValueTask.FromResult<object?>(result);
            }, effectScope, default);
            Assert.True(JsonElement.DeepEquals(expected, Assert.IsType<JsonElement>(returned)));
            var saved = await journal.ReadAsync(lease, default);
            var proposal = Assert.Single(saved.Batches[0].Proposals);
            intent = proposal.IntentId;
            checkpoint = Assert.IsType<AgentToolProtocolEnvelope>(proposal.Result);
            using var envelope = JsonDocument.Parse(checkpoint.PayloadJson);
            var valueProperty = MafToolProtocolCodec.SerializationOptions.PropertyNamingPolicy?.ConvertName("Value") ?? "Value";
            var value = envelope.RootElement.GetProperty("Value").GetProperty(valueProperty);
            Assert.True(JsonElement.DeepEquals(expected, value));
            Assert.Equal(AgentToolEffectState.Unknown, proposal.EffectState);
            Assert.Equal(requiresReconciliation ? AgentToolProposalState.ReconciliationRequired : AgentToolProposalState.Completed,
                proposal.State);
            Assert.Equal(requiresReconciliation, saved.HasUnresolvedEffects);
        }

        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var replayLease = await restarted.AcquireRunAsync(fixture.Session, default);
        using var replayBound = replayLease.Bind();
        if (requiresReconciliation) {
            var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
                OpenAsync(fixture, restarted, replayLease, runtime));
            Assert.Equal("tool-admission.reconciliation-required", denied.Code);
        } else {
            var replay = await OpenAsync(fixture, restarted, replayLease, runtime);
            using var replayActive = replay.Context.Bind();
            Assert.NotNull(await replay.Context.ReplayResponseAsync(requestDigest, default));
            using var effectScope = AgentToolInvocationEffectScope.Begin();
            var returned = await replay.Context.InvokeAsync(call, _ => {
                effects.Add("duplicate");
                return ValueTask.FromResult<object?>(new { Succeeded = true });
            }, effectScope, default);
            Assert.True(JsonElement.DeepEquals(expected, Assert.IsType<JsonElement>(returned)));
        }
        var retained = Assert.Single((await restarted.ReadAsync(replayLease, default)).Batches[0].Proposals);
        Assert.Equal(intent, retained.IntentId);
        Assert.Equal(checkpoint, retained.Result);
        Assert.Equal(["original"], effects);
        Assert.Equal(0, client.Requests);
    }

    private static object CreateObservedResult(OwnerObservationResult kind) {
        var runId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        return kind switch {
            OwnerObservationResult.WorkflowPending or OwnerObservationResult.WorkflowRecovered =>
                new WorkflowAgentStartResult(new(runId, Guid.NewGuid(), Guid.NewGuid(), WorkflowRunState.Running,
                    WorkflowRuntimeBackendKind.InProcess, "retained original run", now, now, null),
                    WorkflowAgentDefinitionSelectionMode.ExactSavedVersion, WorkflowRuntimeBackendKind.InProcess,
                    WorkflowLaunchIdempotencyDisposition.EnforcedNewRun, "Observe the saved admission.") {
                    Observation = kind == OwnerObservationResult.WorkflowPending ? WorkflowLaunchObservation.AdmissionReceiptPending
                        : WorkflowLaunchObservation.RecoveredAfterObserverFailure
                },
            OwnerObservationResult.ProcessPending or OwnerObservationResult.ProcessStarted =>
                new ProjectStructureProcessNodeStartResult(Guid.NewGuid(), "selected-node", Guid.NewGuid(), Guid.NewGuid(),
                    runId, "accepted", "/processes", null, []) {
                    Observation = new(new(Guid.NewGuid()), new(runId), kind == OwnerObservationResult.ProcessPending
                        ? ProcessLaunchContinuationState.ReconciliationRequired : ProcessLaunchContinuationState.Started,
                        ProcessLaunchLinkDeliveryState.Delivered, "Original owner observation.")
                },
            OwnerObservationResult.OrdinaryAcknowledgedMutation => new { Succeeded = true, NativeId = runId },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    public enum OwnerObservationResult {
        WorkflowPending,
        WorkflowRecovered,
        ProcessPending,
        ProcessStarted,
        OrdinaryAcknowledgedMutation
    }
}
