using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Validation;

public enum ValidationType
{
    Stories,
    Layout,
    Architecture,
    Plan,
    Prototype,
    TestCoverage
}

public enum ValidationDecision
{
    Pending,
    Approved,
    Rejected,
    NeedsChanges,
    FollowUpRequired
}

public enum ValidationFindingSeverity
{
    Info,
    Warning,
    Error
}

public sealed class ValidationChecklist
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ValidationType ValidationType { get; set; }

    public string VersionLabel { get; set; } = "v1";

    public string Name { get; set; } = string.Empty;

    public string ItemsJson { get; set; } = "[]";

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ValidationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChecklistId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? ResponsiblePartyId { get; set; }

    public ValidationType ValidationType { get; set; }

    public string ArtifactTitle { get; set; } = string.Empty;

    public string ArtifactRoute { get; set; } = string.Empty;

    public string SourceContent { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public ValidationDecision Decision { get; set; } = ValidationDecision.Pending;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ValidationFinding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ValidationRunId { get; set; }

    public string RuleCode { get; set; } = string.Empty;

    public ValidationFindingSeverity Severity { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public string RecommendedAction { get; set; } = string.Empty;
}

internal sealed class ValidationChecklistConfiguration : IEntityTypeConfiguration<ValidationChecklist>
{
    public void Configure(EntityTypeBuilder<ValidationChecklist> builder)
    {
        builder.ToTable("Validation_Checklists");
        builder.HasKey(checklist => checklist.Id);
        builder.Property(checklist => checklist.VersionLabel).HasMaxLength(40).IsRequired();
        builder.Property(checklist => checklist.Name).HasMaxLength(200).IsRequired();
        builder.Property(checklist => checklist.ItemsJson).HasColumnType("TEXT");
    }
}

internal sealed class ValidationRunConfiguration : IEntityTypeConfiguration<ValidationRun>
{
    public void Configure(EntityTypeBuilder<ValidationRun> builder)
    {
        builder.ToTable("Validation_Runs");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.ArtifactTitle).HasMaxLength(200).IsRequired();
        builder.Property(run => run.ArtifactRoute).HasMaxLength(500).IsRequired();
        builder.Property(run => run.SourceContent).HasColumnType("TEXT");
        builder.Property(run => run.Summary).HasColumnType("TEXT");
        builder.HasIndex(run => run.CreatedAtUtc);
    }
}

internal sealed class ValidationFindingConfiguration : IEntityTypeConfiguration<ValidationFinding>
{
    public void Configure(EntityTypeBuilder<ValidationFinding> builder)
    {
        builder.ToTable("Validation_Findings");
        builder.HasKey(finding => finding.Id);
        builder.Property(finding => finding.RuleCode).HasMaxLength(120).IsRequired();
        builder.Property(finding => finding.Title).HasMaxLength(200).IsRequired();
        builder.Property(finding => finding.Detail).HasColumnType("TEXT");
        builder.Property(finding => finding.RecommendedAction).HasColumnType("TEXT");
    }
}

public sealed record ValidationChecklistSummary(Guid Id, ValidationType ValidationType, string Name, string VersionLabel);

