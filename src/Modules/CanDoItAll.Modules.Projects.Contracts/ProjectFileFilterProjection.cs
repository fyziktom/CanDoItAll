using CanDoItAll.SharedKernel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Modules.Projects;

public enum ProjectHierarchyFilterMode
{
    Descendants,
    Children,
    Parents,
    Related
}

public sealed record ProjectFileFilter
{
    public const int MaximumTextLength = 512;

    public ProjectFileFilter(
        string? search = null,
        ProjectStatus? status = null,
        ProjectPartyPortfolioCategory? relatedPartyCategory = null,
        string? relatedPartyValue = null,
        Guid? hierarchyProjectId = null,
        ProjectHierarchyFilterMode hierarchyMode = ProjectHierarchyFilterMode.Descendants,
        bool includeSubprojects = true)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (relatedPartyCategory.HasValue && !Enum.IsDefined(relatedPartyCategory.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(relatedPartyCategory));
        }

        if (!Enum.IsDefined(hierarchyMode))
        {
            throw new ArgumentOutOfRangeException(nameof(hierarchyMode));
        }

        if (hierarchyProjectId == Guid.Empty)
        {
            throw new ArgumentException("A hierarchy project identifier cannot be empty.", nameof(hierarchyProjectId));
        }

        Search = NormalizeText(search, nameof(search));
        Status = status;
        RelatedPartyCategory = relatedPartyCategory;
        RelatedPartyValue = NormalizeText(relatedPartyValue, nameof(relatedPartyValue));
        HierarchyProjectId = hierarchyProjectId;
        HierarchyMode = hierarchyMode;
        IncludeSubprojects = includeSubprojects;
    }

    public string Search { get; }

    public ProjectStatus? Status { get; }

    public ProjectPartyPortfolioCategory? RelatedPartyCategory { get; }

    public string RelatedPartyValue { get; }

    public Guid? HierarchyProjectId { get; }

    public ProjectHierarchyFilterMode HierarchyMode { get; }

    public bool IncludeSubprojects { get; }

    private static string NormalizeText(string? value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > MaximumTextLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return normalized;
    }
}

public sealed record ProjectFileFilterProjection
{
    private const int MaximumProjectCount = 10_000;
    private const int MaximumHierarchyLinkCount = 100_000;

    private ProjectFileFilterProjection(
        ProjectFileFilter filter,
        IReadOnlyList<ProjectSummary> projects,
        IReadOnlyList<string> availableRelatedPartyValues,
        string fingerprint)
    {
        Filter = filter;
        Projects = projects;
        OrderedProjectIds = projects.Select(project => project.Id).ToArray();
        AvailableRelatedPartyValues = availableRelatedPartyValues;
        Fingerprint = fingerprint;
    }

    public ProjectFileFilter Filter { get; }

    public IReadOnlyList<ProjectSummary> Projects { get; }

    public IReadOnlyList<Guid> OrderedProjectIds { get; }

    public IReadOnlyList<string> AvailableRelatedPartyValues { get; }

    public string Fingerprint { get; }

    public static ProjectFileFilterProjection Create(
        IReadOnlyList<ProjectSummary> projects,
        IReadOnlyList<ProjectHierarchyLinkSummary> hierarchyLinks,
        ProjectFileFilter filter)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(hierarchyLinks);
        ArgumentNullException.ThrowIfNull(filter);
        if (projects.Count > MaximumProjectCount || hierarchyLinks.Count > MaximumHierarchyLinkCount)
        {
            throw new ArgumentOutOfRangeException(nameof(projects), "The project portfolio exceeds its bounded projection contract.");
        }

        var projectsById = new Dictionary<Guid, ProjectSummary>(projects.Count);
        foreach (ProjectSummary project in projects)
        {
            if (!projectsById.TryAdd(project.Id, project))
            {
                throw new ArgumentException("The project portfolio contains duplicate identifiers.", nameof(projects));
            }
        }

        HashSet<Guid> hierarchyProjectIds = BuildHierarchyProjectIds(
            projectsById,
            hierarchyLinks,
            filter);
        ProjectSummary[] filtered = projects
            .Where(project => hierarchyProjectIds.Contains(project.Id) && Matches(project, filter))
            .OrderByDescending(project => project.UpdatedAtUtc)
            .ThenBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(project => project.Id)
            .ToArray();
        string[] relatedPartyValues = projects
            .SelectMany(project => project.RelatedParties ?? [])
            .Where(item => !filter.RelatedPartyCategory.HasValue || item.Category == filter.RelatedPartyCategory.Value)
            .Select(item => item.DisplayName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ProjectFileFilterProjection(
            filter,
            filtered,
            relatedPartyValues,
            BuildFingerprint(filter, filtered));
    }

