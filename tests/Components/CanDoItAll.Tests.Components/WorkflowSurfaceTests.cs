using System.Collections.Immutable;
using Bunit;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.AgentFramework.Workflows.UI;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class WorkflowSurfaceTests {
    public static IEnumerable<object[]> Scenarios => Enum.GetValues<WorkflowScenario>().Select(value => new object[] { value });

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Every_scenario_renders_through_the_production_surfaces_without_workflow_services(WorkflowScenario scenario) {
        using var context = AgentChatSurfaceTests.Context();
        var cut = context.Render<CanDoItAll.AgentFramework.UiSandbox.Components.WorkflowSpecimen>(parameters => parameters
            .Add(component => component.ScenarioQuery, scenario.ToString()));
        Assert.Single(cut.FindComponents<WorkflowShellSurface>());
        Assert.Single(cut.FindAll("[data-testid='sandbox-workflows-specimen']"));
        Assert.Empty(cut.FindAll("script"));
        if (scenario == WorkflowScenario.Templates) {
            Assert.Single(cut.FindComponents<WorkflowTemplateCatalogSurface>());
        }
        if (scenario == WorkflowScenario.DashboardAnalytics) {
            Assert.Single(cut.FindComponents<WorkflowOverviewSurface>());
            cut.Find("[data-testid='workflows-tab-analytics']").Click();
            cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<WorkflowAnalyticsSurface>()));
        }
    }

    [Fact]
    public void Human_response_and_paging_intents_keep_the_rendered_revision_and_exact_target() {
        using var context = AgentChatSurfaceTests.Context();
        var fixture = new WorkflowSandboxFixture();
        fixture.SetScenario(WorkflowScenario.PendingInput);
        var intents = new List<WorkflowIntent>();
        var cut = context.Render<WorkflowHistorySurface>(parameters => parameters
            .Add(component => component.Presentation, fixture.History).Add(component => component.Intent, value => intents.Add(value)));
        cut.Find("[data-testid='workflows-pending-response']").Change("{\"approved\":false}");
        cut.Find("[data-testid='workflows-respond-request']").Click();
        cut.Find("[data-testid='workflows-event-page-next']").Click();
        Assert.Equal(3, intents.Count);
        Assert.All(intents, intent => Assert.Equal(fixture.History.Revision, intent.Revision));
        Assert.Equal(fixture.History.Requests[0].Id, intents[0].TargetId);
        Assert.Equal(intents[0].TargetId, intents[1].TargetId);
        Assert.Equal(WorkflowAction.Respond, intents[1].Action);
        Assert.Equal(1, intents[2].Delta);
        Assert.Equal(WorkflowAction.EventPage, intents[2].Action);
    }

    [Fact]
    public void Template_search_preview_and_long_content_remain_encoded_and_controlled() {
        using var context = AgentChatSurfaceTests.Context();
        var fixture = new WorkflowSandboxFixture();
        fixture.SetScenario(WorkflowScenario.Templates);
        var template = fixture.Templates.Selected! with { Name = "<script>untrusted()</script> " + new string('x', 600) };
        var presentation = fixture.Templates with { Templates = [template], Selected = template };
        var intents = new List<WorkflowIntent>();
        var cut = context.Render<WorkflowTemplateCatalogSurface>(parameters => parameters
            .Add(component => component.Presentation, presentation).Add(component => component.Intent, value => intents.Add(value)));
        Assert.Empty(cut.FindAll("script"));
        Assert.Contains(template.Name, cut.Markup.Replace("&lt;", "<").Replace("&gt;", ">"), StringComparison.Ordinal);
        cut.Find("input").Change("review");
        cut.Find("[data-testid='workflows-template-preview']").Click();
        Assert.Equal(WorkflowAction.TemplateSearch, intents[0].Action);
        Assert.Equal(template.Key, intents[1].TemplateKey);
        Assert.Equal(WorkflowAction.PreviewTemplate, intents[1].Action);
    }
}