public sealed record ValidationRunSummary(
    Guid Id,
    Guid? ProjectId,
    ValidationType ValidationType,
    string ArtifactTitle,
    ValidationDecision Decision,
    int FindingCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record ValidationFindingModel(
    Guid Id,
    string RuleCode,
    ValidationFindingSeverity Severity,
    string Title,
    string Detail,
    string RecommendedAction);

public sealed class ValidationRunEditorModel
{
    public Guid? Id { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? ResponsiblePartyId { get; set; }

    public ValidationType ValidationType { get; set; } = ValidationType.Architecture;

    public Guid? ChecklistId { get; set; }

    public string ArtifactTitle { get; set; } = string.Empty;

    public string ArtifactRoute { get; set; } = "/projects";

    public string SourceContent { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public ValidationDecision Decision { get; set; } = ValidationDecision.Pending;

    public List<ValidationFindingModel> Findings { get; set; } = [];
}

/* codex-capsule
kind: service
name: ValidationService
summary: Stores rule-first validation runs, seeded checklists, findings, and review decisions across supported artifact types.
owns: validation-runs, checklist-seeding, deterministic-findings
deps: AppDbContext, IClock, IActivityStream, ISearchIndexService
risks: underpowered-rules, duplicate-checklist-seed
tests: unit:ValidationServiceTests, integration:ValidationPersistenceTests
inputs: ValidationRunEditorModel, ValidationDecision
outputs: ValidationRunSummary, ValidationFindingModel
*/
public sealed class ValidationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService)
{
    private static readonly Dictionary<ValidationType, string[]> DefaultChecklistItems = new()
    {
        [ValidationType.Stories] = ["Actor is clear", "Acceptance criteria exist", "Dependencies are visible"],
        [ValidationType.Layout] = ["ASCII or layout structure is present", "Primary actions are visible", "Navigation is coherent"],
        [ValidationType.Architecture] = ["Module boundaries are named", "Dependencies are described", "Persistence/storage concerns are covered"],
        [ValidationType.Plan] = ["Milestones or phases exist", "Risks are mentioned", "Acceptance criteria are stated"],
        [ValidationType.Prototype] = ["Interaction flow is described", "Primary states are called out", "Open questions are listed"],
        [ValidationType.TestCoverage] = ["Critical flows are covered", "Negative paths are covered", "Evidence expectations are stated"]
    };

    public async Task<IReadOnlyList<ValidationChecklistSummary>> ListChecklistsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultChecklistsAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ValidationChecklist>()
            .OrderBy(checklist => checklist.ValidationType)
            .Select(checklist => new ValidationChecklistSummary(checklist.Id, checklist.ValidationType, checklist.Name, checklist.VersionLabel))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ValidationRunSummary>> ListRunsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var counts = await dbContext.Set<ValidationFinding>()
            .GroupBy(finding => finding.ValidationRunId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);

        var runs = await dbContext.Set<ValidationRun>().ToListAsync(cancellationToken);

        return runs
            .OrderByDescending(run => run.UpdatedAtUtc)
            .Select(run => new ValidationRunSummary(
            run.Id,
            run.ProjectId,
            run.ValidationType,
            run.ArtifactTitle,
            run.Decision,
            counts.GetValueOrDefault(run.Id),
            run.UpdatedAtUtc)).ToList();
    }

    public async Task<ValidationRunEditorModel> GetRunAsync(Guid? id, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultChecklistsAsync(cancellationToken);
        if (!id.HasValue)
        {
            var checklist = (await ListChecklistsAsync(cancellationToken)).First(item => item.ValidationType == ValidationType.Architecture);
            return new ValidationRunEditorModel { ChecklistId = checklist.Id };
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ValidationRun>().FirstOrDefaultAsync(item => item.Id == id.Value, cancellationToken);
        if (run is null)
        {
            return new ValidationRunEditorModel();
        }

        var findings = await dbContext.Set<ValidationFinding>()
            .Where(item => item.ValidationRunId == run.Id)
            .OrderByDescending(item => item.Severity)
            .Select(item => new ValidationFindingModel(item.Id, item.RuleCode, item.Severity, item.Title, item.Detail, item.RecommendedAction))
            .ToListAsync(cancellationToken);

        return new ValidationRunEditorModel
        {
            Id = run.Id,
            ProjectId = run.ProjectId,
            ResponsiblePartyId = run.ResponsiblePartyId,
            ValidationType = run.ValidationType,
            ChecklistId = run.ChecklistId,
            ArtifactTitle = run.ArtifactTitle,
            ArtifactRoute = run.ArtifactRoute,
            SourceContent = run.SourceContent,
            Summary = run.Summary,
            Decision = run.Decision,
            Findings = findings
        };
    }

    public async Task<Result<Guid>> RunAsync(ValidationRunEditorModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.ArtifactTitle))
        {
            return Result<Guid>.Failure(Error.Validation("Artifact title is required."));
        }

        if (string.IsNullOrWhiteSpace(model.SourceContent))
        {
            return Result<Guid>.Failure(Error.Validation("Source content is required before running validation."));
        }

        await EnsureDefaultChecklistsAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var checklistId = model.ChecklistId ?? await dbContext.Set<ValidationChecklist>()
            .Where(item => item.ValidationType == model.ValidationType)
            .Select(item => item.Id)
            .FirstAsync(cancellationToken);

        var entity = model.Id.HasValue
            ? await dbContext.Set<ValidationRun>().FirstOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new ValidationRun
            {
                CreatedAtUtc = clock.GetUtcNow()
            };
            await dbContext.Set<ValidationRun>().AddAsync(entity, cancellationToken);
        }

        entity.ChecklistId = checklistId;
        entity.ProjectId = model.ProjectId;
        entity.ResponsiblePartyId = model.ResponsiblePartyId;
        entity.ValidationType = model.ValidationType;
        entity.ArtifactTitle = model.ArtifactTitle.Trim();
        entity.ArtifactRoute = string.IsNullOrWhiteSpace(model.ArtifactRoute) ? "/projects" : model.ArtifactRoute.Trim();
        entity.SourceContent = model.SourceContent;

        var findings = BuildFindings(model.ValidationType, model.SourceContent);
        entity.Summary = BuildSummary(findings);
        entity.Decision = findings.Any(finding => finding.Severity == ValidationFindingSeverity.Error)
            ? ValidationDecision.NeedsChanges
            : ValidationDecision.Pending;
        entity.UpdatedAtUtc = clock.GetUtcNow();

        var existingFindings = await dbContext.Set<ValidationFinding>()
            .Where(item => item.ValidationRunId == entity.Id)
            .ToListAsync(cancellationToken);
        dbContext.RemoveRange(existingFindings);

        foreach (var finding in findings)
        {
            await dbContext.Set<ValidationFinding>().AddAsync(new ValidationFinding
            {
                ValidationRunId = entity.Id,
                RuleCode = finding.RuleCode,
                Severity = finding.Severity,
                Title = finding.Title,
                Detail = finding.Detail,
                RecommendedAction = finding.RecommendedAction
            }, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await searchIndexService.UpsertAsync(new SearchDocumentInput(
            "validation",
            entity.Id.ToString(),
            "Validation",
            entity.ArtifactTitle,
            entity.Summary,
            entity.SourceContent,
            $"/validation?runId={entity.Id}",
            entity.ProjectId), cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "validation",
            model.Id.HasValue ? "rerun" : "run",
            $"{(model.Id.HasValue ? "Re-ran" : "Ran")} validation",
            $"{entity.ValidationType} · {entity.ArtifactTitle}",
            ProjectId: entity.ProjectId,
            ArtifactKind: "validation-run",
            ArtifactId: entity.Id,
            Route: $"/validation?runId={entity.Id}"), cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task SetDecisionAsync(Guid runId, ValidationDecision decision, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ValidationRun>().FirstOrDefaultAsync(item => item.Id == runId, cancellationToken);
        if (run is null)
        {
            return;
        }

        run.Decision = decision;
        run.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "validation",
            "decision",
            $"Validation marked as {decision}",
            run.ArtifactTitle,
            ProjectId: run.ProjectId,
            ArtifactKind: "validation-run",
            ArtifactId: run.Id,
            Route: $"/validation?runId={run.Id}"), cancellationToken);
    }

    private async Task EnsureDefaultChecklistsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingTypes = await dbContext.Set<ValidationChecklist>()
            .Select(item => item.ValidationType)
            .ToListAsync(cancellationToken);

        foreach (var (validationType, items) in DefaultChecklistItems)
        {
            if (existingTypes.Contains(validationType))
            {
                continue;
            }

            await dbContext.Set<ValidationChecklist>().AddAsync(new ValidationChecklist
            {
                ValidationType = validationType,
                Name = $"{validationType} review checklist",
                VersionLabel = "v1",
                ItemsJson = System.Text.Json.JsonSerializer.Serialize(items),
                CreatedAtUtc = clock.GetUtcNow()
            }, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static List<ValidationFindingModel> BuildFindings(ValidationType validationType, string sourceContent)
    {
        var normalized = sourceContent.Trim();
        var findings = new List<ValidationFindingModel>();

        if (normalized.Length < 120)
        {
            findings.Add(new ValidationFindingModel(Guid.NewGuid(), "thin-content", ValidationFindingSeverity.Warning, "Content is brief", "The artifact is short and may be missing required detail.", "Expand the artifact with its main decisions, scope, and constraints."));
        }

        switch (validationType)
        {
            case ValidationType.Stories:
                if (!normalized.Contains("acceptance", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new ValidationFindingModel(Guid.NewGuid(), "missing-acceptance", ValidationFindingSeverity.Error, "Acceptance criteria are missing", "The story content does not mention acceptance criteria.", "Add explicit acceptance criteria or completion signals."));
                }
                break;
            case ValidationType.Layout:
                if (!(normalized.Contains('+') || normalized.Contains('|') || normalized.Contains("layout", StringComparison.OrdinalIgnoreCase)))
                {
                    findings.Add(new ValidationFindingModel(Guid.NewGuid(), "missing-layout-structure", ValidationFindingSeverity.Warning, "Layout structure is unclear", "The content does not show a recognizable layout structure.", "Include ASCII layout blocks or a clearer structural description."));
                }
                break;
            case ValidationType.Architecture:
                if (!normalized.Contains("module", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new ValidationFindingModel(Guid.NewGuid(), "missing-modules", ValidationFindingSeverity.Error, "Module boundaries are not explicit", "Architecture content should call out module boundaries.", "Name the modules and describe their responsibilities."));
                }
                if (!normalized.Contains("dependency", StringComparison.OrdinalIgnoreCase) &&
                    !normalized.Contains("dependencies", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new ValidationFindingModel(Guid.NewGuid(), "missing-dependencies", ValidationFindingSeverity.Warning, "Dependencies are not described", "No dependency flow or ownership rules were detected.", "Describe allowed dependencies and ownership boundaries."));
                }
                break;
            case ValidationType.Plan:
                if (!normalized.Contains("milestone", StringComparison.OrdinalIgnoreCase) &&
                    !normalized.Contains("phase", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new ValidationFindingModel(Guid.NewGuid(), "missing-milestones", ValidationFindingSeverity.Error, "Milestones or phases are missing", "The plan does not mention phases or milestones.", "Break the work into phases or milestones with end conditions."));
                }
                break;
            case ValidationType.Prototype:
                if (!normalized.Contains("flow", StringComparison.OrdinalIgnoreCase) &&
                    !normalized.Contains("screen", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new ValidationFindingModel(Guid.NewGuid(), "missing-flow", ValidationFindingSeverity.Warning, "Prototype flow is unclear", "The prototype description does not mention a flow or primary screen state.", "Describe the user flow and the main screens or states."));
                }
                break;
            case ValidationType.TestCoverage:
                if (!normalized.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                    !normalized.Contains("coverage", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new ValidationFindingModel(Guid.NewGuid(), "missing-test-coverage", ValidationFindingSeverity.Error, "Test coverage plan is incomplete", "The artifact does not clearly describe test coverage.", "List critical workflows, negative paths, and evidence expectations."));
                }
                break;
        }

        if (findings.Count == 0)
        {
            findings.Add(new ValidationFindingModel(Guid.NewGuid(), "baseline-pass", ValidationFindingSeverity.Info, "Deterministic checks passed", "The baseline deterministic rules did not find a blocking issue.", "Proceed with human review and optional deeper validation."));
        }

        return findings;
    }

    private static string BuildSummary(IReadOnlyCollection<ValidationFindingModel> findings)
    {
        var errors = findings.Count(finding => finding.Severity == ValidationFindingSeverity.Error);
        var warnings = findings.Count(finding => finding.Severity == ValidationFindingSeverity.Warning);
        return $"{errors} error(s), {warnings} warning(s), {findings.Count} total finding(s).";
    }
}


