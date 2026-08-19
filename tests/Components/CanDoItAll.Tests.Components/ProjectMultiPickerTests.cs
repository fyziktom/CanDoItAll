using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectMultiPickerTests
{
    [Fact]
    public void Renders_multiple_projects_and_returns_a_typed_selection_when_removed()
    {
        var firstProjectId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();
        const string longProjectName = "A deliberately long customer implementation project name";
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IProjectRecordQueryService>(
            new StubProjectRecordQueryService(
                [
                    CreateProject(firstProjectId, longProjectName),
                    CreateProject(secondProjectId, "Customer portal")
                ]));
        IReadOnlyList<Guid>? selectedProjectIds = null;

        var cut = context.Render<ProjectMultiPicker>(parameters => parameters
            .Add(component => component.SelectedProjectIds, [firstProjectId, secondProjectId])
            .Add(
                component => component.SelectedProjectIdsChanged,
                projectIds => selectedProjectIds = projectIds)
            .Add(component => component.TestIdPrefix, "connection-projects"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(longProjectName, cut.Markup);
            Assert.Contains("Customer portal", cut.Markup);
            Assert.Contains("2 linked", cut.Markup);
        });

        cut.Find(".project-multi-picker__name-target").TriggerEvent(
            "onmouseenter",
            new MouseEventArgs
            {
                ClientX = 120,
                ClientY = 80
            });
        var tooltip = context.Services.GetRequiredService<TooltipService>().Current;
        Assert.Equal(longProjectName, tooltip?.Text);

        cut.Find($"[data-testid='connection-projects-remove-{firstProjectId:N}']").Click();

        Assert.NotNull(selectedProjectIds);
        Assert.Equal([secondProjectId], selectedProjectIds);
    }

    private static ProjectRecordQueryItem CreateProject(Guid id, string name)
        => new(
            id,
            name,
            ProjectStatus.Active,
            "Delivery",
            string.Empty,
            DateTimeOffset.UtcNow);

    private sealed class StubProjectRecordQueryService(
        IReadOnlyList<ProjectRecordQueryItem> projects) : IProjectRecordQueryService
    {
        public Task<ProjectRecordQueryItem?> GetAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(projects.SingleOrDefault(project => project.Id == projectId));

        public Task<IReadOnlyList<ProjectRecordQueryItem>> GetManyAsync(
            IReadOnlyCollection<Guid> projectIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectRecordQueryItem>>(
                projects.Where(project => projectIds.Contains(project.Id)).ToList());

        public Task<ProjectRecordPage> SearchAsync(
            ProjectRecordQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProjectRecordPage(
                projects,
                query.PageIndex,
                query.PageSize,
                projects.Count));
    }
}
