using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Projects;

public enum ProjectStatus
{
    Draft,
    Active,
    OnHold,
    Completed,
    Archived
}

public enum ProjectPhaseStatus
{
    Planned,
    Active,
    Blocked,
    Completed
}

public enum ProjectOptionCategory
{
    Language,
    Database,
    Ui,
    ExternalApi,
    Storage,
    Deployment,
    Testing,
    Other
}

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Objective { get; set; } = string.Empty;

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    public string CurrentPhase { get; set; } = string.Empty;

    public DateTime? TargetDateUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ProjectPhase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Goal { get; set; } = string.Empty;

    public ProjectPhaseStatus Status { get; set; } = ProjectPhaseStatus.Planned;

    public int OrderIndex { get; set; }

    public DateTime? StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }
}

public sealed class ProjectOptionSelection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public ProjectOptionCategory Category { get; set; }

    public string OptionName { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects_Projects");
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Name).HasMaxLength(200).IsRequired();
        builder.Property(project => project.Slug).HasMaxLength(200).IsRequired();
        builder.Property(project => project.Description).HasColumnType("TEXT");
        builder.Property(project => project.Objective).HasColumnType("TEXT");
        builder.Property(project => project.CurrentPhase).HasMaxLength(120);
    }
}

internal sealed class ProjectPhaseConfiguration : IEntityTypeConfiguration<ProjectPhase>
{
    public void Configure(EntityTypeBuilder<ProjectPhase> builder)
    {
        builder.ToTable("Projects_ProjectPhases");
        builder.HasKey(phase => phase.Id);
        builder.Property(phase => phase.Name).HasMaxLength(160).IsRequired();
        builder.Property(phase => phase.Goal).HasColumnType("TEXT");
        builder.HasIndex(phase => new { phase.ProjectId, phase.OrderIndex });
    }
}

internal sealed class ProjectOptionSelectionConfiguration : IEntityTypeConfiguration<ProjectOptionSelection>
{
    public void Configure(EntityTypeBuilder<ProjectOptionSelection> builder)
    {
        builder.ToTable("Projects_ProjectOptionSelections");
        builder.HasKey(option => option.Id);
        builder.Property(option => option.OptionName).HasMaxLength(200).IsRequired();
        builder.Property(option => option.Notes).HasColumnType("TEXT");
        builder.HasIndex(option => new { option.ProjectId, option.Category });
    }
}

public sealed record ProjectSummary(
    Guid Id,
    string Name,
    ProjectStatus Status,
    string CurrentPhase,
    int PhaseCount,
    DateTimeOffset UpdatedAtUtc);

public sealed class ProjectPhaseEditorModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Goal { get; set; } = string.Empty;

    public ProjectPhaseStatus Status { get; set; } = ProjectPhaseStatus.Planned;

    public DateTime? StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }
}

public sealed class ProjectOptionEditorModel
{
    public Guid? Id { get; set; }

    public ProjectOptionCategory Category { get; set; }

    public string OptionName { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}

public sealed class ProjectEditorModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Objective { get; set; } = string.Empty;

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    public string CurrentPhase { get; set; } = string.Empty;

    public DateTime? TargetDateUtc { get; set; }

    public List<ProjectPhaseEditorModel> Phases { get; set; } = [];

    public List<ProjectOptionEditorModel> Options { get; set; } = [];
}

public sealed class ProjectsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService)
{
    private static readonly ProjectOptionCategory[] DefaultCategories =
    [
        ProjectOptionCategory.Language,
        ProjectOptionCategory.Database,
        ProjectOptionCategory.Ui,
        ProjectOptionCategory.ExternalApi,
        ProjectOptionCategory.Storage,
        ProjectOptionCategory.Deployment,
        ProjectOptionCategory.Testing
    ];

    public async Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var projects = await dbContext.Set<Project>().ToListAsync(cancellationToken);

        var phases = await dbContext.Set<ProjectPhase>()
            .GroupBy(phase => phase.ProjectId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);

