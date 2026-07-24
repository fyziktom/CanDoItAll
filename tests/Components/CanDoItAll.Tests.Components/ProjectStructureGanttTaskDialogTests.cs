using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureGanttTaskDialogTests
{
    private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset StartUtc = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Duration_and_end_fields_keep_one_consistent_schedule()
    {
        using var context = CreateContext();
        var host = context.RenderComponent<DialogHost>();
        var resultTask = OpenDialog(context, []);

        host.WaitForElement("[data-testid='project-structure-gantt-task-duration']");
        host.Find("[data-testid='project-structure-gantt-task-duration']").Change("4");

        Assert.Equal(
            "2026-07-15T12:00",
            host.Find("[data-testid='project-structure-gantt-task-end']").GetAttribute("value"));

        host.Find("[data-testid='project-structure-gantt-task-end']").Change("2026-07-15T14:00");

        Assert.Equal(
            "6",
            host.Find("[data-testid='project-structure-gantt-task-duration']").GetAttribute("value"));

        host.Find("[data-testid='project-structure-gantt-task-submit']").Click();
        var result = Assert.IsType<ProjectStructureTaskCreateRequest>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(StartUtc, result.StartUtc);
        Assert.Equal(StartUtc.AddHours(6), result.EndUtc);
        Assert.Equal(TimeSpan.FromHours(6), result.Duration);
        Assert.Equal(8m, result.Estimate?.ExpectedEffortHours);
    }

    [Fact]
    public async Task Preset_and_visual_resource_selection_return_typed_create_request()
    {
        using var context = CreateContext();
        var host = context.RenderComponent<DialogHost>();
        var resourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var versionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var resultTask = OpenDialog(
            context,
            [
                new ProjectStructureTaskResourceOption(
                    ProjectStructureTaskResourceKind.Workflow,
                    resourceId,
                    versionId,
                    "Invoice review",
                    "Workflow",
                    "Review and approve an invoice.",
                    false,
                    false)
            ],
            afterTaskNodeId: "custom:previous");

        host.WaitForElement("[data-testid='project-structure-gantt-task-preset-40']");
        host.Find("[data-testid='project-structure-gantt-task-preset-40']").Click();
        host.Find($"[data-testid='project-structure-gantt-task-resource-workflow-{resourceId:N}']").Click();
        host.Find("[data-testid='project-structure-gantt-task-title']").Input("Review invoice");
        host.Find("[data-testid='project-structure-gantt-task-submit']").Click();

        var result = Assert.IsType<ProjectStructureTaskCreateRequest>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal("Review invoice", result.Title);
        Assert.Equal(TimeSpan.FromHours(40), result.Duration);
        Assert.Equal("custom:previous", result.AfterTaskNodeId);
        Assert.Equal(
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Workflow,
                resourceId,
                versionId),
            result.Resource);
    }

    [Theory]
    [InlineData("2026-07-15T14:00:00")]
    [InlineData("2026-07-15T14:00:00.000Z")]
    public void Browser_iso_end_values_recalculate_duration(string browserValue)
    {
        using var context = CreateContext();
        var host = context.RenderComponent<DialogHost>();
        _ = OpenDialog(context, []);

        host.WaitForElement("[data-testid='project-structure-gantt-task-end']");
        host.Find("[data-testid='project-structure-gantt-task-end']").Change(browserValue);

        Assert.Equal(
            "6",
            host.Find("[data-testid='project-structure-gantt-task-duration']").GetAttribute("value"));
        Assert.Empty(host.FindAll("[data-testid='project-structure-gantt-task-validation-error']"));
    }

    [Fact]
    public async Task Edit_mode_returns_progress_effort_cost_and_direct_assignee_change()
    {
        using var context = CreateContext();
        var host = context.RenderComponent<DialogHost>();
        var personId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var replacementPersonId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var assignee = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Person,
            personId);
        var editModel = new ProjectStructureGanttTaskEditModel(
            new CanDoItAll.Components.Gantt.GanttTaskId("custom:task-a"),
            "Customer acceptance",
            StartUtc,
            StartUtc.AddDays(7),
            30,
            new ProjectTaskEstimate(8m, ProjectWorkItemEffortUnit.ManDays, 900m, "USD"),
            assignee);
        var resultTask = OpenEditDialog(
            context,
            editModel,
            [
                new ProjectStructureTaskResourceOption(
                    ProjectStructureTaskResourceKind.Person,
                    personId,
                    null,
                    "Joe Doe",
                    "Person",
                    "joe@example.test",
                    false,
                    false),
                new ProjectStructureTaskResourceOption(
                    ProjectStructureTaskResourceKind.Person,
                    replacementPersonId,
                    null,
                    "Jane Doe",
                    "Person",
                    "jane@example.test",
                    false,
                    false)
            ]);

        host.WaitForElement("[data-testid='project-structure-gantt-task-progress']");
        Assert.Equal(
            "1",
            host.Find("[data-testid='project-structure-gantt-task-estimate-effort']").GetAttribute("value"));
        host.Find("[data-testid='project-structure-gantt-task-progress']").Change("65");
        host.Find("[data-testid='project-structure-gantt-task-estimate-cost']").Change("1200");
        host.Find($"[data-testid='project-structure-gantt-task-resource-person-{replacementPersonId:N}']").Click();
        host.Find("[data-testid='project-structure-gantt-task-submit']").Click();

        var result = Assert.IsType<ProjectStructureTaskEditDialogResult>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(65, result.ProgressPercent);
        Assert.Equal(8m, result.Estimate.ExpectedEffortHours);
        Assert.Equal(ProjectWorkItemEffortUnit.ManDays, result.Estimate.ExpectedEffortUnit);
        Assert.Equal(1200m, result.Estimate.ExpectedCostAmount);
        Assert.Equal("USD", result.Estimate.ExpectedCostCurrencyCode);
        Assert.True(result.AssigneeChanged);
        Assert.Equal(
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Person,
                replacementPersonId),
            result.Assignee);
        Assert.Null(result.ResourceToAttach);
    }

    [Fact]
    public void Started_task_keeps_historical_cost_and_currency_inputs_disabled()
    {
        using var context = CreateContext();
        var host = context.RenderComponent<DialogHost>();
        var editModel = new ProjectStructureGanttTaskEditModel(
            new CanDoItAll.Components.Gantt.GanttTaskId("custom:started-task"),
            "Started delivery",
            StartUtc,
            StartUtc.AddDays(1),
            30,
            new ProjectTaskEstimate(8m, ProjectWorkItemEffortUnit.Hours, 900m, "USD"),
            Assignee: null,
            Execution: new ProjectTaskExecutionSnapshot(
                ProjectTaskExecutionState.Started,
                StartUtc.AddHours(-2),
                null));

        _ = OpenEditDialog(context, editModel, []);

        var cost = host.WaitForElement("[data-testid='project-structure-gantt-task-estimate-cost']");
        var currency = host.Find("[data-testid='project-structure-gantt-task-estimate-currency']");
        var effort = host.Find("[data-testid='project-structure-gantt-task-estimate-effort']");

        Assert.True(cost.HasAttribute("disabled"));
        Assert.True(currency.HasAttribute("disabled"));
        Assert.False(effort.HasAttribute("disabled"));
        Assert.Contains("historical snapshot", host.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Edit_mode_exposes_definition_filters_and_stages_workflow_without_replacing_assignee()
    {
        using var context = CreateContext();
        var host = context.RenderComponent<DialogHost>();
        var personId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var workflowId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var workflowVersionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var processId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var assignee = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Person,
            personId);
        var editModel = new ProjectStructureGanttTaskEditModel(
            new CanDoItAll.Components.Gantt.GanttTaskId("custom:task-a"),
            "Customer acceptance",
            StartUtc,
            StartUtc.AddDays(7),
            30,
            new ProjectTaskEstimate(8m, ProjectWorkItemEffortUnit.Hours, 900m, "USD"),
            assignee,
            Execution: ProjectTaskExecutionSnapshot.NotStarted);
        var workflow = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Workflow,
            workflowId,
            workflowVersionId);
        var resultTask = OpenEditDialog(
            context,
            editModel,
            [
                CreateResource(ProjectStructureTaskResourceKind.Person, personId, "Joe Doe", "Person"),
                new ProjectStructureTaskResourceOption(
                    ProjectStructureTaskResourceKind.Workflow,
                    workflowId,
                    workflowVersionId,
                    "Invoice review",
                    "Workflow",
                    "Review and approve an invoice.",
                    false,
                    false),
                CreateResource(ProjectStructureTaskResourceKind.Process, processId, "Invoice process", "Process")
            ]);

        host.WaitForElement("[data-testid='project-structure-gantt-task-resource-picker-filter-workflow']");
        Assert.Equal(
            "true",
            host.Find("[data-testid='project-structure-gantt-task-resource-picker-filter-workflow']")
                .GetAttribute("aria-pressed"));
        Assert.Equal(
            "true",
            host.Find("[data-testid='project-structure-gantt-task-resource-picker-filter-process']")
                .GetAttribute("aria-pressed"));
        Assert.NotEmpty(host.FindAll($"[data-testid='project-structure-gantt-task-resource-workflow-{workflowId:N}']"));
        Assert.NotEmpty(host.FindAll($"[data-testid='project-structure-gantt-task-resource-process-{processId:N}']"));

        host.Find($"[data-testid='project-structure-gantt-task-resource-workflow-{workflowId:N}']").Click();
        host.Find("[data-testid='project-structure-gantt-task-submit']").Click();

        var result = Assert.IsType<ProjectStructureTaskEditDialogResult>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(result.AssigneeChanged);
        Assert.Equal(assignee, result.Assignee);
        Assert.Equal(workflow, result.ResourceToAttach);
    }

    [Fact]
    public async Task Mixed_assignment_mode_keeps_direct_assignee_read_only_and_allows_workflow_attachment()
    {
        using var context = CreateContext();
        var host = context.RenderComponent<DialogHost>();
        var primaryPersonId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var replacementAgentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var workflowId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var workflowVersionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var primaryAssignee = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Person,
            primaryPersonId);
        var workflow = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Workflow,
            workflowId,
            workflowVersionId);
        var editModel = new ProjectStructureGanttTaskEditModel(
            new CanDoItAll.Components.Gantt.GanttTaskId("custom:task-a"),
            "Customer acceptance",
            StartUtc,
            StartUtc.AddDays(7),
            30,
            new ProjectTaskEstimate(8m, ProjectWorkItemEffortUnit.Hours, 900m, "USD"),
            primaryAssignee,
            CanChangeDirectAssignee: false);
        var resultTask = OpenEditDialog(
            context,
            editModel,
            [
                CreateResource(
                    ProjectStructureTaskResourceKind.Person,
                    primaryPersonId,
                    "Joe Doe",
                    "Person"),
                CreateResource(
                    ProjectStructureTaskResourceKind.Agent,
                    replacementAgentId,
                    "Delivery agent",
                    "AI agent"),
                new ProjectStructureTaskResourceOption(
                    ProjectStructureTaskResourceKind.Workflow,
                    workflowId,
                    workflowVersionId,
                    "Invoice review",
                    "Workflow",
                    "Review and approve an invoice.",
                    false,
                    false)
            ]);

        host.WaitForElement("[data-testid='project-structure-gantt-task-assignee-readonly']");
        Assert.Empty(host.FindAll("[data-testid='project-structure-gantt-task-resource-picker-filter-person']"));
        Assert.Empty(host.FindAll("[data-testid='project-structure-gantt-task-resource-picker-filter-agent']"));
        Assert.Empty(host.FindAll($"[data-testid='project-structure-gantt-task-resource-person-{primaryPersonId:N}']"));
        Assert.Empty(host.FindAll($"[data-testid='project-structure-gantt-task-resource-agent-{replacementAgentId:N}']"));
        Assert.Empty(host.FindAll("[data-testid='project-structure-gantt-task-resource-clear']"));
        Assert.NotEmpty(host.FindAll($"[data-testid='project-structure-gantt-task-resource-workflow-{workflowId:N}']"));

        host.Find("[data-testid='project-structure-gantt-task-title']").Input("Updated acceptance");
        host.Find($"[data-testid='project-structure-gantt-task-resource-workflow-{workflowId:N}']").Click();
        host.Find("[data-testid='project-structure-gantt-task-submit']").Click();

        var result = Assert.IsType<ProjectStructureTaskEditDialogResult>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal("Updated acceptance", result.Title);
        Assert.False(result.AssigneeChanged);
        Assert.Equal(primaryAssignee, result.Assignee);
        Assert.Equal(workflow, result.ResourceToAttach);
    }

    [Fact]
    public async Task Clearing_staged_workflow_restores_and_reprices_the_direct_assignee()
    {
        using var context = CreateContext();
        var host = context.RenderComponent<DialogHost>();
        var personId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var workflowId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var workflowVersionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var assignee = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Person,
            personId);
        var editModel = new ProjectStructureGanttTaskEditModel(
            new CanDoItAll.Components.Gantt.GanttTaskId("custom:task-a"),
            "Customer acceptance",
            StartUtc,
            StartUtc.AddDays(7),
            30,
            new ProjectTaskEstimate(8m, ProjectWorkItemEffortUnit.Hours, 900m, "USD"),
            assignee,
            Execution: ProjectTaskExecutionSnapshot.NotStarted);
        var resultTask = OpenEditDialog(
            context,
            editModel,
            [
                CreateResource(ProjectStructureTaskResourceKind.Person, personId, "Joe Doe", "Person"),
                new ProjectStructureTaskResourceOption(
                    ProjectStructureTaskResourceKind.Workflow,
                    workflowId,
                    workflowVersionId,
                    "Invoice review",
                    "Workflow",
                    "Review and approve an invoice.",
                    false,
                    false)
            ],
            (request, _) => Task.FromResult(CreateQuote(
                request.Resource.Kind == ProjectStructureTaskResourceKind.Workflow ? 500m : 100m,
                request.Resource.Kind == ProjectStructureTaskResourceKind.Workflow
                    ? "Workflow run history"
                    : "CRM workforce rate",
                request.Resource.Kind)));

        host.WaitForElement($"[data-testid='project-structure-gantt-task-resource-workflow-{workflowId:N}']").Click();
        host.WaitForAssertion(() => Assert.Equal(
            "500",
            host.Find("[data-testid='project-structure-gantt-task-estimate-cost']").GetAttribute("value")));

        host.Find("[data-testid='project-structure-gantt-task-resource-clear']").Click();
        host.WaitForAssertion(() => Assert.Equal(
            "100",
            host.Find("[data-testid='project-structure-gantt-task-estimate-cost']").GetAttribute("value")));
        host.Find("[data-testid='project-structure-gantt-task-submit']").Click();

        var result = Assert.IsType<ProjectStructureTaskEditDialogResult>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(result.AssigneeChanged);
        Assert.Equal(assignee, result.Assignee);
        Assert.Null(result.ResourceToAttach);
        Assert.Equal(100m, result.Estimate.ExpectedCostAmount);
    }

    private static TestContext CreateContext()
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static Task<object?> OpenDialog(
        TestContext context,
        IReadOnlyList<ProjectStructureTaskResourceOption> resources,
        string? afterTaskNodeId = null)
    {
        return context.Services.GetRequiredService<DialogService>().OpenAsync<ProjectStructureGanttTaskDialog>(
            "Add project task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureGanttTaskDialog.DefaultStartUtc)] = StartUtc,
                [nameof(ProjectStructureGanttTaskDialog.DefaultEndUtc)] = StartUtc.AddHours(8),
                [nameof(ProjectStructureGanttTaskDialog.ProjectId)] = ProjectId,
                [nameof(ProjectStructureGanttTaskDialog.AfterTaskNodeId)] = afterTaskNodeId,
                [nameof(ProjectStructureGanttTaskDialog.ResourceOptions)] = resources
            },
            new DialogOptions
            {
                TestId = "project-structure-gantt-task-dialog"
            });
    }

    private static Task<object?> OpenEditDialog(
        TestContext context,
        ProjectStructureGanttTaskEditModel editModel,
        IReadOnlyList<ProjectStructureTaskResourceOption> resources,
        Func<ProjectStructureTaskResourceCostRequest, CancellationToken, Task<ProjectStructureTaskResourceCostQuote>>? quoteResolver = null)
    {
        return context.Services.GetRequiredService<DialogService>().OpenAsync<ProjectStructureGanttTaskDialog>(
            "Edit project task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureGanttTaskDialog.DefaultStartUtc)] = editModel.StartUtc,
                [nameof(ProjectStructureGanttTaskDialog.DefaultEndUtc)] = editModel.EndUtc,
                [nameof(ProjectStructureGanttTaskDialog.ProjectId)] = ProjectId,
                [nameof(ProjectStructureGanttTaskDialog.DefaultCurrencyCode)] = "USD",
                [nameof(ProjectStructureGanttTaskDialog.EditModel)] = editModel,
                [nameof(ProjectStructureGanttTaskDialog.ResourceOptions)] = resources,
                [nameof(ProjectStructureGanttTaskDialog.QuoteResolver)] = quoteResolver
            },
            new DialogOptions
            {
                TestId = "project-structure-gantt-task-edit-dialog"
            });
    }

    private static ProjectStructureTaskResourceOption CreateResource(
        ProjectStructureTaskResourceKind kind,
        Guid id,
        string name,
        string typeLabel)
        => new(kind, id, null, name, typeLabel, string.Empty, false, false);

    private static ProjectStructureTaskResourceCostQuote CreateQuote(
        decimal amount,
        string source,
        ProjectStructureTaskResourceKind kind)
        => new(
            ProjectStructureTaskResourceCostQuoteStatus.Available,
            amount,
            "USD",
            source,
            "Calculated for the selected resource.",
            DateTimeOffset.Parse("2026-07-16T16:00:00Z"),
            ProjectStructureTaskResourceCostSourcePolicy.RequireFor(kind));
}
