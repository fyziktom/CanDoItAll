using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using static CanDoItAll.Tests.Support.ProductToolPolicyTestRegistration;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ToolCapabilityOperationEffectsTests {
    [Fact]
    public void Every_installed_metadata_field_matches_the_independent_pre_cut_catalog() {
        var expected = Rows(ExpectedMetadata);
        var actual = ProductToolPolicies.Capabilities.OrderBy(policy => policy.Name, StringComparer.Ordinal).Select(Describe).ToArray();
        Assert.Equal(213, actual.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Every_runtime_descriptor_preserves_identity_classifications_and_side_effect_profile() {
        var actual = ProductToolPolicies.Capabilities.OrderBy(policy => policy.Name, StringComparer.Ordinal)
            .Select(policy => RuntimeToolCapabilityDescriptorFactory.CreateRuntimeToolCapabilityDescriptor(
                policy.Name, policy.Name, "Descriptor parity", [], toolPolicies: ProductToolPolicies))
            .Select(Describe).ToArray();
        Assert.Equal(213, actual.Length);
        Assert.Equal(Rows(ExpectedDescriptors), actual);
    }

    [Fact]
    public void Every_configured_tool_descriptor_preserves_the_same_owner_policy_and_configuration_identity() {
        var catalog = new RuntimeCapabilityDescriptorCatalog(ProductToolPolicies);
        var actual = ProductToolPolicies.Capabilities.OrderBy(policy => policy.Name, StringComparer.Ordinal)
            .Select(policy => catalog.CreateCatalogCapabilityDescriptor(new CapabilityCatalogItem(
                Guid.NewGuid(), CanDoItAll.AgentFramework.Models.CapabilityKind.Tool, policy.Name.Replace('_', '-'),
                policy.Name, "Configured descriptor parity", string.Empty, JsonSerializer.Serialize(new { tool = policy.Name }),
                CapabilityProofStatus.Verified, string.Empty, DateTimeOffset.UnixEpoch, true)))
            .Select(Describe).ToArray();
        var expected = Rows(ExpectedDescriptors).Select(row => {
            var values = row.Split('|');
            values[2] = "maf.tool." + values[1];
            return string.Join('|', values);
        }).ToArray();
        Assert.Equal(213, actual.Length);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(ToolCapabilityOperationRequirementKind.WorkspaceFileMutation)]
    [InlineData(ToolCapabilityOperationRequirementKind.WorkspaceScript)]
    [InlineData(ToolCapabilityOperationRequirementKind.DotNetRun)]
    [InlineData(ToolCapabilityOperationRequirementKind.ProcessArtifactWrite)]
    public void Dynamic_requirements_cannot_silently_receive_missing_owner_effects(ToolCapabilityOperationRequirementKind kind) {
        var exception = Assert.Throws<InvalidOperationException>(() => ToolCapabilityMetadataFactory.Mutation(
            "owner_missing_effects", ToolCapabilitySideEffectKind.WorkspaceWrite, kind));
        Assert.Contains("owner-provided operation effects", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Static_operation_requirements_cannot_silently_receive_missing_owner_effects() {
        Assert.Throws<InvalidOperationException>(() => ToolCapabilityMetadataFactory.Read("owner_static_read",
            ToolCapabilitySideEffectKind.InternalDataRead, ToolCapabilityMetadataFactory.StaticRequirement("owner.operation")));
    }

    [Fact]
    public void External_action_declarations_require_explicit_owner_effects() {
        Assert.Throws<InvalidOperationException>(() => ToolCapabilityMetadataFactory.Mutation(
            "owner_external_action", ToolCapabilitySideEffectKind.ExternalAction));
    }

    [Fact]
    public void Core_carries_opaque_owner_facts_without_deriving_privileges_from_historical_operation_tokens() {
        var requirements = ToolCapabilityMetadataFactory.StaticRequirement("owner.operation", ProcessOperationContractNames.MutateProductTarget);
        var policy = ToolCapabilityMetadataFactory.Mutation("owner_opaque", ToolCapabilitySideEffectKind.InternalStateMutation,
            requirements, new(["owner-scope"], canWriteManagedArtifact: true));
        Assert.Equal(new[] { "owner.operation", ProcessOperationContractNames.MutateProductTarget }, Assert.Single(policy.OperationRequirements).AnyOf);
        Assert.Equal(new[] { "owner-scope" }, policy.TargetScopeRequirements);
        Assert.False(policy.CanMutateProduct);
        Assert.False(policy.CanExecuteExternalAction);
        Assert.True(policy.CanWriteManagedArtifact);
        Assert.True(policy.RequiresApprovalByDefault);
        Assert.Equal(ToolInvocationClassification.Mutation, policy.Classification);
    }

    [Fact]
    public void Supplied_effects_snapshot_cannot_be_changed_through_the_callers_list_or_returned_collection() {
        var scopes = new List<string> { "scope-b", "scope-a", "scope-b" };
        var effects = new ToolCapabilityOperationEffects(scopes, canMutateProduct: true, canExecuteExternalAction: true);
        scopes.Clear();
        var policy = ToolCapabilityMetadataFactory.Validation("owner_validation", ToolCapabilitySideEffectKind.WorkspaceRead,
            ToolCapabilityOperationRequirementKind.DotNetRun, effects);
        Assert.Equal(new[] { "scope-a", "scope-b" }, policy.TargetScopeRequirements);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)effects.TargetScopeRequirements)[0] = "substituted");
        Assert.True(policy.CanMutateProduct);
        Assert.True(policy.CanExecuteExternalAction);
        Assert.False(policy.CanWriteManagedArtifact);
        Assert.Throws<ArgumentNullException>(() => new ToolCapabilityOperationEffects(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" scope")]
    public void Invalid_owner_scope_identifiers_are_rejected(string? scope) {
        Assert.Throws<ArgumentException>(() => new ToolCapabilityOperationEffects([scope!]));
    }

    private static string[] Rows(string value) => value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    private static string Flag(bool value) => value ? "true" : "false";

    private static string Describe(ToolCapabilityMetadata policy) => string.Join('|', policy.Name, policy.Classification.ToString(),
        Flag(policy.RequiresApprovalByDefault), Flag(policy.IsStateChanging), policy.SideEffectKind.ToString(),
        policy.OperationRequirementKind.ToString(), string.Join(';', policy.OperationRequirements.Select(requirement => string.Join(',', requirement.AnyOf))),
        string.Join(',', policy.TargetScopeRequirements), Flag(policy.CanMutateProduct), Flag(policy.CanExecuteExternalAction),
        Flag(policy.CanReadExternalTarget), Flag(policy.CanWriteManagedArtifact), policy.BrowserProofRole.ToString(),
        policy.IdempotencyDescriptor.ToString(), policy.BusinessArgumentRetentionScheme ?? string.Empty, Flag(policy.ProtectRuntimeStateOnExport),
        string.Join(',', policy.OperationClassifications));

    private static string Describe(CapabilityExposureDescriptor descriptor) => string.Join('|', descriptor.RuntimeToolName!.Value.Value,
        descriptor.Identity.Key.Value, descriptor.ImplementationKey!.Value.Value,
        string.Join(',', descriptor.OperationClassifications.Select(value => value.ToString()).Order(StringComparer.Ordinal)),
        descriptor.SideEffectProfile.Kind.ToString(), Flag(descriptor.SideEffectProfile.RequiresApprovalByDefault), Flag(descriptor.SideEffectProfile.IsStateChanging));

    private const string ExpectedMetadata = """
agent_package_export|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
browser_click|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|Interaction|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
browser_console_messages|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|Observation|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
browser_drag|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|Interaction|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
browser_evaluate|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|Observation|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
browser_fill_form|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|Interaction|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
browser_navigate|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|Navigation|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
browser_network_requests|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|Observation|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
browser_press_key|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|Interaction|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
browser_resize|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|Navigation|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
browser_select_option|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|Interaction|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
browser_snapshot|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|EvidenceCapture|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
browser_take_screenshot|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|EvidenceCapture|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
browser_type|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|Interaction|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
browser_wait_for|Validation|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|Observation|RuntimeStateDependent||false|Validation,BrowserAccess,ResourceCleanup
capability_curator_assignment_editor_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
capability_curator_assignment_update|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|capability-curator-approval-redacted-v1|false|
capability_curator_catalog_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|capability-curator-approval-redacted-v1|false|
capability_curator_editor_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
capability_curator_mcp_setup_test|Mutation|true|true|ExternalAction|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|ExternalSideEffect|capability-curator-approval-redacted-v1|false|ExternalAction,ScriptExecution
capability_curator_save|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|capability-curator-approval-redacted-v1|false|
capability_curator_tool_setup_test|Mutation|true|true|ExternalAction|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|ExternalSideEffect|capability-curator-approval-redacted-v1|false|ExternalAction,ScriptExecution
capability_curator_verify|Mutation|true|true|ExternalAction|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|ExternalSideEffect|capability-curator-approval-redacted-v1|false|ExternalAction,ScriptExecution
hr_agent_avatar_generate|Mutation|true|true|MediaGeneration|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging|hr-approval-redacted-v1|true|ExternalAction,ScriptExecution
hr_agent_create|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true|
hr_agent_creation_options_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
hr_agent_process_history_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
hr_agent_process_manager_review_request|Mutation|true|true|ExternalAction|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|ExternalSideEffect|hr-approval-redacted-v1|true|ExternalAction,ScriptExecution
hr_agent_settings_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
hr_agent_settings_update|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true|
hr_agent_usage_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
hr_agents_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true|
hr_crm_affiliation_upsert|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true|
hr_crm_item_summary_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
hr_crm_party_affiliations_list|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true|
hr_crm_party_create|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true|
hr_crm_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true|
hr_simple_chat_create|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true|
hr_simple_chat_create_receipt_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true|
hr_simple_chat_creation_options_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true|
hr_simple_chat_settings_get|Read|true|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true|
hr_simple_chat_settings_update|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true|
hr_simple_chat_status_change|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true|
hr_simple_chats_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true|
image_generation_create|Mutation|true|true|MediaGeneration|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
load_skill|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
local_mcp_launch|Mutation|true|true|LocalProcessExecution|Static|ExecuteExternalAction|ExternalActionControlled|false|true|true|false|None|ExternalSideEffect||false|ExternalAction,ScriptExecution
processes_analytics_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
processes_artifact_record|Mutation|true|true|ProcessMutation|ProcessArtifactWrite||ExternalArtifactDestination,ManagedProcessArtifactsOnly|false|false|false|true|None|StateChanging||false|
processes_assignment_resolve|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
processes_definition_delete|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
processes_definition_editor_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
processes_definition_export|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
processes_definition_import|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
processes_definition_publish|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
processes_definition_role_add|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
processes_definition_save|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
processes_definitions_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
processes_executor_options_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
processes_party_options_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
processes_run_detail_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
processes_run_start|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
processes_runs_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
processes_step_transition|Mutation|true|true|ProcessMutation|Static|EscalateOrDecide,RecoverArtifactsOnly,ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|Read,Write,ExternalAction,ScriptExecution
processes_template_baseline_scenarios_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
processes_template_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
processes_template_import|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
processes_template_live_run_profiles_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
processes_template_mermaid_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
processes_templates_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_plan_summary_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_analytics_query|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_approval_request|Mutation|true|true|ProjectStructureMutation|Static|EscalateOrDecide,ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|Read,ExternalAction,ScriptExecution
project_structure_asset_content_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_asset_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_asset_create_revision|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_asset_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_asset_image_analyze|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_asset_text_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_checklist|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_dependencies_query|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_dependency_link|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_dependency_unlink|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_hierarchy_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_import|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_knowledge_query|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_lease_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_lease_release|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_lease_renew|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_link_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_link_unlink|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_catalog|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_node_command_execute|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_delete|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_descendants_to_project_move|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_marker_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_metadata_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_move|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_priority_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_process_definition_link|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_process_start|Mutation|true|true|ProjectStructureMutation|Static|StartProjectNodeProcess|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ProjectStructure,ExternalAction,ScriptExecution
project_structure_node_progress_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_recompose|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_reparent|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_status_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_type_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_workflow_add_options|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_node_workflow_definition_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_workflow_start|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_node_workflow_status_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_nodes_copy|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_nodes_delete|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_nodes_marker_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_nodes_priority_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_nodes_progress_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_nodes_status_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_nodes_to_new_subproject|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_process_subprocess_launch|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_project_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_project_lease_acquire|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_project_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_projects_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_read|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
project_structure_repo_branch_lease_acquire|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_subproject_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_structure_subproject_link|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_task_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_task_resource_attach|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
project_task_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false|ExternalAction,ScriptExecution
prompt_gallery_catalog_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|prompt-curator-approval-redacted-v1|false|
prompt_gallery_draft_create|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|prompt-curator-approval-redacted-v1|false|
prompt_gallery_draft_update|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|prompt-curator-approval-redacted-v1|false|
prompt_gallery_item_editor_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
prompt_gallery_item_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
prompt_gallery_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
prompt_gallery_version_create|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|prompt-curator-approval-redacted-v1|false|
provider_health|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
read_skill_resource|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
run_skill_script|Mutation|true|true|LocalProcessExecution|Static|ExecuteExternalAction|ExternalActionControlled|false|true|true|false|None|ExternalSideEffect||false|ExternalAction,ScriptExecution
scheduler_workflow_schedule_create|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|scheduler-approval-redacted-v1|false|
scheduler_workflow_schedules_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|scheduler-approval-redacted-v1|false|
scheduler_workflow_targets_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|scheduler-approval-redacted-v1|false|
storage_browse|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
storage_catalog_list|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
storage_delete_object|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging||false|
storage_read_text_file|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
storage_write_text_file|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging||false|
submit_architecture_review_result|Read|false|false|None|None|||false|false|true|false|None|Idempotent||false|
submit_code_review_result|Read|false|false|None|None|||false|false|true|false|None|Idempotent||false|
submit_human_escalation_request|Read|false|false|None|None|||false|false|true|false|None|Idempotent||false|
submit_implementation_plan|Read|false|false|None|None|||false|false|true|false|None|Idempotent||false|
submit_process_state_patch|Read|false|false|None|None|||false|false|true|false|None|Idempotent||false|
submit_process_step_outcome|Read|false|false|None|None|||false|false|true|false|None|Idempotent||false|
submit_test_plan|Read|false|false|None|None|||false|false|true|false|None|Idempotent||false|
submit_tool_execution_decision|Read|false|false|None|None|||false|false|true|false|None|Idempotent||false|
workflow_curator_authoring_options_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
workflow_curator_catalog_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|workflow-curator-approval-redacted-v1|false|
workflow_curator_definition_editor_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false|
workflow_curator_draft_create|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|workflow-curator-approval-redacted-v1|false|
workflow_curator_draft_update|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|workflow-curator-approval-redacted-v1|false|
workflow_curator_lifecycle_change|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|workflow-curator-approval-redacted-v1|false|
workflow_curator_node_update|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|workflow-curator-approval-redacted-v1|false|
workflows_definitions_list|Read|false|false|None|None|||false|false|true|false|None|Idempotent||false|
workflows_external_response_submit|Mutation|true|true|RuntimeLaunch|Static|ExecuteExternalAction|ExternalActionControlled|false|true|true|false|None|StateChanging|workflow-curator-approval-redacted-v1|false|ExternalAction,ScriptExecution
workflows_run_cancel|Mutation|true|true|RuntimeLaunch|Static|ExecuteExternalAction|ExternalActionControlled|false|true|true|false|None|StateChanging||false|ExternalAction,ScriptExecution
workflows_run_start|Mutation|true|true|RuntimeLaunch|Static|LaunchRuntime|ExternalProductTargetReadOnly|false|false|true|false|None|StateChanging|workflow-curator-approval-redacted-v1|false|Validation,RuntimeLaunch,ResourceCleanup,ScriptExecution
workflows_run_status_get|Read|false|false|None|None|||false|false|true|false|None|Idempotent||false|
workspace_analyze_image|Read|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof,ReadProjectStructure|ExternalProductTargetReadOnly|false|false|true|false|None|Idempotent||false|Validation,BrowserAccess,ResourceCleanup,Read,ProjectStructure
workspace_analyze_images|Read|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof,ReadProjectStructure|ExternalProductTargetReadOnly|false|false|true|false|None|Idempotent||false|Validation,BrowserAccess,ResourceCleanup,Read,ProjectStructure
workspace_append_file|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_command_run|Mutation|true|true|LocalProcessExecution|Static|ExecuteExternalAction|ExternalActionControlled|false|true|true|false|None|ExternalSideEffect||false|ExternalAction,ScriptExecution
workspace_convert_document|Validation|false|false|DocumentConversion|Static|ReadProjectStructure,WriteManagedProcessArtifacts|ExternalProductTargetReadOnly,ManagedProcessArtifactsOnly|false|false|true|true|None|RuntimeStateDependent||false|Read,ProjectStructure,Write
workspace_copy_path|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_create_directory|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_delete_path|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_diff_text|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_dotnet_build|Validation|false|false|LocalProcessExecution|Static|RunValidation|ExternalProductTargetReadOnly|false|false|true|false|None|ExternalSideEffect||false|Validation,ScriptExecution
workspace_dotnet_new|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_dotnet_restore|Validation|false|false|LocalProcessExecution|Static|RunValidation|ExternalProductTargetReadOnly|false|false|true|false|None|ExternalSideEffect||false|Validation,ScriptExecution
workspace_dotnet_run|Validation|false|false|RuntimeLaunch|DotNetRun||ExternalProductTargetReadOnly|false|false|true|false|None|RuntimeStateDependent||false|
workspace_dotnet_stop|Validation|false|false|RuntimeLaunch|Static|LaunchRuntime,CaptureRuntimeProof|ExternalProductTargetReadOnly|false|false|true|false|None|RuntimeStateDependent||false|Validation,RuntimeLaunch,ResourceCleanup,ScriptExecution,BrowserAccess
workspace_dotnet_test|Validation|false|false|LocalProcessExecution|Static|RunValidation|ExternalProductTargetReadOnly|false|false|true|false|None|ExternalSideEffect||false|Validation,ScriptExecution
workspace_execution_boundary|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_git_add|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_git_branch_create|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_git_commit|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_git_diff|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_git_log|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_git_show|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_git_status|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_git_switch|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_git_unstage|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_hash_path|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_inspect_image|Read|false|false|RuntimeProofCapture|Static|CaptureRuntimeProof,ReadProjectStructure|ExternalProductTargetReadOnly|false|false|true|false|None|Idempotent||false|Validation,BrowserAccess,ResourceCleanup,Read,ProjectStructure
workspace_inspect_spreadsheet|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_list_directory|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_list_files|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_move_path|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_pwsh_run_script|Mutation|true|true|LocalProcessExecution|WorkspaceScript||ExternalActionControlled,ExternalArtifactDestination,ExternalProductTargetMutable,ExternalProductTargetReadOnly,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|true|true|true|None|ExternalSideEffect||false|
workspace_python_run_file|Mutation|true|true|LocalProcessExecution|WorkspaceScript||ExternalActionControlled,ExternalArtifactDestination,ExternalProductTargetMutable,ExternalProductTargetReadOnly,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|true|true|true|None|ExternalSideEffect||false|
workspace_read_file|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_read_spreadsheet_cell|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_read_spreadsheet_range|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_search|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_spreadsheet_function_catalog|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_spreadsheet_summary|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_stat_path|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false|
workspace_unzip_archive|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_write_file|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_write_spreadsheet|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
workspace_zip_path|Mutation|true|true|WorkspaceWrite|WorkspaceFileMutation||ExternalArtifactDestination,ExternalProductTargetMutable,ManagedOutputProduct,ManagedProcessArtifactsOnly|true|false|true|true|None|StateChanging||false|
""";

    private const string ExpectedDescriptors = """
agent_package_export|agent-package-export|maf.agent_package_export|Read|WorkspaceRead|false|false
browser_click|browser-click|maf.browser_click|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
browser_console_messages|browser-console-messages|maf.browser_console_messages|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
browser_drag|browser-drag|maf.browser_drag|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
browser_evaluate|browser-evaluate|maf.browser_evaluate|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
browser_fill_form|browser-fill-form|maf.browser_fill_form|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
browser_navigate|browser-navigate|maf.browser_navigate|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
browser_network_requests|browser-network-requests|maf.browser_network_requests|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
browser_press_key|browser-press-key|maf.browser_press_key|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
browser_resize|browser-resize|maf.browser_resize|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
browser_select_option|browser-select-option|maf.browser_select_option|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
browser_snapshot|browser-snapshot|maf.browser_snapshot|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
browser_take_screenshot|browser-take-screenshot|maf.browser_take_screenshot|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
browser_type|browser-type|maf.browser_type|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
browser_wait_for|browser-wait-for|maf.browser_wait_for|BrowserAccess,ResourceCleanup,Validation|RuntimeProofCapture|false|false
capability_curator_assignment_editor_get|capability-curator-assignment-editor-get|maf.capability_curator_assignment_editor_get|Read|InternalDataRead|false|false
capability_curator_assignment_update|capability-curator-assignment-update|maf.capability_curator_assignment_update|Mutation|InternalStateMutation|true|true
capability_curator_catalog_search|capability-curator-catalog-search|maf.capability_curator_catalog_search|Read|InternalDataRead|false|false
capability_curator_editor_get|capability-curator-editor-get|maf.capability_curator_editor_get|Read|InternalDataRead|false|false
capability_curator_mcp_setup_test|capability-curator-mcp-setup-test|maf.capability_curator_mcp_setup_test|ExternalAction,ScriptExecution|ExternalAction|true|true
capability_curator_save|capability-curator-save|maf.capability_curator_save|Mutation|InternalStateMutation|true|true
capability_curator_tool_setup_test|capability-curator-tool-setup-test|maf.capability_curator_tool_setup_test|ExternalAction,ScriptExecution|ExternalAction|true|true
capability_curator_verify|capability-curator-verify|maf.capability_curator_verify|ExternalAction,ScriptExecution|ExternalAction|true|true
hr_agent_avatar_generate|hr-agent-avatar-generate|maf.hr_agent_avatar_generate|ExternalAction,ScriptExecution|MediaGeneration|true|true
hr_agent_create|hr-agent-create|maf.hr_agent_create|Mutation|InternalStateMutation|true|true
hr_agent_creation_options_get|hr-agent-creation-options-get|maf.hr_agent_creation_options_get|Read|InternalDataRead|false|false
hr_agent_process_history_get|hr-agent-process-history-get|maf.hr_agent_process_history_get|Read|InternalDataRead|false|false
hr_agent_process_manager_review_request|hr-agent-process-manager-review-request|maf.hr_agent_process_manager_review_request|ExternalAction,ScriptExecution|ExternalAction|true|true
hr_agent_settings_get|hr-agent-settings-get|maf.hr_agent_settings_get|Read|InternalDataRead|false|false
hr_agent_settings_update|hr-agent-settings-update|maf.hr_agent_settings_update|Mutation|InternalStateMutation|true|true
hr_agent_usage_get|hr-agent-usage-get|maf.hr_agent_usage_get|Read|InternalDataRead|false|false
hr_agents_search|hr-agents-search|maf.hr_agents_search|Read|InternalDataRead|false|false
hr_crm_affiliation_upsert|hr-crm-affiliation-upsert|maf.hr_crm_affiliation_upsert|Mutation|InternalStateMutation|true|true
hr_crm_item_summary_get|hr-crm-item-summary-get|maf.hr_crm_item_summary_get|Read|InternalDataRead|false|false
hr_crm_party_affiliations_list|hr-crm-party-affiliations-list|maf.hr_crm_party_affiliations_list|Read|InternalDataRead|false|false
hr_crm_party_create|hr-crm-party-create|maf.hr_crm_party_create|Mutation|InternalStateMutation|true|true
hr_crm_search|hr-crm-search|maf.hr_crm_search|Read|InternalDataRead|false|false
hr_simple_chat_create|hr-simple-chat-create|maf.hr_simple_chat_create|Mutation|InternalStateMutation|true|true
hr_simple_chat_create_receipt_get|hr-simple-chat-create-receipt-get|maf.hr_simple_chat_create_receipt_get|Read|InternalDataRead|false|false
hr_simple_chat_creation_options_get|hr-simple-chat-creation-options-get|maf.hr_simple_chat_creation_options_get|Read|InternalDataRead|false|false
hr_simple_chat_settings_get|hr-simple-chat-settings-get|maf.hr_simple_chat_settings_get|Read|InternalDataRead|true|false
hr_simple_chat_settings_update|hr-simple-chat-settings-update|maf.hr_simple_chat_settings_update|Mutation|InternalStateMutation|true|true
hr_simple_chat_status_change|hr-simple-chat-status-change|maf.hr_simple_chat_status_change|Mutation|InternalStateMutation|true|true
hr_simple_chats_search|hr-simple-chats-search|maf.hr_simple_chats_search|Read|InternalDataRead|false|false
image_generation_create|image-generation-create|maf.image_generation_create|ExternalAction,ScriptExecution|MediaGeneration|true|true
load_skill|load-skill|maf.load_skill|Read|WorkspaceRead|false|false
local_mcp_launch|local-mcp-launch|maf.local_mcp_launch|ExternalAction,ScriptExecution|LocalProcessExecution|true|true
processes_analytics_get|processes-analytics-get|maf.processes_analytics_get|Read|WorkspaceRead|false|false
processes_artifact_record|processes-artifact-record|maf.processes_artifact_record|Write|ProcessMutation|true|true
processes_assignment_resolve|processes-assignment-resolve|maf.processes_assignment_resolve|ExternalAction,ScriptExecution|ProcessMutation|true|true
processes_definition_delete|processes-definition-delete|maf.processes_definition_delete|ExternalAction,ScriptExecution|ProcessMutation|true|true
processes_definition_editor_get|processes-definition-editor-get|maf.processes_definition_editor_get|Read|WorkspaceRead|false|false
processes_definition_export|processes-definition-export|maf.processes_definition_export|Read|WorkspaceRead|false|false
processes_definition_import|processes-definition-import|maf.processes_definition_import|ExternalAction,ScriptExecution|ProcessMutation|true|true
processes_definition_publish|processes-definition-publish|maf.processes_definition_publish|ExternalAction,ScriptExecution|ProcessMutation|true|true
processes_definition_role_add|processes-definition-role-add|maf.processes_definition_role_add|ExternalAction,ScriptExecution|ProcessMutation|true|true
processes_definition_save|processes-definition-save|maf.processes_definition_save|ExternalAction,ScriptExecution|ProcessMutation|true|true
processes_definitions_list|processes-definitions-list|maf.processes_definitions_list|Read|WorkspaceRead|false|false
processes_executor_options_list|processes-executor-options-list|maf.processes_executor_options_list|Read|WorkspaceRead|false|false
processes_party_options_list|processes-party-options-list|maf.processes_party_options_list|Read|WorkspaceRead|false|false
processes_run_detail_get|processes-run-detail-get|maf.processes_run_detail_get|Read|WorkspaceRead|false|false
processes_run_start|processes-run-start|maf.processes_run_start|ExternalAction,ScriptExecution|ProcessMutation|true|true
processes_runs_list|processes-runs-list|maf.processes_runs_list|Read|WorkspaceRead|false|false
processes_step_transition|processes-step-transition|maf.processes_step_transition|ExternalAction,Read,ScriptExecution,Write|ProcessMutation|true|true
processes_template_baseline_scenarios_list|processes-template-baseline-scenarios-list|maf.processes_template_baseline_scenarios_list|Read|WorkspaceRead|false|false
processes_template_get|processes-template-get|maf.processes_template_get|Read|WorkspaceRead|false|false
processes_template_import|processes-template-import|maf.processes_template_import|ExternalAction,ScriptExecution|ProcessMutation|true|true
processes_template_live_run_profiles_list|processes-template-live-run-profiles-list|maf.processes_template_live_run_profiles_list|Read|WorkspaceRead|false|false
processes_template_mermaid_get|processes-template-mermaid-get|maf.processes_template_mermaid_get|Read|WorkspaceRead|false|false
processes_templates_list|processes-templates-list|maf.processes_templates_list|Read|WorkspaceRead|false|false
project_plan_summary_get|project-plan-summary-get|maf.project_plan_summary_get|Read|WorkspaceRead|false|false
project_structure_analytics_query|project-structure-analytics-query|maf.project_structure_analytics_query|Read|WorkspaceRead|false|false
project_structure_approval_request|project-structure-approval-request|maf.project_structure_approval_request|ExternalAction,Read,ScriptExecution|ProjectStructureMutation|true|true
project_structure_asset_content_get|project-structure-asset-content-get|maf.project_structure_asset_content_get|Read|WorkspaceRead|false|false
project_structure_asset_create|project-structure-asset-create|maf.project_structure_asset_create|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_asset_create_revision|project-structure-asset-create-revision|maf.project_structure_asset_create_revision|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_asset_get|project-structure-asset-get|maf.project_structure_asset_get|Read|WorkspaceRead|false|false
project_structure_asset_image_analyze|project-structure-asset-image-analyze|maf.project_structure_asset_image_analyze|Read|WorkspaceRead|false|false
project_structure_asset_text_get|project-structure-asset-text-get|maf.project_structure_asset_text_get|Read|WorkspaceRead|false|false
project_structure_checklist|project-structure-checklist|maf.project_structure_checklist|Read|WorkspaceRead|false|false
project_structure_dependencies_query|project-structure-dependencies-query|maf.project_structure_dependencies_query|Read|WorkspaceRead|false|false
project_structure_dependency_link|project-structure-dependency-link|maf.project_structure_dependency_link|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_dependency_unlink|project-structure-dependency-unlink|maf.project_structure_dependency_unlink|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_hierarchy_get|project-structure-hierarchy-get|maf.project_structure_hierarchy_get|Read|WorkspaceRead|false|false
project_structure_import|project-structure-import|maf.project_structure_import|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_knowledge_query|project-structure-knowledge-query|maf.project_structure_knowledge_query|Read|WorkspaceRead|false|false
project_structure_lease_get|project-structure-lease-get|maf.project_structure_lease_get|Read|WorkspaceRead|false|false
project_structure_lease_release|project-structure-lease-release|maf.project_structure_lease_release|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_lease_renew|project-structure-lease-renew|maf.project_structure_lease_renew|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_link_create|project-structure-link-create|maf.project_structure_link_create|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_link_unlink|project-structure-link-unlink|maf.project_structure_link_unlink|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_catalog|project-structure-node-catalog|maf.project_structure_node_catalog|Read|WorkspaceRead|false|false
project_structure_node_command_execute|project-structure-node-command-execute|maf.project_structure_node_command_execute|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_create|project-structure-node-create|maf.project_structure_node_create|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_delete|project-structure-node-delete|maf.project_structure_node_delete|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_descendants_to_project_move|project-structure-node-descendants-to-project-move|maf.project_structure_node_descendants_to_project_move|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_marker_update|project-structure-node-marker-update|maf.project_structure_node_marker_update|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_metadata_update|project-structure-node-metadata-update|maf.project_structure_node_metadata_update|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_move|project-structure-node-move|maf.project_structure_node_move|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_priority_update|project-structure-node-priority-update|maf.project_structure_node_priority_update|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_process_definition_link|project-structure-node-process-definition-link|maf.project_structure_node_process_definition_link|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_process_start|project-structure-node-process-start|maf.project_structure_node_process_start|ExternalAction,ProjectStructure,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_progress_update|project-structure-node-progress-update|maf.project_structure_node_progress_update|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_recompose|project-structure-node-recompose|maf.project_structure_node_recompose|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_reparent|project-structure-node-reparent|maf.project_structure_node_reparent|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_status_update|project-structure-node-status-update|maf.project_structure_node_status_update|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_type_update|project-structure-node-type-update|maf.project_structure_node_type_update|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_update|project-structure-node-update|maf.project_structure_node_update|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_workflow_add_options|project-structure-node-workflow-add-options|maf.project_structure_node_workflow_add_options|Read|WorkspaceRead|false|false
project_structure_node_workflow_definition_create|project-structure-node-workflow-definition-create|maf.project_structure_node_workflow_definition_create|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_workflow_start|project-structure-node-workflow-start|maf.project_structure_node_workflow_start|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_node_workflow_status_get|project-structure-node-workflow-status-get|maf.project_structure_node_workflow_status_get|Read|WorkspaceRead|false|false
project_structure_nodes_copy|project-structure-nodes-copy|maf.project_structure_nodes_copy|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_nodes_delete|project-structure-nodes-delete|maf.project_structure_nodes_delete|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_nodes_marker_update|project-structure-nodes-marker-update|maf.project_structure_nodes_marker_update|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_nodes_priority_update|project-structure-nodes-priority-update|maf.project_structure_nodes_priority_update|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_nodes_progress_update|project-structure-nodes-progress-update|maf.project_structure_nodes_progress_update|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_nodes_status_update|project-structure-nodes-status-update|maf.project_structure_nodes_status_update|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_nodes_to_new_subproject|project-structure-nodes-to-new-subproject|maf.project_structure_nodes_to_new_subproject|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_process_subprocess_launch|project-structure-process-subprocess-launch|maf.project_structure_process_subprocess_launch|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_project_create|project-structure-project-create|maf.project_structure_project_create|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_project_lease_acquire|project-structure-project-lease-acquire|maf.project_structure_project_lease_acquire|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_project_update|project-structure-project-update|maf.project_structure_project_update|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_projects_list|project-structure-projects-list|maf.project_structure_projects_list|Read|WorkspaceRead|false|false
project_structure_read|project-structure-read|maf.project_structure_read|Read|WorkspaceRead|false|false
project_structure_repo_branch_lease_acquire|project-structure-repo-branch-lease-acquire|maf.project_structure_repo_branch_lease_acquire|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_subproject_create|project-structure-subproject-create|maf.project_structure_subproject_create|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_structure_subproject_link|project-structure-subproject-link|maf.project_structure_subproject_link|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_task_create|project-task-create|maf.project_task_create|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_task_resource_attach|project-task-resource-attach|maf.project_task_resource_attach|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
project_task_update|project-task-update|maf.project_task_update|ExternalAction,ScriptExecution|ProjectStructureMutation|true|true
prompt_gallery_catalog_search|prompt-gallery-catalog-search|maf.prompt_gallery_catalog_search|Read|InternalDataRead|false|false
prompt_gallery_draft_create|prompt-gallery-draft-create|maf.prompt_gallery_draft_create|Mutation|InternalStateMutation|true|true
prompt_gallery_draft_update|prompt-gallery-draft-update|maf.prompt_gallery_draft_update|Mutation|InternalStateMutation|true|true
prompt_gallery_item_editor_get|prompt-gallery-item-editor-get|maf.prompt_gallery_item_editor_get|Read|InternalDataRead|false|false
prompt_gallery_item_get|prompt-gallery-item-get|maf.prompt_gallery_item_get|Read|InternalDataRead|false|false
prompt_gallery_search|prompt-gallery-search|maf.prompt_gallery_search|Read|InternalDataRead|false|false
prompt_gallery_version_create|prompt-gallery-version-create|maf.prompt_gallery_version_create|Mutation|InternalStateMutation|true|true
provider_health|provider-health|maf.provider_health|Read|InternalDataRead|false|false
read_skill_resource|read-skill-resource|maf.read_skill_resource|Read|WorkspaceRead|false|false
run_skill_script|run-skill-script|maf.run_skill_script|ExternalAction,ScriptExecution|LocalProcessExecution|true|true
scheduler_workflow_schedule_create|scheduler-workflow-schedule-create|maf.scheduler_workflow_schedule_create|Mutation|InternalStateMutation|true|true
scheduler_workflow_schedules_search|scheduler-workflow-schedules-search|maf.scheduler_workflow_schedules_search|Read|InternalDataRead|false|false
scheduler_workflow_targets_search|scheduler-workflow-targets-search|maf.scheduler_workflow_targets_search|Read|InternalDataRead|false|false
storage_browse|storage-browse|maf.storage_browse|Read|InternalDataRead|false|false
storage_catalog_list|storage-catalog-list|maf.storage_catalog_list|Read|InternalDataRead|false|false
storage_delete_object|storage-delete-object|maf.storage_delete_object|Mutation|InternalStateMutation|true|true
storage_read_text_file|storage-read-text-file|maf.storage_read_text_file|Read|InternalDataRead|false|false
storage_write_text_file|storage-write-text-file|maf.storage_write_text_file|Mutation|InternalStateMutation|true|true
submit_architecture_review_result|submit-architecture-review-result|maf.submit_architecture_review_result|Read|None|false|false
submit_code_review_result|submit-code-review-result|maf.submit_code_review_result|Read|None|false|false
submit_human_escalation_request|submit-human-escalation-request|maf.submit_human_escalation_request|Read|None|false|false
submit_implementation_plan|submit-implementation-plan|maf.submit_implementation_plan|Read|None|false|false
submit_process_state_patch|submit-process-state-patch|maf.submit_process_state_patch|Read|None|false|false
submit_process_step_outcome|submit-process-step-outcome|maf.submit_process_step_outcome|Read|None|false|false
submit_test_plan|submit-test-plan|maf.submit_test_plan|Read|None|false|false
submit_tool_execution_decision|submit-tool-execution-decision|maf.submit_tool_execution_decision|Read|None|false|false
workflow_curator_authoring_options_get|workflow-curator-authoring-options-get|maf.workflow_curator_authoring_options_get|Read|InternalDataRead|false|false
workflow_curator_catalog_search|workflow-curator-catalog-search|maf.workflow_curator_catalog_search|Read|InternalDataRead|false|false
workflow_curator_definition_editor_get|workflow-curator-definition-editor-get|maf.workflow_curator_definition_editor_get|Read|InternalDataRead|false|false
workflow_curator_draft_create|workflow-curator-draft-create|maf.workflow_curator_draft_create|Mutation|InternalStateMutation|true|true
workflow_curator_draft_update|workflow-curator-draft-update|maf.workflow_curator_draft_update|Mutation|InternalStateMutation|true|true
workflow_curator_lifecycle_change|workflow-curator-lifecycle-change|maf.workflow_curator_lifecycle_change|Mutation|InternalStateMutation|true|true
workflow_curator_node_update|workflow-curator-node-update|maf.workflow_curator_node_update|Mutation|InternalStateMutation|true|true
workflows_definitions_list|workflows-definitions-list|maf.workflows_definitions_list|Read|None|false|false
workflows_external_response_submit|workflows-external-response-submit|maf.workflows_external_response_submit|ExternalAction,ScriptExecution|RuntimeLaunch|true|true
workflows_run_cancel|workflows-run-cancel|maf.workflows_run_cancel|ExternalAction,ScriptExecution|RuntimeLaunch|true|true
workflows_run_start|workflows-run-start|maf.workflows_run_start|ResourceCleanup,RuntimeLaunch,ScriptExecution,Validation|RuntimeLaunch|true|true
workflows_run_status_get|workflows-run-status-get|maf.workflows_run_status_get|Read|None|false|false
workspace_analyze_image|workspace-analyze-image|maf.workspace_analyze_image|Read|RuntimeProofCapture|false|false
workspace_analyze_images|workspace-analyze-images|maf.workspace_analyze_images|Read|RuntimeProofCapture|false|false
workspace_append_file|workspace-append-file|maf.workspace_append_file|Write|WorkspaceWrite|true|true
workspace_command_run|workspace-command-run|maf.workspace_command_run|ExternalAction,ScriptExecution|LocalProcessExecution|true|true
workspace_convert_document|workspace-convert-document|maf.workspace_convert_document|ProjectStructure,Read,Write|DocumentConversion|false|false
workspace_copy_path|workspace-copy-path|maf.workspace_copy_path|Mutation,Write|WorkspaceWrite|true|true
workspace_create_directory|workspace-create-directory|maf.workspace_create_directory|Mutation,Write|WorkspaceWrite|true|true
workspace_delete_path|workspace-delete-path|maf.workspace_delete_path|Mutation,Write|WorkspaceWrite|true|true
workspace_diff_text|workspace-diff-text|maf.workspace_diff_text|Read|WorkspaceRead|false|false
workspace_dotnet_build|workspace-dotnet-build|maf.workspace_dotnet_build|ScriptExecution,Validation|LocalProcessExecution|false|false
workspace_dotnet_new|workspace-dotnet-new|maf.workspace_dotnet_new|Mutation,Write|WorkspaceWrite|true|true
workspace_dotnet_restore|workspace-dotnet-restore|maf.workspace_dotnet_restore|ScriptExecution,Validation|LocalProcessExecution|false|false
workspace_dotnet_run|workspace-dotnet-run|maf.workspace_dotnet_run|Validation|RuntimeLaunch|false|false
workspace_dotnet_stop|workspace-dotnet-stop|maf.workspace_dotnet_stop|BrowserAccess,ResourceCleanup,RuntimeLaunch,ScriptExecution,Validation|RuntimeLaunch|false|false
workspace_dotnet_test|workspace-dotnet-test|maf.workspace_dotnet_test|ScriptExecution,Validation|LocalProcessExecution|false|false
workspace_execution_boundary|workspace-execution-boundary|maf.workspace_execution_boundary|Read|WorkspaceRead|false|false
workspace_git_add|workspace-git-add|maf.workspace_git_add|Mutation,Write|WorkspaceWrite|true|true
workspace_git_branch_create|workspace-git-branch-create|maf.workspace_git_branch_create|Mutation,Write|WorkspaceWrite|true|true
workspace_git_commit|workspace-git-commit|maf.workspace_git_commit|Mutation,Write|WorkspaceWrite|true|true
workspace_git_diff|workspace-git-diff|maf.workspace_git_diff|Read|WorkspaceRead|false|false
workspace_git_log|workspace-git-log|maf.workspace_git_log|Read|WorkspaceRead|false|false
workspace_git_show|workspace-git-show|maf.workspace_git_show|Read|WorkspaceRead|false|false
workspace_git_status|workspace-git-status|maf.workspace_git_status|Read|WorkspaceRead|false|false
workspace_git_switch|workspace-git-switch|maf.workspace_git_switch|Mutation,Write|WorkspaceWrite|true|true
workspace_git_unstage|workspace-git-unstage|maf.workspace_git_unstage|Mutation,Write|WorkspaceWrite|true|true
workspace_hash_path|workspace-hash-path|maf.workspace_hash_path|Read|WorkspaceRead|false|false
workspace_inspect_image|workspace-inspect-image|maf.workspace_inspect_image|Read|RuntimeProofCapture|false|false
workspace_inspect_spreadsheet|workspace-inspect-spreadsheet|maf.workspace_inspect_spreadsheet|Read|WorkspaceRead|false|false
workspace_list_directory|workspace-list-directory|maf.workspace_list_directory|Read|WorkspaceRead|false|false
workspace_list_files|workspace-list-files|maf.workspace_list_files|Read|WorkspaceRead|false|false
workspace_move_path|workspace-move-path|maf.workspace_move_path|Mutation,Write|WorkspaceWrite|true|true
workspace_pwsh_run_script|workspace-pwsh-run-script|maf.workspace_pwsh_run_script|ScriptExecution|LocalProcessExecution|true|true
workspace_python_run_file|workspace-python-run-file|maf.workspace_python_run_file|ScriptExecution|LocalProcessExecution|true|true
workspace_read_file|workspace-read-file|maf.workspace_read_file|Read|WorkspaceRead|false|false
workspace_read_spreadsheet_cell|workspace-read-spreadsheet-cell|maf.workspace_read_spreadsheet_cell|Read|WorkspaceRead|false|false
workspace_read_spreadsheet_range|workspace-read-spreadsheet-range|maf.workspace_read_spreadsheet_range|Read|WorkspaceRead|false|false
workspace_search|workspace-search|maf.workspace_search|Read|WorkspaceRead|false|false
workspace_spreadsheet_function_catalog|workspace-spreadsheet-function-catalog|maf.workspace_spreadsheet_function_catalog|Read|WorkspaceRead|false|false
workspace_spreadsheet_summary|workspace-spreadsheet-summary|maf.workspace_spreadsheet_summary|Read|WorkspaceRead|false|false
workspace_stat_path|workspace-stat-path|maf.workspace_stat_path|Read|WorkspaceRead|false|false
workspace_unzip_archive|workspace-unzip-archive|maf.workspace_unzip_archive|Mutation,Write|WorkspaceWrite|true|true
workspace_write_file|workspace-write-file|maf.workspace_write_file|Write|WorkspaceWrite|true|true
workspace_write_spreadsheet|workspace-write-spreadsheet|maf.workspace_write_spreadsheet|Write|WorkspaceWrite|true|true
workspace_zip_path|workspace-zip-path|maf.workspace_zip_path|Mutation,Write|WorkspaceWrite|true|true
""";
}
