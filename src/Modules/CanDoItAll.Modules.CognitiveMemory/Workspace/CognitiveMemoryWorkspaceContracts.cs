using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryWorkspaceFrameKind
{
    Unknown = 0,
    UserConversation = 1,
    AgentRun = 2,
    WorkflowRun = 3,
    ProcessStep = 4,
    ProbeSession = 5,
    ReviewSession = 6,
    LearningTask = 7
}

public enum CognitiveMemoryWorkspaceFrameStatus
{
    Active = 0,
    Expired = 1,
    Archived = 2
}

public enum CognitiveMemoryWorkingMemorySlotKind
{
    Unknown = 0,
    MemoryRecord = 1,
    Claim = 2,
    SourceItem = 3,
    ProcedureSkill = 4,
    RecallTrace = 5,
    ProbeTurn = 6,
    WorkflowArtifact = 7,
    OpenQuestion = 8,
    ExternalSourcePlaceholder = 9
}

public enum CognitiveMemoryWorkspaceOpenQuestionStatus
{
    Open = 0,
    Answered = 1,
    Cancelled = 2
}

public enum CognitiveMemoryWorkspaceSourceSufficiency
{
    Unknown = 0,
    Missing = 1,
    Weak = 2,
    Sufficient = 3
}

public enum CognitiveMemoryFocusInclusionReasonKind
{
    GoalMatch = 0,
    SourceSufficient = 1,
    WorkspaceCarryover = 2,
    RecallSelected = 3,
    ProbeSelected = 4,
    ReviewSelected = 5
}

public enum CognitiveMemoryInhibitionReasonKind
{
    ContextBoundary = 0,
    BudgetLimit = 1,
    AccessPolicy = 2,
    SourceInsufficient = 3,
    ContradictionRisk = 4,
    Stale = 5,
    RequiresClarification = 6
}

public enum CognitiveMemoryAttentionDecisionKind
{
    Unknown = 0,
    Recall = 1,
    AnswerFromWorkspace = 2,
    AskClarification = 3,
    RunSourceAudit = 4,
    StartProbe = 5,
    CreateReviewItem = 6,
    RequestLearningProposal = 7,
    RunReplay = 8,
    Abstain = 9
}

public enum CognitiveMemoryAttentionReasonKind
{
    ScoreShapeMatched = 0,
    RequiredOperation = 1,
    MissingRequiredDimensions = 2,
    NoSafeOperation = 3
}

