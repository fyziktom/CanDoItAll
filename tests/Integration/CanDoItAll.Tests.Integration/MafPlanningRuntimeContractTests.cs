using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

public enum PlanningRuntimeDrift { ProviderEndpoint, ProviderTransport, Model, Temperature, Instructions, Schema, MissingContract }

[Trait("Category", "FileSystemPortability")]
public sealed class MafPlanningRuntimeContractTests {
    [Theory]
    [InlineData(PlanningRuntimeDrift.ProviderEndpoint)]
    [InlineData(PlanningRuntimeDrift.ProviderTransport)]
    [InlineData(PlanningRuntimeDrift.Model)]
    [InlineData(PlanningRuntimeDrift.Temperature)]
    [InlineData(PlanningRuntimeDrift.Instructions)]
    [InlineData(PlanningRuntimeDrift.Schema)]
    [InlineData(PlanningRuntimeDrift.MissingContract)]
    public async Task Changed_original_runtime_contract_refuses_before_provider_creation(PlanningRuntimeDrift drift) {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Owner.StartAsync(AgentPlanningDecisionExpectation.From(await fixture.ReadAsync()),
            (_, _) => ValueTask.FromResult(true), default);
        using var bound = lease.Bind();
        var participant = new MafPlanningProviderFixture.Participant();
        var provider = new MafPlanningProviderFixture();
        var runtime = new MafPlanningStepRuntime(provider, NullLogger<MafPlanningStepRuntime>.Instance);
        var agent = fixture.Files.Agent;
        var profile = fixture.Files.Provider;
        var contract = runtime.CaptureRuntimeContract(agent, profile, participant);
        switch (drift) {
            case PlanningRuntimeDrift.ProviderEndpoint:
                profile = profile with { BaseUrl = "https://changed.invalid/v1" };
                break;
            case PlanningRuntimeDrift.ProviderTransport:
                profile = profile with { Transport = ProviderTransportKind.Responses };
                break;
            case PlanningRuntimeDrift.Model:
                profile = profile with { DefaultModel = "another-model" };
                break;
            case PlanningRuntimeDrift.Temperature:
                agent = agent with { Temperature = agent.Temperature + 0.1 };
                break;
            case PlanningRuntimeDrift.Instructions:
                participant.Prompt = new("Changed instructions.", participant.Prompt.SchemaName, participant.Prompt.SchemaJson);
                break;
            case PlanningRuntimeDrift.Schema:
                participant.Prompt = new(participant.Prompt.Instructions, "changed_schema", participant.Prompt.SchemaJson);
                break;
            case PlanningRuntimeDrift.MissingContract:
                contract = default;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(drift));
        }
        await fixture.Owner.CaptureBasisAsync(lease.PlanningWork!, participant.CaptureBasisAsync, default);
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => runtime.RunAsync(
            new(agent, profile, fixture.Owner, lease.PlanningWork!, participant, contract)));
        Assert.Equal("planning.provider-boundary", failure.Code);
        Assert.Equal(0, provider.Created);
        Assert.Empty(provider.Inputs);
        Assert.True((await fixture.ReadAsync()).ToolAdmission!.ProviderDispatches.IsDefaultOrEmpty);
    }

    [Fact]
    public async Task Contract_drift_inside_provider_construction_is_checked_again_at_transport_entry() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        await using var lease = await fixture.Owner.StartAsync(AgentPlanningDecisionExpectation.From(await fixture.ReadAsync()),
            (_, _) => ValueTask.FromResult(true), default);
        using var bound = lease.Bind();
        var participant = new MafPlanningProviderFixture.Participant();
        var provider = new MafPlanningProviderFixture {
            OnCreate = () => participant.Prompt = new("Changed after preparation.", participant.Prompt.SchemaName, participant.Prompt.SchemaJson)
        };
        var runtime = new MafPlanningStepRuntime(provider, NullLogger<MafPlanningStepRuntime>.Instance);
        var contract = runtime.CaptureRuntimeContract(fixture.Files.Agent, fixture.Files.Provider, participant);
        await fixture.Owner.CaptureBasisAsync(lease.PlanningWork!, participant.CaptureBasisAsync, default);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => runtime.RunAsync(new(fixture.Files.Agent,
            fixture.Files.Provider, fixture.Owner, lease.PlanningWork!, participant, contract)));
        Assert.Equal(1, provider.Created);
        Assert.Equal(1, provider.Disposed);
        Assert.Empty(provider.Inputs);
        Assert.True((await fixture.ReadAsync()).ToolAdmission!.ProviderDispatches.IsDefaultOrEmpty);
    }

    [Fact]
    public async Task Equivalent_provider_configuration_and_display_metadata_preserve_the_contract() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        var runtime = new MafPlanningStepRuntime(new MafPlanningProviderFixture(), NullLogger<MafPlanningStepRuntime>.Instance);
        var participant = new MafPlanningProviderFixture.Participant();
        var provider = fixture.Files.Provider with { ConfigurationJson = "{\"second\":2,\"first\":1}" };
        var original = runtime.CaptureRuntimeContract(fixture.Files.Agent, provider, participant);
        var equivalent = runtime.CaptureRuntimeContract(fixture.Files.Agent with { Name = "Display change" }, provider with {
            Name = "Display change", Notes = "Administrative note", ConfigurationJson = "{\"first\":1,\"second\":2}"
        }, participant);
        Assert.Equal(original, equivalent);
    }
}
