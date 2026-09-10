using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.TestLab;

public sealed class TestLabOwnerPersistenceTests {
    [Fact]
    public async Task Runtime_model_contains_only_owned_records_with_complete_schema_parity() {
        await using var application = await TestApplication.CreateAsync();
        var ownerFactory = application.Services.GetRequiredService<IDbContextFactory<TestLabDbContext>>();
        var schemaFactory = application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var owner = await ownerFactory.CreateDbContextAsync();
        await using var schema = await schemaFactory.CreateDbContextAsync();
        var ownerModel = owner.GetService<IDesignTimeModel>().Model;
        var schemaModel = schema.GetService<IDesignTimeModel>().Model;
        Type[] ownedTypes = [typeof(TestPlan), typeof(TestCaseRecord), typeof(TestEvidenceRecord), typeof(TestRunRecord)];

        Assert.Equal(ownedTypes.OrderBy(type => type.Name), ownerModel.GetEntityTypes()
            .Select(entity => entity.ClrType).OrderBy(type => type.Name));
        foreach (var entity in ownerModel.GetEntityTypes()) {
            var completeEntity = Assert.IsAssignableFrom<IEntityType>(schemaModel.FindEntityType(entity.ClrType));
            Assert.Equal(completeEntity.ToDebugString(MetadataDebugStringOptions.LongDefault),
                entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        }

        Assert.Throws<InvalidOperationException>(() => owner.Set<Project>().ToQueryString());
    }

    [Fact]
    public async Task Legacy_aggregate_survives_restart_and_owner_edits_preserve_all_ids() {
        await using var environment = CanDoItAllTestEnvironment.Create("testlab-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var planId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2024, 1, 12, 12, 0, 0, TimeSpan.Zero);
        Guid projectId;
        await using (var beforeRestart = await TestApplication.CreateAsync(options)) {
            await using var scope = beforeRestart.Services.CreateAsyncScope();
            var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
            var projectResult = await projects.SaveAsync(new ProjectEditorModel {
                Name = "TestLab migration fixture",
                Description = "Keeps the historical plan linked to its project."
            });
            Assert.True(projectResult.IsSuccess);
            projectId = projectResult.Value;

            var factory = beforeRestart.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var schema = await factory.CreateDbContextAsync();
            schema.AddRange(
                new TestPlan {
                    Id = planId,
                    ProjectId = projectId,
                    Title = "Historical acceptance plan",
                    Phase = "Verification",
                    CoverageGoal = "Preserve recorded delivery evidence.",
                    PlaywrightSpecPath = "tests/browser/acceptance.spec.ts",
                    CreatedAtUtc = createdAt,
                    UpdatedAtUtc = createdAt
                },
                new TestCaseRecord {
                    Id = caseId,
                    TestPlanId = planId,
                    Name = "Open delivery report",
                    StoryOrFeature = "Delivery report",
                    Status = TestCaseStatus.Failed,
                    Notes = "Historical case notes"
                },
                new TestEvidenceRecord {
                    Id = evidenceId,
                    TestPlanId = planId,
                    EvidenceLabel = "Historical screenshot",
                    ArtifactPath = "evidence/acceptance.png",
                    EvidenceKind = "Screenshot",
                    Notes = "Historical evidence notes"
                },
                new TestRunRecord {
                    Id = runId,
                    TestPlanId = planId,
                    ExecutedAtUtc = createdAt,
                    Runner = "Playwright",
                    Result = TestCaseStatus.Failed,
                    Summary = "Historical run summary"
                });
            await schema.SaveChangesAsync();
        }

        await using var afterRestart = await TestApplication.CreateAsync(options);
        await using var ownerScope = afterRestart.Services.CreateAsyncScope();
        var service = ownerScope.ServiceProvider.GetRequiredService<TestLabService>();
        var summary = Assert.Single(await service.ListAsync());
        Assert.Equal(planId, summary.Id);
        Assert.Equal(projectId, summary.ProjectId);
        Assert.Equal(1, summary.CaseCount);
        Assert.Equal(1, summary.EvidenceCount);
        Assert.Equal(TestCaseStatus.Failed, summary.LatestResult);

        var editor = await service.GetAsync(planId);
        Assert.Equal(planId, editor.Id);
        Assert.Equal(projectId, editor.ProjectId);
        Assert.Null(editor.ResponsiblePartyId);
        Assert.Equal("Historical acceptance plan", editor.Title);
        Assert.Equal("Verification", editor.Phase);
        Assert.Equal("Preserve recorded delivery evidence.", editor.CoverageGoal);
        Assert.Equal("tests/browser/acceptance.spec.ts", editor.PlaywrightSpecPath);
        var testCase = Assert.Single(editor.Cases);
        Assert.Equal(caseId, testCase.Id);
        Assert.Equal("Open delivery report", testCase.Name);
        Assert.Equal("Delivery report", testCase.StoryOrFeature);
        Assert.Equal(TestCaseStatus.Failed, testCase.Status);
        Assert.Equal("Historical case notes", testCase.Notes);
        var evidence = Assert.Single(editor.Evidence);
        Assert.Equal(evidenceId, evidence.Id);
        Assert.Equal("Historical screenshot", evidence.EvidenceLabel);
        Assert.Equal("evidence/acceptance.png", evidence.ArtifactPath);
        Assert.Equal("Screenshot", evidence.EvidenceKind);
        Assert.Equal("Historical evidence notes", evidence.Notes);
        var run = Assert.Single(editor.Runs);
        Assert.Equal(runId, run.Id);
        Assert.Equal(createdAt, run.ExecutedAtUtc);
        Assert.Equal("Playwright", run.Runner);
        Assert.Equal(TestCaseStatus.Failed, run.Result);
        Assert.Equal("Historical run summary", run.Summary);

        editor.Title = "Owner acceptance proof";
        editor.Phase = "Accepted";
        testCase.Status = TestCaseStatus.Passed;
        testCase.Notes = "Reviewed through owner";
        evidence.EvidenceLabel = "Reviewed screenshot";
        evidence.Notes = "Retained historical artifact";
        run.Result = TestCaseStatus.Passed;
        run.Summary = "Reviewed historical run";
        var saveResult = await service.SaveAsync(editor);
        Assert.True(saveResult.IsSuccess);
        Assert.Equal(planId, saveResult.Value);

        var ownerFactory = afterRestart.Services.GetRequiredService<IDbContextFactory<TestLabDbContext>>();
        await using var readback = await ownerFactory.CreateDbContextAsync();
        var savedPlan = await readback.Set<TestPlan>().SingleAsync();
        Assert.Equal(planId, savedPlan.Id);
        Assert.Equal(projectId, savedPlan.ProjectId);
        Assert.Null(savedPlan.ResponsiblePartyId);
        Assert.Equal(createdAt, savedPlan.CreatedAtUtc);
        Assert.NotEqual(createdAt, savedPlan.UpdatedAtUtc);
        Assert.Equal(editor.Title, savedPlan.Title);
        Assert.Equal(editor.Phase, savedPlan.Phase);
        Assert.Equal(editor.CoverageGoal, savedPlan.CoverageGoal);
        Assert.Equal(editor.PlaywrightSpecPath, savedPlan.PlaywrightSpecPath);
        var savedCase = await readback.Set<TestCaseRecord>().SingleAsync();
        Assert.Equal(caseId, savedCase.Id);
        Assert.Equal(planId, savedCase.TestPlanId);
        Assert.Equal(testCase.Name, savedCase.Name);
        Assert.Equal(testCase.StoryOrFeature, savedCase.StoryOrFeature);
        Assert.Equal(TestCaseStatus.Passed, savedCase.Status);
        Assert.Equal(testCase.Notes, savedCase.Notes);
        var savedEvidence = await readback.Set<TestEvidenceRecord>().SingleAsync();
        Assert.Equal(evidenceId, savedEvidence.Id);
        Assert.Equal(planId, savedEvidence.TestPlanId);
        Assert.Equal(evidence.EvidenceLabel, savedEvidence.EvidenceLabel);
        Assert.Equal(evidence.ArtifactPath, savedEvidence.ArtifactPath);
        Assert.Equal(evidence.EvidenceKind, savedEvidence.EvidenceKind);
        Assert.Equal(evidence.Notes, savedEvidence.Notes);
        var savedRun = await readback.Set<TestRunRecord>().SingleAsync();
        Assert.Equal(runId, savedRun.Id);
        Assert.Equal(planId, savedRun.TestPlanId);
        Assert.Equal(createdAt, savedRun.ExecutedAtUtc);
        Assert.Equal(run.Runner, savedRun.Runner);
        Assert.Equal(TestCaseStatus.Passed, savedRun.Result);
        Assert.Equal(run.Summary, savedRun.Summary);
        Assert.Equal(TestCaseStatus.Passed, Assert.Single(await service.ListAsync()).LatestResult);

        var search = ownerScope.ServiceProvider.GetRequiredService<ISearchIndexService>();
        Assert.Contains(await search.SearchAsync(editor.Title),
            item => item.Route == $"/test-lab?planId={planId}" && item.Title == editor.Title);
    }

    [Fact]
    public async Task Owner_factories_keep_writes_in_their_original_profiles() {
        await using var environment = CanDoItAllTestEnvironment.Create("testlab-owner-isolation");
        var originalProfile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        await using var original = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = originalProfile
        });
        await using var originalScope = original.Services.CreateAsyncScope();
        var originalService = originalScope.ServiceProvider.GetRequiredService<TestLabService>();
        var originalEditor = new TestPlanEditorModel { Title = "Original profile plan" };
        var originalResult = await originalService.SaveAsync(originalEditor);
        Assert.True(originalResult.IsSuccess);
        originalEditor.Id = originalResult.Value;

        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = otherProfile
        });
        await using var otherScope = other.Services.CreateAsyncScope();
        var otherService = otherScope.ServiceProvider.GetRequiredService<TestLabService>();
        Assert.Empty(await otherService.ListAsync());
        Assert.Null((await otherService.GetAsync(originalResult.Value)).Id);
        var otherResult = await otherService.SaveAsync(new TestPlanEditorModel { Title = "Other profile plan" });
        Assert.True(otherResult.IsSuccess);

        originalEditor.Title = "Original profile edited after other profile initialized";
        var editResult = await originalService.SaveAsync(originalEditor);
        Assert.True(editResult.IsSuccess);
        Assert.Equal(originalResult.Value, editResult.Value);
        Assert.Null((await originalService.GetAsync(otherResult.Value)).Id);
        Assert.Equal(originalResult.Value, Assert.Single(await originalService.ListAsync()).Id);
        var otherSummary = Assert.Single(await otherService.ListAsync());
        Assert.Equal(otherResult.Value, otherSummary.Id);
        Assert.Equal("Other profile plan", otherSummary.Title);
        Assert.Null((await otherService.GetAsync(originalResult.Value)).Id);

        var originalFactory = original.Services.GetRequiredService<IDbContextFactory<TestLabDbContext>>();
        await using var originalReadback = await originalFactory.CreateDbContextAsync();
        var originalPlan = await originalReadback.Set<TestPlan>().SingleAsync();
        Assert.Equal(originalResult.Value, originalPlan.Id);
        Assert.Equal(originalEditor.Title, originalPlan.Title);
        var otherFactory = other.Services.GetRequiredService<IDbContextFactory<TestLabDbContext>>();
        await using var otherReadback = await otherFactory.CreateDbContextAsync();
        Assert.Equal(otherResult.Value, (await otherReadback.Set<TestPlan>().SingleAsync()).Id);
    }
}
