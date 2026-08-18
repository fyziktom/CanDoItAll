using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructureTaskCreateDialogTests
{
    [Fact]
    public async Task Dialog_emits_default_pure_effort_and_normalized_expected_cost()
    {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        var resultTask = OpenDialog(context, []);

        host.WaitForElement("[data-testid='project-structure-task-create-estimate-preset-1d']");
        host.Find("[data-testid='project-structure-task-create-title']").Input("Estimate delivery");
        host.Find("[data-testid='project-structure-task-create-estimate-preset-1d']").Click();
        host.Find("[data-testid='project-structure-task-create-estimate-cost']").Change("1250.5");
        host.Find("[data-testid='project-structure-task-create-estimate-currency']").Input("eur");
        host.Find("[data-testid='project-structure-task-create-submit']").Click();

        var result = Assert.IsType<ProjectStructureTaskDialogResult>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Contains(result.CreateRequest.InputValues!, value =>
            value.Key == ProjectTaskEstimateInputKeys.ExpectedEffortValue && value.Value == "1");
        Assert.Contains(result.CreateRequest.InputValues!, value =>
            value.Key == ProjectTaskEstimateInputKeys.ExpectedEffortUnit && value.Value == "manDays");
        Assert.Contains(result.CreateRequest.InputValues!, value =>
            value.Key == ProjectTaskEstimateInputKeys.ExpectedCostAmount && value.Value == "1250.5");
        Assert.Contains(result.CreateRequest.InputValues!, value =>
            value.Key == ProjectTaskEstimateInputKeys.ExpectedCostCurrencyCode && value.Value == "EUR");
    }

    [Fact]
    public async Task Dialog_returns_direct_person_assignment_and_preserves_task_fields()
    {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        var joeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var agentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var workflowId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var resultTask = OpenDialog(
            context,
            [
                CreateResource(ProjectStructureTaskResourceKind.Person, joeId, "Joe Doe", "Person"),
                CreateResource(ProjectStructureTaskResourceKind.Agent, agentId, "Delivery agent", "AI agent"),
                CreateResource(ProjectStructureTaskResourceKind.Workflow, workflowId, "Legacy workflow", "Workflow")
            ]);

        host.WaitForElement("[data-testid='project-structure-task-create-title']");
        Assert.NotEmpty(host.FindAll($"[data-testid='project-structure-task-create-assignee-person-{joeId:N}']"));
        Assert.NotEmpty(host.FindAll($"[data-testid='project-structure-task-create-assignee-agent-{agentId:N}']"));
        Assert.Empty(host.FindAll($"[data-testid='project-structure-task-create-assignee-workflow-{workflowId:N}']"));

        host.Find("[data-testid='project-structure-task-create-title']").Input("Prepare CRM handoff");
        host.Find("[data-testid='project-structure-task-create-subtitle']").Input("Delivery");
        host.Find("[data-testid='project-structure-task-create-notes']").Input("Confirm ownership.");
        host.Find("[data-testid='project-structure-task-create-due']").Change("2026-07-16T12:00");
        host.Find($"[data-testid='project-structure-task-create-assignee-person-{joeId:N}']").Click();
        host.Find("[data-testid='project-structure-task-create-submit']").Click();

        var result = Assert.IsType<ProjectStructureTaskDialogResult>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal("Prepare CRM handoff", result.CreateRequest.Title);
        Assert.Equal("Delivery", result.CreateRequest.Subtitle);
        Assert.Equal("Confirm ownership.", result.CreateRequest.Notes);
        Assert.Equal(
            new ProjectStructureTaskResourceSelection(ProjectStructureTaskResourceKind.Person, joeId),
            result.Assignee);
        Assert.Contains(result.CreateRequest.InputValues!, value => value.Key == "workItemKind" && value.Value == "task");
        Assert.Contains(result.CreateRequest.InputValues!, value => value.Key == "dueUtc" && value.Value.StartsWith("2026-07-16T12:00", StringComparison.Ordinal));
        Assert.DoesNotContain(result.CreateRequest.InputValues!, value => value.Key == "assigneeRef");
    }

    [Fact]
    public async Task Mixed_assignment_edit_keeps_direct_assignee_read_only_while_task_fields_remain_editable()
    {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        var primaryPersonId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var replacementAgentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var primaryAssignee = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Person,
            primaryPersonId);
        var resultTask = OpenDialog(
            context,
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
                    "AI agent")
            ],
            isEditMode: true,
            initialAssignee: primaryAssignee,
            canChangeDirectAssignee: false);

        host.WaitForElement("[data-testid='project-structure-task-edit-assignee-readonly']");
        Assert.Empty(host.FindAll("[data-testid='project-structure-task-create-assignee-picker-shell']"));
        Assert.Empty(host.FindAll("[data-testid='project-structure-task-create-assignee-clear']"));

        host.Find("[data-testid='project-structure-task-create-title']").Input("Updated CRM handoff");
        host.Find("[data-testid='project-structure-task-create-submit']").Click();

        var result = Assert.IsType<ProjectStructureTaskDialogResult>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal("Updated CRM handoff", result.CreateRequest.Title);
        Assert.Equal(primaryAssignee, result.Assignee);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<ICurrencyFormatter>(new StaticCurrencyFormatter("USD"));
        return context;
    }

    private static Task<object?> OpenDialog(
        BunitContext context,
        IReadOnlyList<ProjectStructureTaskResourceOption> assignees,
        bool isEditMode = false,
        ProjectStructureTaskResourceSelection? initialAssignee = null,
        bool canChangeDirectAssignee = true)
    {
        var request = new CanvasWorkbenchCreateActionRequest(
            ProjectStructureTaskActionIds.Create,
            "parent-node",
            420,
            260,
            "parent-node",
            string.Empty,
            string.Empty,
            string.Empty,
            "child",
            ProjectStructureTaskActionIds.CreateMode,
            "task",
            null,
            [
                new CanvasWorkbenchInputValue
                {
                    Key = "assigneeRef",
                    Value = "legacy-participant"
                }
            ]);
        return context.Services.GetRequiredService<DialogService>().OpenAsync<ProjectStructureTaskCreateDialog>(
            "Add task",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureTaskCreateDialog.ProjectId)] = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                [nameof(ProjectStructureTaskCreateDialog.CreateRequest)] = request,
                [nameof(ProjectStructureTaskCreateDialog.AssigneeOptions)] = assignees,
                [nameof(ProjectStructureTaskCreateDialog.IsEditMode)] = isEditMode,
                [nameof(ProjectStructureTaskCreateDialog.InitialAssignee)] = initialAssignee,
                [nameof(ProjectStructureTaskCreateDialog.CanChangeDirectAssignee)] = canChangeDirectAssignee
            },
            new DialogOptions
            {
                TestId = "project-structure-task-create-dialog"
            });
    }

    private static ProjectStructureTaskResourceOption CreateResource(
        ProjectStructureTaskResourceKind kind,
        Guid id,
        string name,
        string typeLabel)
        => new(kind, id, null, name, typeLabel, string.Empty, false, false);

    private sealed class StaticCurrencyFormatter(string currencyCode) : ICurrencyFormatter
    {
        public string CurrencyCode { get; } = currencyCode;

        public string Format(decimal value)
            => $"{CurrencyCode} {value:0.00}";
    }
}
