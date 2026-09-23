using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.TestLab;

public enum TestCaseStatus
{
    Planned,
    Implemented,
    Passed,
    Failed,
    Blocked
}

public sealed class TestPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public Guid? ProjectLifetimeId { get; set; }

    public Guid? ResponsiblePartyId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string CoverageGoal { get; set; } = string.Empty;

    public string PlaywrightSpecPath { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class TestCaseRecord : ITestPlanChildRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TestPlanId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string StoryOrFeature { get; set; } = string.Empty;

    public TestCaseStatus Status { get; set; } = TestCaseStatus.Planned;

    public string Notes { get; set; } = string.Empty;
}

public sealed class TestEvidenceRecord : ITestPlanChildRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TestPlanId { get; set; }

    public string EvidenceLabel { get; set; } = string.Empty;

    public string ArtifactPath { get; set; } = string.Empty;

    public string EvidenceKind { get; set; } = "Screenshot";

    public string Notes { get; set; } = string.Empty;
}

public sealed class TestRunRecord : ITestPlanChildRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TestPlanId { get; set; }

    public DateTimeOffset ExecutedAtUtc { get; set; }

    public string Runner { get; set; } = string.Empty;

    public TestCaseStatus Result { get; set; } = TestCaseStatus.Planned;

    public string Summary { get; set; } = string.Empty;
}

internal sealed class TestPlanConfiguration : IEntityTypeConfiguration<TestPlan>
{
    public void Configure(EntityTypeBuilder<TestPlan> builder)
    {
        builder.ToTable("TestLab_TestPlans");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Title).HasMaxLength(200).IsRequired();
        builder.Property(plan => plan.Phase).HasMaxLength(120);
        builder.Property(plan => plan.CoverageGoal).HasColumnType("TEXT");
        builder.Property(plan => plan.PlaywrightSpecPath).HasMaxLength(500);
    }
}

internal sealed class TestCaseRecordConfiguration : IEntityTypeConfiguration<TestCaseRecord>
{
    public void Configure(EntityTypeBuilder<TestCaseRecord> builder)
    {
        builder.ToTable("TestLab_TestCases");
        builder.HasKey(testCase => testCase.Id);
        builder.Property(testCase => testCase.Name).HasMaxLength(200).IsRequired();
        builder.Property(testCase => testCase.StoryOrFeature).HasMaxLength(200);
        builder.Property(testCase => testCase.Notes).HasColumnType("TEXT");
    }
}

internal sealed class TestEvidenceRecordConfiguration : IEntityTypeConfiguration<TestEvidenceRecord>
{
    public void Configure(EntityTypeBuilder<TestEvidenceRecord> builder)
    {
        builder.ToTable("TestLab_TestEvidence");
        builder.HasKey(evidence => evidence.Id);
        builder.Property(evidence => evidence.EvidenceLabel).HasMaxLength(200).IsRequired();
        builder.Property(evidence => evidence.ArtifactPath).HasMaxLength(600).IsRequired();
        builder.Property(evidence => evidence.EvidenceKind).HasMaxLength(80).IsRequired();
        builder.Property(evidence => evidence.Notes).HasColumnType("TEXT");
    }
}

internal sealed class TestRunRecordConfiguration : IEntityTypeConfiguration<TestRunRecord>
{
    public void Configure(EntityTypeBuilder<TestRunRecord> builder)
    {
        builder.ToTable("TestLab_TestRuns");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Runner).HasMaxLength(120).IsRequired();
        builder.Property(run => run.Summary).HasColumnType("TEXT");
    }
}

public sealed record TestPlanSummary(
    Guid Id,
    Guid? ProjectId,
    string Title,
    string Phase,
    int CaseCount,
    int EvidenceCount,
    TestCaseStatus? LatestResult,
    DateTimeOffset UpdatedAtUtc) {
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid? ProjectLifetimeId { get; init; }
}

public sealed class TestCaseEditorModel : ITestPlanChildEditor
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string StoryOrFeature { get; set; } = string.Empty;

    public TestCaseStatus Status { get; set; } = TestCaseStatus.Planned;

    public string Notes { get; set; } = string.Empty;
}

