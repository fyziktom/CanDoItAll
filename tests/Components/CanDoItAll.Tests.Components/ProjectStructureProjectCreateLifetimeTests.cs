using CanDoItAll.Workbench.Structure.UI;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureProjectCreateLifetimeTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Project_creation_completion_retains_acknowledged_identity_without_restoring_a_successor(bool reopen) {
        var gate = new CreationGate();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IDbContextFactory<ProjectsDbContext>>(provider => new PooledDbContextFactory<ProjectsDbContext>(
                new DbContextOptionsBuilder<ProjectsDbContext>(provider.GetRequiredService<DbContextOptions<ProjectsDbContext>>())
                    .AddInterceptors(gate).Options));
        });
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = (await projects.SaveAsync(new() { Name = "Hierarchy parent" })).Value;
        var neighbor = (await projects.SaveAsync(new() { Name = "Available neighbor" })).Value;
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, parent));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextAction($"project:{parent:D}", "project:add-subproject", 0, 0));
        var hierarchyId = page.FindComponent<StructureStructuralDialogs>().Instance.ProjectHierarchyDialog!.OpeningId;
        await page.InvokeAsync(() => page.Find("[data-testid='project-structure-hierarchy-create-project']").ClickAsync(new MouseEventArgs()));
        await page.InvokeAsync(() => page.Find("[data-testid='project-name-input']").Input("Original created project"));
        var editor = page.FindComponent<ProjectModalHost>().Instance;
        var draft = editor.Draft;
        gate.Armed = true;
        var pending = page.InvokeAsync(() => editor.Save.InvokeAsync(draft.Capture()));
        ProjectEditorDraft? successor = null;
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await page.InvokeAsync(() => editor.Close.InvokeAsync());
            await page.InvokeAsync(() => page.Find("[data-testid='project-structure-hierarchy-project-select']").Change(neighbor.ToString("D")));
            if (reopen) {
                await page.InvokeAsync(() => page.Find("[data-testid='project-structure-hierarchy-create-project']").ClickAsync(new MouseEventArgs()));
                await page.InvokeAsync(() => page.Find("[data-testid='project-name-input']").Input("Unsubmitted project draft"));
                successor = page.FindComponent<ProjectModalHost>().Instance.Draft;
            }
        } finally {
            gate.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Equal(ProjectStructureAuthoringResultKind.Committed, outcome.Kind);
        Assert.Equal(gate.CreatedProjectId, outcome.TargetProjectId);
        Assert.Equal(gate.CreatedProjectId, draft.Model.Id);
        Assert.Equal(draft.Acknowledgement, outcome.Editor);
        Assert.Equal("Original created project", (await projects.GetAsync(gate.CreatedProjectId)).Name);
        Assert.DoesNotContain(await projects.ListAsync(), project => project.Name == "Unsubmitted project draft");
        Assert.Empty(await projects.ListHierarchyLinksAsync());
        if (reopen) {
            Assert.Same(successor, page.FindComponent<ProjectModalHost>().Instance.Draft);
            Assert.Null(successor!.Model.Id);
            Assert.Equal("Unsubmitted project draft", successor.Model.Name);
            Assert.True(successor.CanMutate);
        } else {
            var restored = page.FindComponent<StructureStructuralDialogs>().Instance.ProjectHierarchyDialog!;
            Assert.Equal(hierarchyId, restored.OpeningId);
            Assert.Equal(neighbor, restored.SelectedProjectId);
        }
    }

    private sealed class CreationGate : SaveChangesInterceptor {
        public bool Armed { get; set; }
        public Guid CreatedProjectId { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (Armed && eventData.Context?.ChangeTracker.Entries<Project>().FirstOrDefault(entry =>
                    entry.State == EntityState.Added && entry.Entity.Name == "Original created project") is { } added) {
                Armed = false;
                CreatedProjectId = added.Entity.Id;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
