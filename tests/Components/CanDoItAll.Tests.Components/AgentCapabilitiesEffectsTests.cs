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

public sealed class AgentCapabilitiesEffectsTests {
    [Fact]
    public async Task Preview_receives_owner_cancellation_token() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var effects = CapabilityLifetimeEffects.Register(fixture);
        var pending = new TaskCompletionSource<CapabilityAccessPreviewResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        effects.Preview = (_, _) => pending.Task;
        var cut = fixture.Render(fixture.Alpha.Id);
        var click = cut.Find("[data-testid='agents-capability-access-preview']").ClickAsync();
        var token = effects.PreviewToken;
        try {
            cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, fixture.Beta.Id));
            cut.WaitForAssertion(() => Assert.Contains(fixture.Beta.Name, cut.Find(".agent-capabilities-panel__heading").TextContent));
            Assert.True(token.CanBeCanceled);
            Assert.True(token.IsCancellationRequested);
        } finally {
            pending.SetResult(CapabilityLifetimeEffects.EmptyPreview);
            await click;
        }
    }

    [Fact]
    public async Task Panel_disposal_closes_owned_details_overlay_and_preserves_unrelated_dialog() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var cut = fixture.Render(fixture.Alpha.Id);
        var dialogs = fixture.Context.Services.GetRequiredService<DialogService>();
        var unrelated = dialogs.OpenAsync("Unrelated", _ => builder => builder.AddContent(0, "Independent overlay"));
        var other = Assert.Single(dialogs.Dialogs);
        var click = cut.Find("[data-testid='agents-capability-details']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Equal(2, dialogs.Dialogs.Count));
        await cut.InvokeAsync(() => cut.FindComponent<AgentCapabilitiesSurface>().Instance.Intent.InvokeAsync(new AgentCapabilitiesIntent.OpenDetails(fixture.Capability.Id)));
        Assert.Equal(2, dialogs.Dialogs.Count);
        try {
            await fixture.Context.DisposeRenderedComponentsAsync();
            Assert.Same(other, Assert.Single(dialogs.Dialogs));
        } finally {
            foreach (var dialog in dialogs.Dialogs.ToArray()) {
                await dialog.CloseAsync();
            }
            await click;
            await unrelated;
        }
    }

    [Theory]
    [InlineData("tool")]
    [InlineData("mcp")]
    [InlineData("skill")]
    public async Task Panel_disposal_closes_owned_setup_overlay_and_preserves_unrelated_dialog(string kind) {
        using var fixture = new AgentCapabilitiesHostFixture();
        var cut = fixture.Render(fixture.Alpha.Id);
        var dialogs = fixture.Context.Services.GetRequiredService<DialogService>();
        var unrelated = dialogs.OpenAsync("Unrelated", _ => builder => builder.AddContent(0, "Independent overlay"));
        var other = dialogs.Dialogs.Single();
        var click = cut.Find($"[data-testid='agents-capability-new-{kind}']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Equal(2, dialogs.Dialogs.Count));
        try {
            await fixture.Context.DisposeRenderedComponentsAsync();
            Assert.Same(other, Assert.Single(dialogs.Dialogs));
        } finally {
            foreach (var dialog in dialogs.Dialogs.ToArray()) {
                await dialog.CloseAsync();
            }
            await click;
            await unrelated;
        }
    }

    [Fact]
    public async Task Curator_launch_is_single_flight_across_selection_changes() {
        using var fixture = new AgentCapabilitiesHostFixture();
        fixture.Workspace.Agents = [fixture.Alpha, fixture.Beta, fixture.Beta with {
            Id = CapabilityCuratorAgentIdentity.AgentId, TemplateKey = CapabilityCuratorAgentIdentity.TemplateKey
        }];
        var effects = CapabilityLifetimeEffects.Register(fixture);
        var pending = new TaskCompletionSource<ActiveAgentChat>(TaskCreationOptions.RunContinuationsAsynchronously);
        effects.Launch = _ => pending.Task;
        var cut = fixture.Render(fixture.Alpha.Id);
        var first = cut.Find("[data-testid='agents-capability-curator-open']").ClickAsync();
        cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, fixture.Beta.Id));
        cut.WaitForAssertion(() => Assert.Contains(fixture.Beta.Name, cut.Find(".agent-capabilities-panel__heading").TextContent));
        var second = cut.InvokeAsync(() => cut.FindComponent<AgentCapabilitiesSurface>().Instance.Intent.InvokeAsync(new AgentCapabilitiesIntent.OpenCurator()));
        try {
            await cut.InvokeAsync(() => Assert.Equal(1, effects.ChatCalls));
        } finally {
            pending.SetResult(CapabilityLifetimeEffects.Chat());
            await Task.WhenAll(first, second);
        }
    }
}

public class CapabilityLifetimeEffects : DispatchProxy {
    public CapabilityLifetimeEffects? Owner { get; set; }
    public Func<CapabilityAccessPreviewRequest, CancellationToken, Task<CapabilityAccessPreviewResult>>? Preview { get; set; }
    public Func<CancellationToken, Task<ActiveAgentChat>>? Launch { get; set; }
    public CancellationToken PreviewToken { get; private set; }
    public int ChatCalls { get; private set; }
    public int PreviewCalls { get; private set; }
    public static CapabilityAccessPreviewResult EmptyPreview => new(Access.CapabilityValidationResult.Passed, new([], []), []);
    public static ActiveAgentChat Chat() => new(new(Guid.NewGuid()),
        new(CapabilityCuratorAgentIdentity.AgentId, "Curator", "Capability review", ""), Guid.NewGuid(),
        ActiveAgentChatVisibility.Visible, ActiveAgentChatRunState.Idle, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null);
    internal static CapabilityLifetimeEffects Register(AgentCapabilitiesHostFixture fixture) {
        var flow = DispatchProxy.Create<IAgentCapabilitySetupFlowService, CapabilityLifetimeEffects>();
        var owner = (CapabilityLifetimeEffects)(object)flow;
        var launcher = DispatchProxy.Create<IAgentChatLauncher, CapabilityLifetimeEffects>();
        ((CapabilityLifetimeEffects)(object)launcher).Owner = owner;
        fixture.Context.Services.AddSingleton(flow);
        fixture.Context.Services.AddSingleton(launcher);
        return owner;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args) {
        var owner = Owner ?? this;
        if (method!.Name == nameof(IAgentCapabilitySetupFlowService.PreviewAccessAsync)) {
            owner.PreviewCalls++;
            owner.PreviewToken = (CancellationToken)args![1]!;
            return owner.Preview?.Invoke((CapabilityAccessPreviewRequest)args[0]!, owner.PreviewToken) ?? Task.FromResult(EmptyPreview);
        }
        if (method.Name == nameof(IAgentChatLauncher.StartNewChatAsync)) {
            owner.ChatCalls++;
            return owner.Launch?.Invoke((CancellationToken)args![1]!) ?? Task.FromResult(Chat());
        }
        throw new InvalidOperationException("Unexpected capability effect.");
    }
}
