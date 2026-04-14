using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessImportExportEnvelope
{
    public ProcessDefinitionImportExportModel Definition { get; set; } = new();

    public List<string> Warnings { get; set; } = [];

    public string SourceFormat { get; set; } = string.Empty;
}

public sealed class ProcessDefinitionImportExportModel
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

    public List<ProcessStepImportExportModel> Steps { get; set; } = [];
}

public sealed class ProcessStepImportExportModel
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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? DependsOnStepId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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

    public List<ProcessStepArtifactInputEditorModel> ArtifactInputs { get; set; } = [];
}
