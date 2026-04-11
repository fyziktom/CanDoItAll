using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Processes;

public enum ProcessDefinitionStatus {
    Draft,
    Published,
    Archived
}

public enum ProcessVersionStatus {
    Draft,
    Published,
    Superseded,
    Archived
}

public enum ProcessCriticality {
    Low,
    Standard,
    High,
    MissionCritical
}

public enum ProcessAutonomyLevel {
    Manual,
    Assisted,
    Guarded,
    Delegated
}

public enum ProcessStepKind {
    Start,
    Work,
    Decision,
    Approval,
    Review,
    Delivery,
    End
}

public enum ProcessResponsibilityKind {
    Responsible,
    Reviewer,
    Approver,
    Backup
}

public enum ProcessArtifactKind {
    Brief,
    Evidence,
    Decision,
    Deliverable,
    Transcript,
    Checklist,
    Prompt,
    Dataset,
    Other
}

public enum ProcessArtifactTrustRequirement {
    None,
    ReviewRequired,
    HumanApproved,
    TrustedSource
}

public enum ProcessSensitivityLevel {
    Public,
    Internal,
    Confidential,
    Restricted
}

public sealed class ProcessDefinition {
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
}

public sealed class ProcessDefinitionVersion {
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
}

public sealed class ProcessRoleRequirement {
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

public sealed class ProcessRoleSkillRequirement {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RoleRequirementId { get; set; }

    public Guid SkillId { get; set; }

    public bool IsRequired { get; set; } = true;

    public int MinimumYearsExperience { get; set; }
}

public sealed class ProcessStepDefinition {
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

public sealed class ProcessStepDependencyDefinition {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StepDefinitionId { get; set; }

    public Guid DependsOnStepId { get; set; }

    public Guid? DependsOnBranchOutcomeId { get; set; }

    public int DisplayOrder { get; set; }
}

public sealed class ProcessStepBranchOutcomeDefinition {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StepDefinitionId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}

public sealed class ProcessStepRoleAssignmentRequirement {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StepDefinitionId { get; set; }

    public Guid RoleRequirementId { get; set; }

    public ProcessResponsibilityKind ResponsibilityKind { get; set; } = ProcessResponsibilityKind.Responsible;

    public bool IsRequired { get; set; } = true;

    public int FallbackOrder { get; set; }

    public string RebindPolicySummary { get; set; } = string.Empty;
}

public sealed class ProcessArtifactExpectation {
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

internal sealed class ProcessDefinitionConfiguration : IEntityTypeConfiguration<ProcessDefinition> {
    public void Configure(EntityTypeBuilder<ProcessDefinition> builder) {
        builder.ToTable("Processes_Definitions");
        builder.HasKey(definition => definition.Id);
        builder.Property(definition => definition.Name).HasMaxLength(200).IsRequired();
        builder.Property(definition => definition.Slug).HasMaxLength(200).IsRequired();
        builder.Property(definition => definition.Summary).HasColumnType("TEXT");
        builder.Property(definition => definition.ValueStatement).HasColumnType("TEXT");
        builder.Property(definition => definition.CustomerName).HasMaxLength(200);
        builder.Property(definition => definition.OwnerName).HasMaxLength(200);
        builder.Property(definition => definition.InterfaceContractSummary).HasColumnType("TEXT");
        builder.Property(definition => definition.GovernanceNotes).HasColumnType("TEXT");
        builder.Property(definition => definition.Criticality).HasConversion<string>().HasMaxLength(48);
        builder.Property(definition => definition.AutonomyLevel).HasConversion<string>().HasMaxLength(48);
        builder.Property(definition => definition.Status).HasConversion<string>().HasMaxLength(48);
        builder.HasIndex(definition => definition.ProjectId);
        builder.HasIndex(definition => definition.Slug).IsUnique();
        builder.HasIndex(definition => definition.Status);
    }
}

internal sealed class ProcessDefinitionVersionConfiguration : IEntityTypeConfiguration<ProcessDefinitionVersion> {
    public void Configure(EntityTypeBuilder<ProcessDefinitionVersion> builder) {
        builder.ToTable("Processes_DefinitionVersions");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Status).HasConversion<string>().HasMaxLength(48);
        builder.Property(version => version.ChangeSummary).HasColumnType("TEXT");
        builder.Property(version => version.GovernancePolicySummary).HasColumnType("TEXT");
        builder.Property(version => version.ConstitutionRuleSummary).HasColumnType("TEXT");
        builder.Property(version => version.OperatingModeSummary).HasColumnType("TEXT");
        builder.Property(version => version.SimulationReadinessSummary).HasColumnType("TEXT");
        builder.Property(version => version.ImportedFrom).HasMaxLength(200);
        builder.Property(version => version.ImportWarnings).HasColumnType("TEXT");
        builder.Property(version => version.PublishedBy).HasMaxLength(160);
        builder.HasIndex(version => new { version.ProcessDefinitionId, version.VersionNumber }).IsUnique();
        builder.HasIndex(version => new { version.ProcessDefinitionId, version.Status });
    }
}

internal sealed class ProcessRoleRequirementConfiguration : IEntityTypeConfiguration<ProcessRoleRequirement> {
    public void Configure(EntityTypeBuilder<ProcessRoleRequirement> builder) {
        builder.ToTable("Processes_RoleRequirements");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Key).HasMaxLength(120).IsRequired();
        builder.Property(role => role.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(role => role.Purpose).HasColumnType("TEXT");
        builder.Property(role => role.StaffingIntent).HasColumnType("TEXT");
        builder.Property(role => role.PreferredExecutorKind).HasMaxLength(80);
        builder.Property(role => role.PreferredProjectAssignmentRole).HasConversion<string>().HasMaxLength(64);
        builder.Property(role => role.RoleTemplateSourceKey).HasMaxLength(160);
        builder.Property(role => role.RoleTemplateSnapshotName).HasMaxLength(200);
        builder.Property(role => role.SnapshotSummary).HasColumnType("TEXT");
        builder.HasIndex(role => new { role.ProcessDefinitionVersionId, role.Key }).IsUnique();
    }
}

internal sealed class ProcessRoleSkillRequirementConfiguration : IEntityTypeConfiguration<ProcessRoleSkillRequirement> {
    public void Configure(EntityTypeBuilder<ProcessRoleSkillRequirement> builder) {
        builder.ToTable("Processes_RoleSkillRequirements");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.RoleRequirementId, item.SkillId }).IsUnique();
        builder.HasIndex(item => item.SkillId);
    }
}

