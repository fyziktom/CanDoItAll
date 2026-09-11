using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using static CanDoItAll.AgentFramework.Core.ToolCapabilityMetadataFactory;

namespace CanDoItAll.Modules.Processes;

public static class ProcessCompatibilityToolPolicy {
    private static readonly ToolCapabilityOperationEffects ExternalActionEffects = new(
        [ProcessOperationContractNames.ExternalActionControlled], canExecuteExternalAction: true);

    private static readonly ToolCapabilityOperationEffects ProcessArtifactEffects = new(
        [ProcessOperationContractNames.ExternalArtifactDestination, ProcessOperationContractNames.ManagedProcessArtifactsOnly], canWriteManagedArtifact: true);

    public const string ProcessesDefinitionSave = "processes_definition_save";
    public const string ProcessesDefinitionRoleAdd = "processes_definition_role_add";
    public const string ProcessesDefinitionPublish = "processes_definition_publish";
    public const string ProcessesDefinitionDelete = "processes_definition_delete";
    public const string ProcessesDefinitionImport = "processes_definition_import";
    public const string ProcessesRunStart = "processes_run_start";
    public const string ProcessesStepTransition = "processes_step_transition";
    public const string ProcessesAssignmentResolve = "processes_assignment_resolve";
    public const string ProcessesArtifactRecord = "processes_artifact_record";
    public const string ProcessesDefinitionsList = "processes_definitions_list";
    public const string ProcessesDefinitionEditorGet = "processes_definition_editor_get";
    public const string ProcessesDefinitionExport = "processes_definition_export";
    public const string ProcessesRunsList = "processes_runs_list";
    public const string ProcessesRunDetailGet = "processes_run_detail_get";
    public const string ProcessesAnalyticsGet = "processes_analytics_get";
    public const string ProcessesPartyOptionsList = "processes_party_options_list";
    public const string ProcessesExecutorOptionsList = "processes_executor_options_list";
    public const string ProcessesTemplatesList = "processes_templates_list";
    public const string ProcessesTemplateGet = "processes_template_get";
    public const string ProcessesTemplateMermaidGet = "processes_template_mermaid_get";
    public const string ProcessesTemplateImport = "processes_template_import";
    public const string ProcessesTemplateBaselineScenariosList = "processes_template_baseline_scenarios_list";
    public const string ProcessesTemplateLiveRunProfilesList = "processes_template_live_run_profiles_list";

    public static IReadOnlyList<ToolCapabilityMetadata> Capabilities { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        Mutation(ProcessesDefinitionSave, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProcessesDefinitionRoleAdd, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProcessesDefinitionPublish, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProcessesDefinitionDelete, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProcessesDefinitionImport, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProcessesRunStart, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProcessesStepTransition, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(
            ProcessOperationContractNames.EscalateOrDecide,
            ProcessOperationContractNames.RecoverArtifactsOnly,
            ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Read, CapabilityOperationClassification.Write, CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProcessesAssignmentResolve, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProcessesArtifactRecord, ToolCapabilitySideEffectKind.ProcessMutation, ToolCapabilityOperationRequirementKind.ProcessArtifactWrite, effects: ProcessArtifactEffects),
        Read(ProcessesDefinitionsList, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProcessesDefinitionEditorGet, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProcessesDefinitionExport, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProcessesRunsList, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProcessesRunDetailGet, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProcessesAnalyticsGet, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProcessesPartyOptionsList, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProcessesExecutorOptionsList, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProcessesTemplatesList, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProcessesTemplateGet, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProcessesTemplateMermaidGet, ToolCapabilitySideEffectKind.WorkspaceRead),
        Mutation(ProcessesTemplateImport, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Read(ProcessesTemplateBaselineScenariosList, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProcessesTemplateLiveRunProfilesList, ToolCapabilitySideEffectKind.WorkspaceRead)
    ]);
}
