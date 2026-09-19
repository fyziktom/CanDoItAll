using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Projects;

public static class ProjectErrorCodes
{
    public const string NotFound = "projects.not-found";
    public const string LifetimeChanged = "projects.lifetime-changed";
    public const string ReservedIdConflict = "projects.reserved-id-conflict";
    public const string ReservationClosed = "projects.reservation-closed";
}

/// <summary>
/// Status of a project phase, as a JSON integer: 0 Planned, 1 Active, 2 Blocked, 3 Completed.
/// </summary>
public enum ProjectPhaseStatus
{
    Planned,
    Active,
    Blocked,
    Completed
}

/// <summary>
/// Category of a project technology option, as a JSON integer: 0 Language, 1 Database, 2 Ui, 3 ExternalApi, 4 Storage,
/// 5 Deployment, 6 Testing, 7 Other.
/// </summary>
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
        builder.Property(project => project.LifetimeId).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(project => project.LegacyAgentAccessBindingEligible).HasDefaultValue(false);
        builder.Property(project => project.Name).HasMaxLength(200).IsRequired();
        builder.Property(project => project.Slug).HasMaxLength(200).IsRequired();
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

/// <summary>
/// Summary of one project, as returned by project lists, project saves and hierarchy reads of the Projects and Project
/// Structure APIs.
/// </summary>
/// <param name="Id">
/// Identifier of the project; use it with the other <c>/api/projects</c> operations and with
/// <c>/api/project-structure</c>.
/// </param>
/// <param name="Name">Name of the project.</param>
/// <param name="Status">
/// Lifecycle status of the project, as a JSON integer: 0 Draft, 1 Active, 2 OnHold, 3 Completed, 4 Archived.
/// </param>
/// <param name="CurrentPhase">Name of the current phase as free text; empty when not set.</param>
/// <param name="PhaseCount">Number of phases of the project.</param>
/// <param name="ParentCount">Number of parent projects.</param>
/// <param name="ChildCount">Number of direct subprojects.</param>
/// <param name="UpdatedAtUtc">When the project record was last saved, in UTC.</param>
/// <param name="PrimaryCustomerName">
/// Display name of the primary customer among the project's participants, or of the first customer by name when none
/// is primary; empty when the project has no customer participant.
/// </param>
/// <param name="PrimaryDeliveryUnitName">
/// Display name of the primary delivery unit among the project's participants, or of the first one by name; empty when
/// there is none.
/// </param>
/// <param name="PrimaryOwnerName">
/// Display name of the primary owner (a participating manager, team member or reviewer), or of the first one by name;
/// empty when there is none.
/// </param>
/// <param name="RelatedParties">
/// Parties that participate in the project as a whole in its current lifetime (task assignments are not included),
/// primary ones first, then by name; an empty array when there are none.
/// </param>
/// <param name="RelatedPartySearchText">
/// One line per related party in the form label:display name, joined by line breaks, for client-side filtering.
/// </param>
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
    string RelatedPartySearchText = "") {
    [JsonIgnore]
    public ProjectWriteAdmission? ExpectedProjectAdmission { get; init; }
}

/// <summary>
/// One project in the lightweight project list of <c>GET /api/projects/access-list</c>.
/// </summary>
/// <param name="Id">Identifier of the project.</param>
/// <param name="Name">Name of the project.</param>
public sealed record ProjectAccessListItem(
    Guid Id,
    string Name);

