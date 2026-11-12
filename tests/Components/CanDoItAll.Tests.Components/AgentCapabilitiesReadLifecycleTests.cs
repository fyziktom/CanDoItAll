using CanDoItAll.AgentFramework.UI.Capabilities;
using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentCapabilitiesReadLifecycleTests {
    [Fact]
    public async Task Retired_access_publishes_unavailable_context_without_revealing_the_previous_catalog() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var states = new List<AgentChatContextAccessState>();
        fixture.Context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        var cut = fixture.Context.Render<AgentCapabilitiesPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, fixture.Alpha.Id)
            .Add(component => component.ContextAccessStateChanged, state => states.Add(state)));
        var services = fixture.Context.Services;
        Assert.IsType<EditingUiTestAccess>(services.GetRequiredService<IEditingUiAccess>()).Allowed = false;
        services.GetRequiredService<EditingChangeFeed>().Publish(new(services.GetRequiredService<AgentConfigurationChanges>().Scope,
            EditingOwner.AgentCapability, fixture.Capability.Id), EditingChangeKind.AccessChanged);
        cut.WaitForElement("[data-testid='capabilities-access-unavailable']");
        Assert.DoesNotContain(fixture.Alpha.Name, cut.Markup);
        Assert.Equal(AgentChatContextAccessState.Failed, states.Last());
    }

    [Fact]
    public async Task Selecting_an_agent_keeps_unread_catalog_hints_until_explicit_refresh() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var cut = fixture.Render(fixture.Alpha.Id);
        cut.WaitForElement("[data-testid='agents-capability-search']").Input("Fixture");
        var reads = fixture.Workspace.CatalogReads;
        fixture.Workspace.Capabilities = [fixture.Capability with { Name = "Fixture peer capability" }];
        var services = fixture.Context.Services;
        services.GetRequiredService<EditingChangeFeed>().Publish(new(services.GetRequiredService<AgentConfigurationChanges>().Scope,
            EditingOwner.AgentCapability, fixture.Capability.Id), EditingChangeKind.EditableContentChanged);
        cut.WaitForElement("[data-testid='capabilities-catalog-changed']");
        Assert.Equal(reads, fixture.Workspace.CatalogReads);
        await cut.InvokeAsync(() => cut.FindComponent<AgentCapabilitiesSurface>().Instance.Intent.InvokeAsync(new AgentCapabilitiesIntent.SelectAgent(fixture.Beta.Id)));
        Assert.Single(cut.FindAll("[data-testid='capabilities-catalog-changed']"));
        Assert.Equal(reads, fixture.Workspace.CatalogReads);
        await cut.Find("[data-testid='capabilities-catalog-refresh']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='capabilities-catalog-changed']"));
        Assert.Equal(reads + 1, fixture.Workspace.CatalogReads);
        Assert.Equal(fixture.Beta.Id, cut.FindComponent<AgentCapabilitiesSurface>().Instance.Selection.AgentId);
        Assert.Equal("Fixture", cut.Find("[data-testid='agents-capability-search']").GetAttribute("value"));
        Assert.Contains("Fixture peer capability", cut.Markup);
    }

    [Fact]
    public async Task Pending_target_read_does_not_render_the_previous_editor() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var pending = new TaskCompletionSource<AgentEditorModel>();
        fixture.Workspace.ReadEditor = (id, _) => id == fixture.Beta.Id
            ? pending.Task : Task.FromResult(AgentEditorModel.FromDefinition(fixture.Alpha));
        var cut = fixture.Render(fixture.Alpha.Id);
        cut.WaitForAssertion(() => Assert.Contains(fixture.Alpha.Name, cut.Find(".agent-capabilities-panel__heading").TextContent));
        cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, fixture.Beta.Id));
        try {
            Assert.DoesNotContain(fixture.Alpha.Name, cut.Find(".agent-capabilities-panel__heading").TextContent);
            Assert.Empty(cut.FindAll("[data-testid='agents-capability-toggle']"));
        }
        finally {
            await cut.InvokeAsync(() => pending.TrySetResult(AgentEditorModel.FromDefinition(fixture.Beta)));
        }
    }

    [Fact]
    public async Task Late_read_failure_does_not_break_the_new_target() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var pending = new TaskCompletionSource<AgentEditorModel>();
        fixture.Workspace.ReadEditor = (id, _) => id == fixture.Alpha.Id
            ? pending.Task : Task.FromResult(AgentEditorModel.FromDefinition(fixture.Beta));
        var cut = fixture.Render(fixture.Alpha.Id);
        cut.Render(parameters => parameters.Add(component => component.PreferredAgentId, fixture.Beta.Id));
        cut.WaitForAssertion(() => Assert.Contains(fixture.Beta.Name, cut.Find(".agent-capabilities-panel__heading").TextContent));
        await cut.InvokeAsync(() => pending.SetException(new InvalidOperationException("Late fixture read failure.")));
        cut.WaitForAssertion(() => {
            Assert.Contains(fixture.Beta.Name, cut.Find(".agent-capabilities-panel__heading").TextContent);
            Assert.Empty(cut.FindAll("[data-testid='agents-capability-load-failed']"));
        });
    }

    [Fact]
    public async Task Disposing_panel_cancels_the_owned_read() {
        using var fixture = new AgentCapabilitiesHostFixture();
        var pending = new TaskCompletionSource<AgentEditorModel>();
        fixture.Workspace.ReadEditor = (_, _) => pending.Task;
        var cut = fixture.Render(fixture.Alpha.Id);
        var token = fixture.Workspace.LastReadToken;
        try {
            await fixture.Context.DisposeComponentsAsync();
            Assert.True(token.IsCancellationRequested);
        }
        finally {
            pending.TrySetResult(AgentEditorModel.FromDefinition(fixture.Alpha));
        }
    }

    [Fact]
    public async Task Assignment_failure_keeps_authoritative_attachment_and_requires_verification() {
        using var fixture = new AgentCapabilitiesHostFixture();
        fixture.Workspace.Save = _ => Task.FromException<Guid>(new InvalidOperationException("Characterized assignment rejection."));
        var cut = fixture.Render(fixture.Alpha.Id);
        cut.WaitForElement("[data-testid='agents-capability-toggle']");
        await cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        cut.WaitForAssertion(() => {
            Assert.Equal(1, fixture.Workspace.SaveCalls);
            Assert.Empty(cut.FindComponent<AgentCapabilitiesSurface>().Instance.Snapshot.SelectedCapabilityIds);
            Assert.NotNull(cut.Find("[data-testid='agents-capability-recover']"));
            Assert.Empty(fixture.Alpha.Capabilities);
        });
    }
}

