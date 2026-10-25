using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentPlanningCoordinatorTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, 1)]
    [InlineData(ReadBasisMode.StepSnapshot, 2)]
    public async Task Completed_steps_retain_each_history_and_require_one_original_approval(ReadBasisMode mode, int captures) {
        var participant = new MafPlanningProviderFixture.Participant { ContinueStep = true };
        using var wire = new MafPlanningProviderFixture.Wire();
        using var client = new HttpClient(wire);
        wire.Client = client;
        wire.OnRequest = _ => {
            participant.ContinueStep = wire.Requests.Count == 1;
            return Task.CompletedTask;
        };
        var provider = new MafProviderAgentFactory(wire, NoOpMafProviderStreamingDispatchGate.Instance, wire, httpClientSelector: wire);
        var runtime = new MafPlanningStepRuntime(provider, NullLogger<MafPlanningStepRuntime>.Instance);
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync(mode, originalOpenAi: true,
            runtimeContract: (agent, profile) => runtime.CaptureRuntimeContract(agent, profile, participant));
        var coordinator = new AgentPlanningCoordinator(fixture.Journal, runtime, [participant]);
        var result = await coordinator.RunAsync(fixture.Files.Agent, fixture.Files.Provider, await fixture.ReadAsync(), default);
        Assert.Null(result.Refusal);
        Assert.Equal(ExecutionState.WaitingOnTool, result.Run.State);
        Assert.Null(result.Run.Outcome);
        Assert.Null(result.Run.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(captures, participant.Captures);
        Assert.Equal(22, result.Response.InputTokens);
        Assert.Equal(14, result.Response.OutputTokens);
        Assert.Equal(2, result.Response.UsageObservations.Count);
        Assert.Equal(2, result.Response.UsageObservations.Select(item => item.HistoryEvidence!.RequestId).Distinct().Count());
        Assert.Equal(2, wire.Requests.Count);
        Assert.Equal(2, wire.Starts.Count);
        Assert.Equal(2, result.Run.ToolAdmission.PlanningDecision.Current.Steps.Length);
        var pending = Assert.Single(result.Response.PendingApprovals);
        Assert.Equal(result.Run.PendingApprovals.Single(), pending);
        Assert.Equal(result.Run.ToolAdmission.PlanningDecision.Current.Steps[^1].Publications.Single().Proposal, pending.ToolAdmission);
        Assert.Empty(result.Response.RuntimeSessionKey);
        Assert.Null(result.Response.SerializedSessionStateJson);
    }

    [Fact]
    public async Task Later_provider_failure_retains_completed_usage_and_original_open_work() {
        var participant = new MafPlanningProviderFixture.Participant { ContinueStep = true };
        using var wire = new MafPlanningProviderFixture.Wire();
        using var client = new HttpClient(wire);
        wire.Client = client;
        wire.OnRequest = _ => {
            wire.StatusCode = wire.Requests.Count == 1 ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
            return Task.CompletedTask;
        };
        var provider = new MafProviderAgentFactory(wire, NoOpMafProviderStreamingDispatchGate.Instance, wire, httpClientSelector: wire);
        var runtime = new MafPlanningStepRuntime(provider, NullLogger<MafPlanningStepRuntime>.Instance);
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync(originalOpenAi: true,
            runtimeContract: (agent, profile) => runtime.CaptureRuntimeContract(agent, profile, participant));
        var coordinator = new AgentPlanningCoordinator(fixture.Journal, runtime, [participant]);
        var failure = await Assert.ThrowsAsync<AgentRuntimeUsageException>(() => coordinator.RunAsync(fixture.Files.Agent,
            fixture.Files.Provider, fixture.Files.Detail.Run, default));
        var usage = Assert.Single(failure.UsageObservations, item => item.InputTokens == 11);
        Assert.Equal(11, usage.InputTokens);
        Assert.Equal(7, usage.OutputTokens);
        Assert.NotNull(usage.HistoryEvidence);
        Assert.Equal(11, failure.UsageObservations.Sum(item => item.InputTokens));
        Assert.Equal(7, failure.UsageObservations.Sum(item => item.OutputTokens));
        Assert.Equal(2, wire.Requests.Count);
        Assert.Equal(2, wire.Starts.Count);
        var retained = await fixture.ReadAsync();
        Assert.NotNull(retained.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(2, retained.ToolAdmission.PlanningDecision.Current.Steps.Length);
        Assert.Single(retained.ToolAdmission.Batches);
        Assert.Equal(AgentToolProviderDispatchState.Started, retained.ToolAdmission.ProviderDispatches[^1].State);
        Assert.Empty(retained.PendingApprovals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_changed_original_contract_refuses_before_work_opens(bool changed) {
        var participant = new MafPlanningProviderFixture.Participant();
        var provider = new MafPlanningProviderFixture();
        var runtime = new MafPlanningStepRuntime(provider, NullLogger<MafPlanningStepRuntime>.Instance);
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync(runtimeContract: changed
            ? (agent, profile) => runtime.CaptureRuntimeContract(agent, profile, participant) : null);
        var original = await fixture.ReadAsync();
        var coordinator = new AgentPlanningCoordinator(fixture.Journal, runtime, [participant]);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => coordinator.RunAsync(fixture.Files.Agent,
            fixture.Files.Provider with { BaseUrl = "https://changed.invalid/v1" }, original, default));
        var retained = await fixture.ReadAsync();
        Assert.Equal(original.Revision, retained.Revision);
        Assert.Null(retained.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Empty(retained.ToolAdmission.PlanningDecision.Current.Steps);
        Assert.Equal(0, provider.Created);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_changed_original_scope_refuses_before_work_opens(bool changed) {
        var participant = new MafPlanningProviderFixture.Participant();
        var provider = new MafPlanningProviderFixture();
        var runtime = new MafPlanningStepRuntime(provider, NullLogger<MafPlanningStepRuntime>.Instance);
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync(
            runtimeContract: (agent, profile) => runtime.CaptureRuntimeContract(agent, profile, participant));
        var original = await fixture.ReadAsync();
        var metadata = JsonNode.Parse(original.MetadataJson)!.AsObject();
        var contract = metadata["agentPlanningRuntimeContract"]!.AsObject();
        contract["Scope"] = changed ? JsonSerializer.SerializeToNode(WorkspaceScopeDescriptor.Organization("another-owner")) : null;
        var coordinator = new AgentPlanningCoordinator(fixture.Journal, runtime, [participant]);
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => coordinator.RunAsync(fixture.Files.Agent,
            fixture.Files.Provider, original with { MetadataJson = metadata.ToJsonString() }, default));
        Assert.Equal("planning.original-contract", failure.Code);
        Assert.Equal(original.Revision, (await fixture.ReadAsync()).Revision);
        Assert.Null((await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(0, provider.Created);
    }
}
