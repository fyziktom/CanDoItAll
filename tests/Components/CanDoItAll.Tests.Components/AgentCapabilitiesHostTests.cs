using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using Access = CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentCapabilitiesHostTests {
    [Fact]
    public void SelectedAgentChanged_matches_the_authoritative_selection() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var selected = new List<AgentDefinition?>();
        var cut = fixture.Context.Render<AgentCapabilitiesPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, fixture.Alpha.Id)
            .Add(component => component.SelectedAgentChanged, agent => selected.Add(agent)));
        cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, fixture.Beta.Id));
        cut.WaitForAssertion(() => {
            Assert.Same(fixture.Beta, selected.Last());
            Assert.Contains(fixture.Beta.Name, cut.Find(".agent-capabilities-panel__heading").TextContent);
        });
        cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, fixture.Beta.Id));
        Assert.Equal(2, selected.Count);
    }

    [Fact]
    public void ContextAccessStateChanged_deduplicates_equivalent_state() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var states = new List<AgentChatContextAccessState>();
        var cut = fixture.Context.Render<AgentCapabilitiesPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, fixture.Alpha.Id)
            .Add(component => component.ContextAccessStateChanged, state => states.Add(state)));
        cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, fixture.Alpha.Id));
        Assert.Equal([AgentChatContextAccessState.Loading, AgentChatContextAccessState.Ready], states);
    }

    [Fact]
    public async Task Exact_managed_curator_can_be_launched() {
        using var fixture = new AgentCapabilitiesHostFixture();
        fixture.Workspace.Agents = [fixture.Alpha, fixture.Beta with {
            Id = CapabilityCuratorAgentIdentity.AgentId, TemplateKey = CapabilityCuratorAgentIdentity.TemplateKey
        }];
        var effects = RegisterEffects(fixture);
        var cut = fixture.Render(fixture.Alpha.Id);
        await cut.Find("[data-testid='agents-capability-curator-open']").ClickAsync();
        Assert.Equal(CapabilityCuratorAgentIdentity.AgentId, effects.StartedAgentId);
        Assert.Equal(1, effects.ChatCalls);
    }

    [Fact]
    public void Spoofed_curator_remains_disabled() {
        using var fixture = new AgentCapabilitiesHostFixture();
        fixture.Workspace.Agents = [fixture.Alpha, fixture.Beta with {
            Name = CapabilityCuratorAgentIdentity.DefaultDisplayName, TemplateKey = CapabilityCuratorAgentIdentity.TemplateKey
        }];
        var cut = fixture.Render(fixture.Alpha.Id);
        Assert.True(cut.Find("[data-testid='agents-capability-curator-open']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Assignment_and_verification_use_existing_services_once() {
        using var fixture = new AgentCapabilitiesHostFixture();
        fixture.Workspace.Save = model => {
            fixture.Workspace.ReadEditor = (_, _) => Task.FromResult(model);
            return Task.FromResult(model.Id!.Value);
        };
        var cut = fixture.Render(fixture.Alpha.Id);
        await cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        await cut.Find("[data-testid='agents-capability-verify']").ClickAsync();
        Assert.Equal(1, fixture.Workspace.SaveCalls);
        Assert.Equal(1, fixture.Workspace.VerifyCalls);
        Assert.Contains(fixture.Alpha.Name, cut.Find(".agent-capabilities-panel__heading").TextContent);
    }

    [Fact]
    public async Task Access_preview_preserves_typed_rule_and_renders_result() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var effects = RegisterEffects(fixture);
        var cut = fixture.Render(fixture.Alpha.Id);
        await cut.Find("[data-testid='agents-capability-access-preview']").ClickAsync();
        Assert.NotNull(effects.PreviewRequest);
        Assert.Equal("deny", effects.PreviewRequest.Policy.Rules[0].Effect);
        Assert.Equal("uiPreview", effects.PreviewRequest.Policy.Rules[0].Scope);
        Assert.Equal("operationClassification", effects.PreviewRequest.Policy.Rules[0].Selector!.Kind);
        Assert.Contains("0 allowed", cut.Markup);
        Assert.Contains("0 suppressed", cut.Markup);
    }

    [Theory]
    [InlineData(CapabilityKind.Tool, "tool")]
    [InlineData(CapabilityKind.McpServer, "mcp")]
    [InlineData(CapabilityKind.Skill, "skill")]
    public async Task Details_and_each_setup_kind_open_through_the_host(CapabilityKind kind, string suffix) {
        using var fixture = new AgentCapabilitiesHostFixture();
        var cut = fixture.Render(fixture.Alpha.Id);
        var dialogs = fixture.Context.Services.GetRequiredService<DialogService>();
        var detailsClick = cut.Find("[data-testid='agents-capability-details']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Single(dialogs.Dialogs));
        var details = Assert.Single(dialogs.Dialogs);
        Assert.Equal(typeof(CapabilityDetailsDialog), details.ComponentType);
        Assert.Equal(fixture.Capability.Id, details.Parameters[nameof(CapabilityDetailsDialog.CapabilityId)]);
        await cut.InvokeAsync(() => dialogs.CloseAsync());
        await detailsClick;
        var setupClick = cut.Find($"[data-testid='agents-capability-new-{suffix}']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Single(dialogs.Dialogs));
        var setup = Assert.Single(dialogs.Dialogs);
        Assert.Equal(typeof(CapabilitySetupWizardDialog), setup.ComponentType);
        Assert.Equal(kind, setup.Parameters[nameof(CapabilitySetupWizardDialog.InitialKind)]);
        await cut.InvokeAsync(() => dialogs.CloseAsync());
        await setupClick;
    }

    [Fact]
    public async Task Late_effect_cannot_publish_into_a_new_selection() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var pending = new TaskCompletionSource<Guid>();
        fixture.Workspace.Save = _ => pending.Task;
        var cut = fixture.Render(fixture.Alpha.Id);
        var notices = fixture.Context.Services.GetRequiredService<NotificationService>();
        var click = cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, fixture.Beta.Id));
        await cut.InvokeAsync(() => pending.SetException(new InvalidOperationException("Late assignment failure")));
        await click;
        Assert.Contains(fixture.Beta.Name, cut.Find(".agent-capabilities-panel__heading").TextContent);
        Assert.False(cut.Find("[data-testid='agents-capability-toggle']").HasAttribute("disabled"));
        Assert.Empty(notices.Messages);
    }

    private static CapabilityEffectsProxy RegisterEffects(AgentCapabilitiesHostFixture fixture) {
        var flow = DispatchProxy.Create<IAgentCapabilitySetupFlowService, CapabilityEffectsProxy>();
        var proxy = (CapabilityEffectsProxy)(object)flow;
        var chat = DispatchProxy.Create<IAgentChatLauncher, CapabilityEffectsProxy>();
        ((CapabilityEffectsProxy)(object)chat).Owner = proxy;
        fixture.Context.Services.AddSingleton(flow);
        fixture.Context.Services.AddSingleton(chat);
        return proxy;
    }
}

public class CapabilityEffectsProxy : DispatchProxy {
    public CapabilityEffectsProxy? Owner { get; set; }
    public CapabilityAccessPreviewRequest? PreviewRequest { get; private set; }
    public Guid? StartedAgentId { get; private set; }
    public int ChatCalls { get; private set; }

    protected override object? Invoke(MethodInfo? method, object?[]? args) {
        var owner = Owner ?? this;
        if (method?.Name == nameof(IAgentChatLauncher.StartNewChatAsync)) {
            owner.StartedAgentId = Assert.IsType<Guid>(args![0]);
            owner.ChatCalls++;
            return Task.FromResult<ActiveAgentChat>(null!);
        }

        if (method?.Name == nameof(IAgentCapabilitySetupFlowService.PreviewAccessAsync)) {
            owner.PreviewRequest = Assert.IsType<CapabilityAccessPreviewRequest>(args![0]);
            return Task.FromResult(new CapabilityAccessPreviewResult(Access.CapabilityValidationResult.Passed, new([], []), []));
        }

        throw new InvalidOperationException($"Unexpected effect call: {method?.Name}");
    }
}
