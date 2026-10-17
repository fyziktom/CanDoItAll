using System.Text.Json.Serialization;
using System.Runtime.ExceptionServices;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Projects;

public sealed class Project : IHasConcurrencyToken {
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonIgnore]
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    [JsonIgnore]
    public Guid LifetimeId { get; private set; } = Guid.NewGuid();

    [JsonIgnore]
    public bool LegacyAgentAccessBindingEligible { get; private set; }

    internal void BindReservedLifetime(Guid lifetimeId) {
        if (lifetimeId == Guid.Empty) {
            throw new ArgumentException("A nonempty reserved lifetime is required.", nameof(lifetimeId));
        }
        LifetimeId = lifetimeId;
    }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? ExternalNamespace { get; set; }

    public string? ExternalKey { get; set; }

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
        builder.ToTable("Projects_Projects", table => table.HasCheckConstraint(
            ProjectExternalIdentity.CheckConstraintName, ProjectExternalIdentity.CheckConstraintSql));
        builder.HasKey(project => project.Id);
        builder.Property(project => project.ConcurrencyToken).IsConcurrencyToken().HasDefaultValueSql("gen_random_uuid()");
        builder.Property(project => project.LifetimeId).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(project => project.LegacyAgentAccessBindingEligible).HasDefaultValue(false);
        builder.Property(project => project.Name).HasMaxLength(200).IsRequired();
        builder.Property(project => project.Slug).HasMaxLength(200).IsRequired();
        builder.Property(project => project.ExternalNamespace).HasMaxLength(ProjectExternalIdentity.MaximumLength);
        builder.Property(project => project.ExternalKey).HasMaxLength(ProjectExternalIdentity.MaximumLength);
        builder.HasIndex(project => new { project.ExternalNamespace, project.ExternalKey })
            .IsUnique().HasDatabaseName(ProjectExternalIdentity.UniqueIndexName)
            .HasFilter("\"ExternalNamespace\" IS NOT NULL");
        builder.Property(project => project.Description).HasColumnType("TEXT");
        builder.Property(project => project.Objective).HasColumnType("TEXT");
        builder.Property(project => project.CurrentPhase).HasMaxLength(120);
        builder.HasIndex(project => new { project.Name, project.Id });
        builder.HasIndex(project => new { project.UpdatedAtUtc, project.Id })
            .IsDescending(true, false);
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

