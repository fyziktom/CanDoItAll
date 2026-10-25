using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed class MafInheritedPlanningRuntimeTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Real_sdk_uses_original_background_instruction_and_inherited_material_on_every_iteration(ReadBasisMode mode) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync(mode);
        await using var lease = await fixture.StartAsync();
        using var bound = lease.Bind();
        var participant = new MafPlanningProviderFixture.Participant { ContinueStep = true };
        var provider = new MafPlanningProviderFixture();
        var runtime = new MafPlanningStepRuntime(provider, NullLogger<MafPlanningStepRuntime>.Instance);
        var contract = runtime.CaptureRuntimeContract(fixture.Files.Agent, fixture.Files.Provider, participant);
        var work = lease.PlanningWork!;
        for (var iteration = 0; iteration < 2; iteration++) {
            await fixture.InheritAsync(work);
            var result = await runtime.RunAsync(new(fixture.Files.Agent, fixture.Files.Provider, fixture.Owner, work, participant, contract));
            Assert.Null(result.Admission.Refusal);
            Assert.Equal(11, result.RuntimeResponse.InputTokens);
            Assert.Equal(7, result.RuntimeResponse.OutputTokens);
            fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            if (iteration == 0) {
                work = await fixture.Owner.AdvanceStepAsync(work, default);
            }
        }
        await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Completed, default);
        Assert.Equal(0, participant.Captures);
        Assert.Equal(2, participant.Preparations);
        Assert.Equal(2, provider.Created);
        Assert.Equal(2, provider.Disposed);
        Assert.Equal(2, provider.Inputs.Count);
        foreach (var messages in provider.Inputs) {
            Assert.Equal(2, messages.Length);
            Assert.Equal(ChatRole.User, messages[1].Role);
            using var input = JsonDocument.Parse(messages[1].Text);
            Assert.Equal(fixture.Original.ToolAdmission!.BackgroundInput!.Content, input.RootElement.GetProperty("OriginalRequest").GetString());
            Assert.Equal("original", input.RootElement.GetProperty("MaterialBasis").GetProperty("tasks")[0].GetString());
        }
        var retained = (await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!;
        Assert.Equal(fixture.Basis, Assert.Single(retained.Current.Bases));
        Assert.Equal(TimeSpan.FromSeconds(2).Ticks, retained.Work.ClosedWorkTicks);
        Assert.Equal(fixture.Original.ToolAdmission!.PlanningDecision!.InheritedOrigin, retained.InheritedOrigin);
    }

    [Fact]
    public async Task Parent_revocation_during_provider_creation_refuses_the_actual_transport_call() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        await using var lease = await fixture.StartAsync();
        using var bound = lease.Bind();
        var work = lease.PlanningWork!;
        await fixture.InheritAsync(work);
        var participant = new MafPlanningProviderFixture.Participant();
        var provider = new MafPlanningProviderFixture { OnCreate = () => fixture.Policy.DispatchAllowed = false };
        var runtime = new MafPlanningStepRuntime(provider, NullLogger<MafPlanningStepRuntime>.Instance);
        var contract = runtime.CaptureRuntimeContract(fixture.Files.Agent, fixture.Files.Provider, participant);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => runtime.RunAsync(new(
            fixture.Files.Agent, fixture.Files.Provider, fixture.Owner, work, participant, contract)));
        Assert.Empty(provider.Inputs);
        Assert.Equal(1, provider.Disposed);
        Assert.True((await fixture.ReadAsync()).ToolAdmission!.ProviderDispatches.IsDefaultOrEmpty);
        await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Cancelled, default);
    }
}