/// <summary>
/// A parent-child link between two projects in the project hierarchy.
/// </summary>
/// <param name="ParentProjectId">Identifier of the parent project.</param>
/// <param name="ChildProjectId">Identifier of the subproject.</param>
/// <param name="CreatedAtUtc">When the link was created, in UTC.</param>
public sealed record ProjectHierarchyLinkSummary(
    Guid ParentProjectId,
    Guid ChildProjectId,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// The direct parents and direct subprojects of one project, as returned by the project hierarchy reads.
/// </summary>
/// <param name="ProjectId">Identifier of the requested project.</param>
/// <param name="ParentProjects">Summaries of the direct parent projects, most recently updated first.</param>
/// <param name="ChildProjects">Summaries of the direct subprojects, most recently updated first.</param>
public sealed record ProjectHierarchySnapshot(
    Guid ProjectId,
    IReadOnlyList<ProjectSummary> ParentProjects,
    IReadOnlyList<ProjectSummary> ChildProjects);

/// <summary>
/// A phase of a project in the project read and save. Phases are stored in array order.
/// </summary>
public sealed class ProjectPhaseEditorModel
{
    /// <summary>
    /// Identifier of the phase; null for a phase to add. On save, an entry whose identifier matches a stored phase of
    /// the project updates it, and stored phases missing from the request are deleted.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Name of the phase; trimmed.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// What the phase should achieve, as free text; trimmed.
    /// </summary>
    public string Goal { get; set; } = string.Empty;

    /// <summary>
    /// Status of the phase, as a JSON integer: 0 Planned, 1 Active, 2 Blocked, 3 Completed. Omitted means Planned.
    /// </summary>
    public ProjectPhaseStatus Status { get; set; } = ProjectPhaseStatus.Planned;

    /// <summary>
    /// Start of the phase as a UTC date and time. A sent value with an offset is converted to UTC and a value without
    /// an offset is taken as UTC. Null means not set.
    /// </summary>
    public DateTime? StartDateUtc { get; set; }

    /// <summary>
    /// End of the phase as a UTC date and time, converted like <c>startDateUtc</c>; not checked against the start. Null
    /// means not set.
    /// </summary>
    public DateTime? EndDateUtc { get; set; }
}

/// <summary>
/// A technology option of a project, such as its programming language or database, in the project read and save.
/// </summary>
public sealed class ProjectOptionEditorModel
{
    /// <summary>
    /// Identifier of the option; null for an option to add and for the empty category entries of a read. On save,
    /// stored options missing from the request are deleted.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Category of the option, as a JSON integer: 0 Language, 1 Database, 2 Ui, 3 ExternalApi, 4 Storage, 5 Deployment,
    /// 6 Testing, 7 Other.
    /// </summary>
    public ProjectOptionCategory Category { get; set; }

    /// <summary>
    /// The chosen option, for example <c>PostgreSQL</c>; trimmed. An entry with an empty name and empty notes is
    /// skipped on save.
    /// </summary>
    public string OptionName { get; set; } = string.Empty;

    /// <summary>
    /// Notes about the choice; trimmed.
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// The editable fields of a project, returned by <c>GET /api/projects/{projectId}</c> and sent to
/// <c>POST /api/projects</c>. Without <c>id</c> it describes a new project. Changes to subprojects, tasks and project
/// participants use other operations.
/// </summary>
public sealed class ProjectEditorModel
{
    /// <summary>
    /// Identifier of the project; null for a new project, including the blank template that the read returns for an
    /// unknown identifier. A save with an identifier that does not exist is rejected (<c>projects.not-found</c>).
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Identifier of the project lifetime that the read returned. Send it back unchanged: a save is rejected
    /// (<c>projects.lifetime-changed</c>) when the project was deleted and recreated with the same identifier since the
    /// read. Null skips the check; ignored when creating a project.
    /// </summary>
    public Guid? ExpectedLifetimeId { get; set; }

    /// <summary>
    /// Name of the project; required on save and trimmed.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the project; trimmed.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Goal of the project; trimmed.
    /// </summary>
    public string Objective { get; set; } = string.Empty;

    /// <summary>
    /// Lifecycle status, as a JSON integer: 0 Draft, 1 Active, 2 OnHold, 3 Completed, 4 Archived. Omitted means Draft.
    /// </summary>
    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    /// <summary>
    /// Name of the current phase as free text; trimmed. It is not linked to the <c>phases</c> list.
    /// </summary>
    public string CurrentPhase { get; set; } = string.Empty;

    /// <summary>
    /// Target completion date and time in UTC. A sent value with an offset is converted to UTC and a value without an
    /// offset is taken as UTC. Null means none.
    /// </summary>
    public DateTime? TargetDateUtc { get; set; }

    /// <summary>
    /// The project's phases in order. On save the list replaces the stored phases: phases missing from it are deleted.
    /// </summary>
    public List<ProjectPhaseEditorModel> Phases { get; set; } = [];

    /// <summary>
    /// The project's technology options. On save the list replaces the stored options: options missing from it are
    /// deleted. A read always includes an entry for each of the categories Language through Testing.
    /// </summary>
    public List<ProjectOptionEditorModel> Options { get; set; } = [];
}

public sealed partial class ProjectsService(
    IDbContextFactory<ProjectsDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge,
    IEnumerable<IProjectDeletionParticipant> deletionParticipants,
    ILogger<ProjectsService> logger,
    SearchIndexService searchMutationService,
    StorageCatalogService storageCatalogService,
    CoordinatedDatabaseTransaction coordinatedTransaction,
    ProjectWriteAdmissionService writeAdmissionService,
    IProjectCreationCompensationGuard? creationCompensationGuard = null) : IProjectSummaryQueryService {
    private const string DeleteRetryGuidance =
        "Retry each exact participant and recovery id returned by the deletion recovery; do not create or select a newer project-deletion operation.";

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
        ProjectMutationAuthorization? authorization = null)
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
            writeAdmissionService, coordinatedTransaction, cancellationToken);
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
        ProjectMutationAuthorization? authorization = null)
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
            writeAdmissionService, coordinatedTransaction, cancellationToken);
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

        var projectPhases = await dbContext.Set<ProjectPhase>()
            .Where(item => item.ProjectId == project.Id)
            .OrderBy(item => item.OrderIndex)
            .ToListAsync(cancellationToken);
        var phases = projectPhases
            .Select(item => new ProjectPhaseEditorModel
            {
                Id = item.Id,
                Name = item.Name,
                Goal = item.Goal,
                Status = item.Status,
                StartDateUtc = NormalizeNullableUtc(item.StartDateUtc),
                EndDateUtc = NormalizeNullableUtc(item.EndDateUtc)
            })
            .ToList();

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
            ExpectedLifetimeId = project.LifetimeId,
            Name = project.Name,
            Description = project.Description,
            Objective = project.Objective,
            Status = project.Status,
            CurrentPhase = project.CurrentPhase,
            TargetDateUtc = NormalizeNullableUtc(project.TargetDateUtc),
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

    public Task<Result<Guid>> CreateAsync(ProjectCreationReservation reservation, ProjectEditorModel model,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(reservation);
        if (model.Id.HasValue) {
            return Task.FromResult(Result<Guid>.Failure(Error.Validation("A new project cannot use an existing project id.")));
        }
        return SaveCoreAsync(model, parentProjectId: null, reservation.ProjectId, cancellationToken, reservation);
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

    private async Task<Result<ProjectSaveOutcome>> SaveWithReceiptCoreAsync(
        ProjectEditorModel model,
        Guid? parentProjectId,
        Guid? newProjectId,
        CancellationToken cancellationToken,
        ProjectCreationReservation? reservation = null,
        ProjectMutationAuthorization? authorization = null)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Result<ProjectSaveOutcome>.Failure(Error.Validation("Project name is required."));
        }

        var targetProjectId = model.Id ?? newProjectId ?? Guid.NewGuid();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await ProjectSourceMutationScope.BeginAsync(
            dbContext,
            parentProjectId.HasValue ? BuildProjectHierarchyMutationScopeKeys(targetProjectId, parentProjectId) : BuildProjectMutationScopeKeys(targetProjectId),
            model.Id.HasValue ? ProjectMutationPurpose.ExistingWrite : parentProjectId.HasValue ? ProjectMutationPurpose.CreateChild : ProjectMutationPurpose.CreateRoot,
            targetProjectId, model.Id.HasValue ? [targetProjectId] : parentProjectId.HasValue ? [parentProjectId.Value] : [],
            authorization, writeAdmissionService, coordinatedTransaction, cancellationToken, reservation);
        if (newProjectId.HasValue && await dbContext.Set<Project>()
                .AnyAsync(project => project.Id == newProjectId.Value, cancellationToken))
        {
            return Result<ProjectSaveOutcome>.Failure(Error.Failure(
                "The reserved project id is already in use.",
                ProjectErrorCodes.ReservedIdConflict));
        }

        Project? parentProject = null;
        if (parentProjectId.HasValue)
        {
            parentProject = await dbContext.Set<Project>()
                .FirstOrDefaultAsync(project => project.Id == parentProjectId.Value, cancellationToken);
            if (parentProject is null)
            {
                return Result<ProjectSaveOutcome>.Failure(Error.Validation("The selected parent project could not be found."));
            }
        }

        var entity = model.Id.HasValue
            ? await dbContext.Set<Project>().FirstOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        if (model.Id.HasValue && entity is null)
        {
            return Result<ProjectSaveOutcome>.Failure(Error.Failure(
                "The project no longer exists.",
                ProjectErrorCodes.NotFound));
        }

        if (entity is not null && model.ExpectedLifetimeId.HasValue && model.ExpectedLifetimeId.Value != entity.LifetimeId) {
            return Result<ProjectSaveOutcome>.Failure(Error.Failure(
                "This project belongs to a different lifetime. Reload it before saving changes.", ProjectErrorCodes.LifetimeChanged));
        }

        if (entity is null)
        {
            if (reservation is null && await dbContext.Set<ProjectCreationReservationRecord>().AnyAsync(record =>
                    record.ProjectId == targetProjectId && record.State == ProjectCreationReservationState.Reserved && record.ImportedHistory == null, cancellationToken)) {
                return Result<ProjectSaveOutcome>.Failure(Error.Failure("The project id belongs to a pending creation reservation.", ProjectErrorCodes.ReservedIdConflict));
            }
            if (reservation is not null && !await writeAdmissionService.TryConsumeCreationAsync(dbContext, reservation, parentProjectId, cancellationToken)) {
                return Result<ProjectSaveOutcome>.Failure(Error.Failure("The exact project creation reservation is closed or its parent lifetime has changed.", ProjectErrorCodes.ReservationClosed));
            }
            entity = new Project
            {
                Id = targetProjectId,
                CreatedAtUtc = clock.GetUtcNow()
            };

            if (reservation is not null) {
                entity.BindReservedLifetime(reservation.LifetimeId);
            }
            await dbContext.Set<Project>().AddAsync(entity, cancellationToken);
        }

        if (parentProject is not null)
        {
            await dbContext.Set<ProjectHierarchyLink>().AddAsync(
                new ProjectHierarchyLink
                {
                    ParentProjectId = parentProject.Id,
                    ChildProjectId = entity.Id,
                    CreatedAtUtc = clock.GetUtcNow()
                },
                cancellationToken);
        }

        entity.Name = model.Name.Trim();
        entity.Slug = FileSafeSlugBuilder.Build(model.Name);
        entity.Description = model.Description?.Trim() ?? string.Empty;
        entity.Objective = model.Objective?.Trim() ?? string.Empty;
        entity.Status = model.Status;
        entity.CurrentPhase = model.CurrentPhase?.Trim() ?? string.Empty;
        entity.TargetDateUtc = NormalizeNullableUtc(model.TargetDateUtc);
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
            phase.StartDateUtc = NormalizeNullableUtc(phaseModel.StartDateUtc);
            phase.EndDateUtc = NormalizeNullableUtc(phaseModel.EndDateUtc);
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
        var admission = writeAdmissionService.Capture(entity);
        var creationReceipt = model.Id.HasValue ? null : new ProjectCreationReceipt(admission, reservation,
            await ReadCreationFingerprintAsync(dbContext, admission, cancellationToken), authorization);
        await mutationScope.CommitAsync(cancellationToken);
        await mutationScope.DisposeAsync();
        await RunPostCommitActionAsync(
            "search-index-upsert",
            entity.Id,
            () => UpsertAdmittedSearchProjectionAsync(admission, new SearchDocumentInput(
                "project",
                entity.Id.ToString(),
                "Projects",
                entity.Name,
                entity.Description,
                $"{entity.Objective}\nCurrent phase: {entity.CurrentPhase}\nOptions: {string.Join(", ", model.Options.Where(option => !string.IsNullOrWhiteSpace(option.OptionName)).Select(option => $"{option.Category}:{option.OptionName}"))}",
                $"/projects?projectId={entity.Id}",
                entity.Id), cancellationToken));
        await RunPostCommitActionAsync(
            "activity-project-save",
            entity.Id,
            () => activityStream.RecordAsync(new ActivityWriteRequest(
                "projects",
                model.Id.HasValue ? "update" : "create",
                $"{(model.Id.HasValue ? "Updated" : "Created")} project",
                entity.Name,
                ProjectId: entity.Id,
                ArtifactKind: "project",
                ArtifactId: entity.Id,
                Route: $"/projects?projectId={entity.Id}"), cancellationToken));
        if (parentProject is not null)
        {
            await RunPostCommitActionAsync(
                "activity-subproject-attach",
                entity.Id,
                () => activityStream.RecordAsync(new ActivityWriteRequest(
                    "projects",
                    "attach-subproject",
                    "Created subproject",
                    $"{entity.Name} is now under {parentProject.Name}.",
                    ProjectId: entity.Id,
                    ArtifactKind: "project",
                    ArtifactId: entity.Id,
                    Route: $"/projects?projectId={entity.Id}"), cancellationToken));
        }

        return Result<ProjectSaveOutcome>.Success(new(entity.Id, creationReceipt));
    }

    private async Task UpsertAdmittedSearchProjectionAsync(ProjectWriteAdmission admission, SearchDocumentInput input,
        CancellationToken cancellationToken) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext, ProjectMutationScopeKeys.ForProject(admission.ProjectId), cancellationToken);
        await writeAdmissionService.RequireAsync(dbContext, admission, cancellationToken);
        using (coordinatedTransaction.Enter(dbContext)) {
            await searchIndexService.UpsertForMutationAsync(input, cancellationToken);
        }
        await mutationScope.CommitAsync(cancellationToken);
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
        ProjectCreationReceipt? creationReceipt = null, ProjectWriteAdmission? expectedProjectAdmission = null)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var orderedParticipants = GetOrderedDeletionParticipants();
        var mutationScopeKeys = ResolveDeletionPreparationScopeKeys(orderedParticipants)
            .Append(ProjectMutationScopeKeys.ForProject(id))
            .Append(ProjectMutationScopeKeys.Hierarchy)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        await using var mutationScope = await ProjectSourceMutationScope.BeginAsync(dbContext, mutationScopeKeys,
            creationReceipt?.Reservation?.ParentProjectId is not null ? ProjectMutationPurpose.CreateChild :
                creationReceipt is not null ? ProjectMutationPurpose.CreateRoot : ProjectMutationPurpose.ExistingWrite,
            id, creationReceipt?.Authorization?.ExpectedProjects.Select(project => project.ProjectId).ToArray() ?? [],
            creationReceipt?.Authorization, writeAdmissionService, coordinatedTransaction, cancellationToken, creationReceipt?.Reservation);
        var project = await dbContext.Set<Project>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (expectedProjectAdmission is not null) {
            ProjectAssignmentAdmission.Require(id, expectedProjectAdmission);
            await writeAdmissionService.RequireAsync(dbContext, expectedProjectAdmission, cancellationToken);
        }
        if (creationReceipt is not null && !await CanCompensateCreationAsync(dbContext, creationReceipt, cancellationToken)) {
            return null;
        }
        var preparedParticipants = new List<(
            IProjectDeletionParticipant Participant,
            ProjectDeletionParticipantPreparation Preparation)>();
        using (coordinatedTransaction.Enter(dbContext)) {
            foreach (var participant in orderedParticipants)
            {
                var preparation = await participant.PrepareAsync(id, cancellationToken);
                if (preparation is null)
                {
                    continue;
                }

                if (preparation.RecoveryId == Guid.Empty)
                {
                    throw new InvalidOperationException(
                        $"Project deletion participant '{participant.Id}' returned an empty recovery id.");
                }

                if (preparation.ProjectId != id)
                {
                    throw new InvalidOperationException(
                        $"Project deletion participant '{participant.Id}' returned recovery state for another project.");
                }

                preparedParticipants.Add((participant, preparation));
            }

            if (creationReceipt?.Reservation is { } originalReservation) {
                await writeAdmissionService.CancelConsumedCreationForCompensationAsync(dbContext, originalReservation, cancellationToken);
            }
            await ProjectWriteAdmissionService.CancelCreationForDeletionAsync(dbContext, id, cancellationToken);
            if (project is not null)
            {
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
        await mutationScope.DisposeAsync();

        if (project is not null)
        {
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

        var failures = new List<ProjectDeletionRecoveryFailure>();
        var failureExceptions = new List<Exception>();
        var warnings = new List<ProjectDeletionWarning>();
        foreach (var (participant, preparation) in preparedParticipants)
        {
            try
            {
                var completion = await participant.CompleteAsync(preparation, cancellationToken);
                ValidateParticipantCompletion(participant, preparation, completion);
                warnings.AddRange(completion.Warnings.Select(warning => new ProjectDeletionWarning(
                    warning.Kind,
                    participant.Id,
                    completion.RecoveryId,
                    warning.RetainedObject,
                    warning.Message,
                    warning.Remediation)));
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Project {ProjectId} was deleted, but participant {Participant} failed cleanup for recovery {RecoveryId}.",
                    id,
                    participant.Id,
                    preparation.RecoveryId);
                var failedRecoveryId = exception is ProjectDeletionParticipantCleanupException cleanupException
                    ? cleanupException.RecoveryId
                    : preparation.RecoveryId;
                failures.Add(new ProjectDeletionRecoveryFailure(
                    ProjectDeletionRecoveryOperation.ParticipantCleanup,
                    participant.Id,
                    failedRecoveryId));
                failureExceptions.Add(exception);
            }
        }

        await RecordDeletionWarningsAsync(id, warnings, cancellationToken);

        if (failures.Count == 0)
        {
            return new ProjectDeletionResult(id, warnings);
        }

        var recovery = new ProjectDeletionRecovery(id, failures, DeleteRetryGuidance);
        var innerException = failureExceptions.Count == 1
            ? failureExceptions[0]
            : new AggregateException(failureExceptions);
        throw new ProjectDeletionPartialCommitException(
            recovery,
            $"Project '{id:D}' was deleted, but cleanup is incomplete. {DeleteRetryGuidance}",
            innerException);
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
