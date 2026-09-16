using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class OwnerPostcommitPersistenceTests {
    [Theory]
    [InlineData(PostcommitFault.Search)]
    [InlineData(PostcommitFault.SearchCancellation)]
    [InlineData(PostcommitFault.Activity)]
    public async Task Resource_confirmed_save_keeps_identity_and_original_failure_across_restart(PostcommitFault fault) {
        await using var environment = CanDoItAllTestEnvironment.Create("resource-postcommit-save");
        var probe = new OwnerPostcommitTestProbe();
        var options = Options(environment, probe);
        using var cancellation = new CancellationTokenSource();
        var originalFailure = Failure(fault, cancellation.Token);
        ResourceEditorModel editor;
        Guid resourceId;
        await using (var application = await TestApplication.CreateAsync(options)) {
            await using var scope = application.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var admission = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Resource confirmed save");
            editor = OwnerPostcommitTestProbe.Resource(services, admission);
            probe.ArmFault(PostcommitOwner.Resource, fault, originalFailure, cancellation);
            var failure = await Assert.ThrowsAsync<ResourceCommittedMutationException>(() =>
                services.GetRequiredService<ResourcesService>().SaveAsync(editor, cancellation.Token));
            resourceId = failure.ResourceId;
            Assert.NotEqual(Guid.Empty, resourceId);
            Assert.Equal(resourceId, editor.Id);
            Assert.Equal(ResourceMutationKind.Save, failure.MutationKind);
            Assert.Same(originalFailure, failure.InnerException);
            AssertCancellation(fault, cancellation, failure.InnerException);
            Assert.Equal(1, probe.SearchReturns);
            Assert.Equal(fault == PostcommitFault.Activity ? 1 : 0, probe.ActivityReturns);
            await AssertResourceAsync(services, editor, resourceId);
        }

        await using var restarted = await TestApplication.CreateAsync(options);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        await AssertResourceAsync(restartedScope.ServiceProvider, editor, resourceId);
        editor.Name = "Explicit same-editor update";
        var saved = await restartedScope.ServiceProvider.GetRequiredService<ResourcesService>().SaveAsync(editor);
        Assert.True(saved.IsSuccess);
        Assert.Equal(resourceId, saved.Value);
        await AssertResourceAsync(restartedScope.ServiceProvider, editor, resourceId);
    }

    [Theory]
    [InlineData(PostcommitFault.Search)]
    [InlineData(PostcommitFault.SearchCancellation)]
    [InlineData(PostcommitFault.Activity)]
    public async Task Resource_confirmed_delete_remains_deleted_after_postcommit_fault_and_restart(PostcommitFault fault) {
        await using var environment = CanDoItAllTestEnvironment.Create("resource-postcommit-delete");
        var probe = new OwnerPostcommitTestProbe();
        var options = Options(environment, probe);
        using var cancellation = new CancellationTokenSource();
        var originalFailure = Failure(fault, cancellation.Token);
        Guid resourceId;
        ProjectWriteAdmission admission;
        await using (var application = await TestApplication.CreateAsync(options)) {
            await using var scope = application.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            admission = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Resource confirmed deletion");
            var owner = services.GetRequiredService<ResourcesService>();
            var saved = await owner.SaveAsync(OwnerPostcommitTestProbe.Resource(services, admission));
            Assert.True(saved.IsSuccess);
            resourceId = saved.Value;
            probe.ArmFault(PostcommitOwner.Resource, fault, originalFailure, cancellation);
            var failure = await Assert.ThrowsAsync<ResourceCommittedMutationException>(() =>
                owner.DeleteAsync(resourceId, admission, cancellation.Token));
            Assert.Equal(resourceId, failure.ResourceId);
            Assert.Equal(ResourceMutationKind.Delete, failure.MutationKind);
            Assert.Same(originalFailure, failure.InnerException);
            AssertCancellation(fault, cancellation, failure.InnerException);
            Assert.Equal(2, probe.SearchReturns);
            Assert.Equal(fault == PostcommitFault.Activity ? 2 : 1, probe.ActivityReturns);
            Assert.Null((await owner.GetAsync(resourceId)).Id);
        }

        await using var restarted = await TestApplication.CreateAsync(options);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var restartedOwner = restartedScope.ServiceProvider.GetRequiredService<ResourcesService>();
        Assert.Null((await restartedOwner.GetAsync(resourceId)).Id);
        var searchReturns = probe.SearchReturns;
        var activityReturns = probe.ActivityReturns;
        await restartedOwner.DeleteAsync(resourceId, admission);
        Assert.Equal(searchReturns, probe.SearchReturns);
        Assert.Equal(activityReturns, probe.ActivityReturns);
        await using var database = await restartedScope.ServiceProvider.GetRequiredService<IDbContextFactory<ResourcesDbContext>>().CreateDbContextAsync();
        Assert.False(await database.Set<ProjectResource>().AnyAsync(item => item.Id == resourceId));
    }

    [Theory]
    [InlineData(false, PostcommitFault.Search)]
    [InlineData(false, PostcommitFault.SearchCancellation)]
    [InlineData(false, PostcommitFault.Activity)]
    [InlineData(true, PostcommitFault.Search)]
    [InlineData(true, PostcommitFault.SearchCancellation)]
    [InlineData(true, PostcommitFault.Activity)]
    public async Task Test_plan_confirmed_save_retains_parent_and_all_child_ids_for_restart_and_explicit_edit(
        bool projectBacked, PostcommitFault fault) {
        await using var environment = CanDoItAllTestEnvironment.Create("testlab-postcommit-save");
        var probe = new OwnerPostcommitTestProbe();
        var options = Options(environment, probe);
        using var cancellation = new CancellationTokenSource();
        var originalFailure = Failure(fault, cancellation.Token);
        TestPlanEditorModel editor;
        Guid planId;
        Guid caseId;
        Guid evidenceId;
        Guid runId;
        await using (var application = await TestApplication.CreateAsync(options)) {
            await using var scope = application.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var admission = projectBacked
                ? await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Test Lab confirmed save") : null;
            editor = OwnerPostcommitTestProbe.Plan(admission);
            probe.ArmFault(PostcommitOwner.TestPlan, fault, originalFailure, cancellation);
            var failure = await Assert.ThrowsAsync<TestPlanCommittedSaveException>(() =>
                services.GetRequiredService<TestLabService>().SaveAsync(editor, cancellation.Token));
            planId = failure.TestPlanId;
            Assert.NotEqual(Guid.Empty, planId);
            Assert.Equal(planId, editor.Id);
            Assert.Same(originalFailure, failure.InnerException);
            AssertCancellation(fault, cancellation, failure.InnerException);
            caseId = RequiredId(Assert.Single(editor.Cases).Id);
            evidenceId = RequiredId(Assert.Single(editor.Evidence).Id);
            runId = RequiredId(Assert.Single(editor.Runs).Id);
            Assert.Equal(4, new[] { planId, caseId, evidenceId, runId }.Distinct().Count());
            Assert.Equal(1, probe.SearchReturns);
            Assert.Equal(fault == PostcommitFault.Activity ? 1 : 0, probe.ActivityReturns);
            await AssertPlanAsync(services, editor, planId, caseId, evidenceId, runId);
        }

        await using var restarted = await TestApplication.CreateAsync(options);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var restartedServices = restartedScope.ServiceProvider;
        await AssertPlanAsync(restartedServices, editor, planId, caseId, evidenceId, runId);
        editor.Title = "Explicit same-editor update";
        editor.Cases[0].Notes = "Updated case evidence";
        editor.Evidence[0].Notes = "Updated artifact evidence";
        editor.Runs[0].Summary = "Updated run summary";
        var saved = await restartedServices.GetRequiredService<TestLabService>().SaveAsync(editor);
        Assert.True(saved.IsSuccess);
        Assert.Equal(planId, saved.Value);
        await AssertPlanAsync(restartedServices, editor, planId, caseId, evidenceId, runId);
    }

    [Theory]
    [InlineData(PostcommitOwner.Resource)]
    [InlineData(PostcommitOwner.TestPlan)]
    public async Task Recreated_project_denial_before_commit_does_not_claim_commit_or_assign_editor_ids(PostcommitOwner owner) {
        var probe = new OwnerPostcommitTestProbe();
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = probe.ConfigureServices });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var original = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Original lifetime");
        var projects = services.GetRequiredService<ProjectsService>();
        await projects.DeleteAsync(original.ProjectId, expectedProjectAdmission: original);
        var replacement = await projects.CreateAsync(original.ProjectId, new ProjectEditorModel { Name = "Replacement lifetime" });
        Assert.True(replacement.IsSuccess);
        var current = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(original.ProjectId);
        Assert.NotEqual(original.LifetimeId, Assert.IsType<ProjectWriteAdmission>(current).LifetimeId);
        var resource = OwnerPostcommitTestProbe.Resource(services, original);
        var plan = OwnerPostcommitTestProbe.Plan(original);
        var failure = await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(async () => {
            if (owner == PostcommitOwner.Resource) {
                await services.GetRequiredService<ResourcesService>().SaveAsync(resource);
            } else {
                await services.GetRequiredService<TestLabService>().SaveAsync(plan);
            }
        });
        Assert.Equal(original, failure.Admission);
        Assert.Null(resource.Id);
        AssertUnassigned(plan);
        Assert.Equal(0, probe.SearchReturns);
        Assert.Equal(0, probe.ActivityReturns);
        await AssertNoOwnedRowsAsync(services, original.ProjectId);
    }

    [Theory]
    [InlineData(PostcommitOwner.Resource)]
    [InlineData(PostcommitOwner.TestPlan)]
    public async Task Cancellation_at_owning_flush_preserves_original_exception_and_no_committed_ids(PostcommitOwner owner) {
        var probe = new OwnerPostcommitTestProbe();
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = probe.ConfigureServices });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Before-commit cancellation");
        var resource = OwnerPostcommitTestProbe.Resource(services, admission);
        var plan = OwnerPostcommitTestProbe.Plan(admission);
        using var cancellation = new CancellationTokenSource();
        var expected = new OperationCanceledException("Owning save was cancelled", cancellation.Token);
        probe.BeforeSave = (actual, _) => {
            Assert.Equal(owner, actual);
            cancellation.Cancel();
            throw expected;
        };
        var observed = await Assert.ThrowsAsync<OperationCanceledException>(async () => {
            if (owner == PostcommitOwner.Resource) {
                await services.GetRequiredService<ResourcesService>().SaveAsync(resource, cancellation.Token);
            } else {
                await services.GetRequiredService<TestLabService>().SaveAsync(plan, cancellation.Token);
            }
        });
        Assert.Same(expected, observed);
        Assert.Null(resource.Id);
        AssertUnassigned(plan);
        Assert.Equal(0, probe.SearchReturns);
        Assert.Equal(0, probe.ActivityReturns);
        await AssertNoOwnedRowsAsync(services, admission.ProjectId);
    }

    private static TestHarnessOptions Options(CanDoItAllTestEnvironment environment, OwnerPostcommitTestProbe probe) => new() {
        TestEnvironment = environment,
        ActiveProfile = environment.CreatePostgreSqlProfile("original"),
        ConfigureServices = probe.ConfigureServices
    };

    private static Exception Failure(PostcommitFault fault, CancellationToken cancellationToken) => fault == PostcommitFault.SearchCancellation
        ? new OperationCanceledException("Cancelled after owner commit", cancellationToken)
        : new InvalidOperationException("Failure after owner commit");

    private static void AssertCancellation(PostcommitFault fault, CancellationTokenSource cancellation, Exception? failure) {
        Assert.Equal(fault == PostcommitFault.SearchCancellation, cancellation.IsCancellationRequested);
        if (fault == PostcommitFault.SearchCancellation) {
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
    }

    private static Guid RequiredId(Guid? value) {
        var id = Assert.IsType<Guid>(value);
        Assert.NotEqual(Guid.Empty, id);
        return id;
    }

    private static void AssertUnassigned(TestPlanEditorModel editor) {
        Assert.Null(editor.Id);
        Assert.Null(Assert.Single(editor.Cases).Id);
        Assert.Null(Assert.Single(editor.Evidence).Id);
        Assert.Null(Assert.Single(editor.Runs).Id);
    }

    private static async Task AssertResourceAsync(IServiceProvider services, ResourceEditorModel expected, Guid resourceId) {
        await using var database = await services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>().CreateDbContextAsync();
        var row = Assert.Single(await database.Set<ProjectResource>().AsNoTracking().Where(item => item.ProjectId == expected.ProjectId).ToListAsync());
        Assert.Equal(resourceId, row.Id);
        Assert.Equal(expected.ExpectedProjectAdmission!.LifetimeId, row.ProjectLifetimeId);
        Assert.Equal(expected.Name, row.Name);
        Assert.Equal(expected.Description, row.Description);
        Assert.Equal(OwnerPostcommitTestProbe.ResourceLocation, row.LocationOrIdentifier);
        Assert.Equal(expected.ConnectorPluginKey, row.ConnectorPluginKey);
        Assert.True(row.SupportsPreview);
        var read = await services.GetRequiredService<ResourcesService>().GetAsync(resourceId);
        Assert.Equal(resourceId, read.Id);
        Assert.Equal(expected.ExpectedProjectAdmission, read.ExpectedProjectAdmission);
        var expectedValues = expected.Configuration.Values.ToDictionary();
        expectedValues[nameof(WebLinkResourceConfig.Url)] = expected.Configuration.Values[ResourceConnectorFieldKeys.WebUrl];
        expectedValues[nameof(WebLinkResourceConfig.TitleHint)] = expected.Configuration.Values[ResourceConnectorFieldKeys.UrlTitleHint];
        Assert.Equal(expectedValues.OrderBy(item => item.Key, StringComparer.Ordinal),
            read.Configuration.Values.OrderBy(item => item.Key, StringComparer.Ordinal));
    }

    private static async Task AssertPlanAsync(IServiceProvider services, TestPlanEditorModel expected,
        Guid planId, Guid caseId, Guid evidenceId, Guid runId) {
        await using var database = await services.GetRequiredService<IDbContextFactory<TestLabDbContext>>().CreateDbContextAsync();
        var row = Assert.Single(await database.Set<TestPlan>().AsNoTracking().ToListAsync());
        Assert.Equal(planId, row.Id);
        Assert.Equal(expected.ProjectId, row.ProjectId);
        Assert.Equal(expected.ExpectedProjectAdmission?.LifetimeId, row.ProjectLifetimeId);
        var read = await services.GetRequiredService<TestLabService>().GetAsync(planId);
        Assert.Equal(expected.Id, read.Id);
        Assert.Equal(expected.ExpectedProjectAdmission, read.ExpectedProjectAdmission);
        Assert.Equal(expected.Title, read.Title);
        Assert.Equal(expected.Phase, read.Phase);
        Assert.Equal(expected.CoverageGoal, read.CoverageGoal);
        Assert.Equal(expected.PlaywrightSpecPath, read.PlaywrightSpecPath);
        var testCase = Assert.Single(read.Cases);
        Assert.Equal(caseId, testCase.Id);
        Assert.Equal(expected.Cases[0].Id, testCase.Id);
        Assert.Equal(expected.Cases[0].Name, testCase.Name);
        Assert.Equal(expected.Cases[0].StoryOrFeature, testCase.StoryOrFeature);
        Assert.Equal(expected.Cases[0].Status, testCase.Status);
        Assert.Equal(expected.Cases[0].Notes, testCase.Notes);
        var evidence = Assert.Single(read.Evidence);
        Assert.Equal(evidenceId, evidence.Id);
        Assert.Equal(expected.Evidence[0].Id, evidence.Id);
        Assert.Equal(expected.Evidence[0].EvidenceLabel, evidence.EvidenceLabel);
        Assert.Equal(expected.Evidence[0].ArtifactPath, evidence.ArtifactPath);
        Assert.Equal(expected.Evidence[0].EvidenceKind, evidence.EvidenceKind);
        Assert.Equal(expected.Evidence[0].Notes, evidence.Notes);
        var run = Assert.Single(read.Runs);
        Assert.Equal(runId, run.Id);
        Assert.Equal(expected.Runs[0].Id, run.Id);
        Assert.Equal(expected.Runs[0].ExecutedAtUtc, run.ExecutedAtUtc);
        Assert.Equal(expected.Runs[0].Runner, run.Runner);
        Assert.Equal(expected.Runs[0].Result, run.Result);
        Assert.Equal(expected.Runs[0].Summary, run.Summary);
        Assert.Equal(1, await database.Set<TestCaseRecord>().CountAsync());
        Assert.Equal(1, await database.Set<TestEvidenceRecord>().CountAsync());
        Assert.Equal(1, await database.Set<TestRunRecord>().CountAsync());
    }

    private static async Task AssertNoOwnedRowsAsync(IServiceProvider services, Guid projectId) {
        await using var resources = await services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>().CreateDbContextAsync();
        await using var testLab = await services.GetRequiredService<IDbContextFactory<TestLabDbContext>>().CreateDbContextAsync();
        Assert.False(await resources.Set<ProjectResource>().AnyAsync(item => item.ProjectId == projectId));
        Assert.False(await testLab.Set<TestPlan>().AnyAsync(item => item.ProjectId == projectId));
        Assert.Empty(await testLab.Set<TestCaseRecord>().ToListAsync());
        Assert.Empty(await testLab.Set<TestEvidenceRecord>().ToListAsync());
        Assert.Empty(await testLab.Set<TestRunRecord>().ToListAsync());
    }
}