        return projects
            .OrderByDescending(project => project.UpdatedAtUtc)
            .Select(project => new ProjectSummary(
                project.Id,
                project.Name,
                project.Status,
                project.CurrentPhase,
                phases.GetValueOrDefault(project.Id),
                project.UpdatedAtUtc))
            .ToList();
    }

    public async Task<ProjectEditorModel> GetAsync(Guid? id, CancellationToken cancellationToken = default)
    {
        if (!id.HasValue)
        {
            return CreateNew();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var project = await dbContext.Set<Project>().FirstOrDefaultAsync(item => item.Id == id.Value, cancellationToken);
        if (project is null)
        {
            return CreateNew();
        }

        var phases = await dbContext.Set<ProjectPhase>()
            .Where(item => item.ProjectId == project.Id)
            .OrderBy(item => item.OrderIndex)
            .Select(item => new ProjectPhaseEditorModel
            {
                Id = item.Id,
                Name = item.Name,
                Goal = item.Goal,
                Status = item.Status,
                StartDateUtc = item.StartDateUtc,
                EndDateUtc = item.EndDateUtc
            })
            .ToListAsync(cancellationToken);

        var options = await dbContext.Set<ProjectOptionSelection>()
            .Where(item => item.ProjectId == project.Id)
            .Select(item => new ProjectOptionEditorModel
            {
                Id = item.Id,
                Category = item.Category,
                OptionName = item.OptionName,
                Notes = item.Notes
            })
            .ToListAsync(cancellationToken);

        EnsureDefaultCategories(options);

        return new ProjectEditorModel
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Objective = project.Objective,
            Status = project.Status,
            CurrentPhase = project.CurrentPhase,
            TargetDateUtc = project.TargetDateUtc,
            Phases = phases,
            Options = options.OrderBy(option => option.Category).ToList()
        };
    }

    public async Task<Result<Guid>> SaveAsync(ProjectEditorModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Result<Guid>.Failure(Error.Validation("Project name is required."));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = model.Id.HasValue
            ? await dbContext.Set<Project>().FirstOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new Project
            {
                CreatedAtUtc = clock.GetUtcNow()
            };

            await dbContext.Set<Project>().AddAsync(entity, cancellationToken);
        }

        entity.Name = model.Name.Trim();
        entity.Slug = BuildSlug(model.Name);
        entity.Description = model.Description?.Trim() ?? string.Empty;
        entity.Objective = model.Objective?.Trim() ?? string.Empty;
        entity.Status = model.Status;
        entity.CurrentPhase = model.CurrentPhase?.Trim() ?? string.Empty;
        entity.TargetDateUtc = model.TargetDateUtc;
        entity.UpdatedAtUtc = clock.GetUtcNow();

        var existingPhases = await dbContext.Set<ProjectPhase>()
            .Where(item => item.ProjectId == entity.Id)
            .ToListAsync(cancellationToken);

        dbContext.RemoveRange(existingPhases.Where(phase => model.Phases.All(item => item.Id != phase.Id)));
        for (var index = 0; index < model.Phases.Count; index++)
        {
            var phaseModel = model.Phases[index];
            var phase = phaseModel.Id.HasValue
                ? existingPhases.FirstOrDefault(item => item.Id == phaseModel.Id.Value)
                : null;

            if (phase is null)
            {
                phase = new ProjectPhase
                {
                    ProjectId = entity.Id
                };

                await dbContext.Set<ProjectPhase>().AddAsync(phase, cancellationToken);
            }

            phase.Name = phaseModel.Name.Trim();
            phase.Goal = phaseModel.Goal?.Trim() ?? string.Empty;
            phase.Status = phaseModel.Status;
            phase.OrderIndex = index;
            phase.StartDateUtc = phaseModel.StartDateUtc;
            phase.EndDateUtc = phaseModel.EndDateUtc;
        }

        var existingOptions = await dbContext.Set<ProjectOptionSelection>()
            .Where(item => item.ProjectId == entity.Id)
            .ToListAsync(cancellationToken);

        dbContext.RemoveRange(existingOptions.Where(option => model.Options.All(item => item.Id != option.Id)));
        foreach (var optionModel in model.Options)
        {
            if (string.IsNullOrWhiteSpace(optionModel.OptionName) && string.IsNullOrWhiteSpace(optionModel.Notes))
            {
                continue;
            }

            var option = optionModel.Id.HasValue
                ? existingOptions.FirstOrDefault(item => item.Id == optionModel.Id.Value)
                : null;

            if (option is null)
            {
                option = new ProjectOptionSelection
                {
                    ProjectId = entity.Id
                };

                await dbContext.Set<ProjectOptionSelection>().AddAsync(option, cancellationToken);
            }

            option.Category = optionModel.Category;
            option.OptionName = optionModel.OptionName.Trim();
            option.Notes = optionModel.Notes?.Trim() ?? string.Empty;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await searchIndexService.UpsertAsync(new SearchDocumentInput(
            "project",
            entity.Id.ToString(),
            "Projects",
            entity.Name,
            entity.Description,
            $"{entity.Objective}\nCurrent phase: {entity.CurrentPhase}\nOptions: {string.Join(", ", model.Options.Where(option => !string.IsNullOrWhiteSpace(option.OptionName)).Select(option => $"{option.Category}:{option.OptionName}"))}",
            $"/projects?projectId={entity.Id}",
            entity.Id), cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "projects",
            model.Id.HasValue ? "update" : "create",
            $"{(model.Id.HasValue ? "Updated" : "Created")} project",
            entity.Name,
            ProjectId: entity.Id,
            ArtifactKind: "project",
            ArtifactId: entity.Id,
            Route: $"/projects?projectId={entity.Id}"), cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var project = await dbContext.Set<Project>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (project is null)
        {
            return;
        }

        var phases = await dbContext.Set<ProjectPhase>().Where(item => item.ProjectId == id).ToListAsync(cancellationToken);
        var options = await dbContext.Set<ProjectOptionSelection>().Where(item => item.ProjectId == id).ToListAsync(cancellationToken);
        dbContext.RemoveRange(phases);
        dbContext.RemoveRange(options);
        dbContext.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        await searchIndexService.DeleteAsync("project", id.ToString(), cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "projects",
            "delete",
            "Deleted project",
            project.Name,
            ProjectId: id,
            ArtifactKind: "project",
            ArtifactId: id,
            Route: "/projects"), cancellationToken);
    }

    private static ProjectEditorModel CreateNew()
    {
        var model = new ProjectEditorModel
        {
            Status = ProjectStatus.Draft
        };

        EnsureDefaultCategories(model.Options);
        return model;
    }

    private static void EnsureDefaultCategories(ICollection<ProjectOptionEditorModel> options)
    {
        foreach (var category in DefaultCategories)
        {
            if (options.Any(option => option.Category == category))
            {
                continue;
            }

            options.Add(new ProjectOptionEditorModel
            {
                Category = category
            });
        }
    }

    private static string BuildSlug(string input)
    {
        var slug = input.Trim().ToLowerInvariant();
        foreach (var character in Path.GetInvalidFileNameChars())
        {
            slug = slug.Replace(character.ToString(), string.Empty, StringComparison.Ordinal);
        }

        slug = slug.Replace(' ', '-');
        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N") : slug;
    }
}


