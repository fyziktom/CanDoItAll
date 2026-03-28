namespace CanDoItAll.Modules.Workspace;

public enum ProjectManagementKnowledgeCategory
{
    Mission,
    Planning,
    Estimation,
    Approval,
    Reporting,
    Risk,
    Collaboration
}

public sealed record ProjectManagementKnowledgeQuery(
    IReadOnlyList<ProjectManagementKnowledgeCategory>? Categories = null,
    string? Query = null,
    int Take = 10);

public sealed record ProjectManagementKnowledgeEntry(
    string Id,
    ProjectManagementKnowledgeCategory Category,
    string Title,
    string Summary,
    string Guidance,
    IReadOnlyList<string> Tags,
    bool IsMissionAnchor);

public interface IProjectManagementKnowledgeProvider
{
    Task<IReadOnlyList<ProjectManagementKnowledgeEntry>> QueryAsync(ProjectManagementKnowledgeQuery query, CancellationToken cancellationToken = default);
}

public sealed class StaticProjectManagementKnowledgeProvider : IProjectManagementKnowledgeProvider
{
    private static readonly IReadOnlyList<ProjectManagementKnowledgeEntry> Entries =
    [
        new(
            "mission-purpose",
            ProjectManagementKnowledgeCategory.Mission,
            "Projects exist to improve life around people",
            "Keep the human purpose of project work explicit when discussing plans, priorities, and tradeoffs.",
            "Humans are trying to make their surrounding better and help others around. That is why they are doing any of their projects. If project is sucessfull, then surrounding is happy and human is happy. That is why we must always find the way how to make project good and sucessfull. Main goal is to fullfill this mission no matter what project we are working on.",
            ["mission", "purpose", "impact"],
            true),
        new(
            "planning-split-work",
            ProjectManagementKnowledgeCategory.Planning,
            "Split work into reviewable deliverables",
            "Break plans into concrete deliverables with prerequisites, decision points, and visible proof targets.",
            "A useful plan separates foundations from dependent work, records prerequisites explicitly, and names the proof that will allow the next phase to start.",
            ["planning", "roadmap", "dependencies"],
            false),
        new(
            "estimation-explicit-uncertainty",
            ProjectManagementKnowledgeCategory.Estimation,
            "State uncertainty with the estimate",
            "Time estimates should explain the confidence level and the unknowns that could move the number.",
            "Use estimates to drive approval and sequencing, not to pretend uncertainty does not exist. Call out blockers, external dependencies, and discovery work explicitly.",
            ["estimation", "approval", "risk"],
            false),
        new(
            "approval-before-high-cost",
            ProjectManagementKnowledgeCategory.Approval,
            "Escalate expensive or risky work before execution",
            "High-cost or high-risk work should produce an approval request with scope, estimate, risk, and rollback context.",
            "Approval records should say what is being requested, why it matters, the expected time cost, the affected project area, and what happens if the change is delayed.",
            ["approval", "governance", "risk"],
            false),
        new(
            "reporting-actionable-state",
            ProjectManagementKnowledgeCategory.Reporting,
            "Status reports must say what changed and what is blocked",
            "Reporting is only useful when it names shipped proof, current blockers, and the next decision or action required.",
            "A good status update identifies delivered work, unresolved risks, required approvals, and the next concrete step instead of vague progress language.",
            ["reporting", "status", "analytics"],
            false),
        new(
            "risk-surface-shared-parts",
            ProjectManagementKnowledgeCategory.Risk,
            "Look for shared parts early",
            "Cross-project analysis should highlight shared dependencies and collision risks before they become merge or rollout problems.",
            "When two efforts touch the same repository, branch, asset lineage, or central workflow, record the shared part early and reserve or coordinate it instead of discovering the conflict late.",
            ["risk", "shared-parts", "coordination"],
            false),
        new(
            "collaboration-write-back",
            ProjectManagementKnowledgeCategory.Collaboration,
            "Write important context back into the structure",
            "Plans, decisions, trouble reports, and approval requests should live close to the project nodes they affect.",
            "Do not leave critical execution context trapped in chat. Record status, blockers, decisions, and approval requests in the project structure so the next agent or operator can continue safely.",
            ["collaboration", "documentation", "handoff"],
            false)
    ];

    public Task<IReadOnlyList<ProjectManagementKnowledgeEntry>> QueryAsync(ProjectManagementKnowledgeQuery query, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var take = Math.Clamp(query.Take, 1, 50);
        var results = Entries.AsEnumerable();

        if (query.Categories is { Count: > 0 })
        {
            var categories = query.Categories.ToHashSet();
            results = results.Where(entry => categories.Contains(entry.Category));
        }

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var needle = query.Query.Trim();
            results = results.Where(entry =>
                entry.Title.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                entry.Summary.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                entry.Guidance.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                entry.Tags.Any(tag => tag.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        }

        return Task.FromResult<IReadOnlyList<ProjectManagementKnowledgeEntry>>(results
            .OrderByDescending(entry => entry.IsMissionAnchor)
            .ThenBy(entry => entry.Category)
            .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList());
    }
}

public sealed class ProjectManagementKnowledgeService(IProjectManagementKnowledgeProvider provider)
{
    public Task<IReadOnlyList<ProjectManagementKnowledgeEntry>> QueryAsync(ProjectManagementKnowledgeQuery query, CancellationToken cancellationToken = default)
    {
        return provider.QueryAsync(query, cancellationToken);
    }
}
