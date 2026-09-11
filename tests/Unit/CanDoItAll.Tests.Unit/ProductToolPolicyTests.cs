using CanDoItAll.Modules.Processes;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Support.ProductToolPolicyTestRegistration;

namespace CanDoItAll.Tests.Unit.AgentFramework;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class ProductToolPolicyTests {
    public static IEnumerable<object[]> LegacyPolicyRows => LegacyPolicies.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(row => new object[] { row.Trim() });

    [Theory]
    [MemberData(nameof(LegacyPolicyRows))]
    public void Owner_policy_preserves_every_pre_cut_field_and_export_disposition(string expected) {
        var name = expected.Split('|')[0];
        Assert.False(ToolCapabilityRegistry.TryResolve(name, out _));
        Assert.True(ProductToolPolicies.TryResolve(name, out var policy));
        Assert.Equal(expected, Describe(policy));
    }

    [Fact]
    public void Every_moved_policy_has_exactly_one_owner_and_a_legacy_contract_row() {
        var names = MovedProductPolicies.Select(policy => policy.Name).Order(StringComparer.Ordinal).ToArray();
        var expected = LegacyPolicyRows.Select(row => ((string)row[0]).Split('|')[0]).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(106, names.Length);
        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(expected, names);
    }

    [Fact]
    public void Independent_compositions_resolve_identical_policy_records_and_repeated_registration_is_idempotent() {
        using var first = new ServiceCollection().AddProductToolPolicies().AddProductToolPolicies().BuildServiceProvider();
        using var second = new ServiceCollection().AddProductToolPolicies().BuildServiceProvider();
        var firstPolicies = first.GetRequiredService<AgentToolPolicyCatalog>();
        var secondPolicies = second.GetRequiredService<AgentToolPolicyCatalog>();
        Assert.Equal(141, first.GetServices<ToolCapabilityMetadata>().Count());
        Assert.Equal(firstPolicies.Capabilities.OrderBy(policy => policy.Name).Select(Describe),
            secondPolicies.Capabilities.OrderBy(policy => policy.Name).Select(Describe));
    }

