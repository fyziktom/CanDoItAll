using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Processes.AgentChat;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class ProcessCompatibilityToolPolicyTests {
    public static IEnumerable<object[]> HistoricalPolicies => HistoricalRows.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(row => new object[] { row.Trim() });

    [Theory]
    [MemberData(nameof(HistoricalPolicies))]
    public void Owner_preserves_every_historical_policy_vector(string expected) {
        var name = expected.Split('|')[0];
        Assert.False(ToolCapabilityRegistry.TryResolve(name, out _));
        Assert.DoesNotContain(name, ToolContractCatalog.KnownToolNames);
        Assert.True(ProductToolPolicyTestRegistration.ProductToolPolicies.TryResolve(name, out var policy));
        Assert.Equal(expected, Describe(policy));
        Assert.Equal(expected, Describe(Assert.Single(ProcessCompatibilityToolPolicy.Capabilities, item => item.Name == name)));
    }

    [Theory]
    [InlineData(ContextualAgentWorkspaceKind.ProjectStructure, "project-structure", "Project structure", "This project", true)]
    [InlineData(ContextualAgentWorkspaceKind.Processes, "process-definition", "Processes", "This process", false)]
    public void Actual_owner_registration_preserves_context_protocol_and_is_idempotent(
        ContextualAgentWorkspaceKind kind, string sourceKind, string label, string selectedLabel, bool includesNodes) {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddWorkbenchModule(configuration);
        services.AddProcessesModule(configuration);
        services.AddWorkbenchModule(configuration);
        services.AddProcessesModule(configuration);
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<ContextualAgentWorkspacePolicyCatalog>();
        Assert.Equal(2, provider.GetServices<IContextualAgentWorkspacePolicy>().Count());
        var policy = catalog.Require(kind);
        Assert.Equal(sourceKind, policy.Descriptor.SourceKind.Value);
        Assert.Equal(label, policy.Descriptor.WorkspaceLabel);
        Assert.Equal(selectedLabel, policy.Descriptor.SelectedScopeLabel);
        Assert.Equal(includesNodes, policy.Descriptor.IncludesSelectedNodeMetadata);
        var projectId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        Assert.Equal(kind == ContextualAgentWorkspaceKind.ProjectStructure ? projectId : definitionId,
            policy.ResolveScopeId(projectId, definitionId));
        var tools = provider.GetRequiredService<AgentToolPolicyCatalog>();
        Assert.Equal(23, ProcessCompatibilityToolPolicy.Capabilities.Count);
        Assert.All(ProcessCompatibilityToolPolicy.Capabilities, entry => {
            Assert.True(tools.TryResolve(entry.Name, out var registered));
            Assert.Equal(Describe(entry), Describe(registered));
            Assert.Single(provider.GetServices<ToolCapabilityMetadata>(), item => item.Name == entry.Name);
        });
    }

    [Fact]
    public void Missing_context_owner_fails_explicitly_before_constructing_prompt_or_access() {
        var catalog = new ContextualAgentWorkspacePolicyCatalog([]);
        var exception = Assert.Throws<InvalidOperationException>(() => catalog.BuildPrompt(
            ContextualAgentWorkspaceKind.Processes, null, Guid.NewGuid(), [], "Edit this process"));
        Assert.Contains("no registered policy owner", exception.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => catalog.Resolve([], ContextualAgentWorkspaceKind.ProjectStructure));
    }

    [Fact]
    public void Duplicate_context_owner_is_rejected() {
        Assert.Throws<ArgumentException>(() => new ContextualAgentWorkspacePolicyCatalog([
            new ProcessContextualWorkspacePolicy(), new ProcessContextualWorkspacePolicy()
        ]));
    }

    [Fact]
    public void Process_owner_preserves_exact_existing_prompt_and_ignores_unrelated_project_selection() {
        var catalog = new ContextualAgentWorkspacePolicyCatalog([new ProcessContextualWorkspacePolicy()]);
        var definitionId = Guid.Parse("a3ab6af3-e91e-4683-8c07-87a279f38c71");
        var result = catalog.BuildPrompt(ContextualAgentWorkspaceKind.Processes, Guid.NewGuid(), definitionId,
            ["node:unrelated"], "Add a reviewer");
        Assert.Equal("""
Context:
- Workspace: process definition.
- Selected process definition id: a3ab6af3-e91e-4683-8c07-87a279f38c71.
- Treat "this process" and "selected process" as that process definition.
- Use process-definition operations for process reads or mutations.
- For adding one process role, use processes_definition_role_add instead of loading and rewriting the full editor model.
- Do not use project-structure operations unless the user explicitly asks about project structure.

User request:
Add a reviewer
""", result);
        Assert.Equal("Add a reviewer", catalog.BuildPrompt(ContextualAgentWorkspaceKind.Processes, Guid.NewGuid(), null,
            ["node:unrelated"], "Add a reviewer"));
    }

    [Fact]
    public void Serialized_context_discriminators_and_access_bits_remain_compatible() {
        Assert.Equal(new[] { "ProjectStructure:0", "Processes:1" }, Enum.GetValues<ContextualAgentWorkspaceKind>()
            .Select(value => $"{value}:{(int)value}"));
        Assert.Equal(new[] { "None:0", "Read:1", "Write:2", "TaskWrite:4", "NonTaskStructureWrite:8", "ProjectCreate:16", "SubprojectCreate:32" },
            Enum.GetValues<ContextualAgentAccessLevel>().Select(value => $"{value}:{(int)value}"));
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

    private const string HistoricalRows = """
processes_definition_save|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
processes_definition_role_add|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
processes_definition_publish|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
processes_definition_delete|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
processes_definition_import|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
processes_run_start|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
processes_step_transition|Mutation|true|true|ProcessMutation|Static|EscalateOrDecide,RecoverArtifactsOnly,ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
processes_assignment_resolve|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
processes_artifact_record|Mutation|true|true|ProcessMutation|ProcessArtifactWrite||ExternalArtifactDestination,ManagedProcessArtifactsOnly|false|false|false|true|None|StateChanging||false
processes_definitions_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
processes_definition_editor_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
processes_definition_export|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
processes_runs_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
processes_run_detail_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
processes_analytics_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
processes_party_options_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
processes_executor_options_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
processes_templates_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
processes_template_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
processes_template_mermaid_get|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
processes_template_import|Mutation|true|true|ProcessMutation|Static|ExecuteExternalAction|ExternalActionControlled|false|true|false|false|None|StateChanging||false
processes_template_baseline_scenarios_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
processes_template_live_run_profiles_list|Read|false|false|WorkspaceRead|None|||false|false|true|false|None|Idempotent||false
""";
}
