using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Support.AgentPlanningDecisionTestData;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafPlanningStepRuntimeTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Real_sdk_steps_use_only_original_request_and_selected_material(ReadBasisMode mode) {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync(mode);
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var participant = new MafPlanningProviderFixture.Participant { ContinueStep = true };
        var provider = new MafPlanningProviderFixture();
        var first = await fixture.StartAsync(lease);
        await fixture.Owner.CaptureBasisAsync(first, participant.CaptureBasisAsync, default);
        var firstResult = await RunAsync(fixture, provider, participant, first);
        Assert.Null(firstResult.Admission.Refusal);
        Assert.Equal(11, firstResult.RuntimeResponse.InputTokens);
        Assert.Equal(7, firstResult.RuntimeResponse.OutputTokens);
        Assert.Null(firstResult.RuntimeResponse.SerializedSessionStateJson);
        await AddOldChatFactAsync(fixture);
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        var second = await fixture.Owner.AdvanceStepAsync(first, default);
        await fixture.Owner.CaptureBasisAsync(second, participant.CaptureBasisAsync, default);
        participant.ContinueStep = false;
        var secondResult = await RunAsync(fixture, provider, participant, second);
        Assert.Null(secondResult.Admission.Refusal);
        Assert.Equal(2, provider.Created);
        Assert.Equal(2, provider.Disposed);
        Assert.Equal(2, provider.Inputs.Count);
        var inputs = provider.Inputs.Select(messages => {
            Assert.Equal(2, messages.Length);
            Assert.Equal(ChatRole.System, messages[0].Role);
            Assert.Equal(ChatRole.User, messages[1].Role);
            Assert.DoesNotContain("stale conversational fact", messages[1].Text, StringComparison.Ordinal);
            return JsonSerializer.Deserialize<JsonElement>(messages[1].Text);
        }).ToArray();
        Assert.Equal(fixture.Files.Detail.Run.ToolAdmission!.OriginalInput!.Content, inputs[0].GetProperty("OriginalRequest").GetString());
        Assert.Equal("first fact", inputs[0].GetProperty("MaterialBasis").GetProperty("tasks")[0].GetString());
        Assert.Equal(mode == ReadBasisMode.RunSnapshot ? "first fact" : "new fact",
            inputs[1].GetProperty("MaterialBasis").GetProperty("tasks")[0].GetString());
        Assert.NotEqual(inputs[0].GetProperty("StepId").GetGuid(), inputs[1].GetProperty("StepId").GetGuid());
        var retained = secondResult.Admission.Run.ToolAdmission!;
        Assert.Equal(2, retained.ProviderDispatches.Length);
        Assert.NotEqual(retained.ProviderDispatches[0].RequestDigest, retained.ProviderDispatches[1].RequestDigest);
        Assert.Single(retained.Batches[1].Proposals);
        Assert.Equal(ExecutionApprovalStatus.Pending, retained.Batches[1].Proposals[0].ApprovalStatus);
    }

    [Theory]
    [InlineData(PlanningProviderWiring.MissingBoundary, "missing")]
    [InlineData(PlanningProviderWiring.ChangedInput, "final planning input")]
    public async Task Missing_boundary_or_changed_sdk_input_refuses_before_transport(PlanningProviderWiring wiring, string expected) {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var participant = new MafPlanningProviderFixture.Participant();
        var provider = new MafPlanningProviderFixture { Mode = wiring };
        var work = await fixture.StartAsync(lease);
        await fixture.Owner.CaptureBasisAsync(work, participant.CaptureBasisAsync, default);
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => RunAsync(fixture, provider, participant, work));
        Assert.Contains(expected, failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(provider.Inputs);
        Assert.True((await fixture.ReadAsync()).ToolAdmission!.ProviderDispatches.IsDefaultOrEmpty);
    }

    [Fact]
    public async Task Current_read_denial_never_sends_captured_facts_to_the_provider() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var participant = new MafPlanningProviderFixture.Participant { DenyRead = true };
        var provider = new MafPlanningProviderFixture();
        var work = await fixture.StartAsync(lease);
        await fixture.Owner.CaptureBasisAsync(work, participant.CaptureBasisAsync, default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RunAsync(fixture, provider, participant, work));
        Assert.Empty(provider.Inputs);
        Assert.Equal(0, participant.Preparations);
        Assert.True((await fixture.ReadAsync()).ToolAdmission!.ProviderDispatches.IsDefaultOrEmpty);
    }

    [Theory]
    [InlineData(AgentPlanningOutputRefusal.UnsupportedContent)]
    [InlineData(AgentPlanningOutputRefusal.LimitExceeded)]
    [InlineData(AgentPlanningOutputRefusal.PreparationFailed)]
    public async Task Invalid_provider_output_is_recorded_without_an_executable_publication(AgentPlanningOutputRefusal kind) {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var participant = new MafPlanningProviderFixture.Participant { ThrowPreparation = kind == AgentPlanningOutputRefusal.PreparationFailed };
        var provider = new MafPlanningProviderFixture {
            Respond = _ => Task.FromResult(kind switch {
                AgentPlanningOutputRefusal.UnsupportedContent => new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent("unexpected-call", "must_never_execute", new Dictionary<string, object?>())])),
                AgentPlanningOutputRefusal.LimitExceeded => MafPlanningProviderFixture.Response(new string('x', AgentPlanningProviderBounds.MaximumTextUtf8Bytes + 1)),
                _ => MafPlanningProviderFixture.Response("{\"actual\":true}")
            })
        };
        var work = await fixture.StartAsync(lease);
        await fixture.Owner.CaptureBasisAsync(work, participant.CaptureBasisAsync, default);
        var result = await RunAsync(fixture, provider, participant, work);
        Assert.Equal(kind, result.OutputRefusal);
        Assert.Equal(AgentPlanningAdmissionRefusal.InvalidProviderOutput, result.Admission.Refusal);
        Assert.Empty(result.Admission.Run.ToolAdmission!.Batches.Single().Proposals);
        Assert.Equal(AgentPlanningGenerationState.Failed, result.Admission.Run.ToolAdmission.PlanningDecision!.Current.State);
        Assert.Single(provider.Inputs);
    }

    [Fact]
    public async Task Original_remaining_budget_bounds_the_actual_provider_call() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var participant = new MafPlanningProviderFixture.Participant();
        var cancelled = false;
        var provider = new MafPlanningProviderFixture { Respond = async token => {
            try {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            } catch (OperationCanceledException) {
                cancelled = true;
                throw;
            }
            throw new InvalidOperationException("An infinite provider wait must be cancelled.");
        } };
        var work = await fixture.StartAsync(lease);
        await fixture.Owner.CaptureBasisAsync(work, participant.CaptureBasisAsync, default);
        fixture.Clock.Timestamp = (TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1)).Ticks;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(fixture, provider, participant, work));
        Assert.True(cancelled);
        Assert.Single(provider.Inputs);
        var retained = (await fixture.ReadAsync()).ToolAdmission!;
        Assert.Equal(AgentToolProviderDispatchState.Started, retained.ProviderDispatches.Single().State);
        Assert.Empty(retained.Batches);
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => RunAsync(fixture, provider, participant, work));
        Assert.Single(provider.Inputs);
    }

    [Fact]
    public async Task Cancellation_during_the_real_sdk_call_does_not_lose_its_late_response() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var participant = new MafPlanningProviderFixture.Participant();
        var provider = new MafPlanningProviderFixture { Respond = async _ => {
            await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Files.NewStore()).UpdateExecutionRunDetailAsync(fixture.Files.Session.ExecutionRunId,
                current => current with { Run = current.Run with {
                    Revision = current.Run.Revision + 1, State = ExecutionState.Completed, Outcome = RunOutcome.Cancelled,
                    ToolAdmission = AgentToolJournalTransitions.RejectUndispatchedOnCancellation(current.Run.ToolAdmission!)
                } });
            return MafPlanningProviderFixture.Response("{\"late\":true}");
        } };
        var work = await fixture.StartAsync(lease);
        await fixture.Owner.CaptureBasisAsync(work, participant.CaptureBasisAsync, default);
        var result = await RunAsync(fixture, provider, participant, work);
        Assert.Equal(AgentPlanningAdmissionRefusal.DecisionInactive, result.Admission.Refusal);
        Assert.Equal(RunOutcome.Cancelled, result.Admission.Run.Outcome);
        Assert.Empty(result.Admission.Run.ToolAdmission!.Batches.Single().Proposals);
        Assert.Equal("{\"late\":true}", MafToolProtocolCodec.Decode<ChatResponse>(result.Admission.Run.ToolAdmission.Batches[0].Response).Text);
    }

    [Fact]
    public async Task Lost_response_acknowledgement_does_not_repeat_the_provider() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var participant = new MafPlanningProviderFixture.Participant();
        var provider = new MafPlanningProviderFixture { Respond = _ => {
            fixture.Fault = ExistingRunDetailCommitStage.RunPersisted;
            return Task.FromResult(MafPlanningProviderFixture.Response("{\"original\":1}"));
        } };
        var work = await fixture.StartAsync(lease);
        await fixture.Owner.CaptureBasisAsync(work, participant.CaptureBasisAsync, default);
        AgentToolAdmissionJournalFixture.RequireScriptedFault(await Assert.ThrowsAnyAsync<Exception>(() =>
            RunAsync(fixture, provider, participant, work)), "planning-publication-ack");
        var retained = (await fixture.ReadAsync()).ToolAdmission!;
        Assert.Equal("{\"original\":1}", MafToolProtocolCodec.Decode<ChatResponse>(retained.Batches.Single().Response).Text);
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => RunAsync(fixture, provider, participant, work));
        Assert.Single(provider.Inputs);
    }

    [Fact]
    public async Task Decision_capacity_is_reserved_before_provider_entry() {
        var target = Envelope("{\"target\":\"" + new string('x', AgentPlanningDecisionRecord.MaximumUtf8Bytes - 200_000) + "\"}");
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync(originalTarget: target);
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        var work = await fixture.StartAsync(lease);
        await fixture.CaptureAsync(work);
        var failure = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.EnterAsync(work));
        Assert.Equal(AgentPlanningAdmissionRefusal.HistoryCapacity, failure.Reason);
        Assert.True((await fixture.ReadAsync()).ToolAdmission!.ProviderDispatches.IsDefaultOrEmpty);
    }

    [Fact]
    public async Task Configured_provider_factory_preserves_original_history_attribution_and_usage() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync(originalOpenAi: true);
        await using var lease = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default);
        using var bound = lease.Bind();
        using var wire = new MafPlanningProviderFixture.Wire();
        using var client = new HttpClient(wire);
        wire.Client = client;
        var factory = new MafProviderAgentFactory(wire, NoOpMafProviderStreamingDispatchGate.Instance, wire,
            httpClientSelector: wire, clock: fixture.Clock);
        var runtime = new MafPlanningStepRuntime(factory, NullLogger<MafPlanningStepRuntime>.Instance);
        var participant = new MafPlanningProviderFixture.Participant();
        var work = await fixture.StartAsync(lease);
        await fixture.Owner.CaptureBasisAsync(work, participant.CaptureBasisAsync, default);
        var contract = runtime.CaptureRuntimeContract(fixture.Files.Agent, fixture.Files.Provider, participant);
        var result = await runtime.RunAsync(new(fixture.Files.Agent, fixture.Files.Provider, fixture.Owner, work, participant, contract));
        Assert.Single(wire.Requests);
        Assert.Contains("first fact", wire.Requests[0], StringComparison.Ordinal);
        Assert.Equal(fixture.Files.Session.ExecutionRunId.ToString("N"), wire.Starts.Single().ContentOwner!.Owner.Value);
        Assert.Equal(1, wire.Completed);
        Assert.NotNull(result.RuntimeResponse.HistoryEvidence);
        Assert.Equal(11, result.RuntimeResponse.InputTokens);
        Assert.Equal(7, result.RuntimeResponse.OutputTokens);
        Assert.Single(result.RuntimeResponse.UsageObservations);
        Assert.Null(result.Admission.Refusal);
    }

    private static Task<AgentPlanningStepResult> RunAsync(AgentPlanningLiveOwnerFixture fixture, MafPlanningProviderFixture provider,
        MafPlanningProviderFixture.Participant participant, AgentPlanningLiveWork work)
        => new MafPlanningStepRuntime(provider, NullLogger<MafPlanningStepRuntime>.Instance)
            .RunAsync(new(fixture.Files.Agent, fixture.Files.Provider, fixture.Owner, work, participant,
                MafPlanningRuntimeContract.Capture(fixture.Files.Agent, fixture.Files.Provider, participant)));

    private static Task<ExecutionRunDetail> AddOldChatFactAsync(AgentPlanningLiveOwnerFixture fixture)
        => ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Files.NewStore()).UpdateExecutionRunDetailAsync(fixture.Files.Session.ExecutionRunId,
            current => current with {
                Run = current.Run with { Revision = current.Run.Revision + 1 },
                ChatSession = current.ChatSession! with {
                    Messages = current.ChatSession.Messages.Append(new ChatMessageRecord(Guid.NewGuid(), ChatMessageRole.Assistant,
                        "stale conversational fact", fixture.Clock.GetUtcNow(), 4)).ToArray()
                }
            });
}
