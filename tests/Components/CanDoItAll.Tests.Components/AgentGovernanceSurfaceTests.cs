using CanDoItAll.AgentFramework.UI.Governance;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentGovernanceSurfaceTests {
    [Fact]
    public void Surface_without_workspace_services_renders_all_sections() {
        using var context = Context();
        Assert.Null(context.Services.GetService<IAgentFrameworkWorkspaceService>());
        var cut = Render(context);
        foreach (var text in new[] { "Approvals", "Artifacts", "Checkpoints", "Tool receipts", "Live execution timeline", "Metrics" }) {
            Assert.Contains(text, cut.Markup);
        }
        Assert.Equal("Run 1", cut.Find("[data-testid=\"agents-governance-detail-title\"]").TextContent.Trim());
    }

    [Fact]
    public async Task Selection_intents_do_not_mutate_controlled_parameters() {
        using var context = Context();
        var intents = new List<GovernanceIntent>();
        var model = Presentation();
        var state = Ready();
        var cut = Render(context, model, state, intents.Add);
        await cut.Find("select").ChangeAsync(new ChangeEventArgs { Value = "" });
        await cut.FindAll("[data-testid='agents-governance-run-item']")[1].ClickAsync();
        Assert.Equal(new GovernanceIntent.SelectAgent(null), intents[0]);
        Assert.Equal(new GovernanceIntent.SelectRun(GovernancePanelFixture.R2), intents[1]);
        Assert.Same(model, cut.Instance.Presentation);
        Assert.Same(state, cut.Instance.State);
        Assert.Equal(GovernancePanelFixture.R1, cut.Instance.State.DesiredRunId);
    }

    public enum RetryLane { Catalog, List, Detail }

    [Theory]
    [InlineData(RetryLane.Catalog)]
    [InlineData(RetryLane.List)]
    [InlineData(RetryLane.Detail)]
    public async Task Retry_intent_addresses_only_its_lane(RetryLane lane) {
        using var context = Context();
        var state = Ready();
        var failed = new GovernanceLaneState(GovernanceReadPhase.Failed, "A bounded read failed.");
        state = lane switch {
            RetryLane.Catalog => state with { Catalog = failed },
            RetryLane.List => state with { List = failed },
            _ => state with { Detail = failed }
        };
        var intents = new List<GovernanceIntent>();
        var cut = Render(context, state: state, intent: intents.Add);
        var selector = lane switch {
            RetryLane.Catalog => "agents-governance-catalog-retry",
            RetryLane.List => "agents-governance-list-retry",
            _ => "agents-governance-detail-retry"
        };
        await cut.Find($"[data-testid='{selector}']").ClickAsync();
        Assert.IsType(lane switch {
            RetryLane.Catalog => typeof(GovernanceIntent.RetryCatalog),
            RetryLane.List => typeof(GovernanceIntent.RetryList),
            _ => typeof(GovernanceIntent.RetryDetail)
        }, Assert.Single(intents));
    }

    [Fact]
    public void Surface_encodes_HTML_like_runtime_labels() {
        using var context = Context();
        const string label = "<img id='governance-injected' src=x onerror=alert(1)>";
        var run = GovernancePanelFixture.Run1 with { Title = label, ProviderName = "<strong>provider</strong>" };
        var projected = GovernancePresentationMapping.Detail(GovernancePanelFixture.Detail(run), "<em>Agent</em>");
        var cut = Render(context, Presentation() with { Detail = projected, Runs = [projected.Run] });
        Assert.Empty(cut.FindAll("#governance-injected"));
        Assert.Equal(label, cut.Find("[data-testid=\"agents-governance-detail-title\"]").TextContent);
        Assert.Contains("&lt;img", cut.Markup);
    }

    [Fact]
    public async Task List_is_usable_while_selected_detail_loads() {
        using var context = Context();
        var intents = new List<GovernanceIntent>();
        var state = Ready() with { Detail = new(GovernanceReadPhase.Loading), AcceptedRunId = null };
        var cut = Render(context, Presentation() with { Detail = null }, state, intents.Add);
        var rows = cut.FindAll("[data-testid='agents-governance-run-item']");
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => {
            Assert.Equal("BUTTON", row.TagName);
            Assert.False(row.HasAttribute("disabled"));
        });
        cut.Find("[data-testid='agents-governance-detail-loading']");
        await rows[1].ClickAsync();
        Assert.Equal(new GovernanceIntent.SelectRun(GovernancePanelFixture.R2), Assert.Single(intents));
    }

    [Fact]
    public void Removed_target_remains_explicit_without_a_replacement_detail() {
        using var context = Context();
        var state = Ready() with { DesiredRunId = GovernancePanelFixture.R2, AcceptedRunId = null,
            Detail = new(GovernanceReadPhase.Unavailable, "Selected run unavailable.") };
        var model = Presentation();
        var cut = Render(context, model with { Runs = [model.Runs[0]], Detail = null }, state);
        cut.Find("[data-testid='agents-governance-detail-unavailable']");
        Assert.Equal(GovernancePanelFixture.R2, cut.Instance.State.DesiredRunId);
        Assert.DoesNotContain("Run 1", cut.Find("[data-testid=\"agents-governance-detail-title\"]").TextContent);
    }

    [Fact]
    public void Forbidden_sentinel_is_absent_after_domain_projection() {
        using var context = Context();
        var denied = "opaque-fixture-" + Guid.NewGuid().ToString("N");
        var run = GovernancePanelFixture.Run1 with { MetadataJson = denied, ResultSummary = denied, InputSummary = denied,
            SerializedSessionStateJson = denied, StructuredOutputRawOutput = denied, StructuredOutputValidationErrorsJson = denied };
        var model = Presentation() with { Detail = GovernancePresentationMapping.Detail(GovernancePanelFixture.Detail(run), "Agent") };
        var cut = Render(context, model);
        Assert.False(cut.Markup.Contains(denied, StringComparison.Ordinal), "A forbidden payload entered markup.");
    }

    [Fact]
    public void Long_labels_are_bounded_and_selected_state_is_accessible() {
        using var context = Context();
        var run = GovernancePanelFixture.Run1 with { Title = new string('x', 10000) };
        var detail = GovernancePresentationMapping.Detail(GovernancePanelFixture.Detail(run), "Agent");
        var cut = Render(context, Presentation() with { Detail = detail, Runs = [detail.Run] });
        Assert.True(cut.Find("[data-testid=\"agents-governance-detail-title\"]").TextContent.Length <= GovernancePresentationMapping.LabelLimit);
        Assert.EndsWith("…", cut.Find("[data-testid=\"agents-governance-detail-title\"]").TextContent);
        var current = cut.Find("[aria-current='true']");
        Assert.Contains("Selected execution run", current.QuerySelector("button")!.TextContent);
        var select = cut.Find("select");
        Assert.NotNull(cut.Find($"label[for='{select.Id}']"));
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static GovernanceViewState Ready() => new(GovernancePanelFixture.A, GovernancePanelFixture.A, true,
        GovernancePanelFixture.R1, GovernancePanelFixture.R1, GovernanceLaneState.Ready, GovernanceLaneState.Ready, GovernanceLaneState.Ready);

    private static GovernancePresentation Presentation() => new([new(GovernancePanelFixture.A, "Agent A")],
        [GovernancePresentationMapping.Run(GovernancePanelFixture.Run1, "Agent A"), GovernancePresentationMapping.Run(GovernancePanelFixture.Run2, "Agent A")],
        GovernancePresentationMapping.Detail(GovernancePanelFixture.Detail(GovernancePanelFixture.Run1), "Agent A"));

    private static IRenderedComponent<AgentGovernanceSurface> Render(BunitContext context, GovernancePresentation? model = null,
        GovernanceViewState? state = null, Action<GovernanceIntent>? intent = null)
        => context.Render<AgentGovernanceSurface>(p => p.Add(c => c.Presentation, model ?? Presentation())
            .Add(c => c.State, state ?? Ready()).Add(c => c.OnIntent, intent ?? (_ => { })));
}
