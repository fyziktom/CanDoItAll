using System.Collections.Immutable;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using AccessEffect = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityAccessEffect;
using AccessScope = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityAccessScope;
using SelectorKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilitySelectorKind;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentCapabilitiesSurfaceTests : IDisposable {
    private readonly BunitContext context = new();
    private readonly List<AgentCapabilitiesIntent> intents = [];
    private readonly AgentCapabilitiesAgent agent = new(Guid.NewGuid(), "Surface agent", "Role", "Model", 0);
    private readonly CapabilityCatalogItem capability = new(Guid.NewGuid(), CapabilityKind.Tool, "surface-tool", "Surface tool",
        "Tool description", "fixture", "{}", CapabilityProofStatus.NotRun, "", null, false) { Tags = ["fixture"] };

    public AgentCapabilitiesSurfaceTests() {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
    }

    private AgentCapabilitiesSnapshot Snapshot => new([agent], [capability], [], new("Curator", "", false));
    private IRenderedComponent<AgentCapabilitiesSurface> Render(AgentCapabilitiesSnapshot? snapshot = null,
        AgentCapabilitiesLoadState state = AgentCapabilitiesLoadState.Ready) => context.Render<AgentCapabilitiesSurface>(parameters => parameters
        .Add(component => component.Snapshot, snapshot ?? Snapshot)
        .Add(component => component.Selection, new AgentCapabilitiesSelection(agent.Id))
        .Add(component => component.LoadState, state)
        .Add(component => component.Intent, intent => intents.Add(intent)));

    [Fact]
    public void Renders_loading_without_services() {
        var cut = Render(state: AgentCapabilitiesLoadState.Loading);
        Assert.NotNull(cut.Find("[data-testid='agents-capability-loading']"));
        Assert.Empty(cut.FindAll("[data-testid='agents-capability-toggle']"));
    }

    [Fact]
    public void Renders_no_agents_without_services() {
        var cut = Render(AgentCapabilitiesSnapshot.Empty);
        Assert.Contains("Choose a technical agent", cut.Markup);
    }

    [Fact]
    public void Renders_selected_agent_and_capabilities_without_services() {
        var cut = Render();
        Assert.Contains(agent.Name, cut.Find(".agent-capabilities-panel__heading").TextContent);
        Assert.Contains(capability.Name, cut.Find("[data-testid='agents-capability-card']").TextContent);
    }

    [Fact]
    public void Renders_no_capabilities_state() {
        var cut = Render(Snapshot with { Capabilities = [] });
        Assert.Contains("No capabilities are cataloged yet", cut.Markup);
    }

    [Fact]
    public async Task Emits_select_agent_intent() {
        var cut = Render();
        await cut.Find("[data-testid='agents-capability-tree-agent']").ClickAsync();
        Assert.Equal(new AgentCapabilitiesIntent.SelectAgent(agent.Id), Assert.Single(intents));
    }

    [Fact]
    public async Task Emits_assignment_intent() {
        var cut = Render();
        await cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        Assert.Equal(new AgentCapabilitiesIntent.ToggleAssignment(capability.Id), Assert.Single(intents));
    }

    [Fact]
    public async Task Emits_verification_intent() {
        var cut = Render(Snapshot with { SelectedCapabilityIds = [capability.Id] });
        await cut.Find("[data-testid='agents-capability-verify']").ClickAsync();
        Assert.Equal(new AgentCapabilitiesIntent.VerifyCapability(capability.Id), Assert.Single(intents));
    }

    [Fact]
    public async Task Emits_details_intent() {
        var cut = Render();
        await cut.Find("[data-testid='agents-capability-details']").ClickAsync();
        Assert.Equal(new AgentCapabilitiesIntent.OpenDetails(capability.Id), Assert.Single(intents));
    }

    [Theory]
    [InlineData(CapabilityKind.Tool, "tool")]
    [InlineData(CapabilityKind.McpServer, "mcp")]
    [InlineData(CapabilityKind.Skill, "skill")]
    public async Task Emits_each_create_kind_intent(CapabilityKind kind, string suffix) {
        var cut = Render();
        await cut.Find($"[data-testid='agents-capability-new-{suffix}']").ClickAsync();
        Assert.Equal(new AgentCapabilitiesIntent.CreateCapability(kind), Assert.Single(intents));
    }

    [Fact]
    public async Task Emits_access_preview_intent_with_typed_draft() {
        var cut = Render();
        cut.Find("[data-testid='agents-capability-access-effect']").Change(nameof(AccessEffect.Allow));
        cut.Find("[data-testid='agents-capability-access-scope']").Change(nameof(AccessScope.AgentDefault));
        cut.Find("[data-testid='agents-capability-access-selector']").Change(nameof(SelectorKind.CapabilityKey));
        cut.Find("[data-testid='agents-capability-access-value']").Change("surface-tool");
        await cut.Find("[data-testid='agents-capability-access-preview']").ClickAsync();
        var intent = Assert.IsType<AgentCapabilitiesIntent.PreviewAccess>(Assert.Single(intents));
        Assert.Equal(new AgentCapabilityAccessDraft(AccessEffect.Allow, AccessScope.AgentDefault, SelectorKind.CapabilityKey, "surface-tool"), intent.Draft);
    }

    [Fact]
    public async Task Emits_curator_intent_only_when_ready() {
        var cut = Render();
        Assert.True(cut.Find("[data-testid='agents-capability-curator-open']").HasAttribute("disabled"));
        Assert.Empty(intents);
        cut.Render(parameters => parameters.Add(component => component.Snapshot, Snapshot with { Curator = new("Curator", "", true) }));
        await cut.Find("[data-testid='agents-capability-curator-open']").ClickAsync();
        Assert.IsType<AgentCapabilitiesIntent.OpenCurator>(Assert.Single(intents));
    }

    [Fact]
    public void Search_and_filter_state_remains_local() {
        var cut = Render();
        cut.Find("[data-testid='agents-capability-search']").Input("absent");
        Assert.Empty(cut.FindAll("[data-testid='agents-capability-card']"));
        cut.Find("[data-testid='agents-capability-search']").Input("Surface");
        cut.Find("[data-testid='agents-capability-type-filter']").Change("Skill");
        Assert.Empty(cut.FindAll("[data-testid='agents-capability-card']"));
        Assert.Empty(intents);
        cut.Find("[data-testid='agents-capability-type-filter']").Change("Tool");
        Assert.Single(cut.FindAll("[data-testid='agents-capability-card']"));
    }

    [Fact]
    public void Snapshot_refresh_preserves_local_filters() {
        var cut = Render();
        cut.Find("[data-testid='agents-capability-search']").Input("absent");
        cut.Render(parameters => parameters.Add(component => component.Snapshot, Snapshot with { SelectedCapabilityIds = [capability.Id] }));
        Assert.Equal("absent", cut.Find("[data-testid='agents-capability-search']").GetAttribute("value"));
        Assert.Empty(cut.FindAll("[data-testid='agents-capability-card']"));
    }

    [Fact]
    public async Task Selected_capability_state_is_controlled_by_parent_snapshot() {
        var cut = Render();
        await cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        Assert.DoesNotContain("Attached", cut.Find("[data-testid='agents-capability-card']").TextContent);
        cut.Render(parameters => parameters.Add(component => component.Snapshot, Snapshot with { SelectedCapabilityIds = [capability.Id] }));
        Assert.Contains("Attached", cut.Find("[data-testid='agents-capability-card']").TextContent);
        Assert.Single(intents);
    }

    public void Dispose() => context.Dispose();
}