    private static HashSet<Guid> BuildHierarchyProjectIds(
        IReadOnlyDictionary<Guid, ProjectSummary> projectsById,
        IReadOnlyList<ProjectHierarchyLinkSummary> hierarchyLinks,
        ProjectFileFilter filter)
    {
        if (!filter.HierarchyProjectId.HasValue)
        {
            var childIds = hierarchyLinks
                .Select(link => link.ChildProjectId)
                .ToHashSet();
            return projectsById.Values
                .Where(project => project.ParentCount == 0 && !childIds.Contains(project.Id))
                .Select(project => project.Id)
                .ToHashSet();
        }

        Guid selectedProjectId = filter.HierarchyProjectId.Value;
        if (!projectsById.ContainsKey(selectedProjectId))
        {
            return [];
        }

        return filter.HierarchyMode switch
        {
            ProjectHierarchyFilterMode.Descendants when filter.IncludeSubprojects
                => BuildDescendantClosure(selectedProjectId, projectsById, hierarchyLinks),
            ProjectHierarchyFilterMode.Descendants => [selectedProjectId],
            ProjectHierarchyFilterMode.Children => hierarchyLinks
                .Where(link => link.ParentProjectId == selectedProjectId && projectsById.ContainsKey(link.ChildProjectId))
                .Select(link => link.ChildProjectId)
                .ToHashSet(),
            ProjectHierarchyFilterMode.Parents => hierarchyLinks
                .Where(link => link.ChildProjectId == selectedProjectId && projectsById.ContainsKey(link.ParentProjectId))
                .Select(link => link.ParentProjectId)
                .ToHashSet(),
            ProjectHierarchyFilterMode.Related => hierarchyLinks
                .Where(link => link.ParentProjectId == selectedProjectId || link.ChildProjectId == selectedProjectId)
                .Select(link => link.ParentProjectId == selectedProjectId ? link.ChildProjectId : link.ParentProjectId)
                .Where(projectsById.ContainsKey)
                .ToHashSet(),
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };
    }

    private static HashSet<Guid> BuildDescendantClosure(
        Guid projectId,
        IReadOnlyDictionary<Guid, ProjectSummary> projectsById,
        IReadOnlyList<ProjectHierarchyLinkSummary> hierarchyLinks)
    {
        Dictionary<Guid, Guid[]> childrenByParent = hierarchyLinks
            .Where(link => projectsById.ContainsKey(link.ParentProjectId) && projectsById.ContainsKey(link.ChildProjectId))
            .GroupBy(link => link.ParentProjectId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(link => link.ChildProjectId).Distinct().ToArray());
        var projectIds = new HashSet<Guid> { projectId };
        var pending = new Stack<Guid>();
        pending.Push(projectId);
        while (pending.TryPop(out Guid currentProjectId))
        {
            if (!childrenByParent.TryGetValue(currentProjectId, out Guid[]? childProjectIds))
            {
                continue;
            }

            foreach (Guid childProjectId in childProjectIds)
            {
                if (projectIds.Add(childProjectId))
                {
                    pending.Push(childProjectId);
                }
            }
        }

        return projectIds;
    }

    private static bool Matches(ProjectSummary project, ProjectFileFilter filter)
    {
        if (!string.IsNullOrEmpty(filter.Search) &&
            !project.Name.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) &&
            !project.CurrentPhase.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) &&
            !project.RelatedPartySearchText.Contains(filter.Search, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Status.HasValue && project.Status != filter.Status.Value)
        {
            return false;
        }

        IReadOnlyList<ProjectPortfolioPartyItem> relatedParties = project.RelatedParties ?? [];
        if (filter.RelatedPartyCategory.HasValue &&
            !relatedParties.Any(item => item.Category == filter.RelatedPartyCategory.Value))
        {
            return false;
        }

        return string.IsNullOrEmpty(filter.RelatedPartyValue) ||
               relatedParties.Any(item => string.Equals(
                   item.DisplayName,
                   filter.RelatedPartyValue,
                   StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildFingerprint(
        ProjectFileFilter filter,
        IReadOnlyList<ProjectSummary> projects)
    {
        IEnumerable<string> projectIds = projects.Select(project => project.Id.ToString("N"));
        string canonical = string.Join('\n',
            [
                "project-file-filter-v1",
                HashText(filter.Search),
                filter.Status.HasValue ? ((int)filter.Status.Value).ToString(CultureInfo.InvariantCulture) : "none",
                filter.RelatedPartyCategory.HasValue
                    ? ((int)filter.RelatedPartyCategory.Value).ToString(CultureInfo.InvariantCulture)
                    : "none",
                HashText(filter.RelatedPartyValue),
                filter.HierarchyProjectId?.ToString("N") ?? "none",
                ((int)filter.HierarchyMode).ToString(CultureInfo.InvariantCulture),
                filter.IncludeSubprojects ? "1" : "0",
                .. projectIds
            ]);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string HashText(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
