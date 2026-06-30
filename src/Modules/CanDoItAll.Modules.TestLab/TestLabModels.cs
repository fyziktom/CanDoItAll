using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

    public Guid? ResponsiblePartyId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string CoverageGoal { get; set; } = string.Empty;

    public string PlaywrightSpecPath { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class TestCaseRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TestPlanId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string StoryOrFeature { get; set; } = string.Empty;

    public TestCaseStatus Status { get; set; } = TestCaseStatus.Planned;

    public string Notes { get; set; } = string.Empty;
}

public sealed class TestEvidenceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TestPlanId { get; set; }

    public string EvidenceLabel { get; set; } = string.Empty;

    public string ArtifactPath { get; set; } = string.Empty;

    public string EvidenceKind { get; set; } = "Screenshot";

    public string Notes { get; set; } = string.Empty;
}

public sealed class TestRunRecord
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
    DateTimeOffset UpdatedAtUtc);

public sealed class TestCaseEditorModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string StoryOrFeature { get; set; } = string.Empty;

    public TestCaseStatus Status { get; set; } = TestCaseStatus.Planned;

    public string Notes { get; set; } = string.Empty;
}

public sealed class TestEvidenceEditorModel
{
    public Guid? Id { get; set; }

    public string EvidenceLabel { get; set; } = string.Empty;

    public string ArtifactPath { get; set; } = string.Empty;

    public string EvidenceKind { get; set; } = "Screenshot";

    public string Notes { get; set; } = string.Empty;
}

public sealed class TestRunEditorModel
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
deps: AppDbContext, IClock, IActivityStream, ISearchIndexService
risks: evidence-path-drift, noisy-test-plan-updates
tests: unit:TestLabServiceTests, integration:TestLabPersistenceTests
inputs: TestPlanEditorModel
outputs: TestPlanSummary, test plan detail
*/
public sealed class TestLabService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService)
{
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
            plan.UpdatedAtUtc)).ToList();
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

    public async Task<Result<Guid>> SaveAsync(TestPlanEditorModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            return Result<Guid>.Failure(Error.Validation("Test plan title is required."));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = model.Id.HasValue
            ? await dbContext.Set<TestPlan>().FirstOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new TestPlan
            {
                CreatedAtUtc = clock.GetUtcNow()
            };
            await dbContext.Set<TestPlan>().AddAsync(entity, cancellationToken);
        }

        entity.ProjectId = model.ProjectId;
        entity.ResponsiblePartyId = model.ResponsiblePartyId;
        entity.Title = model.Title.Trim();
        entity.Phase = model.Phase?.Trim() ?? string.Empty;
        entity.CoverageGoal = model.CoverageGoal?.Trim() ?? string.Empty;
        entity.PlaywrightSpecPath = model.PlaywrightSpecPath?.Trim() ?? string.Empty;
        entity.UpdatedAtUtc = clock.GetUtcNow();

        await SyncCollectionAsync(dbContext.Set<TestCaseRecord>(), entity.Id, model.Cases, MapCase, testCase => testCase.TestPlanId, cancellationToken);
        await SyncCollectionAsync(dbContext.Set<TestEvidenceRecord>(), entity.Id, model.Evidence, MapEvidence, evidence => evidence.TestPlanId, cancellationToken);
        await SyncCollectionAsync(dbContext.Set<TestRunRecord>(), entity.Id, model.Runs, MapRun, run => run.TestPlanId, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await searchIndexService.UpsertAsync(new SearchDocumentInput(
            "test-plan",
            entity.Id.ToString(),
            "Test Lab",
            entity.Title,
            entity.Phase,
            $"{entity.CoverageGoal}\nPlaywright: {entity.PlaywrightSpecPath}",
            $"/test-lab?planId={entity.Id}",
            entity.ProjectId), cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "test-lab",
            model.Id.HasValue ? "update-plan" : "create-plan",
            $"{(model.Id.HasValue ? "Updated" : "Created")} test plan",
            entity.Title,
            ProjectId: entity.ProjectId,
            ArtifactKind: "test-plan",
            ArtifactId: entity.Id,
            Route: $"/test-lab?planId={entity.Id}"), cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

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
        Func<TEntity, Guid> keySelector,
        CancellationToken cancellationToken)
        where TEntity : class, new()
        where TModel : class
    {
        var entities = (await set.ToListAsync(cancellationToken))
            .Where(entity => keySelector(entity) == planId)
            .ToList();
        var modelIds = models
            .Select(model => (Guid?)model.GetType().GetProperty("Id")?.GetValue(model))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        set.RemoveRange(entities.Where(entity =>
        {
            var entityId = (Guid?)entity.GetType().GetProperty("Id")?.GetValue(entity);
            return entityId.HasValue && !modelIds.Contains(entityId.Value);
        }));

        foreach (var model in models)
        {
            var modelId = (Guid?)model.GetType().GetProperty("Id")?.GetValue(model);
            var entity = modelId.HasValue
                ? entities.FirstOrDefault(item => (Guid?)item.GetType().GetProperty("Id")?.GetValue(item) == modelId.Value)
                : null;

            if (entity is null)
            {
                entity = new TEntity();
                await set.AddAsync(entity, cancellationToken);
            }

            map(entity, planId, model);
        }
    }
}


