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

public sealed class ProjectHierarchyLink
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ParentProjectId { get; set; }

    public Guid ChildProjectId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
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

internal sealed class ProjectHierarchyLinkConfiguration : IEntityTypeConfiguration<ProjectHierarchyLink>
{
    public void Configure(EntityTypeBuilder<ProjectHierarchyLink> builder)
    {
        builder.ToTable("Projects_ProjectHierarchyLinks");
        builder.HasKey(link => link.Id);
        builder.HasIndex(link => new { link.ParentProjectId, link.ChildProjectId }).IsUnique();
        builder.HasIndex(link => link.ParentProjectId);
        builder.HasIndex(link => link.ChildProjectId);
    }
}

public sealed record ProjectSummary(
    Guid Id,
    string Name,
    ProjectStatus Status,
    string CurrentPhase,
    int PhaseCount,
    int ParentCount,
    int ChildCount,
    DateTimeOffset UpdatedAtUtc,
    string PrimaryCustomerName = "",
    string PrimaryDeliveryUnitName = "",
    string PrimaryOwnerName = "",
    IReadOnlyList<ProjectPortfolioPartyItem>? RelatedParties = null,
    string RelatedPartySearchText = "");

public sealed record ProjectAccessListItem(
    Guid Id,
    string Name);

public sealed record ProjectHierarchyLinkSummary(
    Guid ParentProjectId,
    Guid ChildProjectId,
    DateTimeOffset CreatedAtUtc);

