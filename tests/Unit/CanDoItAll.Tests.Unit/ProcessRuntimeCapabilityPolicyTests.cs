using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Support.ProductToolPolicyTestRegistration;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProcessRuntimeCapabilityPolicyTests {
    public static IEnumerable<object[]> LegacyDescriptorRows => LegacyDescriptors.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(row => new object[] { row.Trim() });

    [Theory]
    [MemberData(nameof(LegacyDescriptorRows))]
    public void Every_changed_static_descriptor_retains_its_pre_cut_operation_vector(string expected) {
        var name = expected.Split('|')[0];
        var descriptor = RuntimeToolCapabilityDescriptorFactory.CreateRuntimeToolCapabilityDescriptor(name, name,
            "Descriptor parity", [], toolPolicies: ProductToolPolicies);

        Assert.Equal(expected, name + "|" + string.Join(',', descriptor.OperationClassifications.OrderBy(item => item.ToString(),
            StringComparer.Ordinal)));
    }

    [Fact]
    public void Legacy_governed_context_without_owner_fails_before_composition() {
        var failure = Assert.Throws<InvalidOperationException>(() => RuntimeCapabilityAccessPolicyBuilder
            .BuildRuntimeCapabilityAccessPolicies(WorkspaceAccess(), ProcessIntent(), ProductToolPolicies));

        Assert.Equal("This governed run has no owner runtime-capability policy.", failure.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ordinary_browser_filtering_needs_no_Process_policy_registration(bool browserAllowed) {
        var context = AgentRuntimeContextIntent.Empty with { BrowserToolsAllowed = browserAllowed };
        var policies = RuntimeCapabilityAccessPolicyBuilder.BuildRuntimeCapabilityAccessPolicies(WorkspaceAccess(), context);
        var descriptor = RuntimeToolCapabilityDescriptorFactory.CreateRuntimeToolCapabilityDescriptor(
            ToolContractCatalog.BrowserSnapshot, "Snapshot", "Browser snapshot", []);
        var result = new CapabilityAccessPolicyEvaluator().Evaluate(new([descriptor], [], policies, "ordinary-browser"));

        Assert.Equal(browserAllowed, result.AllowedCapabilities.Count == 1);
        Assert.Contains(CapabilityOperationClassification.BrowserAccess, descriptor.OperationClassifications);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Owner_operation_grant_cannot_widen_agent_or_runtime_workspace_flags(bool agentWrite, bool runtimeWorkspace) {
        var context = ProcessIntent() with { WorkspaceToolsEnabled = runtimeWorkspace };
        var policies = RuntimeCapabilityAccessPolicyBuilder.BuildRuntimeCapabilityAccessPolicies(WorkspaceAccess(agentWrite),
            context, ProductToolPolicies, [new ProcessRuntimeCapabilityPolicyContributor()]);
        var descriptor = RuntimeToolCapabilityDescriptorFactory.CreateRuntimeToolCapabilityDescriptor(
            ToolContractCatalog.WorkspaceWriteFile, "Write", "Write file", ["configured"]);
        var result = new CapabilityAccessPolicyEvaluator().Evaluate(new([descriptor], [], policies, "workspace-flags"));

        Assert.Equal(agentWrite && runtimeWorkspace, result.AllowedCapabilities.Count == 1);
    }

    [Fact]
    public void Saved_runtime_override_remains_after_owner_policy_and_retains_denial() {
        var ceiling = new CapabilityAccessPolicy([new(CapabilityRuleId.Create("saved-write-denial"), CapabilityAccessEffect.Deny,
            CapabilityAccessScope.RuntimeOverride, CapabilitySelector.ByRuntimeToolName(RuntimeToolName.Create(ToolContractCatalog.WorkspaceWriteFile)),
            "Original admission did not permit this file writer.")]);
        var context = ProcessIntent() with { CapabilityScopeOverride = new([ceiling], []) };
        var policies = RuntimeCapabilityAccessPolicyBuilder.BuildRuntimeCapabilityAccessPolicies(WorkspaceAccess(), context,
            ProductToolPolicies, [new ProcessRuntimeCapabilityPolicyContributor()]);
        var descriptor = RuntimeToolCapabilityDescriptorFactory.CreateRuntimeToolCapabilityDescriptor(
            ToolContractCatalog.WorkspaceWriteFile, "Write", "Write file", ["configured"]);
        var result = new CapabilityAccessPolicyEvaluator().Evaluate(new([descriptor], [], policies, "saved-ceiling"));

        Assert.Same(ceiling, policies[^1]);
        Assert.Empty(result.AllowedCapabilities);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.RuleId?.Value == "saved-write-denial");
    }

    [Fact]
    public void Invalid_saved_operation_retains_explicit_owner_validation_failure() {
        var context = ProcessIntent() with { AllowedOperations = ["not-an-operation"] };

        var failure = Assert.Throws<InvalidOperationException>(() => RuntimeCapabilityAccessPolicyBuilder
            .BuildRuntimeCapabilityAccessPolicies(WorkspaceAccess(), context, ProductToolPolicies,
                [new ProcessRuntimeCapabilityPolicyContributor()]));

        Assert.Equal("Runtime process operation capability policy is invalid. Process allowed operation 'not-an-operation' is not a known operation contract.", failure.Message);
    }

    [Fact]
    public void Catalog_freezes_owner_descriptor_facts() {
        var classifications = new List<CapabilityOperationClassification> { CapabilityOperationClassification.ExternalAction };
        var policy = ToolCapabilityMetadataFactory.Read("fixture_owned_read", ToolCapabilitySideEffectKind.InternalDataRead) with {
            OperationClassifications = classifications
        };
        var catalog = new AgentToolPolicyCatalog([policy]);
        classifications.Clear();
        classifications.Add(CapabilityOperationClassification.Mutation);
        var actual = RuntimeToolCapabilityDescriptorFactory.ResolveRuntimeToolOperationClassifications(policy.Name, catalog);

        Assert.Equal(CapabilityOperationClassification.ExternalAction, Assert.Single(actual));
    }

    [Fact]
    public void Unknown_owner_descriptor_classification_fails_registration() {
        var policy = ToolCapabilityMetadataFactory.Read("fixture_owned_read", ToolCapabilitySideEffectKind.InternalDataRead) with {
            OperationClassifications = [(CapabilityOperationClassification)int.MaxValue]
        };

        var failure = Assert.Throws<ArgumentException>(() => new AgentToolPolicyCatalog([policy]));

        Assert.Contains("unknown operation classification", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Typed_composition_resolves_one_owner_after_repeated_product_registration() {
        var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection();
        services.AddProductToolPolicies();
        using var provider = services.BuildServiceProvider();
        var dependencies = MafAgentRuntimeDependencies.FromServices(provider);

        Assert.IsType<ProcessRuntimeCapabilityPolicyContributor>(Assert.Single(dependencies.CapabilityDependencies.ContextPolicyContributors));
    }

    private static AgentRuntimeContextIntent ProcessIntent()
        => AgentRuntimeContextIntent.Empty with {
            SourceKind = "process-step", SourceId = "write-result", ProcessRunId = "run-a", ProcessStepId = "step-a",
            IsGovernedProcessStep = true, AllowsProductMutation = true,
            TargetScope = ProcessOperationContractNames.ExternalProductTargetMutable,
            AllowedOperations = [ProcessOperationContractNames.MutateProductTarget]
        };

    private static AgentWorkspaceToolAccessSettings WorkspaceAccess(bool canWrite = true)
        => new() { Profile = AgentWorkspaceToolProfileKind.Custom, CanReadFiles = true, CanWriteFiles = canWrite };

    private const string LegacyDescriptors = """
browser_click|BrowserAccess,ResourceCleanup,Validation
browser_console_messages|BrowserAccess,ResourceCleanup,Validation
browser_drag|BrowserAccess,ResourceCleanup,Validation
browser_evaluate|BrowserAccess,ResourceCleanup,Validation
browser_fill_form|BrowserAccess,ResourceCleanup,Validation
browser_navigate|BrowserAccess,ResourceCleanup,Validation
browser_network_requests|BrowserAccess,ResourceCleanup,Validation
browser_press_key|BrowserAccess,ResourceCleanup,Validation
browser_resize|BrowserAccess,ResourceCleanup,Validation
browser_select_option|BrowserAccess,ResourceCleanup,Validation
browser_snapshot|BrowserAccess,ResourceCleanup,Validation
browser_take_screenshot|BrowserAccess,ResourceCleanup,Validation
browser_type|BrowserAccess,ResourceCleanup,Validation
browser_wait_for|BrowserAccess,ResourceCleanup,Validation
capability_curator_mcp_setup_test|ExternalAction,ScriptExecution
capability_curator_tool_setup_test|ExternalAction,ScriptExecution
capability_curator_verify|ExternalAction,ScriptExecution
hr_agent_avatar_generate|ExternalAction,ScriptExecution
hr_agent_process_manager_review_request|ExternalAction,ScriptExecution
image_generation_create|ExternalAction,ScriptExecution
local_mcp_launch|ExternalAction,ScriptExecution
processes_assignment_resolve|ExternalAction,ScriptExecution
processes_definition_delete|ExternalAction,ScriptExecution
processes_definition_import|ExternalAction,ScriptExecution
processes_definition_publish|ExternalAction,ScriptExecution
processes_definition_role_add|ExternalAction,ScriptExecution
processes_definition_save|ExternalAction,ScriptExecution
processes_run_start|ExternalAction,ScriptExecution
processes_step_transition|ExternalAction,Read,ScriptExecution,Write
processes_template_import|ExternalAction,ScriptExecution
project_structure_approval_request|ExternalAction,Read,ScriptExecution
project_structure_asset_create_revision|ExternalAction,ScriptExecution
project_structure_asset_create|ExternalAction,ScriptExecution
project_structure_dependency_link|ExternalAction,ScriptExecution
project_structure_dependency_unlink|ExternalAction,ScriptExecution
project_structure_import|ExternalAction,ScriptExecution
project_structure_lease_release|ExternalAction,ScriptExecution
project_structure_lease_renew|ExternalAction,ScriptExecution
project_structure_link_create|ExternalAction,ScriptExecution
project_structure_link_unlink|ExternalAction,ScriptExecution
project_structure_node_command_execute|ExternalAction,ScriptExecution
project_structure_node_create|ExternalAction,ScriptExecution
project_structure_node_delete|ExternalAction,ScriptExecution
project_structure_node_descendants_to_project_move|ExternalAction,ScriptExecution
project_structure_node_marker_update|ExternalAction,ScriptExecution
project_structure_node_metadata_update|ExternalAction,ScriptExecution
project_structure_node_move|ExternalAction,ScriptExecution
project_structure_node_priority_update|ExternalAction,ScriptExecution
project_structure_node_process_definition_link|ExternalAction,ScriptExecution
project_structure_node_process_start|ExternalAction,ProjectStructure,ScriptExecution
project_structure_node_progress_update|ExternalAction,ScriptExecution
project_structure_node_recompose|ExternalAction,ScriptExecution
project_structure_node_reparent|ExternalAction,ScriptExecution
project_structure_node_status_update|ExternalAction,ScriptExecution
project_structure_node_type_update|ExternalAction,ScriptExecution
project_structure_node_update|ExternalAction,ScriptExecution
project_structure_node_workflow_definition_create|ExternalAction,ScriptExecution
project_structure_node_workflow_start|ExternalAction,ScriptExecution
project_structure_nodes_copy|ExternalAction,ScriptExecution
project_structure_nodes_delete|ExternalAction,ScriptExecution
project_structure_nodes_marker_update|ExternalAction,ScriptExecution
project_structure_nodes_priority_update|ExternalAction,ScriptExecution
project_structure_nodes_progress_update|ExternalAction,ScriptExecution
project_structure_nodes_status_update|ExternalAction,ScriptExecution
project_structure_nodes_to_new_subproject|ExternalAction,ScriptExecution
project_structure_process_subprocess_launch|ExternalAction,ScriptExecution
project_structure_project_create|ExternalAction,ScriptExecution
project_structure_project_lease_acquire|ExternalAction,ScriptExecution
project_structure_project_update|ExternalAction,ScriptExecution
project_structure_repo_branch_lease_acquire|ExternalAction,ScriptExecution
project_structure_subproject_create|ExternalAction,ScriptExecution
project_structure_subproject_link|ExternalAction,ScriptExecution
project_task_create|ExternalAction,ScriptExecution
project_task_resource_attach|ExternalAction,ScriptExecution
project_task_update|ExternalAction,ScriptExecution
run_skill_script|ExternalAction,ScriptExecution
workflows_external_response_submit|ExternalAction,ScriptExecution
workflows_run_cancel|ExternalAction,ScriptExecution
workflows_run_start|ResourceCleanup,RuntimeLaunch,ScriptExecution,Validation
workspace_analyze_images|Read
workspace_analyze_image|Read
workspace_command_run|ExternalAction,ScriptExecution
workspace_convert_document|ProjectStructure,Read,Write
workspace_dotnet_build|ScriptExecution,Validation
workspace_dotnet_restore|ScriptExecution,Validation
workspace_dotnet_stop|BrowserAccess,ResourceCleanup,RuntimeLaunch,ScriptExecution,Validation
workspace_dotnet_test|ScriptExecution,Validation
workspace_inspect_image|Read
""";
}
