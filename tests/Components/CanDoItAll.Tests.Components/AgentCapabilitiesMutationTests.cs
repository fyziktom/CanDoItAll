using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentCapabilitiesMutationTests {
    [Fact]
    public async Task Assignment_intent_does_not_change_visible_authoritative_set_before_commit() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var pending = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Workspace.Save = _ => pending.Task;
        var cut = fixture.Render(fixture.Alpha.Id);
        var click = cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        try {
            cut.WaitForAssertion(() => Assert.Equal(1, fixture.Workspace.SaveCalls));
            Assert.Empty(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.SelectedCapabilityIds);
        } finally {
            pending.SetResult(fixture.Alpha.Id);
            await click;
        }
    }

    [Fact]
    public async Task Rejected_assignment_leaves_authoritative_attachment_unchanged() {
        using var fixture = new AgentCapabilitiesHostFixture();
        fixture.Workspace.Save = _ => Task.FromException<Guid>(new AgentEditorValidationException("Fixture rejection"));
        var cut = fixture.Render(fixture.Alpha.Id);
        await cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        Assert.Empty(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.SelectedCapabilityIds);
        Assert.False(cut.Find("[data-testid='agents-capability-toggle']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Unknown_verification_finds_desired_state_without_replaying_save() {
        using var fixture = new AgentCapabilitiesHostFixture();
        fixture.Workspace.Save = model => {
            model.ExpectedUpdatedAtUtc = model.ExpectedUpdatedAtUtc!.Value.AddTicks(1);
            fixture.Workspace.ReadEditor = (_, _) => Task.FromResult(model);
            return Task.FromException<Guid>(new IOException("Unknown fixture result"));
        };
        var cut = fixture.Render(fixture.Alpha.Id);
        await cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        Assert.Empty(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.SelectedCapabilityIds);
        await cut.Find("[data-testid='agents-capability-recover']").ClickAsync();
        Assert.Single(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.SelectedCapabilityIds);
        Assert.Equal(1, fixture.Workspace.SaveCalls);
        Assert.False(cut.Find("[data-testid='agents-capability-toggle']").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("[data-testid='agents-capability-recover']"));
    }

    [Fact]
    public async Task Committed_assignment_failed_refresh_retries_only_reads() {
        using var fixture = new AgentCapabilitiesHostFixture();
        AgentEditorModel? committed = null;
        fixture.Workspace.Save = model => {
            committed = model;
            fixture.Workspace.ReadEditor = (_, _) => Task.FromException<AgentEditorModel>(new IOException("Refresh unavailable"));
            return Task.FromResult(model.Id!.Value);
        };
        var cut = fixture.Render(fixture.Alpha.Id);
        await cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        Assert.NotEmpty(cut.FindAll("[data-testid='agents-capability-load-failed']"));
        Assert.Equal("Retry reconciliation", cut.Find("[data-testid='agents-capability-recover']").TextContent.Trim());
        fixture.Workspace.ReadEditor = (_, _) => Task.FromResult(committed!);
        await cut.Find("[data-testid='agents-capability-recover']").ClickAsync();
        Assert.Single(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.SelectedCapabilityIds);
        Assert.Equal(1, fixture.Workspace.SaveCalls);
    }

    [Fact]
    public async Task Target_reentry_or_component_reconstruction_does_not_enable_blind_replay() {
        using var fixture = new AgentCapabilitiesHostFixture();
        fixture.Workspace.Save = _ => Task.FromException<Guid>(new IOException("Unknown fixture result"));
        var cut = fixture.Render(fixture.Alpha.Id);
        await cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        await fixture.Context.DisposeRenderedComponentsAsync();
        var replacement = fixture.Render(fixture.Alpha.Id);
        Assert.True(replacement.Find("[data-testid='agents-capability-toggle']").HasAttribute("disabled"));
        Assert.NotNull(replacement.Find("[data-testid='agents-capability-recover']"));
        Assert.Equal(1, fixture.Workspace.SaveCalls);
    }

    [Fact]
    public async Task Late_A_commit_cannot_replace_B() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var pending = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Workspace.Save = _ => pending.Task;
        var cut = fixture.Render(fixture.Alpha.Id);
        var click = cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, fixture.Beta.Id));
        cut.WaitForAssertion(() => Assert.Contains(fixture.Beta.Name, cut.Find(".agent-capabilities-panel__heading").TextContent));
        pending.SetResult(fixture.Alpha.Id);
        await click;
        Assert.Contains(fixture.Beta.Name, cut.Find(".agent-capabilities-panel__heading").TextContent);
        Assert.False(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.IsBusy);
        Assert.Empty(fixture.Context.Services.GetRequiredService<CanDoItAll.Components.BaseLib.NotificationService>().Messages);
    }

    [Fact]
    public async Task A_to_B_to_A_cannot_start_second_A_assignment() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var pending = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Workspace.Save = _ => pending.Task;
        var cut = fixture.Render(fixture.Alpha.Id);
        var click = cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        try {
            cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, fixture.Beta.Id));
            cut.WaitForAssertion(() => Assert.Contains(fixture.Beta.Name, cut.Find(".agent-capabilities-panel__heading").TextContent));
            cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, fixture.Alpha.Id));
            cut.WaitForAssertion(() => Assert.Contains(fixture.Alpha.Name, cut.Find(".agent-capabilities-panel__heading").TextContent));
            var second = cut.InvokeAsync(() => cut.FindComponent<AgentCapabilitiesSurface>().Instance.Intent.InvokeAsync(new AgentCapabilitiesIntent.ToggleAssignment(fixture.Capability.Id)));
            await cut.InvokeAsync(() => Assert.Equal(1, fixture.Workspace.SaveCalls));
            Assert.True(second.IsCompleted);
        } finally {
            pending.SetResult(fixture.Alpha.Id);
            await click;
        }
    }
}
