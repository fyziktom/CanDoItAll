using CanDoItAll.AgentFramework.UI.Capabilities;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentCapabilitiesAcknowledgementTests {
    [Fact]
    public async Task Receiptless_diagnostic_acknowledgement_releases_only_the_retained_block_and_requires_new_intent() {
        using var fixture = new AgentCapabilitiesHostFixture();
        Attach(fixture);
        fixture.Workspace.VerifyOperation = (_, _, _) => Task.FromException(new IOException("Unknown diagnostic dispatch"));
        var cut = fixture.Render(fixture.Alpha.Id);
        await cut.Find("[data-testid='agents-capability-verify']").ClickAsync();
        await fixture.Context.DisposeRenderedComponentsAsync();
        var replacement = fixture.Render(fixture.Alpha.Id);
        Assert.True(replacement.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.IsBusy);
        Assert.Contains("may have executed", replacement.Find("[data-testid='agents-capability-operation']").TextContent);
        await replacement.Find("[data-testid='agents-capability-acknowledge-diagnostic']").ClickAsync();
        Assert.False(replacement.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.IsBusy);
        Assert.Equal(1, fixture.Workspace.VerifyCalls);
        Assert.Equal(0, fixture.Workspace.SaveCalls);
        await replacement.Find("[data-testid='agents-capability-verify']").ClickAsync();
        Assert.Equal(2, fixture.Workspace.VerifyCalls);
    }

    [Fact]
    public async Task Receipt_backed_unknown_requires_canonical_verification_instead_of_abandonment() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var agent = Attach(fixture);
        var receipt = new CapabilityProofReceipt(Guid.NewGuid(), agent, fixture.Capability,
            new(CapabilityProofStatus.Verified, "Safe proof", agent.UpdatedAtUtc), "no-provider");
        fixture.Workspace.VerifyOperation = (_, _, _) => Task.FromException(new CapabilityVerificationException(new(CapabilityVerificationDisposition.Unconfirmed, receipt)));
        var cut = fixture.Render(agent.Id);
        await cut.Find("[data-testid='agents-capability-verify']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='agents-capability-acknowledge-diagnostic']"));
        Assert.Single(cut.FindAll("[data-testid='agents-capability-recover']"));
        Assert.Equal(1, fixture.Workspace.VerifyCalls);
    }

    [Fact]
    public async Task Curator_acknowledgement_survives_reconstruction_and_never_launches_or_deletes_a_chat() {
        using var fixture = new AgentCapabilitiesHostFixture();
        AddCurator(fixture);
        var effects = CapabilityLifetimeEffects.Register(fixture);
        effects.Launch = _ => Task.FromException<ActiveAgentChat>(new IOException("Unknown chat creation"));
        var cut = fixture.Render(fixture.Alpha.Id);
        await cut.Find("[data-testid='agents-capability-curator-open']").ClickAsync();
        await fixture.Context.DisposeRenderedComponentsAsync();
        var replacement = fixture.Render(fixture.Beta.Id);
        Assert.True(replacement.Find("[data-testid='agents-capability-curator-open']").HasAttribute("disabled"));
        Assert.Contains("Inspect managed chats", replacement.Find("[data-testid='agents-capability-curator-unconfirmed']").TextContent);
        await replacement.Find("[data-testid='agents-capability-curator-acknowledge']").ClickAsync();
        Assert.False(replacement.Find("[data-testid='agents-capability-curator-open']").HasAttribute("disabled"));
        Assert.Equal(1, effects.ChatCalls);
        Assert.Empty(replacement.FindAll("[data-testid='agents-capability-curator-unconfirmed']"));
        await replacement.Find("[data-testid='agents-capability-curator-open']").ClickAsync();
        Assert.Equal(2, effects.ChatCalls);
    }

    [Fact]
    public async Task Curator_acknowledgement_preserves_a_previously_returned_authoritative_chat() {
        using var fixture = new AgentCapabilitiesHostFixture();
        AddCurator(fixture);
        var effects = CapabilityLifetimeEffects.Register(fixture);
        var chat = CapabilityLifetimeEffects.Chat();
        effects.Launch = _ => Task.FromResult(chat);
        var cut = fixture.Render(fixture.Alpha.Id);
        await cut.Find("[data-testid='agents-capability-curator-open']").ClickAsync();
        effects.Launch = _ => Task.FromException<ActiveAgentChat>(new IOException("Later launch is unknown"));
        await cut.Find("[data-testid='agents-capability-curator-open']").ClickAsync();
        await cut.Find("[data-testid='agents-capability-curator-acknowledge']").ClickAsync();
        Assert.Same(chat, fixture.Context.Services.GetRequiredService<CapabilityCuratorLaunch>().OpenedChat);
        Assert.Equal(CapabilityCuratorLaunchStatus.Ready, fixture.Context.Services.GetRequiredService<CapabilityCuratorLaunch>().Status);
        Assert.Equal(2, effects.ChatCalls);
    }

    [Fact]
    public async Task Old_diagnostic_acknowledgement_cannot_release_a_newer_attempt_or_an_assignment() {
        using var fixture = new AgentCapabilitiesHostFixture();
        Attach(fixture);
        fixture.Workspace.VerifyOperation = (_, _, _) => Task.FromException(new IOException("Unknown diagnostic"));
        var cut = fixture.Render(fixture.Alpha.Id);
        await cut.Find("[data-testid='agents-capability-verify']").ClickAsync();
        var first = cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.Operation!;
        await cut.Find("[data-testid='agents-capability-acknowledge-diagnostic']").ClickAsync();
        await cut.Find("[data-testid='agents-capability-verify']").ClickAsync();
        var current = cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.Operation!;
        await cut.InvokeAsync(() => cut.FindComponent<AgentCapabilitiesSurface>().Instance.Intent.InvokeAsync(
            new AgentCapabilitiesIntent.AcknowledgeDiagnostic(first.AgentId, first.AttemptId)));
        Assert.Equal(current, cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.Operation);
        Assert.True(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.IsBusy);
        await cut.Find("[data-testid='agents-capability-acknowledge-diagnostic']").ClickAsync();
        fixture.Workspace.Save = _ => Task.FromException<Guid>(new IOException("Unknown assignment"));
        await cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        var assignment = cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.Operation!;
        await cut.InvokeAsync(() => cut.FindComponent<AgentCapabilitiesSurface>().Instance.Intent.InvokeAsync(
            new AgentCapabilitiesIntent.AcknowledgeDiagnostic(assignment.AgentId, assignment.AttemptId)));
        Assert.True(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.IsBusy);
        Assert.True(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.Operation!.CanVerify);
        Assert.Empty(cut.FindAll("[data-testid='agents-capability-acknowledge-diagnostic']"));
        Assert.Equal(2, fixture.Workspace.VerifyCalls);
        Assert.Equal(1, fixture.Workspace.SaveCalls);
    }

    [Fact]
    public async Task Old_curator_acknowledgement_cannot_release_a_new_pending_or_unknown_launch() {
        using var fixture = new AgentCapabilitiesHostFixture();
        AddCurator(fixture);
        var effects = CapabilityLifetimeEffects.Register(fixture);
        effects.Launch = _ => Task.FromException<ActiveAgentChat>(new IOException("First launch unknown"));
        var cut = fixture.Render(fixture.Alpha.Id);
        await cut.Find("[data-testid='agents-capability-curator-open']").ClickAsync();
        var first = cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.CuratorLaunch.AttemptId;
        await cut.Find("[data-testid='agents-capability-curator-acknowledge']").ClickAsync();
        var pending = new TaskCompletionSource<ActiveAgentChat>(TaskCreationOptions.RunContinuationsAsynchronously);
        effects.Launch = _ => pending.Task;
        var launch = cut.Find("[data-testid='agents-capability-curator-open']").ClickAsync();
        try {
            await cut.InvokeAsync(() => cut.FindComponent<AgentCapabilitiesSurface>().Instance.Intent.InvokeAsync(new AgentCapabilitiesIntent.AcknowledgeCurator(first)));
            Assert.True(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.CuratorLaunch.IsBusy);
        } finally {
            pending.SetException(new IOException("New launch unknown"));
            await launch;
        }
        await cut.InvokeAsync(() => cut.FindComponent<AgentCapabilitiesSurface>().Instance.Intent.InvokeAsync(new AgentCapabilitiesIntent.AcknowledgeCurator(first)));
        Assert.True(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.CuratorLaunch.RequiresAcknowledgement);
        Assert.Equal(2, effects.ChatCalls);
    }

    private static AgentDefinition Attach(AgentCapabilitiesHostFixture fixture) {
        var agent = fixture.Alpha with { Capabilities = [new(fixture.Capability.Id, fixture.Capability.Key, fixture.Capability.Kind, CapabilityProofStatus.NotRun, null, "")] };
        fixture.Workspace.Agents = [agent, fixture.Beta];
        return agent;
    }
    private static void AddCurator(AgentCapabilitiesHostFixture fixture) => fixture.Workspace.Agents = [fixture.Alpha, fixture.Beta,
        fixture.Beta with { Id = CapabilityCuratorAgentIdentity.AgentId, TemplateKey = CapabilityCuratorAgentIdentity.TemplateKey }];
}