public sealed record ProjectHierarchySnapshot(
    Guid ProjectId,
    IReadOnlyList<ProjectSummary> ParentProjects,
    IReadOnlyList<ProjectSummary> ChildProjects);

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
    ISearchIndexService searchIndexService,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge)
{
    private sealed record ProjectHierarchyMetrics(
        IReadOnlyDictionary<Guid, int> ParentCounts,
        IReadOnlyDictionary<Guid, int> ChildCounts,
        IReadOnlyList<ProjectHierarchyLinkSummary> Links);

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
        var hierarchyMetrics = await LoadHierarchyMetricsAsync(dbContext, cancellationToken);
        var phaseCounts = await LoadPhaseCountsAsync(dbContext, cancellationToken);
        var portfolioContexts = await projectPartyIntegrationBridge.GetPortfolioContextsAsync(
            projects.Select(project => project.Id).ToList(),
            cancellationToken);

        return projects
            .OrderByDescending(project => project.UpdatedAtUtc)
            .Select(project => MapProjectSummary(project, phaseCounts, hierarchyMetrics, portfolioContexts.GetValueOrDefault(project.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<ProjectAccessListItem>> ListAccessListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Set<Project>()
            .AsNoTracking()
            .OrderBy(project => project.Name)
            .Select(project => new ProjectAccessListItem(project.Id, project.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectHierarchyLinkSummary>> ListHierarchyLinksAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var hierarchyMetrics = await LoadHierarchyMetricsAsync(dbContext, cancellationToken);
        return hierarchyMetrics.Links;
    }

    public async Task<ProjectHierarchySnapshot> GetHierarchyAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var projects = await dbContext.Set<Project>().ToListAsync(cancellationToken);
        if (projects.All(project => project.Id != projectId))
        {
            return new ProjectHierarchySnapshot(projectId, [], []);
        }

        var hierarchyMetrics = await LoadHierarchyMetricsAsync(dbContext, cancellationToken);
        var phaseCounts = await LoadPhaseCountsAsync(dbContext, cancellationToken);
        var portfolioContexts = await projectPartyIntegrationBridge.GetPortfolioContextsAsync(
            projects.Select(project => project.Id).ToList(),
            cancellationToken);
        var summaryMap = projects.ToDictionary(
            project => project.Id,
            project => MapProjectSummary(project, phaseCounts, hierarchyMetrics, portfolioContexts.GetValueOrDefault(project.Id)));

        var parents = hierarchyMetrics.Links
            .Where(link => link.ChildProjectId == projectId)
            .Select(link => summaryMap.GetValueOrDefault(link.ParentProjectId))
            .OfType<ProjectSummary>()
            .OrderByDescending(project => project.UpdatedAtUtc)
            .ToList();
        var children = hierarchyMetrics.Links
            .Where(link => link.ParentProjectId == projectId)
            .Select(link => summaryMap.GetValueOrDefault(link.ChildProjectId))
            .OfType<ProjectSummary>()
            .OrderByDescending(project => project.UpdatedAtUtc)
            .ToList();

        return new ProjectHierarchySnapshot(projectId, parents, children);
    }

    public async Task<Result> AddSubprojectAsync(
        Guid parentProjectId,
        Guid childProjectId,
        CancellationToken cancellationToken = default)
    {
        if (parentProjectId == childProjectId)
        {
            return Result.Failure(Error.Validation("A project cannot be attached as its own subproject."));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var projects = await dbContext.Set<Project>()
            .Where(project => project.Id == parentProjectId || project.Id == childProjectId)
            .ToDictionaryAsync(project => project.Id, cancellationToken);
        if (!projects.TryGetValue(parentProjectId, out var parentProject))
        {
            return Result.Failure(Error.Validation("The selected parent project could not be found."));
        }

        if (!projects.TryGetValue(childProjectId, out var childProject))
        {
            return Result.Failure(Error.Validation("The selected subproject could not be found."));
        }

        var existingLink = await dbContext.Set<ProjectHierarchyLink>()
            .FirstOrDefaultAsync(
                link => link.ParentProjectId == parentProjectId && link.ChildProjectId == childProjectId,
                cancellationToken);
        if (existingLink is not null)
        {
            return Result.Success();
        }

        var cycleError = await ValidateHierarchyConnectionAsync(dbContext, parentProjectId, childProjectId, cancellationToken);
        if (cycleError is not null)
        {
            return Result.Failure(cycleError);
        }

        await dbContext.Set<ProjectHierarchyLink>().AddAsync(
            new ProjectHierarchyLink
            {
                ParentProjectId = parentProjectId,
                ChildProjectId = childProjectId,
                CreatedAtUtc = clock.GetUtcNow()
            },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "projects",
                "attach-subproject",
                "Attached subproject",
                $"{childProject.Name} is now under {parentProject.Name}.",
                ProjectId: childProjectId,
                ArtifactKind: "project",
                ArtifactId: childProjectId,
                Route: $"/projects?projectId={childProjectId}"),
            cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RemoveSubprojectAsync(
        Guid parentProjectId,
        Guid childProjectId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var link = await dbContext.Set<ProjectHierarchyLink>()
            .FirstOrDefaultAsync(
                item => item.ParentProjectId == parentProjectId && item.ChildProjectId == childProjectId,
                cancellationToken);
        if (link is null)
        {
            return Result.Failure(Error.Validation("The selected project relationship does not exist."));
        }

        var projects = await dbContext.Set<Project>()
            .Where(project => project.Id == parentProjectId || project.Id == childProjectId)
            .ToDictionaryAsync(project => project.Id, cancellationToken);
        if (!projects.TryGetValue(parentProjectId, out var parentProject) ||
            !projects.TryGetValue(childProjectId, out var childProject))
        {
            return Result.Failure(Error.Validation("The selected project relationship is no longer valid."));
        }

        dbContext.Remove(link);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "projects",
                "detach-subproject",
                "Detached subproject",
                $"{childProject.Name} was detached from {parentProject.Name}.",
                ProjectId: childProjectId,
                ArtifactKind: "project",
                ArtifactId: childProjectId,
                Route: $"/projects?projectId={childProjectId}"),
            cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ReconnectSubprojectAsync(
        Guid childProjectId,
        Guid currentParentProjectId,
        Guid newParentProjectId,
        CancellationToken cancellationToken = default)
    {
        if (currentParentProjectId == newParentProjectId)
        {
            return Result.Failure(Error.Validation("Choose a different project before reconnecting the subproject."));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var currentLink = await dbContext.Set<ProjectHierarchyLink>()
            .FirstOrDefaultAsync(
                link => link.ParentProjectId == currentParentProjectId && link.ChildProjectId == childProjectId,
                cancellationToken);
        if (currentLink is null)
        {
            return Result.Failure(Error.Validation("The selected source parent is not connected to this subproject."));
        }

        var projects = await dbContext.Set<Project>()
            .Where(project =>
                project.Id == childProjectId ||
                project.Id == currentParentProjectId ||
                project.Id == newParentProjectId)
            .ToDictionaryAsync(project => project.Id, cancellationToken);
        if (!projects.TryGetValue(childProjectId, out var childProject))
        {
            return Result.Failure(Error.Validation("The selected subproject could not be found."));
        }

        if (!projects.TryGetValue(currentParentProjectId, out var currentParentProject))
        {
            return Result.Failure(Error.Validation("The selected source parent project could not be found."));
        }

        if (!projects.TryGetValue(newParentProjectId, out var newParentProject))
        {
            return Result.Failure(Error.Validation("The selected target parent project could not be found."));
        }

        var targetLink = await dbContext.Set<ProjectHierarchyLink>()
            .FirstOrDefaultAsync(
                link => link.ParentProjectId == newParentProjectId && link.ChildProjectId == childProjectId,
                cancellationToken);
        if (targetLink is null)
        {
            var cycleError = await ValidateHierarchyConnectionAsync(dbContext, newParentProjectId, childProjectId, cancellationToken);
            if (cycleError is not null)
            {
                return Result.Failure(cycleError);
            }

            await dbContext.Set<ProjectHierarchyLink>().AddAsync(
                new ProjectHierarchyLink
                {
                    ParentProjectId = newParentProjectId,
                    ChildProjectId = childProjectId,
                    CreatedAtUtc = clock.GetUtcNow()
                },
                cancellationToken);
        }

        dbContext.Remove(currentLink);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "projects",
                "reconnect-subproject",
                "Reconnected subproject",
                $"{childProject.Name} moved from {currentParentProject.Name} to {newParentProject.Name}.",
                ProjectId: childProjectId,
                ArtifactKind: "project",
                ArtifactId: childProjectId,
                Route: $"/projects?projectId={childProjectId}"),
            cancellationToken);
        return Result.Success();
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
        entity.Slug = FileSafeSlugBuilder.Build(model.Name);
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
        var hierarchyLinks = await dbContext.Set<ProjectHierarchyLink>()
            .Where(item => item.ParentProjectId == id || item.ChildProjectId == id)
            .ToListAsync(cancellationToken);
        dbContext.RemoveRange(phases);
        dbContext.RemoveRange(options);
        dbContext.RemoveRange(hierarchyLinks);
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

    private static ProjectSummary MapProjectSummary(
        Project project,
        IReadOnlyDictionary<Guid, int> phaseCounts,
        ProjectHierarchyMetrics hierarchyMetrics,
        ProjectPortfolioPartyContext? portfolioContext)
    => new(
        project.Id,
        project.Name,
        project.Status,
        project.CurrentPhase,
        phaseCounts.GetValueOrDefault(project.Id),
        hierarchyMetrics.ParentCounts.GetValueOrDefault(project.Id),
        hierarchyMetrics.ChildCounts.GetValueOrDefault(project.Id),
        project.UpdatedAtUtc,
        portfolioContext?.PrimaryCustomerName ?? string.Empty,
        portfolioContext?.PrimaryDeliveryUnitName ?? string.Empty,
        portfolioContext?.PrimaryOwnerName ?? string.Empty,
        portfolioContext?.Items ?? [],
        portfolioContext?.SearchText ?? string.Empty);

    private static async Task<IReadOnlyDictionary<Guid, int>> LoadPhaseCountsAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
        => await dbContext.Set<ProjectPhase>()
            .GroupBy(phase => phase.ProjectId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);

    private static async Task<ProjectHierarchyMetrics> LoadHierarchyMetricsAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var links = await dbContext.Set<ProjectHierarchyLink>()
            .OrderBy(link => link.ParentProjectId)
            .ThenBy(link => link.ChildProjectId)
            .ToListAsync(cancellationToken);

        var parentCounts = links
            .GroupBy(link => link.ChildProjectId)
            .ToDictionary(group => group.Key, group => group.Count());
        var childCounts = links
            .GroupBy(link => link.ParentProjectId)
            .ToDictionary(group => group.Key, group => group.Count());

        return new ProjectHierarchyMetrics(
            parentCounts,
            childCounts,
            links.Select(link => new ProjectHierarchyLinkSummary(link.ParentProjectId, link.ChildProjectId, link.CreatedAtUtc)).ToList());
    }

    private static async Task<Error?> ValidateHierarchyConnectionAsync(
        AppDbContext dbContext,
        Guid parentProjectId,
        Guid childProjectId,
        CancellationToken cancellationToken)
    {
        if (parentProjectId == childProjectId)
        {
            return Error.Validation("A project cannot be attached as its own subproject.");
        }

        var links = await dbContext.Set<ProjectHierarchyLink>()
            .Select(link => new { link.ParentProjectId, link.ChildProjectId })
            .ToListAsync(cancellationToken);
        var childrenByParent = links
            .GroupBy(link => link.ParentProjectId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(link => link.ChildProjectId).ToList());
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(childProjectId);

        while (queue.Count > 0)
        {
            var currentProjectId = queue.Dequeue();
            if (!visited.Add(currentProjectId))
            {
                continue;
            }

            if (currentProjectId == parentProjectId)
            {
                return Error.Validation("Connecting these projects would create a cycle in the project hierarchy.");
            }

            if (!childrenByParent.TryGetValue(currentProjectId, out var descendantIds))
            {
                continue;
            }

            foreach (var descendantId in descendantIds)
            {
                queue.Enqueue(descendantId);
            }
        }

        return null;
    }

    private static string BuildSlug(string input)
    {
        return FileSafeSlugBuilder.Build(input);
    }
}


