using System.Net;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class AgentInheritedPlanningCoordinatorTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Actual_transport_iterations_reuse_one_inherited_basis_and_pause_for_original_publication(ReadBasisMode mode) {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync(mode);
        fixture.Module.ContinueSteps = 1;
        fixture.Wire.OnRequest = _ => {
            fixture.Source.Clock.Advance(TimeSpan.FromSeconds(1));
            return Task.CompletedTask;
        };
        var result = await fixture.RunAsync();

        Assert.Null(result.Refusal);
        Assert.Equal(ExecutionState.WaitingOnTool, result.Run.State);
        Assert.Null(result.Run.Outcome);
        Assert.Null(result.Run.ChatSessionId);
        Assert.Equal(1, fixture.Module.Reads);
        Assert.Equal(2, fixture.Module.Preparations);
        Assert.Equal(2, fixture.Wire.Requests.Count);
        Assert.Equal(22, result.Response.InputTokens);
        Assert.Equal(14, result.Response.OutputTokens);
        Assert.Equal(2, result.Response.UsageObservations.Count);
        Assert.Equal(2, result.Response.UsageObservations.Select(item => item.HistoryEvidence!.RequestId).Distinct().Count());
        Assert.Equal(2, fixture.Wire.Contexts.Count);
        Assert.All(fixture.Wire.Contexts, context => Assert.Equal(
            new HistoryExternalReference(fixture.Source.Files.StorageScope.Key, HistoryExternalReference.LocalProjectType), context.ExternalReference));
        var decision = result.Run.ToolAdmission!.PlanningDecision!;
        Assert.Null(decision.Work.Open);
        Assert.Equal(TimeSpan.FromSeconds(2).Ticks, decision.Work.ClosedWorkTicks);
        Assert.Equal(fixture.Module.Basis, Assert.Single(decision.Current.Bases));
        Assert.Equal(fixture.Original.ToolAdmission!.PlanningDecision!.InheritedOrigin, decision.InheritedOrigin);
        var pending = Assert.Single(result.Response.PendingApprovals);
        Assert.Equal(Assert.Single(result.Run.PendingApprovals), pending);
        Assert.Equal(Assert.Single(decision.Current.Steps[^1].Publications).Proposal, pending.ToolAdmission);
        foreach (var request in fixture.Wire.Requests) {
            using var body = JsonDocument.Parse(request);
            var input = body.RootElement.GetProperty("messages").EnumerateArray().Single(message => message.GetProperty("role").GetString() == "user");
            using var content = JsonDocument.Parse(input.GetProperty("content").GetString()!);
            Assert.Equal("Prepare the inherited task plan.", content.RootElement.GetProperty("OriginalRequest").GetString());
            Assert.Equal("original", content.RootElement.GetProperty("MaterialBasis").GetProperty("tasks")[0].GetString());
        }
    }

    [Fact]
    public async Task Later_provider_failure_keeps_actual_usage_original_grant_and_uncertain_work_without_restart() {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync();
        fixture.Module.ContinueSteps = 1;
        fixture.Wire.OnRequest = _ => {
            fixture.Wire.StatusCode = fixture.Wire.Requests.Count == 1 ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
            return Task.CompletedTask;
        };
        var failure = await Assert.ThrowsAsync<AgentRuntimeUsageException>(() => fixture.RunAsync());
        Assert.Equal(11, failure.UsageObservations.Sum(item => item.InputTokens));
        Assert.Equal(7, failure.UsageObservations.Sum(item => item.OutputTokens));
        Assert.NotNull(Assert.Single(failure.UsageObservations, item => item.InputTokens == 11).HistoryEvidence);
        var retained = await fixture.ReadAsync();
        Assert.NotNull(retained.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(AgentToolProviderDispatchState.Started, retained.ToolAdmission.ProviderDispatches[^1].State);
        Assert.Equal(fixture.Original.ToolAdmission!.PlanningDecision!.InheritedOrigin, retained.ToolAdmission.PlanningDecision.InheritedOrigin);
        var refusal = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.RunAsync(AgentPlanningDecisionExpectation.From(retained)));
        Assert.Equal(AgentPlanningAdmissionRefusal.InterruptedWorkAccountingUnavailable, refusal.Reason);
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(await fixture.ReadAsync()));
        Assert.Equal(2, fixture.Wire.Requests.Count);
    }

    [Fact]
    public async Task Rejected_competing_start_does_not_terminalize_the_active_child() {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Wire.OnRequest = async token => {
            entered.SetResult();
            await release.Task.WaitAsync(token);
        };
        var active = fixture.RunAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var original = await fixture.ReadAsync();
            Assert.NotNull(original.ToolAdmission!.PlanningDecision!.Work.Open);
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.RunAsync());
            Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await fixture.ReadAsync()));
            Assert.False(active.IsCompleted);
        } finally {
            release.SetResult();
        }
        var result = await active.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(ExecutionState.WaitingOnTool, result.Run.State);
        Assert.Single(fixture.Wire.Requests);
    }

    [Theory]
    [InlineData("source-read")]
    [InlineData("source-dispatch")]
    [InlineData("parent-read")]
    public async Task Current_denial_refuses_before_work_or_provider_transport(string denial) {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync();
        fixture.Source.Policy.ReadAllowed = denial != "source-read";
        fixture.Source.Policy.DispatchAllowed = denial != "source-dispatch";
        fixture.Module.DenyRead = denial == "parent-read";
        var failure = await Record.ExceptionAsync(() => fixture.RunAsync());
        if (denial == "source-read") {
            Assert.IsType<AgentToolReceiptAccessDeniedException>(failure);
        } else if (denial == "source-dispatch") {
            Assert.IsType<AgentToolAdmissionException>(failure);
        } else {
            Assert.IsType<UnauthorizedAccessException>(failure);
        }
        Assert.Empty(fixture.Wire.Requests);
        Assert.Equal(JsonSerializer.Serialize(fixture.Original), JsonSerializer.Serialize(await fixture.ReadAsync()));
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("agent-options")]
    [InlineData("prompt")]
    public async Task Changed_original_runtime_contract_refuses_before_opening_work(string change) {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync();
        var agent = change == "agent-options" ? fixture.Agent with { Temperature = fixture.Agent.Temperature + 0.1 } : fixture.Agent;
        var provider = change == "provider" ? fixture.Provider with { BaseUrl = "https://changed.invalid/v1" } : fixture.Provider;
        if (change == "prompt") {
            fixture.Module.Prompt = new("Changed instructions.", fixture.Module.Prompt.SchemaName, fixture.Module.Prompt.SchemaJson);
        }
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Coordinator().RunAsync(agent, provider, fixture.Expected));
        Assert.Equal("planning.runtime-contract-changed", failure.Code);
        Assert.Empty(fixture.Wire.Requests);
        Assert.Equal(JsonSerializer.Serialize(fixture.Original), JsonSerializer.Serialize(await fixture.ReadAsync()));
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("capture-time")]
    [InlineData("content")]
    public async Task Substituted_inherited_material_never_reaches_the_provider(string change) {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync();
        var basis = fixture.Module.Basis;
        fixture.Module.Basis = change switch {
            "identity" => basis with { Id = Guid.NewGuid() },
            "capture-time" => basis with { CapturedAtUtc = basis.CapturedAtUtc.AddSeconds(1) },
            "content" => basis with { Value = AgentPlanningDecisionTestData.Envelope("{\"tasks\":[\"substituted\"]}") },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.RunAsync());
        Assert.Empty(fixture.Wire.Requests);
        var retained = await fixture.ReadAsync();
        Assert.NotNull(retained.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Empty(retained.ToolAdmission.PlanningDecision.Current.Bases);
        Assert.Equal(fixture.Original.ToolAdmission!.PlanningDecision!.InheritedOrigin, retained.ToolAdmission.PlanningDecision.InheritedOrigin);
    }

    [Fact]
    public async Task Missing_inherited_participant_never_uses_the_interactive_participant() {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync();
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Coordinator(registered: false)
            .RunAsync(fixture.Agent, fixture.Provider, fixture.Expected));
        Assert.Equal("planning.unsupported", failure.Code);
        Assert.Empty(fixture.Wire.Requests);
        Assert.Equal(JsonSerializer.Serialize(fixture.Original), JsonSerializer.Serialize(await fixture.ReadAsync()));
    }

    [Fact]
    public async Task Repeating_a_completed_planning_start_cannot_recreate_provider_work() {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync();
        var result = await fixture.RunAsync();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.RunAsync());
        Assert.Equal(JsonSerializer.Serialize(result.Run), JsonSerializer.Serialize(await fixture.ReadAsync()));
        Assert.Single(fixture.Wire.Requests);
        Assert.Single(result.Run.PendingApprovals);
    }
}