public readonly record struct CognitiveMemoryWorkspaceFrameId
{
    [JsonConstructor]
    public CognitiveMemoryWorkspaceFrameId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryWorkspaceFrameId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryWorkspaceSlotId
{
    [JsonConstructor]
    public CognitiveMemoryWorkspaceSlotId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryWorkspaceSlotId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryWorkspaceOpenQuestionId
{
    [JsonConstructor]
    public CognitiveMemoryWorkspaceOpenQuestionId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryWorkspaceOpenQuestionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryAttentionDecisionId
{
    [JsonConstructor]
    public CognitiveMemoryAttentionDecisionId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryAttentionDecisionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryWorkspaceGoalKey
{
    [JsonConstructor]
    public CognitiveMemoryWorkspaceGoalKey(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemoryWorkspaceExternalKey
{
    [JsonConstructor]
    public CognitiveMemoryWorkspaceExternalKey(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record CognitiveMemoryWorkspaceScope
{
    public CognitiveMemoryWorkspaceScope(
        Guid projectId,
        CognitiveMemoryWorkspaceFrameKind frameKind,
        string? ownerUserId = null,
        string? ownerAgentId = null,
        Guid? processRunId = null,
        Guid? workflowRunId = null,
        Guid? processStepId = null,
        Guid? probeSessionId = null,
        Guid? reviewSessionId = null,
        Guid? learningTaskId = null)
    {
        CognitiveMemoryGuard.EnsureNonEmpty(projectId, nameof(projectId));
        if (frameKind == CognitiveMemoryWorkspaceFrameKind.Unknown)
        {
            throw new ArgumentException("Workspace frame kind must be explicit.", nameof(frameKind));
        }

        ProjectId = projectId;
        FrameKind = frameKind;
        OwnerUserId = NormalizeOptional(ownerUserId);
        OwnerAgentId = NormalizeOptional(ownerAgentId);
        ProcessRunId = NormalizeOptional(processRunId);
        WorkflowRunId = NormalizeOptional(workflowRunId);
        ProcessStepId = NormalizeOptional(processStepId);
        ProbeSessionId = NormalizeOptional(probeSessionId);
        ReviewSessionId = NormalizeOptional(reviewSessionId);
        LearningTaskId = NormalizeOptional(learningTaskId);
        ValidateRequiredScope();
    }

    public Guid ProjectId { get; }

    public CognitiveMemoryWorkspaceFrameKind FrameKind { get; }

    public string? OwnerUserId { get; }

    public string? OwnerAgentId { get; }

    public Guid? ProcessRunId { get; }

    public Guid? WorkflowRunId { get; }

    public Guid? ProcessStepId { get; }

    public Guid? ProbeSessionId { get; }

    public Guid? ReviewSessionId { get; }

    public Guid? LearningTaskId { get; }

    private void ValidateRequiredScope()
    {
        var isValid = FrameKind switch
        {
            CognitiveMemoryWorkspaceFrameKind.UserConversation => OwnerUserId is not null,
            CognitiveMemoryWorkspaceFrameKind.AgentRun => OwnerAgentId is not null,
            CognitiveMemoryWorkspaceFrameKind.WorkflowRun => WorkflowRunId is not null,
            CognitiveMemoryWorkspaceFrameKind.ProcessStep => ProcessRunId is not null && ProcessStepId is not null,
            CognitiveMemoryWorkspaceFrameKind.ProbeSession => ProbeSessionId is not null,
            CognitiveMemoryWorkspaceFrameKind.ReviewSession => ReviewSessionId is not null,
            CognitiveMemoryWorkspaceFrameKind.LearningTask => LearningTaskId is not null,
            _ => false
        };

        if (!isValid)
        {
            throw new ArgumentException($"Workspace scope for '{FrameKind}' is missing its required identifier.");
        }
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? NormalizeOptional(Guid? value)
        => value is { } actual && actual != Guid.Empty ? actual : null;
}

public sealed record CognitiveMemoryWorkspaceContextBudget
{
    public CognitiveMemoryWorkspaceContextBudget(
        int tokenLimit,
        int sectionLimit,
        int detailLimit)
    {
        if (tokenLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenLimit), "Workspace token budget must be positive.");
        }

        if (sectionLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sectionLimit), "Workspace section budget must be positive.");
        }

        if (detailLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(detailLimit), "Workspace detail budget must be positive.");
        }

        TokenLimit = tokenLimit;
        SectionLimit = sectionLimit;
        DetailLimit = detailLimit;
    }

    public int TokenLimit { get; }

    public int SectionLimit { get; }

    public int DetailLimit { get; }
}

public sealed record CognitiveMemoryWorkspaceGoalDraft(
    CognitiveMemoryWorkspaceGoalKey GoalKey,
    string Description,
    int Sequence,
    Guid? ParentGoalId = null);

public sealed record CognitiveMemoryWorkspaceOpenQuestionDraft(
    string QuestionText,
    string Reason,
    CognitiveMemoryWorkspaceOpenQuestionStatus Status = CognitiveMemoryWorkspaceOpenQuestionStatus.Open);

public sealed record CognitiveMemoryWorkingMemorySlotDraft
{
    public CognitiveMemoryWorkingMemorySlotDraft(
        CognitiveMemoryWorkingMemorySlotKind slotKind,
        string title,
        string summary,
        int estimatedTokenCount,
        int estimatedSectionCount,
        int estimatedDetailCount,
        CognitiveMemoryFocusInclusionReasonKind inclusionReasonKind,
        string inclusionReason,
        CognitiveMemoryRecordId? memoryRecordId = null,
        CognitiveMemoryClaimId? claimId = null,
        CognitiveMemorySourceItemId? sourceItemId = null,
        Guid? procedureSkillId = null,
        Guid? recallTraceId = null,
        Guid? probeTurnId = null,
        Guid? workflowArtifactId = null,
        CognitiveMemoryWorkspaceOpenQuestionId? openQuestionId = null,
        CognitiveMemoryWorkspaceExternalKey? externalPlaceholderKey = null,
        Guid? attentionScoreEvaluationTraceId = null,
        double? displayAttentionScore = null,
        CognitiveMemoryScoreProjectionBucket attentionBucket = CognitiveMemoryScoreProjectionBucket.Unknown,
        CognitiveMemoryWorkspaceSourceSufficiency sourceSufficiency = CognitiveMemoryWorkspaceSourceSufficiency.Unknown,
        CognitiveMemoryRiskLevel riskLevel = CognitiveMemoryRiskLevel.Low,
        CognitiveMemoryScoreProjectionBucket confidenceBucket = CognitiveMemoryScoreProjectionBucket.Unknown,
        CognitiveMemoryScoreProjectionBucket stalenessBucket = CognitiveMemoryScoreProjectionBucket.Unknown,
        string relationToActiveGoal = "",
        string compressionSummary = "",
        IReadOnlyList<CognitiveMemoryEvidenceAnchorId>? evidenceAnchorIds = null)
    {
        if (slotKind == CognitiveMemoryWorkingMemorySlotKind.Unknown)
        {
            throw new ArgumentException("Workspace focus slot kind must be explicit.", nameof(slotKind));
        }

        if (estimatedTokenCount < 0 || estimatedSectionCount < 0 || estimatedDetailCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedTokenCount), "Workspace slot estimates must not be negative.");
        }

        SlotKind = slotKind;
        Title = CognitiveMemoryGuard.EnsureText(title, nameof(title));
        Summary = CognitiveMemoryGuard.EnsureText(summary, nameof(summary));
        EstimatedTokenCount = estimatedTokenCount;
        EstimatedSectionCount = estimatedSectionCount;
        EstimatedDetailCount = estimatedDetailCount;
        InclusionReasonKind = inclusionReasonKind;
        InclusionReason = CognitiveMemoryGuard.EnsureText(inclusionReason, nameof(inclusionReason));
        MemoryRecordId = memoryRecordId;
        ClaimId = claimId;
        SourceItemId = sourceItemId;
        ProcedureSkillId = NormalizeOptional(procedureSkillId);
        RecallTraceId = NormalizeOptional(recallTraceId);
        ProbeTurnId = NormalizeOptional(probeTurnId);
        WorkflowArtifactId = NormalizeOptional(workflowArtifactId);
        OpenQuestionId = openQuestionId;
        ExternalPlaceholderKey = externalPlaceholderKey;
        AttentionScoreEvaluationTraceId = NormalizeOptional(attentionScoreEvaluationTraceId);
        DisplayAttentionScore = displayAttentionScore;
        AttentionBucket = attentionBucket;
        SourceSufficiency = sourceSufficiency;
        RiskLevel = riskLevel;
        ConfidenceBucket = confidenceBucket;
        StalenessBucket = stalenessBucket;
        RelationToActiveGoal = relationToActiveGoal.Trim();
        CompressionSummary = compressionSummary.Trim();
        EvidenceAnchorIds = evidenceAnchorIds ?? [];
    }

    public CognitiveMemoryWorkingMemorySlotKind SlotKind { get; }

    public string Title { get; }

    public string Summary { get; }

    public int EstimatedTokenCount { get; }

    public int EstimatedSectionCount { get; }

    public int EstimatedDetailCount { get; }

    public CognitiveMemoryFocusInclusionReasonKind InclusionReasonKind { get; }

    public string InclusionReason { get; }

    public CognitiveMemoryRecordId? MemoryRecordId { get; }

    public CognitiveMemoryClaimId? ClaimId { get; }

    public CognitiveMemorySourceItemId? SourceItemId { get; }

    public Guid? ProcedureSkillId { get; }

    public Guid? RecallTraceId { get; }

    public Guid? ProbeTurnId { get; }

    public Guid? WorkflowArtifactId { get; }

    public CognitiveMemoryWorkspaceOpenQuestionId? OpenQuestionId { get; }

    public CognitiveMemoryWorkspaceExternalKey? ExternalPlaceholderKey { get; }

    public Guid? AttentionScoreEvaluationTraceId { get; }

    public double? DisplayAttentionScore { get; }

    public CognitiveMemoryScoreProjectionBucket AttentionBucket { get; }

    public CognitiveMemoryWorkspaceSourceSufficiency SourceSufficiency { get; }

    public CognitiveMemoryRiskLevel RiskLevel { get; }

    public CognitiveMemoryScoreProjectionBucket ConfidenceBucket { get; }

    public CognitiveMemoryScoreProjectionBucket StalenessBucket { get; }

    public string RelationToActiveGoal { get; }

    public string CompressionSummary { get; }

    public IReadOnlyList<CognitiveMemoryEvidenceAnchorId> EvidenceAnchorIds { get; }

    private static Guid? NormalizeOptional(Guid? value)
        => value is { } actual && actual != Guid.Empty ? actual : null;
}

public sealed record CognitiveMemoryInhibitedCandidateDraft
{
    public CognitiveMemoryInhibitedCandidateDraft(
        CognitiveMemoryWorkingMemorySlotKind candidateKind,
        CognitiveMemoryInhibitionReasonKind reasonKind,
        string reason,
        CognitiveMemoryRecordId? memoryRecordId = null,
        CognitiveMemoryClaimId? claimId = null,
        CognitiveMemorySourceItemId? sourceItemId = null,
        CognitiveMemoryWorkspaceExternalKey? externalCandidateKey = null,
        Guid? inhibitionScoreEvaluationTraceId = null,
        CognitiveMemoryScoreProjectionBucket inhibitionBucket = CognitiveMemoryScoreProjectionBucket.Inhibit,
        double? displayRelevanceScore = null,
        double? displayInhibitionStrength = null)
    {
        if (candidateKind == CognitiveMemoryWorkingMemorySlotKind.Unknown)
        {
            throw new ArgumentException("Inhibited candidate kind must be explicit.", nameof(candidateKind));
        }

        CandidateKind = candidateKind;
        ReasonKind = reasonKind;
        Reason = CognitiveMemoryGuard.EnsureText(reason, nameof(reason));
        MemoryRecordId = memoryRecordId;
        ClaimId = claimId;
        SourceItemId = sourceItemId;
        ExternalCandidateKey = externalCandidateKey;
        InhibitionScoreEvaluationTraceId = inhibitionScoreEvaluationTraceId is { } id && id != Guid.Empty ? id : null;
        InhibitionBucket = inhibitionBucket;
        DisplayRelevanceScore = displayRelevanceScore;
        DisplayInhibitionStrength = displayInhibitionStrength;
    }

    public CognitiveMemoryWorkingMemorySlotKind CandidateKind { get; }

    public CognitiveMemoryInhibitionReasonKind ReasonKind { get; }

    public string Reason { get; }

    public CognitiveMemoryRecordId? MemoryRecordId { get; }

    public CognitiveMemoryClaimId? ClaimId { get; }

    public CognitiveMemorySourceItemId? SourceItemId { get; }

    public CognitiveMemoryWorkspaceExternalKey? ExternalCandidateKey { get; }

    public Guid? InhibitionScoreEvaluationTraceId { get; }

    public CognitiveMemoryScoreProjectionBucket InhibitionBucket { get; }

    public double? DisplayRelevanceScore { get; }

    public double? DisplayInhibitionStrength { get; }
}

public sealed record CognitiveMemoryWorkspaceOpenRequest(
    CognitiveMemoryWorkspaceScope Scope,
    CognitiveMemoryWorkspaceContextBudget ContextBudget,
    DateTimeOffset? ExpiresAtUtc,
    IReadOnlyList<CognitiveMemoryWorkspaceGoalDraft>? GoalStack = null,
    IReadOnlyList<CognitiveMemoryWorkspaceOpenQuestionDraft>? OpenQuestions = null,
    Guid? CognitiveLoadScoreEvaluationTraceId = null,
    CognitiveMemoryScoreProjectionBucket CognitiveLoadBucket = CognitiveMemoryScoreProjectionBucket.Unknown,
    double? DisplayCognitiveLoadScore = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemoryWorkspaceUpdateRequest(
    CognitiveMemoryWorkspaceFrameId WorkspaceFrameId,
    IReadOnlyList<CognitiveMemoryWorkspaceGoalDraft>? GoalStack,
    IReadOnlyList<CognitiveMemoryWorkingMemorySlotDraft>? FocusSlots,
    IReadOnlyList<CognitiveMemoryInhibitedCandidateDraft>? InhibitedCandidates,
    IReadOnlyList<CognitiveMemoryWorkspaceOpenQuestionDraft>? OpenQuestions,
    CognitiveMemoryWorkspaceContextBudget? ContextBudget = null,
    Guid? CognitiveLoadScoreEvaluationTraceId = null,
    CognitiveMemoryScoreProjectionBucket CognitiveLoadBucket = CognitiveMemoryScoreProjectionBucket.Unknown,
    double? DisplayCognitiveLoadScore = null,
    Guid? LastSelfRegulationAssessmentId = null,
    Guid? LastAnswerPostureDecisionId = null);

public sealed record CognitiveMemoryWorkspaceBudgetResult(
    int AcceptedSlotCount,
    int InhibitedByBudgetCount,
    int TokenEstimate,
    int SectionEstimate,
    int DetailEstimate,
    CognitiveMemoryBudgetLimit? LimitingBudget);

public sealed record CognitiveMemoryWorkspaceSnapshot(
    CognitiveMemoryWorkspaceFrameRecord Frame,
    IReadOnlyList<CognitiveMemoryWorkspaceGoalRecord> Goals,
    IReadOnlyList<CognitiveMemoryWorkingMemorySlotRecord> FocusSlots,
    IReadOnlyList<CognitiveMemoryWorkspaceOpenQuestionRecord> OpenQuestions,
    IReadOnlyList<CognitiveMemoryInhibitedCandidateRecord> InhibitedCandidates,
    CognitiveMemoryWorkspaceBudgetResult BudgetResult);

public sealed record CognitiveMemoryAttentionSignalSet(
    double? SourceSufficiency,
    double? ContextAmbiguity,
    double? CognitiveLoad = null,
    double? RiskImpact = null,
    double? AvailableWorkspaceEvidence = null,
    double? MissingKnowledgePressure = null,
    double? CalibrationRisk = null,
    double? ActionCost = null,
    double? ExpectedValue = null);

public sealed record CognitiveMemoryAttentionRoutingRequest(
    Guid ProjectId,
    CognitiveMemoryWorkspaceFrameId WorkspaceFrameId,
    string RequestText,
    CognitiveMemoryAttentionSignalSet Signals,
    IReadOnlyList<CognitiveMemoryAttentionDecisionKind>? RequiredDecisionKinds = null,
    Guid? SelfRegulationAssessmentId = null,
    Guid? AnswerPostureDecisionId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemoryAttentionRoutingDecision(
    CognitiveMemoryAttentionDecisionId Id,
    Guid ProjectId,
    CognitiveMemoryWorkspaceFrameId WorkspaceFrameId,
    CognitiveMemoryAttentionDecisionKind DecisionKind,
    CognitiveMemoryAttentionReasonKind ReasonKind,
    string Explanation,
    IReadOnlyList<string> RequiredNextActions,
    CognitiveMemoryScoreEvaluationTrace RoutingTrace,
    DateTimeOffset CreatedAtUtc);

public interface ICognitiveMemoryWorkspaceService
{
    ValueTask<CognitiveMemoryWorkspaceSnapshot> GetOrCreateAsync(
        CognitiveMemoryWorkspaceOpenRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryWorkspaceSnapshot> UpdateAsync(
        CognitiveMemoryWorkspaceUpdateRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryAttentionRouter
{
    ValueTask<CognitiveMemoryAttentionRoutingDecision> RouteAsync(
        CognitiveMemoryAttentionRoutingRequest request,
        CancellationToken cancellationToken = default);
}