public sealed class TestEvidenceEditorModel : ITestPlanChildEditor
{
    public Guid? Id { get; set; }

    public string EvidenceLabel { get; set; } = string.Empty;

    public string ArtifactPath { get; set; } = string.Empty;

    public string EvidenceKind { get; set; } = "Screenshot";

    public string Notes { get; set; } = string.Empty;
}

public sealed class TestRunEditorModel : ITestPlanChildEditor
{
    public Guid? Id { get; set; }

    public DateTimeOffset ExecutedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string Runner { get; set; } = "Playwright";

    public TestCaseStatus Result { get; set; } = TestCaseStatus.Planned;

    public string Summary { get; set; } = string.Empty;
}

public sealed class TestPlanEditorModel
{
    public Guid? Id { get; set; }

    public Guid? ProjectId { get; set; }

    public ProjectWriteAdmission? ExpectedProjectAdmission { get; set; }

    public Guid? ResponsiblePartyId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string CoverageGoal { get; set; } = string.Empty;

    public string PlaywrightSpecPath { get; set; } = string.Empty;

    public List<TestCaseEditorModel> Cases { get; set; } = [];

    public List<TestEvidenceEditorModel> Evidence { get; set; } = [];

    public List<TestRunEditorModel> Runs { get; set; } = [];
}

