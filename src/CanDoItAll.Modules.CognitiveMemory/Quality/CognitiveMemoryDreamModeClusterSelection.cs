namespace CanDoItAll.Modules.CognitiveMemory;

public interface ICognitiveMemoryDreamModeClusterSelector
{
    CognitiveMemoryDreamModeSelectionPolicy ResolvePolicy(CognitiveMemoryConsolidationMode mode);

    CognitiveMemoryClusterPlanningScope ResolvePlanningScope(CognitiveMemoryDreamRunRequest request);

    bool IsSelected(CognitiveMemoryConsolidationMode mode, CognitiveMemoryClusterPlan cluster);

    string ResolveSelectionReasonCode(CognitiveMemoryConsolidationMode mode, CognitiveMemoryClusterPlan cluster);
}

public sealed class CognitiveMemoryDreamModeClusterSelector : ICognitiveMemoryDreamModeClusterSelector
{
    public static readonly CognitiveMemoryDreamModeClusterSelector Instance = new();

    public CognitiveMemoryDreamModeSelectionPolicy ResolvePolicy(CognitiveMemoryConsolidationMode mode)
        => mode switch
        {
            CognitiveMemoryConsolidationMode.ProjectNightly => new(mode, "dream.project-nightly"),
            CognitiveMemoryConsolidationMode.CrossProjectWeekly => new(mode, "dream.cross-project-weekly"),
            CognitiveMemoryConsolidationMode.ProcedureMining => new(mode, "dream.procedure-mining"),
            CognitiveMemoryConsolidationMode.FailureLearning => new(mode, "dream.failure-learning"),
            CognitiveMemoryConsolidationMode.KnowledgeCoverageRefresh => new(mode, "dream.knowledge-coverage-refresh"),
            CognitiveMemoryConsolidationMode.EpistemicDriveScan => new(mode, "dream.epistemic-drive-scan"),
            CognitiveMemoryConsolidationMode.LearningOpportunityReview => new(mode, "dream.learning-opportunity-review"),
            CognitiveMemoryConsolidationMode.IncrementalRecent => throw new ArgumentException("Dream consolidation must be explicit and must not run through the incremental profile.", nameof(mode)),
            _ => throw new NotSupportedException($"Consolidation mode '{mode}' is not supported by dream consolidation.")
        };

    public CognitiveMemoryClusterPlanningScope ResolvePlanningScope(CognitiveMemoryDreamRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Mode == CognitiveMemoryConsolidationMode.CrossProjectWeekly && request.ProjectId is null)
        {
            return CognitiveMemoryClusterPlanningScope.PolicyConstrainedCrossProject;
        }

        return request.ProjectId is null
            ? CognitiveMemoryClusterPlanningScope.Global
            : CognitiveMemoryClusterPlanningScope.ProjectOnly;
    }

    public bool IsSelected(CognitiveMemoryConsolidationMode mode, CognitiveMemoryClusterPlan cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        return mode switch
        {
            CognitiveMemoryConsolidationMode.ProjectNightly => cluster.Readiness is CognitiveMemoryQualityClusterReadiness.AggregateReady
                or CognitiveMemoryQualityClusterReadiness.NeedsHumanReview
                or CognitiveMemoryQualityClusterReadiness.Contradictory
                or CognitiveMemoryQualityClusterReadiness.Restricted,
            CognitiveMemoryConsolidationMode.CrossProjectWeekly => cluster.Readiness == CognitiveMemoryQualityClusterReadiness.AggregateReady &&
                                                                   cluster.QualityMetrics.AggregateEligible &&
                                                                   HasMultipleSourceProjects(cluster),
            CognitiveMemoryConsolidationMode.ProcedureMining => HasKey(cluster, CognitiveMemoryQualityClusterKeyFamily.TaskIntent, "procedure") ||
                                                                HasKey(cluster, CognitiveMemoryQualityClusterKeyFamily.TaskIntent, "workflow"),
            CognitiveMemoryConsolidationMode.FailureLearning => HasKey(cluster, CognitiveMemoryQualityClusterKeyFamily.TaskIntent, "failure") ||
                                                                cluster.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory,
            CognitiveMemoryConsolidationMode.KnowledgeCoverageRefresh => cluster.Readiness is CognitiveMemoryQualityClusterReadiness.NeedsMoreEvidence
                or CognitiveMemoryQualityClusterReadiness.NeedsHumanReview,
            CognitiveMemoryConsolidationMode.EpistemicDriveScan => cluster.Readiness is CognitiveMemoryQualityClusterReadiness.NeedsMoreEvidence
                or CognitiveMemoryQualityClusterReadiness.Contradictory,
            CognitiveMemoryConsolidationMode.LearningOpportunityReview => HasKey(cluster, CognitiveMemoryQualityClusterKeyFamily.TaskIntent, "testing") ||
                                                                          HasKey(cluster, CognitiveMemoryQualityClusterKeyFamily.TaskIntent, "coverage") ||
                                                                          cluster.Readiness == CognitiveMemoryQualityClusterReadiness.NeedsHumanReview,
            _ => false
        };
    }

    public string ResolveSelectionReasonCode(CognitiveMemoryConsolidationMode mode, CognitiveMemoryClusterPlan cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        return mode switch
        {
            CognitiveMemoryConsolidationMode.ProjectNightly => "dream.project-nightly.aggregate-ready",
            CognitiveMemoryConsolidationMode.CrossProjectWeekly => "dream.cross-project-weekly.policy-constrained-cross-project",
            CognitiveMemoryConsolidationMode.ProcedureMining => "dream.procedure-mining.task-intent",
            CognitiveMemoryConsolidationMode.FailureLearning => cluster.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory
                ? "dream.failure-learning.contradiction"
                : "dream.failure-learning.incident",
            CognitiveMemoryConsolidationMode.KnowledgeCoverageRefresh => "dream.knowledge-coverage.refresh",
            CognitiveMemoryConsolidationMode.EpistemicDriveScan => "dream.epistemic-drive.scan",
            CognitiveMemoryConsolidationMode.LearningOpportunityReview => "dream.learning-opportunity.review",
            _ => ResolvePolicy(mode).ReasonCode
        };
    }

    private static bool HasKey(
        CognitiveMemoryClusterPlan cluster,
        CognitiveMemoryQualityClusterKeyFamily family,
        string value)
        => cluster.Keys.Any(key => key.Family == family && key.Key.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static bool HasMultipleSourceProjects(CognitiveMemoryClusterPlan cluster)
        => cluster.Members
            .Where(member => member.MemberKind == CognitiveMemoryQualityClusterMemberKind.MemoryRecord)
            .Select(member => member.ProjectId)
            .Where(projectId => projectId.HasValue)
            .Distinct()
            .Take(2)
            .Count() == 2;
}

public sealed record CognitiveMemoryDreamModeSelectionPolicy(
    CognitiveMemoryConsolidationMode Mode,
    string ReasonCode);
