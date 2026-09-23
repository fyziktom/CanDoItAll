using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Resources.Pages;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.TestLab.Pages;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class OwnerPostcommitPageTests {
    [Theory]
    [InlineData(PostcommitOwner.Resource, PostcommitFault.Search)]
    [InlineData(PostcommitOwner.Resource, PostcommitFault.Activity)]
    [InlineData(PostcommitOwner.TestPlan, PostcommitFault.Search)]
    [InlineData(PostcommitOwner.TestPlan, PostcommitFault.Activity)]
    public async Task Confirmed_owner_fault_warns_and_keeps_ids_for_explicit_same_editor_save(PostcommitOwner owner, PostcommitFault fault) {
        var probe = new OwnerPostcommitTestProbe();
        await using var harness = await ComponentTestHarness.CreateAsync(probe.ConfigureServices);
        var services = harness.Context.Services;
        var admission = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Confirmed UI save");
        var page = Render(harness, owner, admission.ProjectId);
        await FillAsync(page, owner, services, admission);
        var originalEditor = Editor(page);
        probe.ArmFault(owner, fault, new InvalidOperationException("Owner postcommit failure"));
        await SubmitAsync(page);
        var id = RequiredId(EditorId(page));
        Assert.Same(originalEditor, Editor(page));
        AssertWarning(services, owner, id);
        var childIds = ChildIds(page);
        Assert.All(childIds, value => Assert.NotEqual(Guid.Empty, value));
        Assert.Equal(owner == PostcommitOwner.TestPlan ? 3 : 0, childIds.Length);
        await ChangeTitleAsync(page, owner, "Explicit same-editor save");
        await SubmitAsync(page);
        Assert.Equal(id, EditorId(page));
        Assert.Equal(childIds, ChildIds(page));
        await AssertSavedAsync(services, owner, admission, id, "Explicit same-editor save");
    }

    [Theory]
    [InlineData(PostcommitOwner.Resource, false)]
    [InlineData(PostcommitOwner.Resource, true)]
    [InlineData(PostcommitOwner.TestPlan, false)]
    [InlineData(PostcommitOwner.TestPlan, true)]
    public async Task Refresh_failure_or_missing_saved_row_keeps_confirmed_id_and_warns(PostcommitOwner owner, bool rowDisappears) {
        var probe = new OwnerPostcommitTestProbe();
        await using var harness = await ComponentTestHarness.CreateAsync(probe.ConfigureServices);
        var services = harness.Context.Services;
        var admission = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "UI refresh failure");
        var page = Render(harness, owner, admission.ProjectId);
        await FillAsync(page, owner, services, admission);
        Guid committedId = default;
        probe.AfterActivity = async (actual, request, _) => {
            Assert.Equal(owner, actual);
            probe.AfterActivity = null;
            committedId = RequiredId(request.ArtifactId);
            if (rowDisappears) {
                await RemoveFixtureAggregateAsync(services, owner, committedId, admission);
            } else {
                probe.FailNextRead(owner, new InvalidOperationException("Refresh query failed after successful save"));
            }
        };
        await SubmitAsync(page);
        Assert.NotEqual(Guid.Empty, committedId);
        Assert.Equal(committedId, EditorId(page));
        AssertWarning(services, owner, committedId);
        Assert.DoesNotContain(services.GetRequiredService<NotificationService>().Messages,
            message => message.Severity == NotificationSeverity.Success);
        if (!rowDisappears) {
            await AssertSavedAsync(services, owner, admission, committedId,
                owner == PostcommitOwner.Resource ? "Retained resource" : "Retained test plan");
        }
    }

    [Theory]
    [InlineData(PostcommitOwner.Resource)]
    [InlineData(PostcommitOwner.TestPlan)]
    public async Task Late_known_commit_warning_does_not_replace_new_editor_or_project_selection(PostcommitOwner owner) {
        var probe = new OwnerPostcommitTestProbe();
        await using var harness = await ComponentTestHarness.CreateAsync(probe.ConfigureServices);
        var services = harness.Context.Services;
        var original = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Original UI surface");
        var next = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Next UI surface");
        var page = Render(harness, owner, original.ProjectId);
        await FillAsync(page, owner, services, original);
        var gate = new BoundaryGate();
        Guid committedId = default;
        probe.AfterActivity = async (actual, request, cancellationToken) => {
            Assert.Equal(owner, actual);
            probe.AfterActivity = null;
            committedId = RequiredId(request.ArtifactId);
            await gate.PauseAsync(cancellationToken);
            throw new InvalidOperationException("Late postcommit activity boundary failure");
        };
        var pending = SubmitAsync(page);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await page.FindComponent<EditForm>().FindAll("button").Single(button => button.TextContent.Trim() == "Reset").ClickAsync();
            await ChangeProjectAsync(page, owner, next.ProjectId);
            await ChangeTitleAsync(page, owner, "Keep the next editor");
            Assert.Null(EditorId(page));
            Assert.Equal(next.ProjectId, EditorProject(page));
        } finally {
            gate.Release();
        }
        await pending;
        Assert.Null(EditorId(page));
        Assert.Equal(next.ProjectId, EditorProject(page));
        Assert.Equal("Keep the next editor", EditorTitle(page));
        Assert.Empty(ChildIds(page));
        AssertWarning(services, owner, committedId);
        await AssertSavedAsync(services, owner, original, committedId,
            owner == PostcommitOwner.Resource ? "Retained resource" : "Retained test plan");
    }

    [Fact]
    public async Task TestLab_project_selection_during_owning_flush_cannot_inherit_original_plan_or_child_ids() {
        var probe = new OwnerPostcommitTestProbe();
        await using var harness = await ComponentTestHarness.CreateAsync(probe.ConfigureServices);
        var services = harness.Context.Services;
        var original = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Original test plan target");
        var next = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "New test plan target");
        var page = Render(harness, PostcommitOwner.TestPlan, original.ProjectId);
        await FillAsync(page, PostcommitOwner.TestPlan, services, original);
        var originalEditor = Assert.IsType<TestPlanEditorModel>(Editor(page));
        var gate = new BoundaryGate();
        probe.BeforeSave = async (owner, cancellationToken) => {
            Assert.Equal(PostcommitOwner.TestPlan, owner);
            probe.BeforeSave = null;
            await gate.PauseAsync(cancellationToken);
        };
        var pending = SubmitAsync(page);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await ChangeProjectAsync(page, PostcommitOwner.TestPlan, next.ProjectId);
            Assert.Same(originalEditor, Editor(page));
            Assert.Equal(next, originalEditor.ExpectedProjectAdmission);
        } finally {
            gate.Release();
        }
        await pending;
        var current = Assert.IsType<TestPlanEditorModel>(Editor(page));
        Assert.Same(originalEditor, current);
        Assert.Equal(next.ProjectId, current.ProjectId);
        Assert.Equal(next, current.ExpectedProjectAdmission);
        Assert.Null(current.Id);
        Assert.All(current.Cases, child => Assert.Null(child.Id));
        Assert.All(current.Evidence, child => Assert.Null(child.Id));
        Assert.All(current.Runs, child => Assert.Null(child.Id));
        await using var database = await services.GetRequiredService<IDbContextFactory<TestLabDbContext>>().CreateDbContextAsync();
        var saved = Assert.Single(await database.Set<TestPlan>().ToListAsync());
        Assert.Equal(original.ProjectId, saved.ProjectId);
        Assert.Equal(original.LifetimeId, saved.ProjectLifetimeId);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal(1, await database.Set<TestCaseRecord>().CountAsync(item => item.TestPlanId == saved.Id));
        Assert.Equal(1, await database.Set<TestEvidenceRecord>().CountAsync(item => item.TestPlanId == saved.Id));
        Assert.Equal(1, await database.Set<TestRunRecord>().CountAsync(item => item.TestPlanId == saved.Id));
    }

    [Fact]
    public async Task TestLab_late_commit_retains_only_still_present_original_child_references() {
        var probe = new OwnerPostcommitTestProbe();
        await using var harness = await ComponentTestHarness.CreateAsync(probe.ConfigureServices);
        var services = harness.Context.Services;
        var admission = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Child identity retention");
        var page = Render(harness, PostcommitOwner.TestPlan, admission.ProjectId);
        await FillAsync(page, PostcommitOwner.TestPlan, services, admission);
        var editor = Assert.IsType<TestPlanEditorModel>(Editor(page));
        var removedCase = Assert.Single(editor.Cases);
        var insertedCase = new TestCaseEditorModel { Name = removedCase.Name, Notes = "Added while awaiting save" };
        var originalEvidence = Assert.Single(editor.Evidence);
        var originalRun = Assert.Single(editor.Runs);
        var gate = new BoundaryGate();
        probe.AfterActivity = async (_, _, cancellationToken) => {
            probe.AfterActivity = null;
            await gate.PauseAsync(cancellationToken);
            throw new InvalidOperationException("Known commit before editor changed");
        };
        var pending = SubmitAsync(page);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await page.InvokeAsync(() => {
                editor.Cases.Remove(removedCase);
                editor.Cases.Add(insertedCase);
            });
        } finally {
            gate.Release();
        }
        await pending;
        var planId = RequiredId(editor.Id);
        Assert.Null(removedCase.Id);
        Assert.Null(insertedCase.Id);
        var evidenceId = RequiredId(originalEvidence.Id);
        var runId = RequiredId(originalRun.Id);
        AssertWarning(services, PostcommitOwner.TestPlan, planId);
        var beforeEdit = await services.GetRequiredService<TestLabService>().GetAsync(planId);
        var originalStoredCaseId = RequiredId(Assert.Single(beforeEdit.Cases).Id);
        await SubmitAsync(page);
        var afterEdit = await services.GetRequiredService<TestLabService>().GetAsync(planId);
        Assert.NotEqual(originalStoredCaseId, RequiredId(Assert.Single(afterEdit.Cases).Id));
        Assert.Equal(insertedCase.Notes, Assert.Single(afterEdit.Cases).Notes);
        Assert.Equal(evidenceId, Assert.Single(afterEdit.Evidence).Id);
        Assert.Equal(runId, Assert.Single(afterEdit.Runs).Id);
    }

    [Fact]
    public async Task TestLab_replaced_missing_children_keep_new_committed_ids_after_fault_and_explicit_resave() {
        var probe = new OwnerPostcommitTestProbe();
        await using var harness = await ComponentTestHarness.CreateAsync(probe.ConfigureServices);
        var services = harness.Context.Services;
        var admission = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Missing saved child IDs");
        var owner = services.GetRequiredService<TestLabService>();
        var original = OwnerPostcommitTestProbe.Plan(admission);
        var saved = await owner.SaveAsync(original);
        Assert.True(saved.IsSuccess);
        var page = Render(harness, PostcommitOwner.TestPlan, admission.ProjectId, saved.Value);
        var oldIds = ChildIds(page);
        Assert.Equal(3, oldIds.Length);
        await using (var database = await services.GetRequiredService<IDbContextFactory<TestLabDbContext>>().CreateDbContextAsync()) {
            database.RemoveRange(await database.Set<TestCaseRecord>().Where(item => item.TestPlanId == saved.Value).ToListAsync());
            database.RemoveRange(await database.Set<TestEvidenceRecord>().Where(item => item.TestPlanId == saved.Value).ToListAsync());
            database.RemoveRange(await database.Set<TestRunRecord>().Where(item => item.TestPlanId == saved.Value).ToListAsync());
            await database.SaveChangesAsync();
        }
        probe.ArmFault(PostcommitOwner.TestPlan, PostcommitFault.Search, new InvalidOperationException("Postcommit replacement fault"));
        await SubmitAsync(page);
        Assert.Equal(saved.Value, EditorId(page));
        var retainedIds = ChildIds(page);
        var actual = await owner.GetAsync(saved.Value);
        Assert.Equal(new[] { actual.Cases.Single().Id!.Value, actual.Evidence.Single().Id!.Value, actual.Runs.Single().Id!.Value }, retainedIds);
        Assert.DoesNotContain(retainedIds, id => oldIds.Contains(id));
        AssertWarning(services, PostcommitOwner.TestPlan, saved.Value);
        await SubmitAsync(page);
        Assert.Equal(retainedIds, ChildIds(page));
        actual = await owner.GetAsync(saved.Value);
        Assert.Equal(retainedIds, new[] { Assert.Single(actual.Cases).Id!.Value, Assert.Single(actual.Evidence).Id!.Value, Assert.Single(actual.Runs).Id!.Value });
    }

    [Theory]
    [InlineData(PostcommitFault.Search)]
    [InlineData(PostcommitFault.Activity)]
    public async Task Resource_delete_after_commit_warns_and_clears_original_selection(PostcommitFault fault) {
        var probe = new OwnerPostcommitTestProbe();
        await using var harness = await ComponentTestHarness.CreateAsync(probe.ConfigureServices);
        var services = harness.Context.Services;
        var admission = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Delete UI state");
        var owner = services.GetRequiredService<ResourcesService>();
        var saved = await owner.SaveAsync(OwnerPostcommitTestProbe.Resource(services, admission));
        Assert.True(saved.IsSuccess);
        var page = Render(harness, PostcommitOwner.Resource, admission.ProjectId, saved.Value);
        probe.ArmFault(PostcommitOwner.Resource, fault, new InvalidOperationException("Delete postcommit boundary"));
        await DeleteAsync(page);
        Assert.Null(EditorId(page));
        Assert.Null((await owner.GetAsync(saved.Value)).Id);
        Assert.DoesNotContain(await owner.ListAsync(), item => item.Id == saved.Value);
        AssertWarning(services, PostcommitOwner.Resource, saved.Value, deleted: true);
    }

    [Theory]
    [InlineData(PostcommitOwner.Resource)]
    [InlineData(PostcommitOwner.TestPlan)]
    public async Task Same_id_project_recreation_before_save_keeps_original_editor_and_reports_ordinary_failure(PostcommitOwner owner) {
        var probe = new OwnerPostcommitTestProbe();
        await using var harness = await ComponentTestHarness.CreateAsync(probe.ConfigureServices);
        var services = harness.Context.Services;
        var original = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Old visible project");
        var page = Render(harness, owner, original.ProjectId);
        await FillAsync(page, owner, services, original);
        var editor = Editor(page);
        var projects = services.GetRequiredService<ProjectsService>();
        await projects.DeleteAsync(original.ProjectId, expectedProjectAdmission: original);
        var recreated = await projects.CreateAsync(original.ProjectId, new ProjectEditorModel { Name = "Different lifetime" });
        Assert.True(recreated.IsSuccess);
        var current = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(original.ProjectId);
        Assert.NotEqual(original.LifetimeId, Assert.IsType<ProjectWriteAdmission>(current).LifetimeId);
        await SubmitAsync(page);
        Assert.Same(editor, Editor(page));
        Assert.Null(EditorId(page));
        Assert.Contains(services.GetRequiredService<NotificationService>().Messages,
            message => message.Severity == NotificationSeverity.Error && message.Summary ==
                (owner == PostcommitOwner.Resource ? "Resource save failed" : "Test plan save failed"));
        Assert.DoesNotContain(services.GetRequiredService<NotificationService>().Messages,
            message => message.Severity is NotificationSeverity.Success or NotificationSeverity.Warning);
        Assert.Equal(0, probe.SearchReturns);
        Assert.Equal(0, probe.ActivityReturns);
    }

    [Fact]
    public async Task Resource_delete_denied_before_commit_keeps_existing_selection_and_ordinary_error() {
        var probe = new OwnerPostcommitTestProbe();
        await using var harness = await ComponentTestHarness.CreateAsync(probe.ConfigureServices);
        var services = harness.Context.Services;
        var original = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Original deletion target");
        var unrelated = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Unrelated deletion admission");
        var owner = services.GetRequiredService<ResourcesService>();
        var saved = await owner.SaveAsync(OwnerPostcommitTestProbe.Resource(services, original));
        Assert.True(saved.IsSuccess);
        var page = Render(harness, PostcommitOwner.Resource, original.ProjectId, saved.Value);
        await page.InvokeAsync(() => Assert.IsType<ResourceEditorModel>(Editor(page)).ExpectedProjectAdmission = unrelated);
        var searchReturns = probe.SearchReturns;
        await DeleteAsync(page);
        Assert.Equal(saved.Value, EditorId(page));
        Assert.Equal(saved.Value, (await owner.GetAsync(saved.Value)).Id);
        Assert.Equal(searchReturns, probe.SearchReturns);
        Assert.Contains(services.GetRequiredService<NotificationService>().Messages,
            message => message.Severity == NotificationSeverity.Error && message.Summary == "Resource delete failed");
        Assert.DoesNotContain(services.GetRequiredService<NotificationService>().Messages,
            message => message.Severity is NotificationSeverity.Success or NotificationSeverity.Warning);
    }

    private static IRenderedComponent<IComponent> Render(ComponentTestHarness harness, PostcommitOwner owner, Guid projectId, Guid? id = null) {
        var route = owner == PostcommitOwner.Resource ? "resources" : "test-lab";
        var idParameter = owner == PostcommitOwner.Resource ? "resourceId" : "planId";
        var suffix = id.HasValue ? $"&{idParameter}={id:D}" : string.Empty;
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/{route}?projectId={projectId:D}{suffix}");
        IRenderedComponent<IComponent> page = owner == PostcommitOwner.Resource
            ? harness.Context.Render<ResourcesPage>() : harness.Context.Render<TestLabPage>();
        page.WaitForAssertion(() => {
            Assert.Equal(projectId, EditorProject(page));
            Assert.Equal(id, EditorId(page));
        }, TimeSpan.FromSeconds(30));
        harness.Context.Services.GetRequiredService<NotificationService>().Messages.Clear();
        return page;
    }

    private static async Task FillAsync(IRenderedComponent<IComponent> page, PostcommitOwner owner, IServiceProvider services, ProjectWriteAdmission admission) {
        await page.InvokeAsync(() => {
            if (owner == PostcommitOwner.Resource) {
                var target = Assert.IsType<ResourceEditorModel>(Editor(page));
                var source = OwnerPostcommitTestProbe.Resource(services, admission);
                target.ConnectorPluginKey = source.ConnectorPluginKey;
                target.Configuration = source.Configuration;
                target.ConfigJson = source.ConfigJson;
                target.Description = source.Description;
                target.SupportsPreview = source.SupportsPreview;
            } else {
                var target = Assert.IsType<TestPlanEditorModel>(Editor(page));
                var source = OwnerPostcommitTestProbe.Plan(admission);
                target.Phase = source.Phase;
                target.CoverageGoal = source.CoverageGoal;
                target.PlaywrightSpecPath = source.PlaywrightSpecPath;
                target.Cases = source.Cases;
                target.Evidence = source.Evidence;
                target.Runs = source.Runs;
            }
        });
        await ChangeTitleAsync(page, owner, owner == PostcommitOwner.Resource ? "Retained resource" : "Retained test plan");
    }

    private static async Task ChangeProjectAsync(IRenderedComponent<IComponent> page, PostcommitOwner owner, Guid projectId) {
        await page.InvokeAsync(() => page.Find(owner == PostcommitOwner.Resource
                ? "[data-testid='resource-project-select']" : "[data-testid='testlab-project-select']")
            .ChangeAsync(new ChangeEventArgs { Value = projectId.ToString() }));
        page.WaitForAssertion(() => Assert.Equal(projectId, EditorProject(page)), TimeSpan.FromSeconds(30));
    }

    private static Task ChangeTitleAsync(IRenderedComponent<IComponent> page, PostcommitOwner owner, string value)
        => page.InvokeAsync(() => page.Find(owner == PostcommitOwner.Resource
                ? "[data-testid='resource-name-input']" : "[data-testid='testlab-title-input']")
            .ChangeAsync(new ChangeEventArgs { Value = value }));

    private static Task SubmitAsync(IRenderedComponent<IComponent> page) => page.FindComponent<EditForm>().Find("form").SubmitAsync();
    private static Task DeleteAsync(IRenderedComponent<IComponent> page) => page.FindComponent<EditForm>().FindAll("button")
        .Single(button => button.TextContent.Trim() == "Delete").ClickAsync();
    private static object Editor(IRenderedComponent<IComponent> page) => page.FindComponent<EditForm>().Instance.EditContext!.Model;
    private static Guid? EditorId(IRenderedComponent<IComponent> page) => Editor(page) switch {
        ResourceEditorModel resource => resource.Id,
        TestPlanEditorModel plan => plan.Id,
        _ => throw new InvalidOperationException("Expected an actual owner editor")
    };
    private static Guid? EditorProject(IRenderedComponent<IComponent> page) => Editor(page) switch {
        ResourceEditorModel resource => resource.ProjectId,
        TestPlanEditorModel plan => plan.ProjectId,
        _ => throw new InvalidOperationException("Expected an actual owner editor")
    };
    private static string EditorTitle(IRenderedComponent<IComponent> page) => Editor(page) switch {
        ResourceEditorModel resource => resource.Name,
        TestPlanEditorModel plan => plan.Title,
        _ => throw new InvalidOperationException("Expected an actual owner editor")
    };
    private static Guid[] ChildIds(IRenderedComponent<IComponent> page) => Editor(page) is TestPlanEditorModel plan
        ? plan.Cases.Select(item => RequiredId(item.Id)).Concat(plan.Evidence.Select(item => RequiredId(item.Id)))
            .Concat(plan.Runs.Select(item => RequiredId(item.Id))).ToArray() : [];
    private static Guid RequiredId(Guid? value) {
        var id = Assert.IsType<Guid>(value);
        Assert.NotEqual(Guid.Empty, id);
        return id;
    }

    private static void AssertWarning(IServiceProvider services, PostcommitOwner owner, Guid id, bool deleted = false) {
        var summary = owner == PostcommitOwner.Resource
            ? deleted ? "Resource deleted; refresh incomplete" : "Resource saved; refresh incomplete"
            : "Test plan saved; refresh incomplete";
        var warning = Assert.Single(services.GetRequiredService<NotificationService>().Messages,
            message => message.Severity == NotificationSeverity.Warning && message.Summary == summary);
        Assert.Contains(id.ToString("D"), warning.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(services.GetRequiredService<NotificationService>().Messages,
            message => message.Severity == NotificationSeverity.Error);
    }

    private static async Task AssertSavedAsync(IServiceProvider services, PostcommitOwner owner, ProjectWriteAdmission admission, Guid id, string title) {
        if (owner == PostcommitOwner.Resource) {
            var service = services.GetRequiredService<ResourcesService>();
            var saved = await service.GetAsync(id);
            Assert.Equal(id, saved.Id);
            Assert.Equal(admission, saved.ExpectedProjectAdmission);
            Assert.Equal(title, saved.Name);
            Assert.Equal(id, Assert.Single(await service.ListAsync(), item => item.ProjectId == admission.ProjectId).Id);
        } else {
            var service = services.GetRequiredService<TestLabService>();
            var saved = await service.GetAsync(id);
            Assert.Equal(id, saved.Id);
            Assert.Equal(admission, saved.ExpectedProjectAdmission);
            Assert.Equal(title, saved.Title);
            Assert.Equal(id, Assert.Single(await service.ListAsync(), item => item.ProjectId == admission.ProjectId).Id);
            Assert.NotNull(Assert.Single(saved.Cases).Id);
            Assert.NotNull(Assert.Single(saved.Evidence).Id);
            Assert.NotNull(Assert.Single(saved.Runs).Id);
        }
    }

    private static async Task RemoveFixtureAggregateAsync(IServiceProvider services, PostcommitOwner owner, Guid id, ProjectWriteAdmission admission) {
        if (owner == PostcommitOwner.Resource) {
            await services.GetRequiredService<ResourcesService>().DeleteAsync(id, admission);
            return;
        }
        await using var database = await services.GetRequiredService<IDbContextFactory<TestLabDbContext>>().CreateDbContextAsync();
        database.RemoveRange(await database.Set<TestCaseRecord>().Where(item => item.TestPlanId == id).ToListAsync());
        database.RemoveRange(await database.Set<TestEvidenceRecord>().Where(item => item.TestPlanId == id).ToListAsync());
        database.RemoveRange(await database.Set<TestRunRecord>().Where(item => item.TestPlanId == id).ToListAsync());
        database.Remove(await database.Set<TestPlan>().SingleAsync(item => item.Id == id));
        await database.SaveChangesAsync();
    }

    private sealed class BoundaryGate {
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task PauseAsync(CancellationToken cancellationToken) {
            Entered.TrySetResult();
            await released.Task.WaitAsync(cancellationToken);
        }
        public void Release() => released.TrySetResult();
    }
}