internal sealed class ProcessStepDefinitionConfiguration : IEntityTypeConfiguration<ProcessStepDefinition> {
    public void Configure(EntityTypeBuilder<ProcessStepDefinition> builder) {
        builder.ToTable("Processes_StepDefinitions");
        builder.HasKey(step => step.Id);
        builder.Property(step => step.Key).HasMaxLength(120).IsRequired();
        builder.Property(step => step.Title).HasMaxLength(200).IsRequired();
        builder.Property(step => step.Subtitle).HasMaxLength(200);
        builder.Property(step => step.Notes).HasColumnType("TEXT");
        builder.Property(step => step.StepKind).HasConversion<string>().HasMaxLength(48);
        builder.Property(step => step.InputContractSummary).HasColumnType("TEXT");
        builder.Property(step => step.OutputContractSummary).HasColumnType("TEXT");
        builder.Property(step => step.EvidenceContractSummary).HasColumnType("TEXT");
        builder.Property(step => step.DecisionRightsSummary).HasColumnType("TEXT");
        builder.Property(step => step.ExceptionPolicySummary).HasColumnType("TEXT");
        builder.HasIndex(step => new { step.ProcessDefinitionVersionId, step.OrderIndex });
        builder.HasIndex(step => new { step.ProcessDefinitionVersionId, step.Key }).IsUnique();
        builder.HasIndex(step => step.DependsOnStepId);
        builder.HasIndex(step => step.DependsOnBranchOutcomeId);
        builder.HasIndex(step => step.DecisionRoleRequirementId);
    }
}

internal sealed class ProcessStepDependencyDefinitionConfiguration : IEntityTypeConfiguration<ProcessStepDependencyDefinition> {
    public void Configure(EntityTypeBuilder<ProcessStepDependencyDefinition> builder) {
        builder.ToTable("Processes_StepDependencies");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.StepDefinitionId);
        builder.HasIndex(item => item.DependsOnStepId);
        builder.HasIndex(item => item.DependsOnBranchOutcomeId);
        builder.HasIndex(item => new { item.StepDefinitionId, item.DependsOnStepId, item.DependsOnBranchOutcomeId }).IsUnique();
        builder.HasIndex(item => new { item.StepDefinitionId, item.DisplayOrder });
    }
}

