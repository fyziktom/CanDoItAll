using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureGanttTaskDialogTests
{
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
                [nameof(ProjectStructureGanttTaskDialog.AfterTaskNodeId)] = afterTaskNodeId,
                [nameof(ProjectStructureGanttTaskDialog.ResourceOptions)] = resources
            },
            new DialogOptions
            {
                TestId = "project-structure-gantt-task-dialog"
            });
    }
}
