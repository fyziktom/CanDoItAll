using System.Collections.ObjectModel;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessTemplatePack
{
    public string RootPath { get; init; } = string.Empty;

    public ProcessTemplatePackManifest Manifest { get; init; } = new();

    public IReadOnlyList<ProcessFrameworkSource> FrameworkSources { get; init; } = [];

    public IReadOnlyList<ProcessTemplateToolboxRoleSeed> RoleTemplates { get; init; } = [];

    public IReadOnlyList<ProcessTemplateToolboxStepSeed> StepTemplates { get; init; } = [];

    public ProcessTemplateToolboxChromeCatalog ChromeActions { get; init; } = new();

    public IReadOnlyDictionary<string, ProcessTemplateDefinition> Processes { get; init; } =
        new ReadOnlyDictionary<string, ProcessTemplateDefinition>(new Dictionary<string, ProcessTemplateDefinition>(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyList<ProcessTemplateBaselineScenario> BaselineScenarios { get; init; } = [];

    public IReadOnlyDictionary<string, ProcessTemplateRoleResource> SharedRoles { get; init; } =
        new ReadOnlyDictionary<string, ProcessTemplateRoleResource>(new Dictionary<string, ProcessTemplateRoleResource>(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyDictionary<string, ProcessTemplateArtifactResource> SharedArtifacts { get; init; } =
        new ReadOnlyDictionary<string, ProcessTemplateArtifactResource>(new Dictionary<string, ProcessTemplateArtifactResource>(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyDictionary<string, ProcessTemplateChecklistResource> SharedChecklists { get; init; } =
        new ReadOnlyDictionary<string, ProcessTemplateChecklistResource>(new Dictionary<string, ProcessTemplateChecklistResource>(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyDictionary<string, ProcessTemplateValidationResource> SharedValidations { get; init; } =
        new ReadOnlyDictionary<string, ProcessTemplateValidationResource>(new Dictionary<string, ProcessTemplateValidationResource>(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyDictionary<string, ProcessTemplatePromptResource> SharedPrompts { get; init; } =
        new ReadOnlyDictionary<string, ProcessTemplatePromptResource>(new Dictionary<string, ProcessTemplatePromptResource>(StringComparer.OrdinalIgnoreCase));
}

public sealed class ProcessTemplatePackManifest
{
    public string PackKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string GeneratedAtUtc { get; set; } = string.Empty;

    public List<string> SourceFrameworkKeys { get; set; } = [];

    public string FrameworkSourcesPath { get; set; } = string.Empty;

    public ProcessTemplateToolboxManifest Toolbox { get; set; } = new();

    public ProcessTemplateSeedCatalogManifest SeedCatalog { get; set; } = new();

    public List<ProcessTemplateManifestProcessEntry> Processes { get; set; } = [];
}

public sealed class ProcessTemplateManifestProcessEntry
{
    public string Key { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;
}

public sealed class ProcessTemplateToolboxManifest
{
    public string RoleTemplatesPath { get; set; } = string.Empty;

    public string StepTemplatesPath { get; set; } = string.Empty;

    public string ChromeActionsPath { get; set; } = string.Empty;
}

public sealed class ProcessTemplateSeedCatalogManifest
{
    public string BaselineScenariosPath { get; set; } = string.Empty;
}

public sealed class ProcessFrameworkSource
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Focus { get; set; } = string.Empty;

    public string WhyUsed { get; set; } = string.Empty;

    public string LicenseNote { get; set; } = string.Empty;

    public List<string> ExtractedPatterns { get; set; } = [];
}

public sealed class ProcessTemplateToolboxRoleSeed
{
    public string ActionId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string TemplateRoleKey { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = string.Empty;

    public string DisplayNameTemplate { get; set; } = string.Empty;

    public string PreferredExecutorKind { get; set; } = string.Empty;

    public int DefaultAllocationPercent { get; set; }
}

public sealed class ProcessTemplateToolboxStepSeed
{
    public string ActionId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public ProcessTemplateStepDefinition Template { get; set; } = new();
}

public sealed class ProcessTemplateToolboxChromeCatalog
{
    public List<string> DefinitionQuickCreateActions { get; set; } = [];

    public List<string> DefinitionGroupContextActions { get; set; } = [];

    public List<string> RuntimeQuickActions { get; set; } = [];
}

public sealed class ProcessTemplateDefinition
{
    public string Kind { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

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

    public string Criticality { get; set; } = string.Empty;

    public string AutonomyLevel { get; set; } = string.Empty;

    public string OperatingMode { get; set; } = string.Empty;

    public List<string> SourceFrameworkKeys { get; set; } = [];

    public List<string> SharedRoleRefs { get; set; } = [];

    public List<string> SharedArtifactRefs { get; set; } = [];

    public List<string> SharedChecklistRefs { get; set; } = [];

    public List<string> SharedValidationRefs { get; set; } = [];

    public List<string> SharedPromptRefs { get; set; } = [];

    public List<string> LocalRoleRefs { get; set; } = [];

    public List<string> LocalArtifactRefs { get; set; } = [];

    public List<string> LocalChecklistRefs { get; set; } = [];

    public List<string> LocalValidationRefs { get; set; } = [];

    public List<string> LocalPromptRefs { get; set; } = [];

    public List<string> Metrics { get; set; } = [];

    public List<string> Risks { get; set; } = [];

    public List<string> TailoringRules { get; set; } = [];

    public List<ProcessTemplateRoleUsage> RoleUsages { get; set; } = [];

    public List<ProcessTemplateStepDefinition> Steps { get; set; } = [];

    public string DocPath { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string DefinitionJsonPath { get; set; } = string.Empty;

    public string DefinitionMarkdownPath { get; set; } = string.Empty;

    public string CurrentModuleImportEnvelopePath { get; set; } = string.Empty;

    public string CurrentModuleCompatibilityReportPath { get; set; } = string.Empty;

    public string CurrentModuleCompatibilityReportMarkdownPath { get; set; } = string.Empty;

    public string FlowchartPath { get; set; } = string.Empty;

    public string SequencePath { get; set; } = string.Empty;

    public List<ProcessTemplateRoleResource> LocalRoles { get; set; } = [];

    public List<ProcessTemplateArtifactResource> LocalArtifacts { get; set; } = [];

    public List<ProcessTemplateChecklistResource> LocalChecklists { get; set; } = [];

    public List<ProcessTemplateValidationResource> LocalValidations { get; set; } = [];

    public List<ProcessTemplatePromptResource> LocalPrompts { get; set; } = [];
}

public sealed class ProcessTemplateRoleUsage
{
    public string Key { get; set; } = string.Empty;

    public string RoleResourceKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string StaffingIntent { get; set; } = string.Empty;

    public string PreferredExecutorKind { get; set; } = string.Empty;

    public string PreferredProjectAssignmentRole { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;

    public bool AllowsFallback { get; set; } = true;

    public bool RequiresExplicitApproval { get; set; }

    public int DefaultAllocationPercent { get; set; }

    public double CanvasX { get; set; }

    public double CanvasY { get; set; }

    public string Notes { get; set; } = string.Empty;
}

public sealed class ProcessTemplateStepDefinition
{
    public int Order { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string StepKind { get; set; } = string.Empty;

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

    public string DependsOnStepKey { get; set; } = string.Empty;

    public string DependsOnBranchOutcomeKey { get; set; } = string.Empty;

    public string DecisionRoleKey { get; set; } = string.Empty;

    public double CanvasX { get; set; }

    public double CanvasY { get; set; }

    public double BranchCanvasX { get; set; }

    public double BranchCanvasY { get; set; }

    public List<ProcessTemplateBranchOutcomeDefinition> BranchOutcomes { get; set; } = [];

    public List<ProcessTemplateStepDependency> Dependencies { get; set; } = [];

    public List<ProcessTemplateStepRoleAssignment> RoleAssignments { get; set; } = [];

    public List<ProcessTemplateArtifactExpectation> ArtifactExpectations { get; set; } = [];

    public List<ProcessTemplateStepArtifactInput> ArtifactInputs { get; set; } = [];

    public List<string> ChecklistRefs { get; set; } = [];

    public List<string> ValidationRefs { get; set; } = [];

    public List<string> PromptRefs { get; set; } = [];

    public List<string> DocRefs { get; set; } = [];
}

public sealed class ProcessTemplateStepDependency
{
    public string DependsOnStepKey { get; set; } = string.Empty;

    public string DependsOnBranchOutcomeKey { get; set; } = string.Empty;
}

public sealed class ProcessTemplateBranchOutcomeDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class ProcessTemplateStepRoleAssignment
{
    public string RoleKey { get; set; } = string.Empty;

    public string ResponsibilityKind { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;

    public int FallbackOrder { get; set; }

    public string RebindPolicySummary { get; set; } = string.Empty;
}

public sealed class ProcessTemplateArtifactExpectation
{
    public string Key { get; set; } = string.Empty;

    public string TemplateKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ArtifactKind { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;

    public string TrustRequirement { get; set; } = string.Empty;

    public string SensitivityLevel { get; set; } = string.Empty;

    public int RetentionDays { get; set; }

    public string AllowedFutureUsageSummary { get; set; } = string.Empty;

    public string ValidationRequirementSummary { get; set; } = string.Empty;
}

public sealed class ProcessTemplateStepArtifactInput
{
    public string SourceStepKey { get; set; } = string.Empty;

    public string ArtifactExpectationKey { get; set; } = string.Empty;
}

public sealed class ProcessTemplateRoleResource
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string StaffingIntent { get; set; } = string.Empty;

    public string SnapshotSummary { get; set; } = string.Empty;

    public string PreferredExecutorKind { get; set; } = string.Empty;

    public string PreferredProjectAssignmentRole { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;

    public bool AllowsFallback { get; set; } = true;

    public bool RequiresExplicitApproval { get; set; }

    public int DefaultAllocationPercent { get; set; }

    public string RoleTemplateSourceKey { get; set; } = string.Empty;

    public string RoleTemplateSnapshotName { get; set; } = string.Empty;

    public string SeniorityBand { get; set; } = string.Empty;

    public int MinimumYearsInPrimaryDiscipline { get; set; }

    public int MinimumYearsInSoftwareDelivery { get; set; }

    public List<string> DomainTags { get; set; } = [];

    public List<string> KnowledgeRequirements { get; set; } = [];

    public List<string> ExperienceRequirements { get; set; } = [];

    public List<string> DecisionRights { get; set; } = [];

    public List<string> OwnedArtifacts { get; set; } = [];

    public List<string> CollaborationExpectations { get; set; } = [];

    public List<string> AntiPatterns { get; set; } = [];

    public List<string> FitnessEvidence { get; set; } = [];

    public string Scope { get; set; } = string.Empty;

    public string ProcessKey { get; set; } = string.Empty;

    public string DocPath { get; set; } = string.Empty;
}

public sealed class ProcessTemplateArtifactResource
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ArtifactKind { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string OwnerRoleKey { get; set; } = string.Empty;

    public string DefaultTrustRequirement { get; set; } = string.Empty;

    public string DefaultSensitivityLevel { get; set; } = string.Empty;

    public int DefaultRetentionDays { get; set; }

    public string AllowedFutureUsageSummary { get; set; } = string.Empty;

    public string ValidationRequirementSummary { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string ProcessKey { get; set; } = string.Empty;

    public string DocPath { get; set; } = string.Empty;
}

public sealed class ProcessTemplateChecklistResource
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string OwnerRoleKey { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string EntryCriteria { get; set; } = string.Empty;

    public string ExitCriteria { get; set; } = string.Empty;

    public List<string> Checks { get; set; } = [];

    public List<string> EvidenceExpectations { get; set; } = [];

    public string Scope { get; set; } = string.Empty;

    public string ProcessKey { get; set; } = string.Empty;

    public string DocPath { get; set; } = string.Empty;
}

public sealed class ProcessTemplateValidationResource
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string OwnerRoleKey { get; set; } = string.Empty;

    public string Gate { get; set; } = string.Empty;

    public string FailureSeverity { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string PassCriteria { get; set; } = string.Empty;

    public string FailCriteria { get; set; } = string.Empty;

    public string EscalationRule { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string ProcessKey { get; set; } = string.Empty;

    public string DocPath { get; set; } = string.Empty;
}

public sealed class ProcessTemplatePromptResource
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string AudienceRoleKey { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<string> RequiredInputs { get; set; } = [];

    public List<string> OutputSchema { get; set; } = [];

    public List<string> RefusalConditions { get; set; } = [];

    public string Scope { get; set; } = string.Empty;

    public string ProcessKey { get; set; } = string.Empty;

    public string DocPath { get; set; } = string.Empty;
}

public sealed class ProcessTemplateBaselineScenario
{
    public string Key { get; set; } = string.Empty;

    public string ProcessTemplateKey { get; set; } = string.Empty;

    public string RunName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string OperatingMode { get; set; } = string.Empty;

    public string TriggerReason { get; set; } = string.Empty;

    public List<ProcessTemplateBaselineAssignment> Assignments { get; set; } = [];

    public List<ProcessTemplateBaselineTransition> Transitions { get; set; } = [];

    public List<ProcessTemplateBaselineArtifactRecord> Artifacts { get; set; } = [];
}

public sealed class ProcessTemplateBaselineAssignment
{
    public string StepKey { get; set; } = string.Empty;

    public string RoleKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ExecutorKind { get; set; } = string.Empty;

    public string BindingReason { get; set; } = string.Empty;

    public bool IsFallback { get; set; }
}

public sealed class ProcessTemplateBaselineTransition
{
    public string StepKey { get; set; } = string.Empty;

    public string TargetStatus { get; set; } = string.Empty;

    public string SelectedBranchOutcomeKey { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string DecidedBy { get; set; } = string.Empty;
}

public sealed class ProcessTemplateBaselineArtifactRecord
{
    public string StepKey { get; set; } = string.Empty;

    public string ArtifactKind { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string TrustStatus { get; set; } = string.Empty;

    public string SensitivityLevel { get; set; } = string.Empty;

    public string ProvenanceSummary { get; set; } = string.Empty;

    public string AllowedFutureUsageSummary { get; set; } = string.Empty;

    public string ReviewSummary { get; set; } = string.Empty;
}

public sealed record ProcessTemplateCatalogItem(
    string Key,
    string DisplayName,
    string Summary,
    string Criticality,
    string AutonomyLevel,
    int StepCount,
    int SharedRoleCount,
    int LocalRoleCount,
    string RelativePath);

public sealed record ProcessTemplateMermaidDocument(
    string ProcessKey,
    string ProcessName,
    string Flowchart,
    string Sequence,
    IReadOnlyList<string> SupportingFiles);

public sealed record ProcessTemplateImportResult(
    string ProcessKey,
    Guid DefinitionId,
    IReadOnlyList<string> Warnings);

public sealed record ProcessTemplateBaselineScenarioSummary(
    string Key,
    string ProcessTemplateKey,
    string RunName,
    string OperatingMode,
    int AssignmentCount,
    int TransitionCount,
    int ArtifactCount);
