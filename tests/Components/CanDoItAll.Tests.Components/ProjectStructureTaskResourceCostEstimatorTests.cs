using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureTaskResourceCostEstimatorTests : BunitContext
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
        var cut = Render<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
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
                    DateTimeOffset.Parse("2026-07-16T16:00:00Z"),
                    ProjectStructureTaskResourceCostSource.CrmWorkforceRate));
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
    public async Task Failed_refresh_does_not_replace_the_existing_cost_with_stale_data()
    {
        var initialEstimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            275m,
            "USD");
        ProjectTaskEstimate? changedEstimate = null;
        var cut = Render<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, initialEstimate)
            .Add(component => component.EstimateChanged, value => changedEstimate = value)
            .Add(component => component.QuoteResolver, (_, _) =>
                Task.FromException<ProjectStructureTaskResourceCostQuote>(new InvalidOperationException("pricing unavailable"))));

        await cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, initialEstimate));
        cut.Render();

        Assert.Null(changedEstimate);
        Assert.Contains("cannot replace it with an invented or stale amount", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unavailable_authoritative_quote_clears_the_existing_cost_preview()
    {
        var initialEstimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            275m,
            "USD");
        ProjectTaskEstimate? changedEstimate = null;
        var cut = Render<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, initialEstimate)
            .Add(component => component.EstimateChanged, value => changedEstimate = value)
            .Add(component => component.QuoteResolver, (_, _) =>
                Task.FromResult(ProjectStructureTaskResourceCostQuote.Unavailable(
                    "CRM workforce rate",
                    "No rate is configured.",
                    DateTimeOffset.Parse("2026-07-16T16:00:00Z"),
                    ProjectStructureTaskResourceCostSource.CrmWorkforceRate))));

        await cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, initialEstimate));

        Assert.NotNull(changedEstimate);
        Assert.Null(changedEstimate!.ExpectedCostAmount);
        Assert.Equal(string.Empty, changedEstimate.ExpectedCostCurrencyCode);
    }

    [Fact]
    public async Task Started_task_preserves_historical_cost_without_requesting_a_quote()
    {
        var quoteCalls = 0;
        var initialEstimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            275m,
            "USD");
        ProjectTaskEstimate? changedEstimate = null;
        var cut = Render<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, initialEstimate)
            .Add(component => component.ExecutionState, ProjectTaskExecutionState.Started)
            .Add(component => component.EstimateChanged, value => changedEstimate = value)
            .Add(component => component.QuoteResolver, (_, _) =>
            {
                quoteCalls++;
                return Task.FromResult(ProjectStructureTaskResourceCostQuote.Unavailable(
                    "CRM workforce rate",
                    "No rate is configured.",
                    DateTimeOffset.Parse("2026-07-16T16:00:00Z"),
                    ProjectStructureTaskResourceCostSource.CrmWorkforceRate));
            }));

        await cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, initialEstimate));

        Assert.Equal(0, quoteCalls);
        Assert.Null(changedEstimate);
        Assert.Contains("execution history", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.True(cut
            .Find("[data-testid='project-structure-task-resource-cost-refresh']")
            .HasAttribute("disabled"));
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
        var cut = Render<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, initialEstimate)
            .Add(component => component.EstimateChanged, value => changedEstimate = value)
            .Add(component => component.QuoteResolver, (_, _) => quoteCompletion.Task));

        var pendingQuote = cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, initialEstimate));
        cut.Render(parameters => parameters
            .Add(component => component.Estimate, initialEstimate with { ExpectedEffortHours = 4m }));
        quoteCompletion.SetResult(new ProjectStructureTaskResourceCostQuote(
            ProjectStructureTaskResourceCostQuoteStatus.Available,
            800m,
            "USD",
            "CRM workforce rate",
            "Calculated for the previous effort.",
            DateTimeOffset.Parse("2026-07-16T16:00:00Z"),
            ProjectStructureTaskResourceCostSource.CrmWorkforceRate));
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
        var cut = Render<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, initialEstimate)
            .Add(component => component.EstimateChanged, value => changedEstimate = value)
            .Add(component => component.QuoteResolver, (_, _) => quoteCompletion.Task));

        var pendingQuote = cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, initialEstimate));
        cut.Render(parameters => parameters
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
            DateTimeOffset.Parse("2026-07-16T16:00:00Z"),
            ProjectStructureTaskResourceCostSource.CrmWorkforceRate));
        await pendingQuote;

        Assert.Null(changedEstimate);
    }
}
