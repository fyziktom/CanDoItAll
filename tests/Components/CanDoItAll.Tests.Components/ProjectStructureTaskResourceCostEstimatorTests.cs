using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureTaskResourceCostEstimatorTests : TestContext
{
    private static readonly Guid ProjectId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly ProjectStructureTaskResourceSelection Person = new(
        ProjectStructureTaskResourceKind.Person,
        Guid.Parse("20000000-0000-0000-0000-000000000002"));

    public ProjectStructureTaskResourceCostEstimatorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public async Task Quote_is_lazy_cached_and_refreshed_only_by_explicit_action()
    {
        var quoteCalls = 0;
        ProjectTaskEstimate? changedEstimate = null;
        var initialEstimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            75m,
            "USD");
        var cut = RenderComponent<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, initialEstimate)
            .Add(component => component.EstimateChanged, value => changedEstimate = value)
            .Add(component => component.QuoteResolver, (request, _) =>
            {
                quoteCalls++;
                Assert.Equal(Person, request.Resource);
                return Task.FromResult(new ProjectStructureTaskResourceCostQuote(
                    ProjectStructureTaskResourceCostQuoteStatus.Available,
                    400m + quoteCalls,
                    "EUR",
                    "CRM workforce rate",
                    "Calculated from the selected rate.",
                    DateTimeOffset.Parse("2026-07-16T16:00:00Z")));
            }));

        Assert.Equal(0, quoteCalls);

        await cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, initialEstimate));
        Assert.Equal(1, quoteCalls);
        Assert.Equal(401m, changedEstimate?.ExpectedCostAmount);
        Assert.Equal("EUR", changedEstimate?.ExpectedCostCurrencyCode);

        await cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, initialEstimate));
        Assert.Equal(1, quoteCalls);

        await cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(
            Person,
            initialEstimate with { ExpectedEffortHours = 4m }));
        Assert.Equal(2, quoteCalls);

        cut.Find("[data-testid='project-structure-task-resource-cost-refresh']").Click();
        cut.WaitForAssertion(() => Assert.Equal(3, quoteCalls));
        Assert.Equal(403m, changedEstimate?.ExpectedCostAmount);
    }

    [Fact]
    public async Task Failed_refresh_preserves_the_existing_manual_cost()
    {
        var initialEstimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            275m,
            "USD");
        ProjectTaskEstimate? changedEstimate = null;
        var cut = RenderComponent<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, initialEstimate)
            .Add(component => component.EstimateChanged, value => changedEstimate = value)
            .Add(component => component.QuoteResolver, (_, _) =>
                Task.FromException<ProjectStructureTaskResourceCostQuote>(new InvalidOperationException("pricing unavailable"))));

        await cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, initialEstimate));
        cut.Render();

        Assert.Null(changedEstimate);
        Assert.Contains("existing expected cost was preserved", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Quote_for_previous_effort_is_not_applied_after_effort_changes()
    {
        var initialEstimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            275m,
            "USD");
        var quoteCompletion = new TaskCompletionSource<ProjectStructureTaskResourceCostQuote>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ProjectTaskEstimate? changedEstimate = null;
        var cut = RenderComponent<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, initialEstimate)
            .Add(component => component.EstimateChanged, value => changedEstimate = value)
            .Add(component => component.QuoteResolver, (_, _) => quoteCompletion.Task));

        var pendingQuote = cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, initialEstimate));
        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Estimate, initialEstimate with { ExpectedEffortHours = 4m }));
        quoteCompletion.SetResult(new ProjectStructureTaskResourceCostQuote(
            ProjectStructureTaskResourceCostQuoteStatus.Available,
            800m,
            "USD",
            "CRM workforce rate",
            "Calculated for the previous effort.",
            DateTimeOffset.Parse("2026-07-16T16:00:00Z")));
        await pendingQuote;

        Assert.Null(changedEstimate);
    }

    [Fact]
    public async Task Late_quote_does_not_overwrite_a_manual_cost_edit()
    {
        var initialEstimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            275m,
            "USD");
        var quoteCompletion = new TaskCompletionSource<ProjectStructureTaskResourceCostQuote>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ProjectTaskEstimate? changedEstimate = null;
        var cut = RenderComponent<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, initialEstimate)
            .Add(component => component.EstimateChanged, value => changedEstimate = value)
            .Add(component => component.QuoteResolver, (_, _) => quoteCompletion.Task));

        var pendingQuote = cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, initialEstimate));
        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Estimate, initialEstimate with
            {
                ExpectedCostAmount = 999m,
                ExpectedCostCurrencyCode = "EUR"
            }));
        quoteCompletion.SetResult(new ProjectStructureTaskResourceCostQuote(
            ProjectStructureTaskResourceCostQuoteStatus.Available,
            800m,
            "USD",
            "CRM workforce rate",
            "Calculated before the manual edit.",
            DateTimeOffset.Parse("2026-07-16T16:00:00Z")));
        await pendingQuote;

        Assert.Null(changedEstimate);
    }
}