/* codex-capsule
kind: service
name: TestLabService
summary: Persists test plans, linked cases, evidence, and execution results for delivery traceability.
owns: test-plan aggregate, evidence records, latest run summary
deps: TestLabDbContext, CoordinatedDatabaseTransaction, IClock, IActivityStream, ISearchIndexService
risks: evidence-path-drift, noisy-test-plan-updates
tests: integration:TestLabOwnerPersistenceTests, integration:WorkbenchOwnerProjectionIntegrationTests, integration:CrmHrCrossModuleIntegrationTests, integration:ProjectStructureAgentIntegrationTests, integration:ProjectStructureAutomaticPlacementIntegrationTests
inputs: TestPlanEditorModel
outputs: TestPlanSummary, test plan detail, projection and scope facts
*/
public sealed class TestLabService(
    IDbContextFactory<TestLabDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService,
    DbContextOptions<TestLabDbContext> contextOptions,
    CoordinatedDatabaseTransaction coordinatedTransaction,
    ProjectWriteAdmissionService writeAdmissions,
    ILogger<TestLabService>? logger = null)
{
    public async Task<IReadOnlyList<TestPlanProjectionFact>> ListProjectProjectionFactsAsync(
        Guid projectId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var admission = await writeAdmissions.CaptureAsync(projectId, cancellationToken);
        return admission is null ? [] : await LoadProjectProjectionFactsAsync(dbContext, projectId, admission.LifetimeId, cancellationToken);
    }

    public async Task<IReadOnlyList<TestPlanProjectionFact>> ListProjectProjectionFactsForMutationAsync(
        Guid projectId, CancellationToken cancellationToken = default) {
        await using var dbContext = await coordinatedTransaction.CreateEnlistedAsync(contextOptions,
            static options => new TestLabDbContext(options), cancellationToken);
        var admission = await writeAdmissions.CaptureForMutationAsync(projectId, cancellationToken);
        return admission is null ? [] : await LoadProjectProjectionFactsAsync(dbContext, projectId, admission.LifetimeId, cancellationToken);
    }

    private static async Task<IReadOnlyList<TestPlanProjectionFact>> LoadProjectProjectionFactsAsync(
        TestLabDbContext dbContext, Guid projectId, Guid lifetimeId, CancellationToken cancellationToken) {
        var plans = await dbContext.Set<TestPlan>().AsNoTracking()
            .Where(plan => plan.ProjectId == projectId && plan.ProjectLifetimeId == lifetimeId)
            .Select(plan => new TestPlanProjectionFact(plan.Id, plan.Title, plan.Phase,
                plan.CoverageGoal, plan.CreatedAtUtc, plan.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        return plans.OrderByDescending(plan => plan.UpdatedAtUtc).ToArray();
    }

    public async Task<TestPlanProjectionScopeFact?> ReadProjectionScopeAsync(
        Guid testPlanId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var plan = await dbContext.Set<TestPlan>().AsNoTracking().Where(plan => plan.Id == testPlanId)
            .Select(plan => new { plan.ProjectId, plan.ProjectLifetimeId }).SingleOrDefaultAsync(cancellationToken);
        if (plan is null || plan.ProjectId is null) {
            return plan is null ? null : new(null);
        }
        var current = plan.ProjectId == Guid.Empty ? null : await writeAdmissions.CaptureAsync(plan.ProjectId.Value, cancellationToken);
        return current is null || current.LifetimeId != plan.ProjectLifetimeId ? null : new(plan.ProjectId);
    }

    public async Task<IReadOnlyList<TestPlanSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var caseCounts = await dbContext.Set<TestCaseRecord>()
            .GroupBy(item => item.TestPlanId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);
        var evidenceCounts = await dbContext.Set<TestEvidenceRecord>()
            .GroupBy(item => item.TestPlanId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);
        var latestRuns = (await dbContext.Set<TestRunRecord>().ToListAsync(cancellationToken))
            .GroupBy(item => item.TestPlanId)
            .Select(group => group.OrderByDescending(item => item.ExecutedAtUtc).First())
            .ToList();

        var runLookup = latestRuns.ToDictionary(item => item.TestPlanId, item => (TestCaseStatus?)item.Result);
        var plans = (await dbContext.Set<TestPlan>().ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();

        return plans.Select(plan => new TestPlanSummary(
            plan.Id,
            plan.ProjectId,
            plan.Title,
            plan.Phase,
            caseCounts.GetValueOrDefault(plan.Id),
            evidenceCounts.GetValueOrDefault(plan.Id),
            runLookup.GetValueOrDefault(plan.Id),
            plan.UpdatedAtUtc) { ProjectLifetimeId = plan.ProjectLifetimeId }).ToList();
    }

    public async Task<TestPlanEditorModel> GetAsync(Guid? id, CancellationToken cancellationToken = default)
    {
        if (!id.HasValue)
        {
            return new TestPlanEditorModel();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var plan = await dbContext.Set<TestPlan>().FirstOrDefaultAsync(item => item.Id == id.Value, cancellationToken);
        if (plan is null)
        {
            return new TestPlanEditorModel();
        }

        var runRecords = await dbContext.Set<TestRunRecord>()
            .Where(item => item.TestPlanId == plan.Id)
            .ToListAsync(cancellationToken);

        return new TestPlanEditorModel
        {
            Id = plan.Id,
            ProjectId = plan.ProjectId,
            ExpectedProjectAdmission = plan.ProjectLifetimeId is { } lifetimeId && plan.ProjectId is { } projectId && projectId != Guid.Empty
                ? new(writeAdmissions.DatabaseProfileId, projectId, lifetimeId) : null,
            ResponsiblePartyId = plan.ResponsiblePartyId,
            Title = plan.Title,
            Phase = plan.Phase,
            CoverageGoal = plan.CoverageGoal,
            PlaywrightSpecPath = plan.PlaywrightSpecPath,
            Cases = await dbContext.Set<TestCaseRecord>()
                .Where(item => item.TestPlanId == plan.Id)
                .OrderBy(item => item.Name)
                .Select(item => new TestCaseEditorModel { Id = item.Id, Name = item.Name, StoryOrFeature = item.StoryOrFeature, Status = item.Status, Notes = item.Notes })
                .ToListAsync(cancellationToken),
            Evidence = await dbContext.Set<TestEvidenceRecord>()
                .Where(item => item.TestPlanId == plan.Id)
                .OrderBy(item => item.EvidenceLabel)
                .Select(item => new TestEvidenceEditorModel { Id = item.Id, EvidenceLabel = item.EvidenceLabel, ArtifactPath = item.ArtifactPath, EvidenceKind = item.EvidenceKind, Notes = item.Notes })
                .ToListAsync(cancellationToken),
            Runs = runRecords
                .OrderByDescending(item => item.ExecutedAtUtc)
                .Select(item => new TestRunEditorModel { Id = item.Id, ExecutedAtUtc = item.ExecutedAtUtc, Runner = item.Runner, Result = item.Result, Summary = item.Summary })
                .ToList()
        };
    }

    public async Task<Result<Guid>> SaveAsync(TestPlanEditorModel model, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(model.Title)) {
            return Result<Guid>.Failure(Error.Validation("Test plan title is required."));
        }
        var admission = model.ExpectedProjectAdmission;
        if (model.ProjectId == Guid.Empty || model.ProjectId.HasValue && (admission is null || admission.ProjectId != model.ProjectId) ||
            !model.ProjectId.HasValue && admission is not null) {
            return Result<Guid>.Failure(Error.Validation("Select a current project, or explicitly select a global plan, before saving."));
        }
        var editing = model.Id.HasValue;
        var entityId = model.Id ?? Guid.NewGuid();
        TestPlan entity;
        List<Action> committedIdentityUpdates = [];
        var committed = false;
        try {
            await using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken)) {
                var previous = await dbContext.Set<TestPlan>().AsNoTracking().Where(item => item.Id == entityId)
                    .Select(item => new { item.ProjectId, item.ProjectLifetimeId }).SingleOrDefaultAsync(cancellationToken);
                if (editing && previous is null) {
                    return Result<Guid>.Failure(Error.Validation("The test plan no longer exists. Reload Test Lab before saving."));
                }
                var keys = BuildMutationKeys(entityId, previous?.ProjectId, admission?.ProjectId);
                await using var mutation = await SerializableMutationScope.BeginAsync(dbContext, keys, cancellationToken);
                using (coordinatedTransaction.Enter(dbContext)) {
                    if (admission is not null) {
                        await writeAdmissions.RequireForMutationAsync(admission, cancellationToken);
                    }
                    var stored = await dbContext.Set<TestPlan>().SingleOrDefaultAsync(item => item.Id == entityId, cancellationToken);
                    if (previous is not null && (stored is null || stored.ProjectId != previous.ProjectId || stored.ProjectLifetimeId != previous.ProjectLifetimeId)) {
                        throw new InvalidOperationException("The test plan project binding changed. Reload Test Lab before saving.");
                    }
                    entity = stored ?? new TestPlan { Id = entityId, CreatedAtUtc = clock.GetUtcNow() };
                    if (previous is null) {
                        dbContext.Add(entity);
                    }
                    entity.ProjectId = admission?.ProjectId;
                    entity.ProjectLifetimeId = admission?.LifetimeId;
                    entity.ResponsiblePartyId = model.ResponsiblePartyId;
                    entity.Title = model.Title.Trim();
                    entity.Phase = model.Phase?.Trim() ?? string.Empty;
                    entity.CoverageGoal = model.CoverageGoal?.Trim() ?? string.Empty;
                    entity.PlaywrightSpecPath = model.PlaywrightSpecPath?.Trim() ?? string.Empty;
                    entity.UpdatedAtUtc = clock.GetUtcNow();
                    await SyncCollectionAsync(dbContext.Set<TestCaseRecord>(), entity.Id, model.Cases, MapCase, committedIdentityUpdates, cancellationToken);
                    await SyncCollectionAsync(dbContext.Set<TestEvidenceRecord>(), entity.Id, model.Evidence, MapEvidence, committedIdentityUpdates, cancellationToken);
                    await SyncCollectionAsync(dbContext.Set<TestRunRecord>(), entity.Id, model.Runs, MapRun, committedIdentityUpdates, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                await mutation.CommitAsync(cancellationToken);
                committed = true;
                model.Id = entity.Id;
                foreach (var retainIdentity in committedIdentityUpdates) {
                    retainIdentity();
                }
            }
            await UpsertSearchAsync(admission, new SearchDocumentInput(
                "test-plan", entity.Id.ToString(), "Test Lab", entity.Title, entity.Phase,
                $"{entity.CoverageGoal}\nPlaywright: {entity.PlaywrightSpecPath}", $"/test-lab?planId={entity.Id}", entity.ProjectId), cancellationToken);
            await activityStream.RecordAsync(new ActivityWriteRequest(
                "test-lab", editing ? "update-plan" : "create-plan", $"{(editing ? "Updated" : "Created")} test plan",
                entity.Title, ProjectId: entity.ProjectId, ArtifactKind: "test-plan", ArtifactId: entity.Id,
                Route: $"/test-lab?planId={entity.Id}"), cancellationToken);
            return Result<Guid>.Success(entity.Id);
        } catch (Exception exception) when (committed) {
            logger?.LogError("Test plan {TestPlanId} was saved in profile {ProfileId}; a subsequent operation failed with {FailureType}.",
                entityId, writeAdmissions.DatabaseProfileId, exception.GetType().FullName);
            throw new TestPlanCommittedSaveException(entityId, exception);
        }
    }

    private async Task UpsertSearchAsync(ProjectWriteAdmission? admission, SearchDocumentInput input, CancellationToken cancellationToken) {
        if (admission is null) {
            await searchIndexService.UpsertAsync(input, cancellationToken);
            return;
        }
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutation = await SerializableMutationScope.BeginAsync(context,
            ProjectMutationScopeKeys.ForProject(admission.ProjectId), cancellationToken);
        using (coordinatedTransaction.Enter(context)) {
            await writeAdmissions.RequireForMutationAsync(admission, cancellationToken);
            await searchIndexService.UpsertForMutationAsync(input, cancellationToken);
        }
        await mutation.CommitAsync(cancellationToken);
    }

    private static string[] BuildMutationKeys(Guid planId, params Guid?[] projectIds)
        => projectIds.Where(id => id.HasValue && id != Guid.Empty).Select(id => ProjectMutationScopeKeys.ForProject(id!.Value))
            .Append($"test-plan:{planId:D}").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static void MapCase(TestCaseRecord entity, Guid planId, TestCaseEditorModel model)
    {
        entity.TestPlanId = planId;
        entity.Name = model.Name.Trim();
        entity.StoryOrFeature = model.StoryOrFeature?.Trim() ?? string.Empty;
        entity.Status = model.Status;
        entity.Notes = model.Notes?.Trim() ?? string.Empty;
    }

    private static void MapEvidence(TestEvidenceRecord entity, Guid planId, TestEvidenceEditorModel model)
    {
        entity.TestPlanId = planId;
        entity.EvidenceLabel = model.EvidenceLabel.Trim();
        entity.ArtifactPath = model.ArtifactPath.Trim();
        entity.EvidenceKind = string.IsNullOrWhiteSpace(model.EvidenceKind) ? "Screenshot" : model.EvidenceKind.Trim();
        entity.Notes = model.Notes?.Trim() ?? string.Empty;
    }

    private static void MapRun(TestRunRecord entity, Guid planId, TestRunEditorModel model)
    {
        entity.TestPlanId = planId;
        entity.ExecutedAtUtc = model.ExecutedAtUtc;
        entity.Runner = string.IsNullOrWhiteSpace(model.Runner) ? "Playwright" : model.Runner.Trim();
        entity.Result = model.Result;
        entity.Summary = model.Summary?.Trim() ?? string.Empty;
    }

    private static async Task SyncCollectionAsync<TEntity, TModel>(
        DbSet<TEntity> set,
        Guid planId,
        IReadOnlyCollection<TModel> models,
        Action<TEntity, Guid, TModel> map,
        List<Action> committedIdentityUpdates,
        CancellationToken cancellationToken)
        where TEntity : class, ITestPlanChildRecord, new()
        where TModel : class, ITestPlanChildEditor {
        var entities = await set.Where(entity => EF.Property<Guid>(entity, nameof(ITestPlanChildRecord.TestPlanId)) == planId)
            .ToDictionaryAsync(entity => entity.Id, cancellationToken);
        var modelIds = models.Select(model => model.Id).ToHashSet();
        set.RemoveRange(entities.Values.Where(entity => !modelIds.Contains(entity.Id)));
        foreach (var model in models) {
            var entity = model.Id is { } id ? entities.GetValueOrDefault(id) : null;
            if (entity is null) {
                entity = new TEntity();
                set.Add(entity);
            }
            map(entity, planId, model);
            var committedId = entity.Id;
            committedIdentityUpdates.Add(() => model.Id = committedId);
        }
    }
}

internal interface ITestPlanChildRecord {
    Guid Id { get; }
    Guid TestPlanId { get; }
}

internal interface ITestPlanChildEditor {
    Guid? Id { get; set; }
}
