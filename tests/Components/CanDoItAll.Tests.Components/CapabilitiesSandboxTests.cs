using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.UI.Catalog;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.AgentFramework.UiSandbox.Components;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class CapabilitiesSandboxTests : IDisposable {
    private readonly BunitContext context = new();

    public CapabilitiesSandboxTests() {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        using var stream = typeof(CatalogAssets).Assembly.GetManifestResourceStream("CatalogFixture.json")!;
        context.Services.AddSingleton(JsonSerializer.Deserialize<AgentCatalogSnapshot>(stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
    }

    [Fact]
    public void Capabilities_query_renders_the_real_surface_without_feature_services() {
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/agents?specimen=capabilities&scenario=baseline");
        var cut = context.Render<Routes>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='agents-capabilities-panel']")));
    }

    [Theory]
    [InlineData("loading", "agents-capability-loading")]
    [InlineData("failed", "agents-capability-load-failed")]
    [InlineData("missing-target", "agents-capability-load-failed")]
    [InlineData("assignment-pending", "agents-capability-operation")]
    [InlineData("assignment-rejected", "agents-capability-operation")]
    [InlineData("assignment-conflict", "agents-capability-recover")]
    [InlineData("committed-warning", "agents-capability-recover")]
    [InlineData("unconfirmed", "agents-capability-recover")]
    [InlineData("exact-before", "agents-capability-retry-assignment")]
    [InlineData("intervening", "agents-capability-adopt")]
    [InlineData("verification-pending", "agents-capability-operation")]
    [InlineData("verification-superseded", "agents-capability-operation")]
    [InlineData("verification-recovery", "agents-capability-recover")]
    [InlineData("diagnostic-acknowledgement", "agents-capability-acknowledge-diagnostic")]
    [InlineData("curator-unconfirmed", "agents-capability-curator-acknowledge")]
    public void Query_restores_loading_and_recovery_actions(string scenario, string testId) {
        var cut = Render(scenario);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find($"[data-testid='{testId}']")));
    }

    [Theory]
    [InlineData("no-agents", "Create a technical agent first")]
    [InlineData("no-capabilities", "No capabilities are cataloged yet")]
    [InlineData("selected", "Capabilities Alpha")]
    [InlineData("kinds-and-proof", "Plugin inspection")]
    [InlineData("long-content", "deliberately long")]
    [InlineData("preview-valid", "1 allowed")]
    [InlineData("preview-invalid", "A selector value is required.")]
    public void Query_restores_empty_selected_and_preview_presentations(string scenario, string text) {
        var cut = Render(scenario);
        cut.WaitForAssertion(() => Assert.Contains(text, cut.Markup));
    }

    [Theory]
    [InlineData("curator-available", false)]
    [InlineData("curator-unavailable", true)]
    [InlineData("curator-pending", true)]
    [InlineData("curator-opened", false)]
    [InlineData("curator-acknowledged", false)]
    public void Curator_action_matches_sample_eligibility(string scenario, bool disabled) {
        var cut = Render(scenario);
        Assert.Equal(disabled, cut.Find("[data-testid='agents-capability-curator-open']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Controlled_assignment_changes_only_the_selected_sample() {
        var cut = Render("selected");
        var before = cut.Find("[data-testid='agents-capability-toggle']").TextContent;
        await cut.Find("[data-testid='agents-capability-toggle']").ClickAsync();
        Assert.NotEqual(before, cut.Find("[data-testid='agents-capability-toggle']").TextContent);
        Assert.Contains("Sample assignment changed:", cut.Find("[data-testid='sandbox-intent']").TextContent);
    }

    [Fact]
    public async Task Scenario_replacement_preserves_raw_access_draft_and_local_filter() {
        var cut = Render("selected");
        cut.Find("[data-testid='agents-capability-access-reason']").Change("  Keep this raw sample reason  ");
        cut.Find("[data-testid='agents-capability-search']").Input("Review skill");
        await cut.Find("[data-testid='sandbox-capabilities-scenario']").ChangeAsync("unconfirmed");
        Assert.Equal("  Keep this raw sample reason  ", cut.Find("[data-testid='agents-capability-access-reason']").GetAttribute("value"));
        Assert.Equal("Review skill", cut.Find("[data-testid='agents-capability-search']").GetAttribute("value"));
        Assert.Single(cut.FindAll("[data-testid='agents-capability-card']"));
    }

    [Theory]
    [InlineData("diagnostic-acknowledgement", "agents-capability-acknowledge-diagnostic", "No diagnostic runs.")]
    [InlineData("curator-unconfirmed", "agents-capability-curator-acknowledge", "No chat is created or deleted.")]
    [InlineData("unconfirmed", "agents-capability-recover", "No mutation is replayed.")]
    [InlineData("exact-before", "agents-capability-retry-assignment", "Deliberate sample assignment retry")]
    [InlineData("intervening", "agents-capability-adopt", "Sample current state adopted.")]
    public async Task Recovery_intents_unlock_only_controlled_sample_state(string scenario, string testId, string expected) {
        var cut = Render(scenario);
        await cut.Find($"[data-testid='{testId}']").ClickAsync();
        Assert.Contains(expected, cut.Find("[data-testid='sandbox-intent']").TextContent);
        Assert.Empty(cut.FindAll($"[data-testid='{testId}']"));
    }

    [Fact]
    public async Task Failed_target_retry_preserves_its_identity() {
        var cut = Render("selected", "ffffffff-ffff-ffff-ffff-ffffffffffff");
        await cut.Find("[data-testid='agents-capability-load-retry']").ClickAsync();
        Assert.NotNull(cut.Find("[data-testid='agents-capability-load-failed']"));
        Assert.Contains("agentId=ffffffff-ffff-ffff-ffff-ffffffffffff",
            context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Catalog_query_compatibility_and_unknown_specimen_normalization_are_preserved() {
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/agents?specimen=unknown&scenario=card-states&layout=flexible");
        var cut = context.Render<Routes>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".catalog-sandbox__specimen")));
        Assert.Contains("scenario=card-states", navigation.Uri);
        Assert.Contains("layout=flexible", navigation.Uri);
        Assert.DoesNotContain("specimen=", navigation.Uri);
    }

    [Fact]
    public async Task Returning_to_catalog_preserves_the_requested_layout() {
        var cut = Render("selected", layout: "flexible");
        await cut.Find("[data-testid='sandbox-catalog']").ClickAsync();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='sandbox-normal']")));
        Assert.Contains("layout=flexible", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    private IRenderedComponent<Routes> Render(string scenario, string? agent = null, string layout = "matched") {
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(
            $"/agents?specimen=capabilities&scenario={scenario}&layout={layout}" +
            (agent is null ? "" : $"&agentId={agent}"));
        var cut = context.Render<Routes>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='agents-capabilities-panel']")));
        return cut;
    }


    public void Dispose() => context.Dispose();
}