public sealed partial class ProjectsService(
    IDbContextFactory<ProjectsDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge,
    IEnumerable<IProjectDeletionParticipant> deletionParticipants,
    ILogger<ProjectsService> logger,
    SearchIndexService searchMutationService,
    StorageCatalogService storageCatalogService,
    CoordinatedDatabaseTransaction coordinatedTransaction,
    ProjectWriteAdmissionService writeAdmissionService,
    ProjectDefinitionWriter definitionWriter,
    ProjectDefinitionReader definitionReader,
    ICanonicalRuntimeDatabase canonicalDatabase,
    DatasetActivationService datasetActivation,
    IProjectCreationCompensationGuard? creationCompensationGuard = null) : IProjectSummaryQueryService {
    private const string DeleteRetryGuidance =
        "Retry each exact participant and recovery id returned by the deletion recovery; do not create or select a newer project-deletion operation.";
    private const string DeleteOwnerReviewGuidance =
        "The project deletion committed, but owner release or completion is unconfirmed. Keep the exact recovery identifiers for operator review; do not retry cleanup or deletion until owner release is independently established. A pending-list read does not establish release.";

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

    public async Task<ProjectSummary?> GetSummaryAsync(Guid projectId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var project = await dbContext.Set<Project>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null) {
            return null;
        }
        var hierarchyMetrics = await LoadHierarchyMetricsAsync(dbContext, cancellationToken, projectId);
        var phaseCounts = await LoadPhaseCountsAsync(dbContext, cancellationToken, projectId);
        var portfolioContexts = await projectPartyIntegrationBridge.GetPortfolioContextsAsync([projectId], cancellationToken);
        return MapProjectSummary(project, phaseCounts, hierarchyMetrics, portfolioContexts.GetValueOrDefault(projectId));
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
        CancellationToken cancellationToken = default,
        ProjectMutationAuthorization? authorization = null,
        IReadOnlyCollection<ProjectWriteAdmission>? expectedProjectAdmissions = null)
    {
        if (parentProjectId == childProjectId)
        {
            return Result.Failure(Error.Validation("A project cannot be attached as its own subproject."));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await ProjectSourceMutationScope.BeginAsync(
            dbContext,
            BuildProjectHierarchyMutationScopeKeys(
                parentProjectId,
                childProjectId),
            ProjectMutationPurpose.HierarchyWrite, childProjectId, [parentProjectId, childProjectId], authorization,
            writeAdmissionService, coordinatedTransaction, cancellationToken, expectedProjectAdmissions: expectedProjectAdmissions);
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
        await mutationScope.CommitAsync(cancellationToken);
        await RunPostCommitActionAsync(
            "activity-subproject-attach",
            childProjectId,
            () => activityStream.RecordAsync(new ActivityWriteRequest(
                "projects",
                "attach-subproject",
                "Attached subproject",
                $"{childProject.Name} is now under {parentProject.Name}.",
                ProjectId: childProjectId,
                ArtifactKind: "project",
                ArtifactId: childProjectId,
                Route: $"/projects?projectId={childProjectId}"),
                cancellationToken));
        return Result.Success();
    }

    public async Task<Result> RemoveSubprojectAsync(
        Guid parentProjectId,
        Guid childProjectId,
        CancellationToken cancellationToken = default,
        ProjectMutationAuthorization? authorization = null)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await ProjectSourceMutationScope.BeginAsync(
            dbContext,
            BuildProjectHierarchyMutationScopeKeys(
                parentProjectId,
                childProjectId),
            ProjectMutationPurpose.HierarchyWrite, childProjectId, [parentProjectId, childProjectId], authorization,
            writeAdmissionService, coordinatedTransaction, cancellationToken);
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
        await mutationScope.CommitAsync(cancellationToken);
        await RunPostCommitActionAsync(
            "activity-subproject-detach",
            childProjectId,
            () => activityStream.RecordAsync(new ActivityWriteRequest(
                "projects",
                "detach-subproject",
                "Detached subproject",
                $"{childProject.Name} was detached from {parentProject.Name}.",
                ProjectId: childProjectId,
                ArtifactKind: "project",
                ArtifactId: childProjectId,
                Route: $"/projects?projectId={childProjectId}"),
                cancellationToken));
        return Result.Success();
    }

    public async Task<Result> ReconnectSubprojectAsync(
        Guid childProjectId,
        Guid currentParentProjectId,
        Guid newParentProjectId,
        CancellationToken cancellationToken = default,
        ProjectMutationAuthorization? authorization = null,
        IReadOnlyCollection<ProjectWriteAdmission>? expectedProjectAdmissions = null)
    {
        if (currentParentProjectId == newParentProjectId)
        {
            return Result.Failure(Error.Validation("Choose a different project before reconnecting the subproject."));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await ProjectSourceMutationScope.BeginAsync(
            dbContext,
            BuildProjectHierarchyMutationScopeKeys(
                childProjectId,
                currentParentProjectId,
                newParentProjectId),
            ProjectMutationPurpose.HierarchyWrite, childProjectId, [childProjectId, currentParentProjectId, newParentProjectId], authorization,
            writeAdmissionService, coordinatedTransaction, cancellationToken, expectedProjectAdmissions: expectedProjectAdmissions);
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
        await mutationScope.CommitAsync(cancellationToken);
        await RunPostCommitActionAsync(
            "activity-subproject-reconnect",
            childProjectId,
            () => activityStream.RecordAsync(new ActivityWriteRequest(
                "projects",
                "reconnect-subproject",
                "Reconnected subproject",
                $"{childProject.Name} moved from {currentParentProject.Name} to {newParentProject.Name}.",
                ProjectId: childProjectId,
                ArtifactKind: "project",
                ArtifactId: childProjectId,
                Route: $"/projects?projectId={childProjectId}"),
                cancellationToken));
        return Result.Success();
    }

    public Task<Result<ProjectDefinitionSnapshot>> GetDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
        => definitionReader.ReadAsync(id, cancellationToken);

    public async Task<ProjectEditorModel> GetAsync(Guid? id, CancellationToken cancellationToken = default) {
        if (!id.HasValue) {
            return CreateNew();
        }
        var result = await GetDefinitionAsync(id.Value, cancellationToken);
        if (!result.IsSuccess) {
            if (result.Errors.All(error => error.Code == ProjectErrorCodes.NotFound)) {
                return CreateNew();
            }
            throw new InvalidOperationException("The project definition could not be read.");
        }
        var definition = result.Value!;
        var phases = definition.Phases.Select(item => new ProjectPhaseEditorModel {
            Id = item.Id,
            Name = item.Name,
            Goal = item.Goal,
            Status = item.Status,
            StartDateUtc = NormalizeNullableUtc(item.StartDateUtc),
            EndDateUtc = NormalizeNullableUtc(item.EndDateUtc)
        }).ToList();
        var options = definition.Options.Select(item => new ProjectOptionEditorModel {
            Id = item.Id,
            Category = item.Category,
            OptionName = item.OptionName,
            Notes = item.Notes
        }).ToList();
        EnsureDefaultCategories(options);
        return new ProjectEditorModel {
            Id = definition.Expectation.Project.ProjectId,
            ExpectedDefinition = definition.Expectation,
            ExpectedLifetimeId = definition.Expectation.Project.LifetimeId,
            ExpectedProjectAdmission = definition.Expectation.Project,
            ExternalNamespace = definition.ExternalIdentity?.Namespace,
            ExternalKey = definition.ExternalIdentity?.Key,
            Name = definition.Scalars.Name,
            Description = definition.Scalars.Description,
            Objective = definition.Scalars.Objective,
            Status = definition.Scalars.Status,
            CurrentPhase = definition.Scalars.CurrentPhase,
            TargetDateUtc = NormalizeNullableUtc(definition.Scalars.TargetDateUtc),
            Phases = phases,
            Options = options.OrderBy(option => option.Category).ToList()
        };
    }

    public async Task<Result<ProjectWriteAdmission>> CreateWithAdmissionAsync(ProjectEditorModel model,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Id.HasValue) {
            return Result<ProjectWriteAdmission>.Failure(Error.Validation("A new project cannot edit an existing project."));
        }
        var result = await SaveWithReceiptCoreAsync(model, parentProjectId: null, newProjectId: null, cancellationToken);
        return result.IsSuccess
            ? Result<ProjectWriteAdmission>.Success(result.Value?.CreationReceipt?.Project
                ?? throw new InvalidOperationException("The project commit returned no lifetime receipt."))
            : Result<ProjectWriteAdmission>.Failure(result.Errors.ToArray());
    }

    public Task<Result<Guid>> SaveAsync(
        ProjectEditorModel model,
        CancellationToken cancellationToken = default,
        ProjectMutationAuthorization? authorization = null)
        => SaveCoreAsync(model, parentProjectId: null, newProjectId: null, cancellationToken, authorization: authorization);

    public Task<Result<ProjectDefinitionScalarsAcknowledgement>> SaveScalarsAsync(UpdateProjectDefinitionScalarsCommand command,
        CancellationToken cancellationToken = default, ProjectMutationAuthorization? authorization = null)
        => definitionWriter.UpdateScalarsAsync(command, cancellationToken, authorization);

    public async Task<Result<ProjectEditorAcknowledgement>> SaveEditorAsync(ProjectEditorModel model,
        CancellationToken cancellationToken = default) {
        var result = await SaveWithReceiptCoreAsync(model, parentProjectId: null, newProjectId: null, cancellationToken);
        return result.IsSuccess
            ? Result<ProjectEditorAcknowledgement>.Success(result.Value!.Editor)
            : Result<ProjectEditorAcknowledgement>.Failure(result.Errors);
    }

    public Task<Result<Guid>> CreateAsync(ProjectCreationReservation reservation, ProjectEditorModel model,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(reservation);
        if (model.Id.HasValue) {
            return Task.FromResult(Result<Guid>.Failure(Error.Validation("A new project cannot use an existing project id.")));
        }
        return SaveCoreAsync(model, parentProjectId: null, reservation.ProjectId, cancellationToken, reservation);
    }

    public async Task<Result<ProjectEditorAcknowledgement>> CreateEditorAsync(ProjectCreationReservation reservation,
        ProjectEditorModel model, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(reservation);
        if (model.Id.HasValue || reservation.ParentProjectId.HasValue) {
            return Result<ProjectEditorAcknowledgement>.Failure(Error.Validation("A new project editor requires its own root creation reservation."));
        }
        var result = await SaveWithReceiptCoreAsync(model, null, reservation.ProjectId, cancellationToken, reservation);
        return result.IsSuccess ? Result<ProjectEditorAcknowledgement>.Success(result.Value!.Editor)
            : Result<ProjectEditorAcknowledgement>.Failure(result.Errors);
    }

    public Task<Result<Guid>> CreateSubprojectAsync(Guid parentProjectId, ProjectCreationReservation reservation,
        ProjectEditorModel model, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(reservation);
        if (parentProjectId == Guid.Empty || model.Id.HasValue) {
            return Task.FromResult(Result<Guid>.Failure(Error.Validation("A new subproject requires a parent and cannot edit an existing project.")));
        }
        return SaveCoreAsync(model, parentProjectId, reservation.ProjectId, cancellationToken, reservation);
    }

    public Task<Result<Guid>> CreateAsync(
        Guid newProjectId,
        ProjectEditorModel model,
        CancellationToken cancellationToken = default)
    {
        if (newProjectId == Guid.Empty)
        {
            return Task.FromResult(Result<Guid>.Failure(Error.Validation("A new project id is required.")));
        }

        if (model.Id.HasValue)
        {
            return Task.FromResult(Result<Guid>.Failure(Error.Validation("A new project cannot use an existing project id.")));
        }

        return SaveCoreAsync(
            model,
            parentProjectId: null,
            newProjectId,
            cancellationToken);
    }

    public Task<Result<Guid>> CreateSubprojectAsync(
        Guid parentProjectId,
        ProjectEditorModel model,
        CancellationToken cancellationToken = default)
    {
        if (parentProjectId == Guid.Empty)
        {
            return Task.FromResult(Result<Guid>.Failure(Error.Validation("A parent project is required.")));
        }

        if (model.Id.HasValue)
        {
            return Task.FromResult(Result<Guid>.Failure(Error.Validation("A new subproject cannot use an existing project id.")));
        }

        return SaveCoreAsync(
            model,
            parentProjectId,
            newProjectId: null,
            cancellationToken);
    }

    public Task<Result<Guid>> CreateSubprojectAsync(
        Guid parentProjectId,
        Guid newProjectId,
        ProjectEditorModel model,
        CancellationToken cancellationToken = default)
    {
        if (parentProjectId == Guid.Empty)
        {
            return Task.FromResult(Result<Guid>.Failure(Error.Validation("A parent project is required.")));
        }

        if (newProjectId == Guid.Empty)
        {
            return Task.FromResult(Result<Guid>.Failure(Error.Validation("A new subproject id is required.")));
        }

        if (model.Id.HasValue)
        {
            return Task.FromResult(Result<Guid>.Failure(Error.Validation("A new subproject cannot use an existing project id.")));
        }

        return SaveCoreAsync(
            model,
            parentProjectId,
            newProjectId,
            cancellationToken);
    }

    private async Task<Result<Guid>> SaveCoreAsync(ProjectEditorModel model, Guid? parentProjectId, Guid? newProjectId,
        CancellationToken cancellationToken, ProjectCreationReservation? reservation = null, ProjectMutationAuthorization? authorization = null) {
        var result = await SaveWithReceiptCoreAsync(model, parentProjectId, newProjectId, cancellationToken, reservation, authorization);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Errors);
    }

    private async Task<Result<ProjectSaveOutcome>> SaveWithReceiptCoreAsync(ProjectEditorModel model,
        Guid? parentProjectId, Guid? newProjectId, CancellationToken cancellationToken,
        ProjectCreationReservation? reservation = null, ProjectMutationAuthorization? authorization = null) {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Phases is null || model.Options is null || model.Phases.Any(phase => phase is null) || model.Options.Any(option => option is null)) {
            return Result<ProjectSaveOutcome>.Failure(Error.Validation("A full definition requires both complete child collections.", ProjectErrorCodes.DefinitionChildInvalid));
        }
        var expected = model.ExpectedDefinition;
        if (model.Id.HasValue && expected is null) {
            return Result<ProjectSaveOutcome>.Failure(Error.Validation("An existing project requires its original definition expectation.", ProjectErrorCodes.DefinitionExpectationRequired));
        }
        if (model.Id.HasValue && (model.Id == Guid.Empty || expected!.Project.ProjectId != model.Id ||
                model.ExpectedLifetimeId.HasValue && model.ExpectedLifetimeId != expected.Project.LifetimeId ||
                model.ExpectedProjectAdmission is not null && model.ExpectedProjectAdmission != expected.Project) ||
                !model.Id.HasValue && (expected is not null || model.ExpectedLifetimeId.HasValue || model.ExpectedProjectAdmission is not null)) {
            return Result<ProjectSaveOutcome>.Failure(Error.Validation("The definition expectation and editor identities disagree.", ProjectErrorCodes.DefinitionExpectationInvalid));
        }
        var scalars = new ProjectDefinitionScalars(model.Name, model.Description, model.Objective, model.Status, model.CurrentPhase, model.TargetDateUtc);
        var phases = model.Phases.Select(phase => new ProjectDefinitionPhaseInput(phase.Id, phase.Name, phase.Goal,
            phase.Status, phase.StartDateUtc, phase.EndDateUtc)).ToArray();
        var options = model.Options.Select(option => new ProjectDefinitionOptionInput(option.Id, option.Category, option.OptionName, option.Notes)).ToArray();
        var identity = model.ExternalNamespace is null && model.ExternalKey is null ? null
            : new ProjectDefinitionExternalIdentity(model.ExternalNamespace!, model.ExternalKey!);
        if (expected is not null) {
            var replacement = await definitionWriter.ReplaceAsync(new(expected, scalars, phases, options, identity), cancellationToken, authorization);
            return replacement.IsSuccess ? Result<ProjectSaveOutcome>.Success(new(replacement.Value!.Project.ProjectId, null, replacement.Value))
                : Result<ProjectSaveOutcome>.Failure(replacement.Errors);
        }
        var created = await definitionWriter.CreateAsync(new CreateProjectDefinitionCommand(scalars, phases, options, identity),
            new(newProjectId ?? Guid.NewGuid(), parentProjectId, reservation, authorization), cancellationToken);
        return created.IsSuccess ? Result<ProjectSaveOutcome>.Success(new(created.Value!.Editor.Project.ProjectId, created.Value.Receipt, created.Value.Editor))
            : Result<ProjectSaveOutcome>.Failure(created.Errors);
    }

    private static string[] BuildProjectMutationScopeKeys(
        params Guid?[] projectIds)
    {
        return projectIds
            .Where(projectId => projectId.HasValue)
            .Select(projectId => projectId!.Value)
            .Where(projectId => projectId != Guid.Empty)
            .Distinct()
            .Order()
            .Select(ProjectMutationScopeKeys.ForProject)
            .ToArray();
    }

    private static string[] BuildProjectHierarchyMutationScopeKeys(
        params Guid?[] projectIds)
        => BuildProjectMutationScopeKeys(projectIds)
            .Append(ProjectMutationScopeKeys.Hierarchy)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private async Task RunPostCommitActionAsync(
        string action,
        Guid projectId,
        Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Project {ProjectId} was committed, but post-commit action {Action} failed.",
                projectId,
                action);
        }
    }

    private static DateTime? NormalizeNullableUtc(DateTime? value)
    {
        return value?.Kind switch
        {
            null => null,
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    public async Task<ProjectDeletionResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default,
        ProjectWriteAdmission? expectedProjectAdmission = null)
        => await DeleteCoreAsync(id, cancellationToken, expectedProjectAdmission: expectedProjectAdmission)
            ?? throw new InvalidOperationException("An ordinary project deletion returned a creation-compensation refusal.");

    private async Task<ProjectDeletionResult?> DeleteCoreAsync(Guid id, CancellationToken cancellationToken,
        ProjectCreationReceipt? creationReceipt = null, ProjectWriteAdmission? expectedProjectAdmission = null) {
        ProjectsDbContext? dbContext = null;
        ProjectSourceMutationScope? mutationScope = null;
        Project? project = null;
        Exception? ownerFailure = null;
        var compensationRefused = false;
        var preparedParticipants = new List<(IProjectDeletionParticipant Participant, ProjectDeletionParticipantPreparation Preparation)>();
        try {
            var profile = canonicalDatabase.Profile;
            var generation = canonicalDatabase.Generation;
            var binding = profile.Profile.ProviderKind == DatabaseProviderKind.PostgreSql
                ? await datasetActivation.ReadActiveBindingAsync(profile.Profile.Id, cancellationToken)
                : null;
            dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            coordinatedTransaction.RequireOwnerProfile(dbContext);
            CoordinatedDatabaseTransaction.ForProfile(profile).RequireOwnerProfile(dbContext);
            void RequireCurrentOwner() {
                if (canonicalDatabase.Profile != profile || canonicalDatabase.Generation != generation) {
                    throw new InvalidOperationException("The original project deletion database owner is no longer current.");
                }
            }
            RequireCurrentOwner();
            var orderedParticipants = GetOrderedDeletionParticipants();
            var mutationScopeKeys = ResolveDeletionPreparationScopeKeys(orderedParticipants)
                .Append(ProjectMutationScopeKeys.ForProject(id))
                .Append(ProjectMutationScopeKeys.Hierarchy)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var purpose = creationReceipt?.Reservation?.ParentProjectId is not null ? ProjectMutationPurpose.CreateChild :
                creationReceipt is not null ? ProjectMutationPurpose.CreateRoot : ProjectMutationPurpose.ExistingWrite;
            var existingIds = creationReceipt?.Authorization?.ExpectedProjects.Select(item => item.ProjectId).ToArray() ?? [];
            mutationScope = binding is not null
                ? await ProjectSourceMutationScope.BeginDeletionAsync(dbContext, mutationScopeKeys, purpose, id, existingIds,
                    creationReceipt?.Authorization, writeAdmissionService, coordinatedTransaction, binding, RequireCurrentOwner,
                    cancellationToken, creationReceipt?.Reservation)
                : await ProjectSourceMutationScope.BeginAsync(dbContext, mutationScopeKeys, purpose, id, existingIds,
                    creationReceipt?.Authorization, writeAdmissionService, coordinatedTransaction, cancellationToken, creationReceipt?.Reservation);
            project = await dbContext.Set<Project>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (expectedProjectAdmission is not null) {
                ProjectAssignmentAdmission.Require(id, expectedProjectAdmission);
                await writeAdmissionService.RequireAsync(dbContext, expectedProjectAdmission, cancellationToken);
            }
            if (creationReceipt is not null && !await CanCompensateCreationAsync(dbContext, creationReceipt, cancellationToken)) {
                compensationRefused = true;
            } else {
                RequireCurrentOwner();
                using (coordinatedTransaction.Enter(dbContext)) {
                    foreach (var participant in orderedParticipants) {
                        var preparation = await participant.PrepareAsync(id, cancellationToken);
                        if (preparation is null) {
                            continue;
                        }

                        if (preparation.RecoveryId == Guid.Empty) {
                            throw new InvalidOperationException(
                                $"Project deletion participant '{participant.Id}' returned an empty recovery id.");
                        }

                        if (preparation.ProjectId != id) {
                            throw new InvalidOperationException(
                                $"Project deletion participant '{participant.Id}' returned recovery state for another project.");
                        }

                        preparedParticipants.Add((participant, preparation));
                    }

                    if (creationReceipt?.Reservation is { } originalReservation) {
                        await writeAdmissionService.CancelConsumedCreationForCompensationAsync(dbContext, originalReservation, cancellationToken);
                    }
                    await ProjectWriteAdmissionService.CancelCreationForDeletionAsync(dbContext, id, cancellationToken);
                    if (project is not null) {
                        var phases = await dbContext.Set<ProjectPhase>().Where(item => item.ProjectId == id).ToListAsync(cancellationToken);
                        var options = await dbContext.Set<ProjectOptionSelection>().Where(item => item.ProjectId == id).ToListAsync(cancellationToken);
                        var hierarchyLinks = await dbContext.Set<ProjectHierarchyLink>()
                            .Where(item => item.ParentProjectId == id || item.ChildProjectId == id)
                            .ToListAsync(cancellationToken);
                        dbContext.RemoveRange(phases);
                        dbContext.RemoveRange(options);
                        dbContext.RemoveRange(hierarchyLinks);
                        dbContext.Add(new ProjectRetirementRecord {
                            ProjectId = project.Id, LifetimeId = project.LifetimeId, RetiredAtUtc = clock.GetUtcNow()
                        });
                        dbContext.Remove(project);
                    }

                    await searchMutationService.DeleteProjectSearchForMutationAsync(id, cancellationToken);
                    await storageCatalogService.DeleteProjectRoutingForMutationAsync(id, cancellationToken);

                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                await mutationScope.CommitAsync(cancellationToken);
            }
        } catch (Exception failure) {
            ownerFailure = failure;
        }
        if (mutationScope is not null) {
            try {
                await mutationScope.DisposeAsync();
            } catch (Exception failure) {
                ownerFailure = ownerFailure is null ? failure : new AggregateException(ownerFailure, failure);
            }
        }
        if (dbContext is not null) {
            try {
                await dbContext.DisposeAsync();
            } catch (Exception failure) {
                ownerFailure = ownerFailure is null ? failure : new AggregateException(ownerFailure, failure);
            }
        }
        if (ownerFailure is not null) {
            try {
                logger.LogError("Project {ProjectId} deletion owner failed with {FailureType}. CommitAttempted={CommitAttempted}; CommitAcknowledged={CommitAcknowledged}.",
                    id, ownerFailure.GetType().FullName, mutationScope?.CommitAttempted == true, mutationScope?.IsCommitted == true);
            } catch (Exception loggingFailure) {
                ownerFailure = new AggregateException(ownerFailure, loggingFailure);
            }
            if (mutationScope?.IsCommitted == true) {
                throw OwnerCompletionFailure(preparedParticipants, ownerFailure);
            }
            ExceptionDispatchInfo.Capture(ownerFailure).Throw();
        }
        if (compensationRefused) {
            return null;
        }

        var failures = new List<ProjectDeletionRecoveryFailure>();
        var failureExceptions = new List<Exception>();
        var warnings = new List<ProjectDeletionWarning>();
        var completedParticipants = 0;
        try {
            if (project is not null) {
                await RunPostCommitActionAsync(
                    "activity-project-delete",
                    id,
                    () => activityStream.RecordAsync(new ActivityWriteRequest(
                        "projects",
                        "delete",
                        "Deleted project",
                        project.Name,
                        ProjectId: id,
                        ArtifactKind: "project",
                        ArtifactId: id,
                        Route: "/projects"), cancellationToken));
            }

            foreach (var (participant, preparation) in preparedParticipants) {
                try {
                    var completion = await participant.CompleteAsync(preparation, cancellationToken);
                    ValidateParticipantCompletion(participant, preparation, completion);
                    warnings.AddRange(completion.Warnings.Select(warning => new ProjectDeletionWarning(
                        warning.Kind,
                        participant.Id,
                        completion.RecoveryId,
                        warning.RetainedObject,
                        warning.Message,
                        warning.Remediation)));
                } catch (Exception exception) {
                    var failedRecoveryId = exception is ProjectDeletionParticipantCleanupException cleanupException
                        ? cleanupException.RecoveryId
                        : preparation.RecoveryId;
                    failures.Add(new ProjectDeletionRecoveryFailure(
                        ProjectDeletionRecoveryOperation.ParticipantCleanup,
                        participant.Id,
                        failedRecoveryId));
                    failureExceptions.Add(exception);
                    logger.LogError("Project {ProjectId} committed deletion; participant {Participant} cleanup for recovery {RecoveryId} failed with {FailureType}.",
                        id, participant.Id, preparation.RecoveryId, exception.GetType().FullName);
                }
                completedParticipants++;
            }

            await RecordDeletionWarningsAsync(id, warnings, cancellationToken);

            if (failures.Count == 0) {
                return new ProjectDeletionResult(id, warnings);
            }

            var recovery = new ProjectDeletionRecovery(id, failures.ToArray(), DeleteRetryGuidance);
            var innerException = failureExceptions.Count == 1
                ? failureExceptions[0]
                : new AggregateException(failureExceptions);
            throw new ProjectDeletionPartialCommitException(
                recovery,
                $"Project '{id:D}' was deleted, but cleanup is incomplete. {DeleteRetryGuidance}",
                innerException);
        } catch (ProjectDeletionPartialCommitException) {
            throw;
        } catch (Exception failure) {
            var pending = failures.Concat(preparedParticipants.Skip(completedParticipants).Select(item =>
                new ProjectDeletionRecoveryFailure(ProjectDeletionRecoveryOperation.ParticipantCleanup,
                    item.Participant.Id, item.Preparation.RecoveryId))).Distinct().ToArray();
            throw new ProjectDeletionPartialCommitException(new(id, pending, DeleteOwnerReviewGuidance, true),
                DeleteOwnerReviewGuidance, failureExceptions.Count == 0 ? failure : new AggregateException(failureExceptions.Append(failure)));
        }

        ProjectDeletionPartialCommitException OwnerCompletionFailure(
            IEnumerable<(IProjectDeletionParticipant Participant, ProjectDeletionParticipantPreparation Preparation)> pending, Exception failure)
            => new(new(id, pending.Select(item => new ProjectDeletionRecoveryFailure(
                ProjectDeletionRecoveryOperation.ParticipantCleanup, item.Participant.Id, item.Preparation.RecoveryId)).ToArray(),
                DeleteOwnerReviewGuidance, true), DeleteOwnerReviewGuidance, failure);
    }

    public async Task<IReadOnlyList<ProjectDeletionPendingCleanup>> ListPendingDeletionCleanupsAsync(
        CancellationToken cancellationToken = default)
    {
        var pending = new List<ProjectDeletionPendingCleanup>();
        foreach (var participant in GetOrderedDeletionParticipants())
        {
            var recoveries = await participant.ListPendingRecoveriesAsync(cancellationToken);
            foreach (var recovery in recoveries)
            {
                if (recovery.ProjectId == Guid.Empty || recovery.RecoveryId == Guid.Empty)
                {
                    throw new InvalidOperationException(
                        $"Project deletion participant '{participant.Id}' returned an invalid pending recovery identity.");
                }

                pending.Add(new ProjectDeletionPendingCleanup(
                    recovery.ProjectId,
                    participant.Id,
                    recovery.RecoveryId,
                    recovery.Status,
                    recovery.CanRetryNow,
                    recovery.RetryAvailableAtUtc,
                    recovery.RetryGuidance));
            }
        }

        return pending
            .OrderBy(item => item.ProjectId)
            .ThenBy(item => item.ParticipantId.Value, StringComparer.Ordinal)
            .ThenBy(item => item.RecoveryId)
            .ToArray();
    }

    public async Task<IReadOnlyList<ProjectDeletionCompletionNotice>> ListDeletionCompletionNoticesAsync(
        CancellationToken cancellationToken = default)
    {
        var notices = new List<ProjectDeletionCompletionNotice>();
        foreach (var participant in GetOrderedDeletionParticipants())
        {
            var participantNotices = await participant.ListCompletionNoticesAsync(
                cancellationToken);
            foreach (var notice in participantNotices)
            {
                if (notice.ProjectId == Guid.Empty || notice.RecoveryId == Guid.Empty)
                {
                    throw new InvalidOperationException(
                        $"Project deletion participant '{participant.Id}' returned an invalid completion notice identity.");
                }

                notices.Add(new ProjectDeletionCompletionNotice(
                    notice.ProjectId,
                    participant.Id,
                    notice.RecoveryId,
                    notice.Operation,
                    notice.Warnings.Select(warning => new ProjectDeletionWarning(
                        warning.Kind,
                        participant.Id,
                        notice.RecoveryId,
                        warning.RetainedObject,
                        warning.Message,
                        warning.Remediation)).ToArray()));
            }
        }

        return notices
            .OrderBy(notice => notice.ProjectId)
            .ThenBy(notice => notice.ParticipantId.Value, StringComparer.Ordinal)
            .ThenBy(notice => notice.RecoveryId)
            .ToArray();
    }

    public async Task<ProjectDeletionResult> RetryDeletionCleanupAsync(
        Guid projectId,
        ProjectDeletionParticipantId participantId,
        Guid recoveryId,
        CancellationToken cancellationToken = default)
    {
        var participant = GetOrderedDeletionParticipants()
            .SingleOrDefault(candidate => candidate.Id == participantId)
            ?? throw new ProjectDeletionRecoveryNotFoundException(
                projectId,
                participantId,
                recoveryId);
        var recoveries = await participant.ListPendingRecoveriesAsync(cancellationToken);
        var recovery = recoveries.SingleOrDefault(candidate =>
            candidate.ProjectId == projectId && candidate.RecoveryId == recoveryId);
        if (recovery is null)
        {
            var completionNotice = (await participant.ListCompletionNoticesAsync(cancellationToken))
                .SingleOrDefault(candidate =>
                    candidate.ProjectId == projectId &&
                    candidate.RecoveryId == recoveryId);
            if (completionNotice is not null)
            {
                var completedWarnings = completionNotice.Warnings.Select(warning =>
                    new ProjectDeletionWarning(
                        warning.Kind,
                        participant.Id,
                        completionNotice.RecoveryId,
                        warning.RetainedObject,
                        warning.Message,
                        warning.Remediation))
                    .ToArray();
                return new ProjectDeletionResult(projectId, completedWarnings);
            }

            throw new ProjectDeletionRecoveryNotFoundException(
                projectId,
                participantId,
                recoveryId);
        }

        try
        {
            var preparation = new ProjectDeletionParticipantPreparation(projectId, recoveryId);
            var completion = await participant.CompleteAsync(
                preparation,
                cancellationToken);
            ValidateParticipantCompletion(participant, preparation, completion);
            var warnings = completion.Warnings.Select(warning => new ProjectDeletionWarning(
                warning.Kind,
                participant.Id,
                completion.RecoveryId,
                warning.RetainedObject,
                warning.Message,
                    warning.Remediation))
                .ToArray();
            await RecordDeletionWarningsAsync(projectId, warnings, cancellationToken);
            return new ProjectDeletionResult(projectId, warnings);
        }
        catch (ProjectDeletionPartialCommitException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failedRecoveryId = exception is ProjectDeletionParticipantCleanupException cleanupException
                ? cleanupException.RecoveryId
                : recoveryId;
            logger.LogError(
                exception,
                "Project {ProjectId} participant {Participant} failed exact cleanup retry {RecoveryId}.",
                projectId,
                participant.Id,
                failedRecoveryId);
            throw new ProjectDeletionPartialCommitException(
                new ProjectDeletionRecovery(
                    projectId,
                    [new ProjectDeletionRecoveryFailure(
                        ProjectDeletionRecoveryOperation.ParticipantCleanup,
                        participant.Id,
                        failedRecoveryId)],
                    DeleteRetryGuidance),
                $"Project '{projectId:D}' is deleted, but cleanup is incomplete. {DeleteRetryGuidance}",
                exception);
        }
    }

    private IReadOnlyList<IProjectDeletionParticipant> GetOrderedDeletionParticipants()
    {
        var orderedParticipants = deletionParticipants
            .OrderBy(participant => participant.Id.Value, StringComparer.Ordinal)
            .ToList();
        if (orderedParticipants.Any(participant => string.IsNullOrWhiteSpace(participant.Id.Value)))
        {
            throw new InvalidOperationException(
                "Project deletion participants require a non-empty strongly typed id.");
        }

        var duplicateParticipantId = orderedParticipants
            .GroupBy(participant => participant.Id)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateParticipantId.HasValue)
        {
            throw new InvalidOperationException(
                $"Project deletion participant id '{duplicateParticipantId.Value}' is registered more than once.");
        }

        return orderedParticipants;
    }

    private static IReadOnlyList<string> ResolveDeletionPreparationScopeKeys(
        IReadOnlyCollection<IProjectDeletionParticipant> participants)
    {
        var scopeKeys = new List<string>();
        foreach (var participant in participants)
        {
            var participantScopeKeys = participant.PreparationScopeKeys
                ?? throw new InvalidOperationException(
                    $"Project deletion participant '{participant.Id}' returned a null preparation scope-key collection.");
            foreach (var scopeKey in participantScopeKeys)
            {
                if (string.IsNullOrWhiteSpace(scopeKey.Value))
                {
                    throw new InvalidOperationException(
                        $"Project deletion participant '{participant.Id}' returned an empty preparation scope key.");
                }

                scopeKeys.Add(scopeKey.Value);
            }
        }

        return scopeKeys;
    }

    private static void ValidateParticipantCompletion(
        IProjectDeletionParticipant participant,
        ProjectDeletionParticipantPreparation preparation,
        ProjectDeletionParticipantCompletion completion)
    {
        if (completion.RecoveryId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Project deletion participant '{participant.Id}' returned an empty completion recovery identity for project '{preparation.ProjectId:D}'.");
        }
    }

    private Task RecordDeletionWarningsAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectDeletionWarning> warnings,
        CancellationToken cancellationToken)
    {
        if (warnings.Count == 0)
        {
            return Task.CompletedTask;
        }

        return RunPostCommitActionAsync(
            "activity-project-delete-retained-media",
            projectId,
            () => activityStream.RecordAsync(new ActivityWriteRequest(
                "projects",
                "delete-retained-media",
                "Project deleted with retained managed media",
                string.Join(" ", warnings.Select(warning => warning.Message)),
                ProjectId: projectId,
                ArtifactKind: "project",
                ArtifactId: projectId,
                Route: "/projects"), cancellationToken));
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

    private ProjectSummary MapProjectSummary(
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
        portfolioContext?.SearchText ?? string.Empty) { ExpectedProjectAdmission = writeAdmissionService.Capture(project) };

    private static async Task<IReadOnlyDictionary<Guid, int>> LoadPhaseCountsAsync(
        ProjectsDbContext dbContext,
        CancellationToken cancellationToken,
        Guid? projectId = null) {
        var phases = dbContext.Set<ProjectPhase>().AsQueryable();
        if (projectId.HasValue) {
            phases = phases.Where(phase => phase.ProjectId == projectId.Value);
        }
        return await phases
            .GroupBy(phase => phase.ProjectId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);
    }

    private static async Task<ProjectHierarchyMetrics> LoadHierarchyMetricsAsync(
        ProjectsDbContext dbContext,
        CancellationToken cancellationToken,
        Guid? projectId = null) {
        var query = dbContext.Set<ProjectHierarchyLink>().AsQueryable();
        if (projectId.HasValue) {
            query = query.Where(link => link.ParentProjectId == projectId.Value || link.ChildProjectId == projectId.Value);
        }
        var links = await query
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
        ProjectsDbContext dbContext,
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
