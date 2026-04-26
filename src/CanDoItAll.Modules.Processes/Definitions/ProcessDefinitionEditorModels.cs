using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Processes;

public sealed record ProcessDefinitionListItem(
    Guid Id,
    Guid? ProjectId,
    string Name,
    ProcessDefinitionStatus Status,
    int LatestVersionNumber,
    bool HasPublishedVersion,
    int RoleCount,
    int StepCount,
    int ActiveRunCount,
    int UnfilledRoleCount,
    string Summary,
    string ValueStatement,
    string ProjectName,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProcessRuntimeTileModel(
    int Definitions,
    int PublishedDefinitions,
    int ActiveRuns,
    int BlockedRuns,
    int UnfilledAssignments,
    int ImprovementCandidates,
    decimal EstimatedCost,
    decimal ActualCost);

public sealed class ProcessDefinitionEditorModel
{
    public Guid? Id { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? WorkingVersionId { get; set; }

    public Guid? DefinitionConcurrencyToken { get; set; }

    public Guid? WorkingVersionConcurrencyToken { get; set; }

    public int WorkingVersionNumber { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string ValueStatement { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string InterfaceContractSummary { get; set; } = string.Empty;

    public string GovernanceNotes { get; set; } = string.Empty;

    public string ChangeSummary { get; set; } = string.Empty;

    public string GovernancePolicySummary { get; set; } = string.Empty;

    public string ConstitutionRuleSummary { get; set; } = string.Empty;

    public string OperatingModeSummary { get; set; } = string.Empty;

    public string SimulationReadinessSummary { get; set; } = string.Empty;

    public ProcessCriticality Criticality { get; set; } = ProcessCriticality.Standard;

    public ProcessAutonomyLevel AutonomyLevel { get; set; } = ProcessAutonomyLevel.Assisted;

    public ProcessDefinitionStatus Status { get; set; } = ProcessDefinitionStatus.Draft;

    public List<ProcessRoleEditorModel> Roles { get; set; } = [];

    public List<ProcessRoleMessagingPolicyEditorModel> MessagingPolicies { get; set; } = [];

    public List<ProcessStepEditorModel> Steps { get; set; } = [];
}

public sealed class ProcessDefinitionPublishRequest
{
    public Guid DefinitionId { get; set; }

    public Guid? DefinitionConcurrencyToken { get; set; }

    public Guid? DraftVersionConcurrencyToken { get; set; }
}

public sealed class ProcessRoleEditorModel
{
    public Guid? Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string StaffingIntent { get; set; } = string.Empty;

    public string PreferredExecutorKind { get; set; } = string.Empty;

    public ProjectPartyAssignmentRole? PreferredProjectAssignmentRole { get; set; }

    public bool IsRequired { get; set; } = true;

    public bool AllowsFallback { get; set; } = true;

    public bool RequiresExplicitApproval { get; set; }

    public int DefaultAllocationPercent { get; set; } = 100;

    public string RoleTemplateSourceKey { get; set; } = string.Empty;

    public string RoleTemplateSnapshotName { get; set; } = string.Empty;

    public string SnapshotSummary { get; set; } = string.Empty;

    public List<Guid> RequiredSkillIds { get; set; } = [];

    public double CanvasX { get; set; }

    public double CanvasY { get; set; }
}

public sealed class ProcessRoleMessagingPolicyEditorModel
{
    public Guid? Id { get; set; }

    public Guid? SourceRoleRequirementId { get; set; }

    public Guid? TargetRoleRequirementId { get; set; }
}

public sealed class ProcessStepEditorModel
{
    public Guid? Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public ProcessStepKind StepKind { get; set; } = ProcessStepKind.Work;

    public bool AllowsManualSkip { get; set; }

    public bool AllowsSafeRefusal { get; set; }

    public bool RequiresApproval { get; set; }

    public bool RequiresDecisionRecord { get; set; }

    public string InputContractSummary { get; set; } = string.Empty;

    public string OutputContractSummary { get; set; } = string.Empty;

    public string EvidenceContractSummary { get; set; } = string.Empty;

    public string DecisionRightsSummary { get; set; } = string.Empty;

    public string ExceptionPolicySummary { get; set; } = string.Empty;

    public int TargetLeadHours { get; set; }

    public Guid? DecisionRoleRequirementId { get; set; }

    public double CanvasX { get; set; }

    public double CanvasY { get; set; }

    public double BranchCanvasX { get; set; }

    public double BranchCanvasY { get; set; }

    public List<ProcessStepBranchOutcomeEditorModel> BranchOutcomes { get; set; } = [];

    public List<ProcessStepDependencyEditorModel> Dependencies { get; set; } = [];

    public List<ProcessStepRoleRequirementEditorModel> RoleAssignments { get; set; } = [];

    public List<ProcessArtifactExpectationEditorModel> ArtifactExpectations { get; set; } = [];

    public List<ProcessStepArtifactInputEditorModel> ArtifactInputs { get; set; } = [];
}

public sealed class ProcessStepDependencyEditorModel
{
    public Guid? Id { get; set; }

    public Guid? DependsOnStepId { get; set; }

    public Guid? DependsOnBranchOutcomeId { get; set; }
}

public sealed class ProcessStepBranchOutcomeEditorModel
{
    public Guid? Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class ProcessStepRoleRequirementEditorModel
{
    public Guid? Id { get; set; }

    public Guid? RoleRequirementId { get; set; }

    public ProcessResponsibilityKind ResponsibilityKind { get; set; } = ProcessResponsibilityKind.Responsible;

    public bool IsRequired { get; set; } = true;

    public int FallbackOrder { get; set; }

    public string RebindPolicySummary { get; set; } = string.Empty;
}

public sealed class ProcessArtifactExpectationEditorModel
{
    public Guid? Id { get; set; }

    public ProcessArtifactKind ArtifactKind { get; set; } = ProcessArtifactKind.Evidence;

    public string Title { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;

    public ProcessArtifactTrustRequirement TrustRequirement { get; set; } = ProcessArtifactTrustRequirement.ReviewRequired;

    public ProcessSensitivityLevel SensitivityLevel { get; set; } = ProcessSensitivityLevel.Internal;

    public int RetentionDays { get; set; } = 90;

    public string AllowedFutureUsageSummary { get; set; } = string.Empty;

    public string ValidationRequirementSummary { get; set; } = string.Empty;
}

public sealed class ProcessStepArtifactInputEditorModel
{
    public Guid? Id { get; set; }

    public Guid? ArtifactExpectationId { get; set; }
}
