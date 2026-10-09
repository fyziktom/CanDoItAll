using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.UI;

public sealed class ProcessDefinitionDraft {
    private ProcessDefinitionEditorDraftProjection? baseline;
    private string scopeLabel = string.Empty;
    public ProcessDefinitionCatalogItemKey? DefinitionKey { get; set; }
    public ProcessDefinitionEditorVersionToken? VersionToken { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ValueStatement { get; set; } = string.Empty;
    public string ManagerOverrideSummary { get; set; } = string.Empty;
    public string GovernanceNotes { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty;
    public string GovernancePolicySummary { get; set; } = string.Empty;
    public string InterfaceContractSummary { get; set; } = string.Empty;
    public string ConstitutionRuleSummary { get; set; } = string.Empty;
    public string OperatingModeSummary { get; set; } = string.Empty;
    public string SimulationReadinessSummary { get; set; } = string.Empty;
    public ProcessDefinitionCriticalityLevel Criticality { get; set; } = ProcessDefinitionCriticalityLevel.Standard;
    public ProcessDefinitionAutonomyLevel AutonomyLevel { get; set; } = ProcessDefinitionAutonomyLevel.Assisted;
    public ProcessDefinitionOperatingModeKind OperatingMode { get; set; } = ProcessDefinitionOperatingModeKind.AssistedExecution;
    public ProcessDefinitionAuthoringStatus WorkingStatus { get; set; } = ProcessDefinitionAuthoringStatus.TemplateDefault;
    public int StepCount { get; set; }
    public int RequiredRoleCount { get; set; }
    public int RequiredArtifactExpectationCount { get; set; }
    public bool HasConflict { get; private set; }
    public bool IsDirty => baseline is not null && Capture() != baseline;

    public void Observe(ProcessDefinitionEditorProjection? editor) {
        if (editor is null) {
            return;
        }
        if (DefinitionKey == editor.DefinitionKey && IsDirty) {
            HasConflict |= VersionToken != editor.VersionToken;
            return;
        }
        Discard(editor);
    }

    public void Discard(ProcessDefinitionEditorProjection editor) {
        DefinitionKey = editor.DefinitionKey;
        VersionToken = editor.VersionToken;
        scopeLabel = editor.Identity.ScopeLabel;
        HasConflict = false;
        Name = editor.Identity.Name;
        CustomerName = editor.Identity.CustomerName;
        OwnerName = editor.Identity.OwnerName;
        Summary = editor.Identity.Summary;
        ValueStatement = editor.Identity.ValueStatement;
        Criticality = editor.Governance.Criticality == ProcessDefinitionCriticalityLevel.Unspecified
            ? ProcessDefinitionCriticalityLevel.Standard
            : editor.Governance.Criticality;
        AutonomyLevel = editor.Governance.AutonomyLevel == ProcessDefinitionAutonomyLevel.Unspecified
            ? ProcessDefinitionAutonomyLevel.Assisted
            : editor.Governance.AutonomyLevel;
        OperatingMode = editor.Governance.OperatingMode == ProcessDefinitionOperatingModeKind.Unspecified
            ? ProcessDefinitionOperatingModeKind.AssistedExecution
            : editor.Governance.OperatingMode;
        WorkingStatus = editor.Status;
        ManagerOverrideSummary = editor.Governance.ManagerOverrideSummary;
        GovernanceNotes = editor.Governance.GovernanceNotes;
        ChangeSummary = editor.Governance.ChangeSummary;
        GovernancePolicySummary = editor.Governance.GovernancePolicySummary;
        InterfaceContractSummary = editor.Contracts.InterfaceContractSummary;
        ConstitutionRuleSummary = editor.Contracts.ConstitutionRuleSummary;
        OperatingModeSummary = editor.Contracts.OperatingModeSummary;
        SimulationReadinessSummary = editor.Simulation.SimulationReadinessSummary;
        StepCount = editor.Simulation.StepCount;
        RequiredRoleCount = editor.Simulation.RequiredRoleCount;
        RequiredArtifactExpectationCount = editor.Simulation.RequiredArtifactExpectationCount;
        baseline = Capture();
    }

    public void Accept(ProcessDefinitionEditorProjection editor, ProcessDefinitionEditorDraftProjection submitted) {
        if (DefinitionKey != editor.DefinitionKey) {
            return;
        }
        var current = Capture();
        Discard(editor);
        Name = current.Identity.Name == submitted.Identity.Name ? Name : current.Identity.Name;
        CustomerName = current.Identity.CustomerName == submitted.Identity.CustomerName ? CustomerName : current.Identity.CustomerName;
        OwnerName = current.Identity.OwnerName == submitted.Identity.OwnerName ? OwnerName : current.Identity.OwnerName;
        Summary = current.Identity.Summary == submitted.Identity.Summary ? Summary : current.Identity.Summary;
        ValueStatement = current.Identity.ValueStatement == submitted.Identity.ValueStatement ? ValueStatement : current.Identity.ValueStatement;
        WorkingStatus = current.Governance.WorkingStatus == submitted.Governance.WorkingStatus ? WorkingStatus : current.Governance.WorkingStatus;
        ManagerOverrideSummary = current.Governance.ManagerOverrideSummary == submitted.Governance.ManagerOverrideSummary ? ManagerOverrideSummary : current.Governance.ManagerOverrideSummary;
        GovernanceNotes = current.Governance.GovernanceNotes == submitted.Governance.GovernanceNotes ? GovernanceNotes : current.Governance.GovernanceNotes;
        ChangeSummary = current.Governance.ChangeSummary == submitted.Governance.ChangeSummary ? ChangeSummary : current.Governance.ChangeSummary;
        GovernancePolicySummary = current.Governance.GovernancePolicySummary == submitted.Governance.GovernancePolicySummary ? GovernancePolicySummary : current.Governance.GovernancePolicySummary;
        InterfaceContractSummary = current.Contracts.InterfaceContractSummary == submitted.Contracts.InterfaceContractSummary ? InterfaceContractSummary : current.Contracts.InterfaceContractSummary;
        ConstitutionRuleSummary = current.Contracts.ConstitutionRuleSummary == submitted.Contracts.ConstitutionRuleSummary ? ConstitutionRuleSummary : current.Contracts.ConstitutionRuleSummary;
        OperatingModeSummary = current.Contracts.OperatingModeSummary == submitted.Contracts.OperatingModeSummary ? OperatingModeSummary : current.Contracts.OperatingModeSummary;
        SimulationReadinessSummary = current.Simulation.SimulationReadinessSummary == submitted.Simulation.SimulationReadinessSummary ? SimulationReadinessSummary : current.Simulation.SimulationReadinessSummary;
        StepCount = current.Simulation.StepCount == submitted.Simulation.StepCount ? StepCount : current.Simulation.StepCount;
        RequiredRoleCount = current.Simulation.RequiredRoleCount == submitted.Simulation.RequiredRoleCount ? RequiredRoleCount : current.Simulation.RequiredRoleCount;
        RequiredArtifactExpectationCount = current.Simulation.RequiredArtifactExpectationCount == submitted.Simulation.RequiredArtifactExpectationCount ? RequiredArtifactExpectationCount : current.Simulation.RequiredArtifactExpectationCount;
        Criticality = current.Governance.Criticality == submitted.Governance.Criticality ? Criticality : current.Governance.Criticality;
        AutonomyLevel = current.Governance.AutonomyLevel == submitted.Governance.AutonomyLevel ? AutonomyLevel : current.Governance.AutonomyLevel;
        OperatingMode = current.Governance.OperatingMode == submitted.Governance.OperatingMode ? OperatingMode : current.Governance.OperatingMode;
    }

    public ProcessDefinitionEditorDraftProjection Capture()
        => new(
            DefinitionKey ?? throw new InvalidOperationException("No definition draft is open."),
            new ProcessDefinitionEditorIdentityProjection(
                Name,
                scopeLabel,
                CustomerName,
                OwnerName,
                Summary,
                ValueStatement),
            new ProcessDefinitionEditorGovernanceProjection(
                Criticality,
                AutonomyLevel,
                OperatingMode,
                WorkingStatus,
                ManagerOverrideSummary,
                GovernanceNotes,
                ChangeSummary,
                GovernancePolicySummary),
            new ProcessDefinitionEditorContractProjection(
                InterfaceContractSummary,
                ConstitutionRuleSummary,
                OperatingModeSummary),
            new ProcessDefinitionEditorSimulationProjection(
                SimulationReadinessSummary,
                StepCount,
                RequiredRoleCount,
                RequiredArtifactExpectationCount,
                !string.IsNullOrWhiteSpace(SimulationReadinessSummary) && StepCount > 0));

}