internal sealed class AgentCapabilitiesHostFixture : IDisposable {
    public BunitContext Context { get; } = new();
    public AgentDefinition Alpha { get; } = Agent("Alpha read fixture");
    public AgentDefinition Beta { get; } = Agent("Beta read fixture");
    public CapabilityCatalogItem Capability { get; } = new(
        Guid.NewGuid(), CapabilityKind.Tool, "capability-fixture", "Fixture capability",
        "Local capability fixture", "fixture", "{}", CapabilityProofStatus.NotRun, "", null, false) { Tags = ["fixture"] };
    public CapabilitiesWorkspaceProxy Workspace { get; }

    public AgentCapabilitiesHostFixture() {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.Services.AddLogging();
        Context.Services.AddCanDoItAllBaseLib();
        Context.Services.AddSingleton<CanDoItAll.Infrastructure.Persistence.IDatabaseSwitchNotificationService,
            CanDoItAll.Infrastructure.Persistence.DatabaseSwitchNotificationService>();
        Context.Services.AddAgentConfigurationFixture();
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, CapabilitiesWorkspaceProxy>();
        Workspace = (CapabilitiesWorkspaceProxy)(object)service;
        Workspace.Agents = [Alpha, Beta];
        Workspace.Capabilities = [Capability];
        Context.Services.AddSingleton(service);
        Context.Services.AddSingleton(DispatchProxy.Create<IAgentCapabilitySetupFlowService, CapabilitiesUnexpectedProxy>());
        Context.Services.AddSingleton(DispatchProxy.Create<IAgentChatLauncher, CapabilitiesUnexpectedProxy>());
    }

