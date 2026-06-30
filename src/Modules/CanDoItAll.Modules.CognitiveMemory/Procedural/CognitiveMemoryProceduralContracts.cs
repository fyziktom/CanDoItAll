namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryProcedureSkillMaturity
{
    Unknown = 0,
    Draft = 1,
    Observed = 2,
    Reviewed = 3,
    Validated = 4,
    Automatable = 5,
    Deprecated = 6
}

public enum CognitiveMemoryProcedureValidationEvidenceRole
{
    Unknown = 0,
    SourceAnchor = 1,
    SuccessfulEpisode = 2,
    HumanReview = 3,
    RegressionProof = 4,
    RuntimeObservation = 5
}

public enum CognitiveMemoryProcedureAutomationBindingKind
{
    Unknown = 0,
    WorkflowTemplate = 1,
    WorkflowExecutorGuidance = 2,
    MafProcedureGuidance = 3,
    PluginTool = 4
}

public enum CognitiveMemoryProcedureAutomationBindingState
{
    Draft = 0,
    Rejected = 1,
    NeedsReview = 2,
    Bound = 3
}

public enum CognitiveMemoryProcedureSimulationOutputKind
{
    Unknown = 0,
    CandidatePlan = 1,
    RiskAnalysis = 2,
    MissingPreconditions = 3,
    ExpectedOutcome = 4,
    LikelyFailureModes = 5,
    RequiredSourcesOrTests = 6,
    SuggestedProbeOrRegression = 7,
    ProcedureImprovementProposal = 8
}

public enum CognitiveMemoryProcedureSimulationStatus
{
    Speculative = 0,
    NeedsReview = 1,
    Rejected = 2,
    SourceBacked = 3
}

public readonly record struct CognitiveMemoryProcedureSkillId
{
    public CognitiveMemoryProcedureSkillId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryProcedureSkillId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryProcedureStepId
{
    public CognitiveMemoryProcedureStepId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryProcedureStepId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryProcedureFailureModeId
{
    public CognitiveMemoryProcedureFailureModeId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryProcedureFailureModeId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryProcedureSimulationId
{
    public CognitiveMemoryProcedureSimulationId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryProcedureSimulationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record CognitiveMemoryProcedureStepDraft(
    string StepKey,
    int Order,
    string Action,
    string RequiredInput,
    string ExpectedOutput,
    string ValidationCheck,
    string FailureHandling,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorId>? EvidenceAnchorIds = null,
    string ToolBindingKey = "",
    int? TimeoutSeconds = null,
    int RetryLimit = 0,
    bool IsRollbackStep = false,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemoryProcedureFailureModeDraft(
    string FailureKey,
    string Condition,
    string DetectionSignal,
    string LikelyCause,
    string Mitigation,
    string RollbackOrCompensation,
    IReadOnlyList<CognitiveMemoryPredictionErrorId>? RelatedPredictionErrorIds = null,
    IReadOnlyList<CognitiveMemoryTemporalEpisodeId>? RelatedEpisodeIds = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemoryProcedureValidationEvidenceDraft(
    CognitiveMemoryProcedureValidationEvidenceRole EvidenceRole,
    CognitiveMemoryEvidenceAnchorId EvidenceAnchorId,
    string Summary,
    CognitiveMemoryTemporalEpisodeId? EpisodeId = null,
    CognitiveMemoryReviewItemId? ReviewItemId = null);

public sealed record CognitiveMemoryProcedureSkillProposalRequest(
    Guid ProjectId,
    string Title,
    string Purpose,
    CognitiveMemoryPolicyContext PolicyContext,
    IReadOnlyList<CognitiveMemoryProcedureStepDraft> Steps,
    IReadOnlyList<CognitiveMemoryProcedureFailureModeDraft> FailureModes,
    IReadOnlyList<CognitiveMemoryProcedureValidationEvidenceDraft> ValidationEvidence,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<string> Postconditions,
    CognitiveMemoryRiskLevel RiskLevel = CognitiveMemoryRiskLevel.Low,
    CognitiveMemoryProcedureSkillMaturity InitialMaturity = CognitiveMemoryProcedureSkillMaturity.Draft,
    CognitiveMemoryValidationState ValidationState = CognitiveMemoryValidationState.MachineGenerated,
    CognitiveMemoryAccessLevel AccessLevel = CognitiveMemoryAccessLevel.Project,
    CognitiveMemoryConsolidationCandidateId? SourceConsolidationCandidateId = null,
    CognitiveMemoryTemporalEpisodeId? LastSuccessfulEpisodeId = null,
    IReadOnlyList<Guid>? ContextFrameIds = null,
    IReadOnlyList<string>? RequiredRoles = null,
    IReadOnlyList<string>? RequiredToolKeys = null,
    string InputSchemaJson = "{}",
    string OutputSchemaJson = "{}",
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemoryProcedureMaturityUpdateRequest(
    CognitiveMemoryProcedureSkillId SkillId,
    CognitiveMemoryProcedureSkillMaturity TargetMaturity,
    CognitiveMemoryPolicyContext PolicyContext,
    IReadOnlyList<CognitiveMemoryProcedureValidationEvidenceDraft> AdditionalValidationEvidence,
    CognitiveMemoryValidationState? ValidationState = null,
    CognitiveMemoryRiskLevel? RiskLevel = null,
    CognitiveMemoryTemporalEpisodeId? LastSuccessfulEpisodeId = null);

public sealed record CognitiveMemoryProcedureAutomationBindingRequest(
    CognitiveMemoryProcedureSkillId SkillId,
    CognitiveMemoryProcedureAutomationBindingKind BindingKind,
    string BindingKey,
    CognitiveMemoryPolicyContext PolicyContext,
    bool HumanReviewApproved = false,
    CognitiveMemoryReviewItemId? ReviewItemId = null);

public sealed record CognitiveMemoryProcedureSimulationRequest(
    Guid ProjectId,
    CognitiveMemoryProcedureSimulationOutputKind OutputKind,
    string Summary,
    CognitiveMemoryPolicyContext PolicyContext,
    IReadOnlyList<CognitiveMemoryProcedureSkillId> RelatedProcedureSkillIds,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorId> EvidenceAnchorIds,
    IReadOnlyList<string> RequiredValidationSteps,
    CognitiveMemoryRiskLevel RiskLevel = CognitiveMemoryRiskLevel.Low,
    bool AllowCrossProjectAnalogies = false,
    string SourceScopeKey = "",
    IReadOnlyDictionary<string, string>? Metadata = null);

public interface ICognitiveMemoryProcedureSkillMemoryService
{
    ValueTask<CognitiveMemoryProcedureSkillRecord> ProposeSkillAsync(
        CognitiveMemoryProcedureSkillProposalRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryProcedureSkillRecord> UpdateMaturityAsync(
        CognitiveMemoryProcedureMaturityUpdateRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryProcedureAutomationBindingRecord> RequestAutomationBindingAsync(
        CognitiveMemoryProcedureAutomationBindingRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemorySimulationSandboxService
{
    ValueTask<CognitiveMemoryProcedureSimulationRecord> SimulateAsync(
        CognitiveMemoryProcedureSimulationRequest request,
        CancellationToken cancellationToken = default);
}