    [Fact]
    public void Production_module_registration_contributes_its_complete_policy_family_once() {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddAgentFrameworkModule(configuration);
        services.AddWorkbenchModule(configuration);
        services.AddSchedulerPlannerModule(configuration);
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<AgentToolPolicyCatalog>();
        var registered = provider.GetServices<ToolCapabilityMetadata>().ToArray();
        Assert.Equal(111, registered.Length);
        Assert.Equal(111, registered.Select(policy => policy.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(registered, policy => Assert.True(catalog.TryResolve(policy.Name, out _)));
        Assert.All(registered.Where(policy => policy.ProtectRuntimeStateOnExport), policy =>
            Assert.Equal("hr-approval-redacted-v1", policy.BusinessArgumentRetentionScheme));
    }

    [Fact]
    public void Configured_catalog_descriptor_uses_owner_mutation_and_approval_metadata() {
        var catalog = new RuntimeCapabilityDescriptorCatalog(ProductToolPolicies);
        var capability = new CapabilityCatalogItem(Guid.NewGuid(), CanDoItAll.AgentFramework.Models.CapabilityKind.Tool,
            "workflows-run-start", "Start workflow", "Run a workflow.", string.Empty,
            """{"tool":"workflows_run_start"}""", CapabilityProofStatus.Verified, string.Empty, DateTimeOffset.UtcNow,
            IsBuiltIn: true);
        var descriptor = catalog.CreateCatalogCapabilityDescriptor(capability);
        Assert.Equal("workflows_run_start", descriptor.RuntimeToolName?.Value);
        Assert.Equal(CapabilitySideEffectKind.RuntimeLaunch, descriptor.SideEffectProfile.Kind);
        Assert.True(descriptor.SideEffectProfile.RequiresApprovalByDefault);
        Assert.True(descriptor.SideEffectProfile.IsStateChanging);
    }

    [Fact]
    public void Registered_external_operation_requirements_still_generate_the_same_governed_deny_rule() {
        var intent = AgentRuntimeContextIntent.Empty with {
            SourceKind = "process-step",
            SourceId = "step",
            ProcessRunId = "process",
            ProcessStepId = "step",
            IsGovernedProcessStep = true,
            WorkspaceToolsEnabled = false
        };
        var policies = RuntimeCapabilityAccessPolicyBuilder.BuildRuntimeCapabilityAccessPolicies(
            AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.ReadOnly), intent, ProductToolPolicies, [new ProcessRuntimeCapabilityPolicyContributor()]);
        Assert.Contains(policies.SelectMany(policy => policy.Rules), rule =>
            rule.Id.Value == "deny-operation-contract-workflows-run-start");
    }

    private static string Describe(ToolCapabilityMetadata policy) {
        static string Flag(bool value) => value ? "true" : "false";
        return string.Join('|', policy.Name, policy.Classification.ToString(), Flag(policy.RequiresApprovalByDefault),
            Flag(policy.IsStateChanging), policy.SideEffectKind.ToString(), policy.OperationRequirementKind.ToString(),
            string.Join(';', policy.OperationRequirements.Select(requirement => string.Join(',', requirement.AnyOf))),
            string.Join(',', policy.TargetScopeRequirements), Flag(policy.CanMutateProduct), Flag(policy.CanExecuteExternalAction),
            Flag(policy.CanReadExternalTarget), Flag(policy.CanWriteManagedArtifact), policy.BrowserProofRole.ToString(),
            policy.IdempotencyDescriptor.ToString(), policy.BusinessArgumentRetentionScheme ?? string.Empty, Flag(policy.ProtectRuntimeStateOnExport));
    }

    private const string LegacyPolicies = """
capability_curator_assignment_editor_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false
capability_curator_assignment_update|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|capability-curator-approval-redacted-v1|false
capability_curator_catalog_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|capability-curator-approval-redacted-v1|false
capability_curator_editor_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false
capability_curator_mcp_setup_test|Mutation|true|true|ExternalAction|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|ExternalSideEffect|capability-curator-approval-redacted-v1|false
capability_curator_save|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|capability-curator-approval-redacted-v1|false
capability_curator_tool_setup_test|Mutation|true|true|ExternalAction|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|ExternalSideEffect|capability-curator-approval-redacted-v1|false
capability_curator_verify|Mutation|true|true|ExternalAction|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|ExternalSideEffect|capability-curator-approval-redacted-v1|false
hr_agent_avatar_generate|Mutation|true|true|MediaGeneration|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging|hr-approval-redacted-v1|true
hr_agent_create|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true
hr_agent_creation_options_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false
hr_agent_process_history_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false
hr_agent_process_manager_review_request|Mutation|true|true|ExternalAction|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|ExternalSideEffect|hr-approval-redacted-v1|true
hr_agent_settings_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false
hr_agent_settings_update|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true
hr_agent_usage_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false
hr_agents_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true
hr_crm_affiliation_upsert|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true
hr_crm_item_summary_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false
hr_crm_party_affiliations_list|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true
hr_crm_party_create|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true
hr_crm_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true
hr_simple_chat_create_receipt_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true
hr_simple_chat_create|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true
hr_simple_chat_creation_options_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true
hr_simple_chat_settings_get|Read|true|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true
hr_simple_chat_settings_update|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true
hr_simple_chat_status_change|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|hr-approval-redacted-v1|true
hr_simple_chats_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|hr-approval-redacted-v1|true
image_generation_create|Mutation|true|true|MediaGeneration|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_plan_summary_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_analytics_query|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_approval_request|Mutation|true|true|ProjectStructureMutation|Static|EscalateOrDecide,ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_asset_content_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_asset_create_revision|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_asset_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_asset_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_asset_image_analyze|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_asset_text_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_checklist|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_dependencies_query|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_dependency_link|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_dependency_unlink|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_hierarchy_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_import|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_knowledge_query|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_lease_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_lease_release|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_lease_renew|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_link_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_link_unlink|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_catalog|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_node_command_execute|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_delete|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_descendants_to_project_move|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_marker_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_metadata_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_move|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_priority_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_process_definition_link|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_process_start|Mutation|true|true|ProjectStructureMutation|Static|StartProjectNodeProcess|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_progress_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_recompose|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_reparent|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_status_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_type_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_workflow_add_options|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_node_workflow_definition_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_workflow_start|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_node_workflow_status_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_nodes_copy|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_nodes_delete|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_nodes_marker_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_nodes_priority_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_nodes_progress_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_nodes_status_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_nodes_to_new_subproject|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_process_subprocess_launch|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_project_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_project_lease_acquire|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_project_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_projects_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_read|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
project_structure_repo_branch_lease_acquire|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_subproject_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_structure_subproject_link|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_task_create|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_task_resource_attach|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
project_task_update|Mutation|true|true|ProjectStructureMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
scheduler_workflow_schedule_create|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|scheduler-approval-redacted-v1|false
scheduler_workflow_schedules_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|scheduler-approval-redacted-v1|false
scheduler_workflow_targets_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|scheduler-approval-redacted-v1|false
workflow_curator_authoring_options_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false
workflow_curator_catalog_search|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent|workflow-curator-approval-redacted-v1|false
workflow_curator_definition_editor_get|Read|false|false|InternalDataRead|None|||false|false|true|false|None|Idempotent||false
workflow_curator_draft_create|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|workflow-curator-approval-redacted-v1|false
workflow_curator_draft_update|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|workflow-curator-approval-redacted-v1|false
workflow_curator_lifecycle_change|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|workflow-curator-approval-redacted-v1|false
workflow_curator_node_update|Mutation|true|true|InternalStateMutation|None|||false|false|false|false|None|StateChanging|workflow-curator-approval-redacted-v1|false
workflows_definitions_list|Read|false|false|None|None|||false|false|true|false|None|Idempotent||false
workflows_external_response_submit|Mutation|true|true|RuntimeLaunch|Static|ExecuteExternalAction|ExternalActionControlled|false|true|true|false|None|StateChanging|workflow-curator-approval-redacted-v1|false
workflows_run_cancel|Mutation|true|true|RuntimeLaunch|Static|ExecuteExternalAction|ExternalActionControlled|false|true|true|false|None|StateChanging||false
workflows_run_start|Mutation|true|true|RuntimeLaunch|Static|LaunchRuntime|ExternalProductTargetReadOnly|false|false|true|false|None|StateChanging|workflow-curator-approval-redacted-v1|false
workflows_run_status_get|Read|false|false|None|None|||false|false|true|false|None|Idempotent||false
""";
}
