using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryProcedureSkillRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public CognitiveMemoryProcedureSkillMaturity Maturity { get; set; } = CognitiveMemoryProcedureSkillMaturity.Draft;

    public CognitiveMemoryRiskLevel RiskLevel { get; set; } = CognitiveMemoryRiskLevel.Low;

    public CognitiveMemoryValidationState ValidationState { get; set; } = CognitiveMemoryValidationState.MachineGenerated;

    public CognitiveMemoryAccessLevel AccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public Guid? SourceConsolidationCandidateId { get; set; }

    public Guid? LastSuccessfulEpisodeId { get; set; }

    public Guid MaturityScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket MaturityBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayMaturityScore { get; set; }

    public string PreconditionsJson { get; set; } = "[]";

    public string PostconditionsJson { get; set; } = "[]";

    public string RequiredParticipantsJson { get; set; } = "[]";

    public string RequiredToolKeysJson { get; set; } = "[]";

    public string InputSchemaJson { get; set; } = "{}";

    public string OutputSchemaJson { get; set; } = "{}";

    public int StepCount { get; set; }

    public int FailureModeCount { get; set; }

    public int ValidationEvidenceCount { get; set; }

    public int AutomationBindingCount { get; set; }

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryProcedureStepRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcedureSkillId { get; set; }

    public Guid ProjectId { get; set; }

    public string StepKey { get; set; } = string.Empty;

    public int SequenceIndex { get; set; }

    public string Action { get; set; } = string.Empty;

    public string RequiredInput { get; set; } = string.Empty;

    public string ExpectedOutput { get; set; } = string.Empty;

    public string ValidationCheck { get; set; } = string.Empty;

    public string FailureHandling { get; set; } = string.Empty;

    public string ToolBindingKey { get; set; } = string.Empty;

    public int? TimeoutSeconds { get; set; }

    public int RetryLimit { get; set; }

    public bool IsRollbackStep { get; set; }

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryProcedureStepEvidenceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcedureStepId { get; set; }

    public Guid ProcedureSkillId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid EvidenceAnchorId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryProcedureFailureModeRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcedureSkillId { get; set; }

    public Guid ProjectId { get; set; }

    public string FailureKey { get; set; } = string.Empty;

    public string Condition { get; set; } = string.Empty;

    public string DetectionSignal { get; set; } = string.Empty;

    public string LikelyCause { get; set; } = string.Empty;

    public string Mitigation { get; set; } = string.Empty;

    public string RollbackOrCompensation { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryProcedureFailureModePredictionErrorRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcedureFailureModeId { get; set; }

    public Guid ProcedureSkillId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid PredictionErrorId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryProcedureFailureModeEpisodeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcedureFailureModeId { get; set; }

    public Guid ProcedureSkillId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid EpisodeId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryProcedureValidationEvidenceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcedureSkillId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryProcedureValidationEvidenceRole EvidenceRole { get; set; } = CognitiveMemoryProcedureValidationEvidenceRole.Unknown;

    public Guid EvidenceAnchorId { get; set; }

    public Guid? EpisodeId { get; set; }

    public Guid? ReviewItemId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryProcedureAutomationBindingRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcedureSkillId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryProcedureAutomationBindingKind BindingKind { get; set; } = CognitiveMemoryProcedureAutomationBindingKind.Unknown;

    public string BindingKey { get; set; } = string.Empty;

    public CognitiveMemoryProcedureAutomationBindingState State { get; set; } = CognitiveMemoryProcedureAutomationBindingState.Draft;

    public bool RequiresHumanReview { get; set; }

    public Guid? ReviewItemId { get; set; }

    public string RejectionCode { get; set; } = string.Empty;

    public string RejectionReason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryProcedureSimulationRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public CognitiveMemoryProcedureSimulationOutputKind OutputKind { get; set; } = CognitiveMemoryProcedureSimulationOutputKind.Unknown;

    public CognitiveMemoryProcedureSimulationStatus Status { get; set; } = CognitiveMemoryProcedureSimulationStatus.Speculative;

    public string Summary { get; set; } = string.Empty;

    public bool IsSpeculative { get; set; } = true;

    public string SpeculationLabel { get; set; } = string.Empty;

    public CognitiveMemoryRiskLevel RiskLevel { get; set; } = CognitiveMemoryRiskLevel.Low;

    public Guid RiskScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket RiskBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayRiskScore { get; set; }

    public string PolicyProfileId { get; set; } = string.Empty;

    public string SourceScopeKey { get; set; } = string.Empty;

    public string RequiredValidationStepsJson { get; set; } = "[]";

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryProcedureSimulationSkillRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SimulationId { get; set; }

    public Guid ProcedureSkillId { get; set; }

    public Guid ProjectId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryProcedureSimulationEvidenceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SimulationId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid EvidenceAnchorId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
