using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentCapabilitiesRecoveryEffectsTests {
    [Fact]
    public async Task Proof_recovery_reconciles_canonical_evidence_without_another_diagnostic() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var agent = Attach(fixture);
        var proof = new CapabilityVerificationResult(CapabilityProofStatus.Verified, "Safe inline proof", DateTimeOffset.UnixEpoch.AddDays(1));
        var receipt = new CapabilityProofReceipt(Guid.NewGuid(), agent, fixture.Capability, proof, "no-provider");
        fixture.Workspace.VerifyOperation = (_, _, _) => {
            fixture.Workspace.Agents = [CapabilityProofReceipt.Apply(agent, fixture.Capability.Id, proof), fixture.Beta];
            fixture.Workspace.Capabilities = [CapabilityProofReceipt.Apply(fixture.Capability, proof)];
            return Task.FromException(new CapabilityVerificationException(new(CapabilityVerificationDisposition.Unconfirmed, receipt)));
        };
        var cut = fixture.Render(agent.Id);
        await cut.Find("[data-testid='agents-capability-verify']").ClickAsync();
        Assert.True(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.IsBusy);
        await cut.Find("[data-testid='agents-capability-recover']").ClickAsync();
        Assert.False(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.IsBusy);
        Assert.Equal(CapabilityProofStatus.Verified, cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.Capabilities.Single().ProofStatus);
        Assert.Equal(1, fixture.Workspace.VerifyCalls);
        Assert.Equal(0, fixture.Workspace.SaveCalls);
    }

    [Fact]
    public async Task Failed_proof_verification_remains_unconfirmed_without_replay() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var agent = Attach(fixture);
        var receipt = new CapabilityProofReceipt(Guid.NewGuid(), agent, fixture.Capability,
            new(CapabilityProofStatus.Verified, "Safe inline proof", DateTimeOffset.UnixEpoch.AddDays(1)), "no-provider");
        fixture.Workspace.VerifyOperation = (_, _, _) => Task.FromException(new CapabilityVerificationException(new(CapabilityVerificationDisposition.Unconfirmed, receipt)));
        var cut = fixture.Render(agent.Id);
        await cut.Find("[data-testid='agents-capability-verify']").ClickAsync();
        fixture.Workspace.CatalogReadFails = true;
        await cut.Find("[data-testid='agents-capability-recover']").ClickAsync();
        Assert.Equal(AgentCapabilityOperationStatus.Unconfirmed, cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.Operation!.Status);
        Assert.True(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.IsBusy);
        Assert.Equal(1, fixture.Workspace.VerifyCalls);
    }

    [Fact]
    public async Task A_to_B_to_A_cannot_duplicate_proof_or_overlap_assignment() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var agent = Attach(fixture);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Workspace.VerifyOperation = (_, _, _) => pending.Task;
        var cut = fixture.Render(agent.Id);
        var first = cut.Find("[data-testid='agents-capability-verify']").ClickAsync();
        try {
            cut.Render(p => p.Add(x => x.PreferredAgentId, fixture.Beta.Id));
            cut.WaitForAssertion(() => Assert.Contains(fixture.Beta.Name, cut.Find(".agent-capabilities-panel__heading").TextContent));
            cut.Render(p => p.Add(x => x.PreferredAgentId, agent.Id));
            cut.WaitForAssertion(() => Assert.Contains(agent.Name, cut.Find(".agent-capabilities-panel__heading").TextContent));
            await cut.InvokeAsync(() => cut.FindComponent<AgentCapabilitiesSurface>().Instance.Intent.InvokeAsync(new AgentCapabilitiesIntent.VerifyCapability(fixture.Capability.Id)));
            await cut.InvokeAsync(() => cut.FindComponent<AgentCapabilitiesSurface>().Instance.Intent.InvokeAsync(new AgentCapabilitiesIntent.ToggleAssignment(fixture.Capability.Id)));
            Assert.Equal(1, fixture.Workspace.VerifyCalls);
            Assert.Equal(0, fixture.Workspace.SaveCalls);
        } finally {
            pending.SetResult();
            await first;
        }
    }

    [Fact]
    public async Task Late_A_verification_cannot_publish_under_B() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var agent = Attach(fixture);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Workspace.VerifyOperation = (_, _, _) => pending.Task;
        var cut = fixture.Render(agent.Id);
        var first = cut.Find("[data-testid='agents-capability-verify']").ClickAsync();
        cut.Render(p => p.Add(x => x.PreferredAgentId, fixture.Beta.Id));
        cut.WaitForAssertion(() => Assert.Contains(fixture.Beta.Name, cut.Find(".agent-capabilities-panel__heading").TextContent));
        pending.SetResult();
        await first;
        Assert.Null(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.Operation);
        Assert.False(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.IsBusy);
        Assert.Empty(fixture.Context.Services.GetRequiredService<NotificationService>().Messages);
    }

    [Fact]
    public async Task Superseded_proof_requires_explicit_adoption_before_new_intent() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var agent = Attach(fixture);
        fixture.Workspace.VerifyOperation = (_, _, _) => Task.FromException(new CapabilityVerificationException(new(CapabilityVerificationDisposition.Superseded)));
        var cut = fixture.Render(agent.Id);
        await cut.Find("[data-testid='agents-capability-verify']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='agents-capability-retry-assignment']"));
        await cut.Find("[data-testid='agents-capability-adopt']").ClickAsync();
        Assert.False(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.IsBusy);
        Assert.Equal(1, fixture.Workspace.VerifyCalls);
    }

    [Fact]
    public async Task Old_preview_finally_cannot_clear_new_busy_state() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var effects = CapabilityLifetimeEffects.Register(fixture);
        var firstResult = new TaskCompletionSource<CapabilityAccessPreviewResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResult = new TaskCompletionSource<CapabilityAccessPreviewResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        effects.Preview = (_, _) => effects.PreviewCalls == 1 ? firstResult.Task : secondResult.Task;
        var cut = fixture.Render(fixture.Alpha.Id);
        var first = cut.Find("[data-testid='agents-capability-access-preview']").ClickAsync();
        var firstToken = effects.PreviewToken;
        var second = cut.InvokeAsync(() => cut.FindComponent<AgentCapabilitiesSurface>().Instance.Intent.InvokeAsync(
            new AgentCapabilitiesIntent.PreviewAccess(new(Reason: "New raw draft"))));
        try {
            cut.WaitForAssertion(() => Assert.Equal(2, effects.PreviewCalls));
            Assert.True(firstToken.IsCancellationRequested);
            firstResult.SetResult(CapabilityLifetimeEffects.EmptyPreview);
            await first;
            Assert.True(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.IsAccessPreviewBusy);
            Assert.Empty(fixture.Context.Services.GetRequiredService<NotificationService>().Messages);
        } finally {
            firstResult.TrySetResult(CapabilityLifetimeEffects.EmptyPreview);
            secondResult.SetResult(CapabilityLifetimeEffects.EmptyPreview);
            await Task.WhenAll(first, second);
        }
        Assert.False(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.IsAccessPreviewBusy);
    }

    [Fact]
    public async Task Raw_preview_draft_and_filter_survive_assignment_reconciliation() {
        using var fixture = new AgentCapabilitiesHostFixture();
        fixture.Workspace.Save = model => {
            fixture.Workspace.ReadEditor = (_, _) => Task.FromResult(model);
            return Task.FromResult(model.Id!.Value);
        };
        var cut = fixture.Render(fixture.Alpha.Id);
        var surface = cut.FindComponent<AgentCapabilitiesSurface>().Instance;
        cut.Find("[data-testid='agents-capability-access-reason']").Change("  Preserve raw reason  ");
        cut.Find("[data-testid='agents-capability-access-server-key']").Change("  raw-server  ");
        cut.Find("[data-testid='agents-capability-search']").Input("Fixture");
        await cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        Assert.Same(surface, cut.FindComponent<AgentCapabilitiesSurface>().Instance);
        Assert.Equal("  Preserve raw reason  ", cut.Find("[data-testid='agents-capability-access-reason']").GetAttribute("value"));
        Assert.Equal("  raw-server  ", cut.Find("[data-testid='agents-capability-access-server-key']").GetAttribute("value"));
        Assert.Equal("Fixture", cut.Find("[data-testid='agents-capability-search']").GetAttribute("value"));
    }

    [Fact]
    public async Task Global_dialog_result_refreshes_current_selection_and_prevents_duplicate_ownership() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var cut = fixture.Render(fixture.Alpha.Id);
        var dialogs = fixture.Context.Services.GetRequiredService<DialogService>();
        var open = cut.Find("[data-testid='agents-capability-new-skill']").ClickAsync();
        var wizard = Assert.Single(dialogs.Dialogs);
        cut.Render(p => p.Add(x => x.PreferredAgentId, fixture.Beta.Id));
        cut.WaitForAssertion(() => Assert.Contains(fixture.Beta.Name, cut.Find(".agent-capabilities-panel__heading").TextContent));
        await cut.InvokeAsync(() => cut.FindComponent<AgentCapabilitiesSurface>().Instance.Intent.InvokeAsync(new AgentCapabilitiesIntent.CreateCapability(CapabilityKind.Tool)));
        Assert.Same(wizard, Assert.Single(dialogs.Dialogs));
        var created = fixture.Capability with { Id = Guid.NewGuid(), Name = "Created while Beta selected" };
        fixture.Workspace.Capabilities = [fixture.Capability, created];
        await cut.InvokeAsync(() => wizard.CloseAsync(new CapabilityDetailsDialogResult(created.Id)));
        await open;
        Assert.Equal(fixture.Beta.Id, cut.FindComponent<AgentCapabilitiesSurface>().Instance.Selection.AgentId);
        Assert.Contains(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.Capabilities, item => item.Id == created.Id);
    }

    [Fact]
    public async Task Details_owned_read_is_canceled_and_late_failure_is_silent() {
        using var fixture = new AgentCapabilitiesHostFixture();
        using var owner = new CancellationTokenSource();
        var pending = new TaskCompletionSource<CapabilityEditorModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        fixture.Workspace.ReadCapability = (_, token) => {
            captured = token;
            return pending.Task;
        };
        var details = fixture.Context.Render<CapabilityDetailsDialog>(p => p.Add(x => x.CapabilityId, fixture.Capability.Id)
            .Add(x => x.OwnerCancellationToken, owner.Token));
        owner.Cancel();
        Assert.True(captured.IsCancellationRequested);
        await fixture.Context.DisposeRenderedComponentsAsync();
        pending.SetException(new IOException("Late read failure"));
        await Task.Yield();
        Assert.Empty(fixture.Context.Services.GetRequiredService<NotificationService>().Messages);
    }

    [Fact]
    public async Task Curator_known_chat_survives_owner_disposal_without_late_notification() {
        using var fixture = new AgentCapabilitiesHostFixture();
        AddCurator(fixture);
        var effects = CapabilityLifetimeEffects.Register(fixture);
        var pending = new TaskCompletionSource<ActiveAgentChat>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken launchToken = default;
        effects.Launch = token => {
            launchToken = token;
            return pending.Task;
        };
        var cut = fixture.Render(fixture.Alpha.Id);
        var open = cut.Find("[data-testid='agents-capability-curator-open']").ClickAsync();
        await fixture.Context.DisposeRenderedComponentsAsync();
        Assert.False(launchToken.IsCancellationRequested);
        var chat = CapabilityLifetimeEffects.Chat();
        pending.SetResult(chat);
        await open;
        Assert.Same(chat, fixture.Context.Services.GetRequiredService<CapabilityCuratorLaunch>().OpenedChat);
        Assert.Empty(fixture.Context.Services.GetRequiredService<NotificationService>().Messages);
    }

    [Fact]
    public async Task Curator_unconfirmed_launch_survives_component_reconstruction_without_replay() {
        using var fixture = new AgentCapabilitiesHostFixture();
        AddCurator(fixture);
        var effects = CapabilityLifetimeEffects.Register(fixture);
        effects.Launch = _ => Task.FromException<ActiveAgentChat>(new IOException("Unknown chat creation"));
        var cut = fixture.Render(fixture.Alpha.Id);
        await cut.Find("[data-testid='agents-capability-curator-open']").ClickAsync();
        await fixture.Context.DisposeRenderedComponentsAsync();
        var replacement = fixture.Render(fixture.Beta.Id);
        replacement.WaitForElement("[data-testid='agents-capability-curator-unconfirmed']");
        Assert.True(replacement.Find("[data-testid='agents-capability-curator-open']").HasAttribute("disabled"));
        Assert.Equal(1, effects.ChatCalls);
    }

    private static AgentDefinition Attach(AgentCapabilitiesHostFixture fixture) {
        var agent = fixture.Alpha with { Capabilities = [new(fixture.Capability.Id, fixture.Capability.Key, fixture.Capability.Kind, CapabilityProofStatus.NotRun, null, "")] };
        fixture.Workspace.Agents = [agent, fixture.Beta];
        return agent;
    }
    private static void AddCurator(AgentCapabilitiesHostFixture fixture) => fixture.Workspace.Agents = [fixture.Alpha, fixture.Beta,
        fixture.Beta with { Id = CapabilityCuratorAgentIdentity.AgentId, TemplateKey = CapabilityCuratorAgentIdentity.TemplateKey }];
}