    public IRenderedComponent<AgentCapabilitiesPanel> Render(Guid? agentId) {
        Context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        return Context.Render<AgentCapabilitiesPanel>(parameters => parameters.Add(component => component.PreferredAgentId, agentId));
    }

    public void Dispose() => Context.Dispose();

    public static AgentDefinition Agent(string name) => new(
        Guid.NewGuid(), name, "Test agent", "Capability seam fixture", "Stay scoped.",
        AgentLifecycleStatus.Active, null, "fixture-model", AgentWorkloadKind.General,
        AgentChatHistoryMode.FrameworkManaged, 0.2, true, false, "{}", false, "",
        AgentPermissionsPolicy.Default, [], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
}

public class CapabilitiesWorkspaceProxy : DispatchProxy {
    public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];
    public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; set; } = [];
    public Func<Guid, CancellationToken, Task<AgentEditorModel>>? ReadEditor { get; set; }
    public Func<AgentEditorModel, Task<Guid>>? Save { get; set; }
    public Func<Guid, Guid, CancellationToken, Task>? VerifyOperation { get; set; }
    public Func<Guid, CancellationToken, Task<CapabilityEditorModel>>? ReadCapability { get; set; }
    public bool CatalogReadFails { get; set; }
    public int SaveCalls { get; private set; }
    public int VerifyCalls { get; private set; }
    public int CatalogReads { get; private set; }
    public CancellationToken LastReadToken { get; private set; }

    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch {
        nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) => CatalogReadFails ? Task.FromException<IReadOnlyList<AgentDefinition>>(new IOException("Catalog unavailable")) : Task.FromResult(Agents),
        nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) => ReadCapabilities(),
        nameof(IAgentFrameworkWorkspaceService.GetAgentEditorAsync) => Read(Assert.IsType<Guid>(args![0]), Assert.IsType<CancellationToken>(args[1])),
        nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync) => SaveDraft(Assert.IsType<AgentEditorModel>(args![0])),
        nameof(IAgentFrameworkWorkspaceService.VerifyCapabilityAsync) => Verify((Guid)args![0]!, (Guid)args[1]!, (CancellationToken)args[2]!),
        nameof(IAgentFrameworkWorkspaceService.GetCapabilityEditorAsync) => ReadCapability?.Invoke((Guid)args![0]!, (CancellationToken)args[1]!)
            ?? Task.FromResult(CapabilityEditorModel.FromDefinition(Capabilities.Single(item => item.Id == (Guid)args![0]!))),
        _ => throw new InvalidOperationException($"Unexpected workspace call: {method?.Name}")
    };

    private Task<IReadOnlyList<CapabilityCatalogItem>> ReadCapabilities() {
        CatalogReads++;
        return Task.FromResult(Capabilities);
    }

    private Task<AgentEditorModel> Read(Guid id, CancellationToken token) {
        LastReadToken = token;
        return ReadEditor?.Invoke(id, token) ?? Task.FromResult(AgentEditorModel.FromDefinition(Agents.Single(agent => agent.Id == id)));
    }

    private Task<Guid> SaveDraft(AgentEditorModel model) {
        SaveCalls++;
        return Save?.Invoke(model) ?? Task.FromResult(model.Id!.Value);
    }

    private async Task<CapabilityVerificationOutcome> Verify(Guid agentId, Guid capabilityId, CancellationToken token) {
        VerifyCalls++;
        await (VerifyOperation?.Invoke(agentId, capabilityId, token) ?? Task.CompletedTask);
        return new(CapabilityVerificationDisposition.Committed);
    }
}

public class CapabilitiesUnexpectedProxy : DispatchProxy {
    protected override object? Invoke(MethodInfo? method, object?[]? args)
        => throw new InvalidOperationException($"Unexpected service call: {method?.Name}");
}