internal sealed class ProcessStepBranchOutcomeDefinitionConfiguration : IEntityTypeConfiguration<ProcessStepBranchOutcomeDefinition> {
    public void Configure(EntityTypeBuilder<ProcessStepBranchOutcomeDefinition> builder) {
        builder.ToTable("Processes_StepBranchOutcomes");
        builder.HasKey(outcome => outcome.Id);
        builder.Property(outcome => outcome.Key).HasMaxLength(120).IsRequired();
        builder.Property(outcome => outcome.Title).HasMaxLength(200).IsRequired();
        builder.Property(outcome => outcome.Description).HasColumnType("TEXT");
        builder.HasIndex(outcome => new { outcome.StepDefinitionId, outcome.Key }).IsUnique();
        builder.HasIndex(outcome => new { outcome.StepDefinitionId, outcome.DisplayOrder });
    }
}

internal sealed class ProcessStepRoleAssignmentRequirementConfiguration : IEntityTypeConfiguration<ProcessStepRoleAssignmentRequirement> {
    public void Configure(EntityTypeBuilder<ProcessStepRoleAssignmentRequirement> builder) {
        builder.ToTable("Processes_StepRoleRequirements");
        builder.HasKey(requirement => requirement.Id);
        builder.Property(requirement => requirement.ResponsibilityKind).HasConversion<string>().HasMaxLength(48);
        builder.Property(requirement => requirement.RebindPolicySummary).HasColumnType("TEXT");
        builder.HasIndex(requirement => new { requirement.StepDefinitionId, requirement.RoleRequirementId, requirement.ResponsibilityKind }).IsUnique();
    }
}

internal sealed class ProcessArtifactExpectationConfiguration : IEntityTypeConfiguration<ProcessArtifactExpectation> {
    public void Configure(EntityTypeBuilder<ProcessArtifactExpectation> builder) {
        builder.ToTable("Processes_ArtifactExpectations");
        builder.HasKey(expectation => expectation.Id);
        builder.Property(expectation => expectation.ArtifactKind).HasConversion<string>().HasMaxLength(48);
        builder.Property(expectation => expectation.Title).HasMaxLength(160).IsRequired();
        builder.Property(expectation => expectation.TrustRequirement).HasConversion<string>().HasMaxLength(48);
        builder.Property(expectation => expectation.SensitivityLevel).HasConversion<string>().HasMaxLength(48);
        builder.Property(expectation => expectation.AllowedFutureUsageSummary).HasColumnType("TEXT");
        builder.Property(expectation => expectation.ValidationRequirementSummary).HasColumnType("TEXT");
        builder.HasIndex(expectation => expectation.StepDefinitionId);
    }
}

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

public sealed class ProcessDefinitionEditorModel {
    public Guid? Id { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? WorkingVersionId { get; set; }

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

    public List<ProcessStepEditorModel> Steps { get; set; } = [];
}

public sealed class ProcessRoleEditorModel {
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

public sealed class ProcessStepEditorModel {
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

    public Guid? DependsOnStepId { get; set; }

    public Guid? DependsOnBranchOutcomeId { get; set; }

    public Guid? DecisionRoleRequirementId { get; set; }

    public double CanvasX { get; set; }

    public double CanvasY { get; set; }

    public double BranchCanvasX { get; set; }

    public double BranchCanvasY { get; set; }

    public List<ProcessStepBranchOutcomeEditorModel> BranchOutcomes { get; set; } = [];

    public List<ProcessStepDependencyEditorModel> Dependencies { get; set; } = [];

    public List<ProcessStepRoleRequirementEditorModel> RoleAssignments { get; set; } = [];

    public List<ProcessArtifactExpectationEditorModel> ArtifactExpectations { get; set; } = [];
}

public sealed class ProcessStepDependencyEditorModel {
    public Guid? Id { get; set; }

    public Guid? DependsOnStepId { get; set; }

    public Guid? DependsOnBranchOutcomeId { get; set; }
}

public sealed class ProcessStepBranchOutcomeEditorModel {
    public Guid? Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class ProcessStepRoleRequirementEditorModel {
    public Guid? Id { get; set; }

    public Guid? RoleRequirementId { get; set; }

    public ProcessResponsibilityKind ResponsibilityKind { get; set; } = ProcessResponsibilityKind.Responsible;

    public bool IsRequired { get; set; } = true;

    public int FallbackOrder { get; set; }

    public string RebindPolicySummary { get; set; } = string.Empty;
}

public sealed class ProcessArtifactExpectationEditorModel {
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
