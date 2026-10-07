using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructureTaskQuoteLifetimeTests {
    private static readonly Guid ProjectId = Guid.Parse("8315cb97-0975-4bf3-a696-d8b0b7858026");
    private static readonly ProjectStructureTaskResourceSelection Person = new(
        ProjectStructureTaskResourceKind.Person,
        Guid.Parse("4ea3b4b8-fefe-47af-a0d9-03fe91bbb110"));
    private static readonly ProjectTaskEstimate Estimate = new(8m, ProjectWorkItemEffortUnit.Hours, 900m, "USD");
    private static readonly DateTimeOffset Start = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ProjectTaskExecutionState.Started, Completion.Available)]
    [InlineData(ProjectTaskExecutionState.Started, Completion.Unavailable)]
    [InlineData(ProjectTaskExecutionState.Started, Completion.Error)]
    [InlineData(ProjectTaskExecutionState.Unknown, Completion.Available)]
    [InlineData(ProjectTaskExecutionState.Unknown, Completion.Unavailable)]
    [InlineData(ProjectTaskExecutionState.Unknown, Completion.Error)]
    public async Task Held_quote_cannot_cross_execution_eligibility(
        ProjectTaskExecutionState state,
        Completion completion) {
        await using var context = CreateContext();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<ProjectStructureTaskResourceCostQuote>(TaskCreationOptions.RunContinuationsAsynchronously);
        var changes = new List<ProjectTaskEstimate>();
        var cut = context.Render<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, Estimate)
            .Add(component => component.EstimateChanged, value => changes.Add(value))
            .Add(component => component.QuoteResolver, (_, _) => {
                entered.SetResult();
                return response.Task;
            }));

        var pending = cut.Find("[data-testid='project-structure-task-resource-cost-refresh']").ClickAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Render(parameters => parameters.Add(component => component.ExecutionState, state));
        Complete(response, completion);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(changes);
        Assert.Empty(cut.FindAll("[data-testid='project-structure-task-resource-cost-loading']"));
        Assert.Contains("Automatic pricing is disabled", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("retired quote", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("could not be loaded", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Completion.Available)]
    [InlineData(Completion.Unavailable)]
    [InlineData(Completion.Error)]
    public async Task Real_task_form_preserves_cost_after_execution_changes_during_quote(Completion completion) {
        await using var context = CreateContext();
        var host = context.Render<DialogHost>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<ProjectStructureTaskResourceCostQuote>(TaskCreationOptions.RunContinuationsAsynchronously);
        var edit = new ProjectStructureGanttTaskEditModel(
            new GanttTaskId("custom:wb1-quote-task"),
            "Retain the historical cost",
            Start,
            Start.AddHours(8),
            0,
            Estimate,
            Person,
            Execution: ProjectTaskExecutionSnapshot.NotStarted);
        Func<ProjectStructureTaskResourceCostRequest, CancellationToken, Task<ProjectStructureTaskResourceCostQuote>> resolve = (_, _) => {
            entered.SetResult();
            return response.Task;
        };
        var resultTask = context.Services.GetRequiredService<DialogService>().OpenAsync<ProjectStructureGanttTaskDialog>(
            "Edit project task",
            new Dictionary<string, object?> {
                [nameof(ProjectStructureGanttTaskDialog.ProjectId)] = ProjectId,
                [nameof(ProjectStructureGanttTaskDialog.EditModel)] = edit,
                [nameof(ProjectStructureGanttTaskDialog.QuoteResolver)] = resolve
            });
        var pending = host.WaitForElement("[data-testid='project-structure-gantt-task-resource-cost-refresh']").ClickAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.Find("[data-testid='project-structure-gantt-task-execution-state']")
            .ChangeAsync(new ChangeEventArgs { Value = nameof(ProjectTaskExecutionState.Started) });
        await host.Find("[data-testid='project-structure-gantt-task-execution-started']")
            .ChangeAsync(new ChangeEventArgs { Value = "2026-10-05T08:00" });
        Complete(response, completion);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("900", host.Find("[data-testid='project-structure-gantt-task-estimate-cost']").GetAttribute("value"));
        Assert.Contains("Automatic pricing is disabled", host.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("retired quote", host.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("could not be loaded", host.Markup, StringComparison.Ordinal);
        await host.Find("[data-testid='project-structure-gantt-task-submit']").ClickAsync();
        var result = Assert.IsType<ProjectStructureTaskEditDialogResult>(await resultTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(Estimate, result.Estimate);
        Assert.Equal(ProjectTaskExecutionState.Started, result.Execution?.State);
        Assert.Equal(Start, result.Execution?.ActualStartedAtUtc);
    }

    public static TheoryData<Retirement, Completion> RetiredQuotes {
        get {
            var cases = new TheoryData<Retirement, Completion>();
            foreach (var retirement in Enum.GetValues<Retirement>()) {
                foreach (var completion in Enum.GetValues<Completion>()) {
                    cases.Add(retirement, completion);
                }
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(RetiredQuotes))]
    public async Task Retired_quote_cannot_publish_after_context_input_or_A_B_A_change(Retirement retirement, Completion completion) {
        await using var context = CreateContext();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<ProjectStructureTaskResourceCostQuote>(TaskCreationOptions.RunContinuationsAsynchronously);
        var changes = new List<ProjectTaskEstimate>();
        var successorChanges = new List<ProjectTaskEstimate>();
        var origin = new ProjectTaskQuoteContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var cut = context.Render<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.Context, origin)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, Estimate)
            .Add(component => component.EstimateChanged, value => changes.Add(value))
            .Add(component => component.QuoteResolver, (_, _) => {
                entered.SetResult();
                return response.Task;
            }));
        var pending = cut.Find("[data-testid='project-structure-task-resource-cost-refresh']").ClickAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Retire(cut, retirement, origin, successorChanges);
        Complete(response, completion);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(changes);
        Assert.Empty(successorChanges);
        Assert.Empty(cut.FindAll("[data-testid='project-structure-task-resource-cost-loading']"));
        Assert.DoesNotContain("retired quote", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("could not be loaded", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Retirement.Project)]
    [InlineData(Retirement.Profile)]
    [InlineData(Retirement.ProjectLifetime)]
    [InlineData(Retirement.Opening)]
    [InlineData(Retirement.ExecutionRoundTrip)]
    public async Task Cached_quote_is_lazy_and_belongs_to_its_original_context(Retirement retirement) {
        await using var context = CreateContext();
        var origin = new ProjectTaskQuoteContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var calls = 0;
        var changes = new List<ProjectTaskEstimate>();
        var cut = context.Render<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.Context, origin)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, Estimate)
            .Add(component => component.EstimateChanged, value => changes.Add(value))
            .Add(component => component.QuoteResolver, (_, _) => {
                calls++;
                return Task.FromResult(Quote(calls));
            }));
        Assert.Equal(0, calls);
        await cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, Estimate));
        await cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, Estimate));
        Assert.Equal(1, calls);
        Assert.Equal(2, changes.Count);

        Retire(cut, retirement, origin, []);
        Assert.Equal(1, calls);
        await cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, Estimate));
        Assert.Equal(2, calls);
        Assert.Equal(2m, changes[^1].ExpectedCostAmount);
        await cut.Find("[data-testid='project-structure-task-resource-cost-refresh']").ClickAsync();
        Assert.Equal(3, calls);
        Assert.Equal(3m, changes[^1].ExpectedCostAmount);
    }

    [Theory]
    [InlineData(Completion.Available)]
    [InlineData(Completion.Unavailable)]
    [InlineData(Completion.Error)]
    public async Task Retired_resolver_keeps_its_token_until_unwound_and_cannot_clear_successor_busy_state(Completion completion) {
        await using var context = CreateContext();
        var first = new TaskCompletionSource<ProjectStructureTaskResourceCostQuote>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<ProjectStructureTaskResourceCostQuote>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var changes = new List<ProjectTaskEstimate>();
        CancellationToken retiredToken = default;
        var calls = 0;
        var cut = context.Render<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, Estimate)
            .Add(component => component.EstimateChanged, value => changes.Add(value))
            .Add(component => component.QuoteResolver, (_, token) => {
                if (++calls == 1) {
                    retiredToken = token;
                    entered.SetResult();
                    return first.Task;
                }
                secondEntered.SetResult();
                return second.Task;
            }));
        var pending = cut.Find("[data-testid='project-structure-task-resource-cost-refresh']").ClickAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Render(parameters => parameters.Add(component => component.Context, new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())));
        Assert.True(retiredToken.IsCancellationRequested);
        Assert.True(retiredToken.WaitHandle.WaitOne(0));
        var successor = cut.Find("[data-testid='project-structure-task-resource-cost-refresh']").ClickAsync();
        await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Complete(first, completion);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(changes);
        Assert.NotEmpty(cut.FindAll("[data-testid='project-structure-task-resource-cost-loading']"));
        Assert.Throws<ObjectDisposedException>(() => retiredToken.WaitHandle);
        second.SetResult(Quote(200m));
        await successor.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(200m, Assert.Single(changes).ExpectedCostAmount);
    }

    [Theory]
    [InlineData(Completion.Available)]
    [InlineData(Completion.Unavailable)]
    [InlineData(Completion.Error)]
    public async Task Disposal_retires_callbacks_and_a_reopened_widget_has_an_independent_cache(Completion completion) {
        await using var context = CreateContext();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<ProjectStructureTaskResourceCostQuote>(TaskCreationOptions.RunContinuationsAsynchronously);
        var changes = new List<ProjectTaskEstimate>();
        var cut = context.Render<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, Estimate)
            .Add(component => component.EstimateChanged, value => changes.Add(value))
            .Add(component => component.QuoteResolver, (_, _) => {
                entered.SetResult();
                return response.Task;
            }));
        var pending = cut.InvokeAsync(() => cut.Instance.EstimateSelectedAsync(Person, Estimate));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await context.DisposeRenderedComponentsAsync();
        var reopened = context.Render<ProjectStructureTaskResourceCostEstimator>(parameters => parameters
            .Add(component => component.ProjectId, ProjectId)
            .Add(component => component.SelectedResource, Person)
            .Add(component => component.Estimate, Estimate)
            .Add(component => component.EstimateChanged, value => changes.Add(value))
            .Add(component => component.QuoteResolver, (_, _) => Task.FromResult(Quote(300m))));
        Complete(response, completion);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(changes);
        await reopened.InvokeAsync(() => reopened.Instance.EstimateSelectedAsync(Person, Estimate));
        Assert.Equal(300m, Assert.Single(changes).ExpectedCostAmount);
    }

    private static void Retire(IRenderedComponent<ProjectStructureTaskResourceCostEstimator> cut,
        Retirement retirement, ProjectTaskQuoteContext origin, List<ProjectTaskEstimate> successorChanges) {
        switch (retirement) {
            case Retirement.Project:
            case Retirement.ProjectRoundTrip:
                cut.Render(parameters => parameters.Add(component => component.ProjectId, Guid.NewGuid()));
                if (retirement == Retirement.ProjectRoundTrip) {
                    cut.Render(parameters => parameters.Add(component => component.ProjectId, ProjectId));
                }
                break;
            case Retirement.Profile:
                cut.Render(parameters => parameters.Add(component => component.Context, origin with { DatabaseProfileId = Guid.NewGuid() }));
                break;
            case Retirement.ProjectLifetime:
                cut.Render(parameters => parameters.Add(component => component.Context, origin with { ProjectLifetimeId = Guid.NewGuid() }));
                break;
            case Retirement.Opening:
                cut.Render(parameters => parameters.Add(component => component.Context, origin with { OpeningId = Guid.NewGuid() }));
                break;
            case Retirement.Resource:
                cut.Render(parameters => parameters.Add(component => component.SelectedResource, Person with { ResourceId = Guid.NewGuid() }));
                break;
            case Retirement.Effort:
            case Retirement.EstimateRoundTrip:
                cut.Render(parameters => parameters.Add(component => component.Estimate, Estimate with { ExpectedEffortHours = 4m }));
                if (retirement == Retirement.EstimateRoundTrip) {
                    cut.Render(parameters => parameters.Add(component => component.Estimate, Estimate));
                }
                break;
            case Retirement.ManualCost:
                cut.Render(parameters => parameters.Add(component => component.Estimate, Estimate with { ExpectedCostAmount = 999m }));
                break;
            case Retirement.ExecutionRoundTrip:
                cut.Render(parameters => parameters.Add(component => component.ExecutionState, ProjectTaskExecutionState.Started));
                cut.Render(parameters => parameters.Add(component => component.ExecutionState, ProjectTaskExecutionState.NotStarted));
                break;
            case Retirement.Callback:
                cut.Render(parameters => parameters.Add(component => component.EstimateChanged, value => successorChanges.Add(value)));
                break;
            case Retirement.Resolver:
                cut.Render(parameters => parameters.Add(component => component.QuoteResolver, (_, _) => Task.FromResult(Quote(500m))));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(retirement));
        }
    }

    private static ProjectStructureTaskResourceCostQuote Quote(decimal amount)
        => new(ProjectStructureTaskResourceCostQuoteStatus.Available, amount, "USD", "Current rate", "Current estimate", Start, ProjectStructureTaskResourceCostSource.CrmWorkforceRate);

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static void Complete(TaskCompletionSource<ProjectStructureTaskResourceCostQuote> response, Completion completion) {
        if (completion == Completion.Error) {
            response.SetException(new InvalidOperationException("retired quote failed"));
            return;
        }

        response.SetResult(completion == Completion.Available
            ? new ProjectStructureTaskResourceCostQuote(
                ProjectStructureTaskResourceCostQuoteStatus.Available,
                1600m,
                "EUR",
                "retired quote",
                "The original rate belongs to the retired request.",
                Start,
                ProjectStructureTaskResourceCostSource.CrmWorkforceRate)
            : ProjectStructureTaskResourceCostQuote.Unavailable(
                "retired quote",
                "No current rate.",
                Start,
                ProjectStructureTaskResourceCostSource.CrmWorkforceRate));
    }

    public enum Completion {
        Available,
        Unavailable,
        Error
    }

    public enum Retirement {
        Project,
        Profile,
        ProjectLifetime,
        Opening,
        Resource,
        Effort,
        ManualCost,
        ExecutionRoundTrip,
        EstimateRoundTrip,
        ProjectRoundTrip,
        Callback,
        Resolver
    }
}
