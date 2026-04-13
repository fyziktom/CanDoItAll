using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessDefinition : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string ValueStatement { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string InterfaceContractSummary { get; set; } = string.Empty;

    public string GovernanceNotes { get; set; } = string.Empty;

    public ProcessCriticality Criticality { get; set; } = ProcessCriticality.Standard;

    public ProcessAutonomyLevel AutonomyLevel { get; set; } = ProcessAutonomyLevel.Assisted;

    public ProcessDefinitionStatus Status { get; set; } = ProcessDefinitionStatus.Draft;

    public Guid? ActivePublishedVersionId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}

public sealed class ProcessDefinitionVersion : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessDefinitionId { get; set; }

    public int VersionNumber { get; set; } = 1;

    public ProcessVersionStatus Status { get; set; } = ProcessVersionStatus.Draft;

    public string ChangeSummary { get; set; } = string.Empty;

    public string GovernancePolicySummary { get; set; } = string.Empty;

    public string ConstitutionRuleSummary { get; set; } = string.Empty;

    public string OperatingModeSummary { get; set; } = string.Empty;

    public string SimulationReadinessSummary { get; set; } = string.Empty;

    public string ImportedFrom { get; set; } = string.Empty;

    public string ImportWarnings { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? PublishedAtUtc { get; set; }

    public string PublishedBy { get; set; } = string.Empty;

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}

public sealed class ProcessRoleRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessDefinitionVersionId { get; set; }

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

    public int DisplayOrder { get; set; }

    public double CanvasX { get; set; }

    public double CanvasY { get; set; }
}

public sealed class ProcessRoleSkillRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RoleRequirementId { get; set; }

    public Guid SkillId { get; set; }

    public bool IsRequired { get; set; } = true;

    public int MinimumYearsExperience { get; set; }
}

public sealed class ProcessStepDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessDefinitionVersionId { get; set; }

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

    public int OrderIndex { get; set; }

    public Guid? DependsOnStepId { get; set; }

    public Guid? DependsOnBranchOutcomeId { get; set; }

    public Guid? DecisionRoleRequirementId { get; set; }

    public double CanvasX { get; set; }

    public double CanvasY { get; set; }

    public double BranchCanvasX { get; set; }

    public double BranchCanvasY { get; set; }
}

public sealed class ProcessStepDependencyDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StepDefinitionId { get; set; }

    public Guid DependsOnStepId { get; set; }

    public Guid? DependsOnBranchOutcomeId { get; set; }

    public int DisplayOrder { get; set; }
}

public sealed class ProcessStepBranchOutcomeDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StepDefinitionId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}

public sealed class ProcessStepRoleAssignmentRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StepDefinitionId { get; set; }

    public Guid RoleRequirementId { get; set; }

    public ProcessResponsibilityKind ResponsibilityKind { get; set; } = ProcessResponsibilityKind.Responsible;

    public bool IsRequired { get; set; } = true;

    public int FallbackOrder { get; set; }

    public string RebindPolicySummary { get; set; } = string.Empty;
}

public sealed class ProcessArtifactExpectation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StepDefinitionId { get; set; }

    public ProcessArtifactKind ArtifactKind { get; set; } = ProcessArtifactKind.Evidence;

    public string Title { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;

    public ProcessArtifactTrustRequirement TrustRequirement { get; set; } = ProcessArtifactTrustRequirement.ReviewRequired;

    public ProcessSensitivityLevel SensitivityLevel { get; set; } = ProcessSensitivityLevel.Internal;

    public int RetentionDays { get; set; } = 90;

    public string AllowedFutureUsageSummary { get; set; } = string.Empty;

    public string ValidationRequirementSummary { get; set; } = string.Empty;
}

public sealed class ProcessStepArtifactInputDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StepDefinitionId { get; set; }

    public Guid ArtifactExpectationId { get; set; }

    public int DisplayOrder { get; set; }
}
