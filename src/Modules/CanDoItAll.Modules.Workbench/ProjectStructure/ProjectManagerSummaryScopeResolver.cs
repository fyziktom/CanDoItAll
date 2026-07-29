using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectManagerSummaryScopeResolver
{
    public const int LargeScopeWarningThreshold =
        ProjectManagerSummaryScopePolicy.ConfirmationDescendantCount;

    private readonly IDbContextFactory<AppDbContext> dbContextFactory;
    private readonly ProjectPlanAnalyticsQueryService planAnalytics;
    private readonly ProjectManagerSummaryScopeLimits limits;

    public ProjectManagerSummaryScopeResolver(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ProjectPlanAnalyticsQueryService planAnalytics)
        : this(
            dbContextFactory,
            planAnalytics,
            ProjectManagerSummaryScopeLimits.Default)
    {
    }

    internal ProjectManagerSummaryScopeResolver(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ProjectPlanAnalyticsQueryService planAnalytics,
        ProjectManagerSummaryScopeLimits limits)
    {
        this.dbContextFactory = dbContextFactory;
        this.planAnalytics = planAnalytics;
        this.limits = limits.Validate();
    }

    public async Task<ProjectManagerSummaryScopeResolution> ResolveAsync(
        Guid projectId,
        ProjectManagerSummaryScope scope,
        ProjectManagerSummaryContentMode contentMode,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project identifier is required.", nameof(projectId));
        }

        var planMode = ResolvePlanMode(contentMode);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var projectName = await dbContext.Set<Project>()
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.Name)
            .SingleOrDefaultAsync(cancellationToken);
        if (projectName is null)
        {
            throw new ProjectStructureAgentException(
                404,
                "ProjectNotFound",
                $"Project '{projectId:D}' was not found.");
        }

        if (scope == ProjectManagerSummaryScope.UncategorizedAgentActivity)
        {
            return new ProjectManagerSummaryScopeResolution(
                projectId,
                projectName,
                scope,
                [],
                DescendantCount: 0,
                RequiresConfirmation: false);
        }

        IReadOnlyList<Guid> projectIds;
        var descendantCount = 0;
        if (scope == ProjectManagerSummaryScope.CurrentProject)
        {
            projectIds = [projectId];
        }
        else if (scope == ProjectManagerSummaryScope.ProjectAndDescendants)
        {
            projectIds = await ResolveDescendantsAsync(
                dbContext,
                projectId,
                cancellationToken);
            descendantCount = projectIds.Count - 1;
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                nameof(scope),
                scope,
                "Unsupported manager summary scope.");
        }

        var planPreflight = await planAnalytics.PreflightManagerSummaryAsync(
            projectIds,
            planMode,
            cancellationToken);
        return new ProjectManagerSummaryScopeResolution(
            projectId,
            projectName,
            scope,
            projectIds,
            descendantCount,
            descendantCount >= limits.ConfirmationDescendantCount ||
            planPreflight.RequiresConfirmation)
        {
            PlanPreflight = planPreflight
        };
    }

    private static ProjectPlanManagerSummaryMode ResolvePlanMode(
        ProjectManagerSummaryContentMode contentMode)
    {
        return contentMode switch
        {
            ProjectManagerSummaryContentMode.HistoryOnly =>
                ProjectPlanManagerSummaryMode.ScheduleOnly,
            ProjectManagerSummaryContentMode.HistoryAndFuture =>
                ProjectPlanManagerSummaryMode.ScheduleAndRemainingCosts,
            _ => throw new ArgumentOutOfRangeException(
                nameof(contentMode),
                contentMode,
                "Unsupported manager summary content mode.")
        };
    }

    private async Task<IReadOnlyList<Guid>> ResolveDescendantsAsync(
        AppDbContext dbContext,
        Guid rootProjectId,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { rootProjectId };
        Guid[] frontier = [rootProjectId];
        while (frontier.Length > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nextFrontier = new HashSet<Guid>();
            foreach (var parentBatch in frontier.Chunk(limits.HierarchyFrontierBatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remainingProjectCapacity = limits.MaximumProjectCount - visited.Count;
                var visitedProjectIds = visited.ToArray();
                var childProjectIds = await dbContext.Set<ProjectHierarchyLink>()
                    .AsNoTracking()
                    .Where(link =>
                        parentBatch.Contains(link.ParentProjectId) &&
                        !visitedProjectIds.Contains(link.ChildProjectId))
                    .Select(link => link.ChildProjectId)
                    .Distinct()
                    .OrderBy(childProjectId => childProjectId)
                    .Take(remainingProjectCapacity + 1)
                    .ToArrayAsync(cancellationToken);
                foreach (var childProjectId in childProjectIds)
                {
                    if (!visited.Add(childProjectId))
                    {
                        continue;
                    }

                    if (visited.Count > limits.MaximumProjectCount)
                    {
                        throw CreateScopeLimitExceeded(rootProjectId, visited.Count);
                    }

                    nextFrontier.Add(childProjectId);
                }
            }

            frontier = nextFrontier.ToArray();
        }

        return visited
            .Where(projectId => projectId != rootProjectId)
            .OrderBy(static projectId => projectId)
            .Prepend(rootProjectId)
            .ToArray();
    }

    private ProjectStructureAgentException CreateScopeLimitExceeded(
        Guid rootProjectId,
        int observedProjectCount)
    {
        return new ProjectStructureAgentException(
            413,
            ProjectPlanAnalyticsErrorCodes.ScopeLimitExceeded,
            $"Project '{rootProjectId:D}' has more than the hard manager-summary scope limit of " +
            $"{limits.MaximumProjectCount:N0} projects. Select the current project or a smaller subtree.",
            new ProjectPlanAnalyticsLimitDetails(
                observedProjectCount,
                PlanNodeCount: null,
                PlanLinkCount: null,
                limits.MaximumProjectCount,
                ProjectPlanAnalyticsPayloadPolicy.MaximumNodeCount,
                ProjectPlanAnalyticsPayloadPolicy.MaximumLinkCount));
    }
}

internal sealed record ProjectManagerSummaryScopeLimits(
    int ConfirmationDescendantCount,
    int MaximumProjectCount,
    int HierarchyFrontierBatchSize)
{
    public static ProjectManagerSummaryScopeLimits Default { get; } = new(
        ProjectManagerSummaryScopePolicy.ConfirmationDescendantCount,
        ProjectManagerSummaryScopePolicy.MaximumProjectCount,
        ProjectManagerSummaryScopePolicy.HierarchyFrontierBatchSize);

    public ProjectManagerSummaryScopeLimits Validate()
    {
        if (ConfirmationDescendantCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ConfirmationDescendantCount),
                ConfirmationDescendantCount,
                "The project-scope confirmation threshold cannot be negative.");
        }

        if (MaximumProjectCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumProjectCount),
                MaximumProjectCount,
                "The maximum project scope must be positive.");
        }

        if (HierarchyFrontierBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(HierarchyFrontierBatchSize),
                HierarchyFrontierBatchSize,
                "The hierarchy frontier batch size must be positive.");
        }

        return this;
    }
}
