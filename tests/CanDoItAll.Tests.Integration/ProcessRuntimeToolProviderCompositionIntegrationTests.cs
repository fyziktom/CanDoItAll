using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRuntimeToolProviderCompositionIntegrationTests
{
    private static readonly string[] ExpectedProcessToolNames =
    [
        "processes_definitions_list",
        "processes_definition_editor_get",
        "processes_definition_save",
        "processes_definition_role_add",
        "processes_definition_publish",
        "processes_definition_delete",
        "processes_definition_export",
        "processes_definition_import",
        "processes_runs_list",
        "processes_run_detail_get",
        "processes_analytics_get",
        "processes_run_start",
        "processes_step_transition",
        "processes_assignment_resolve",
        "processes_artifact_record",
        "processes_party_options_list",
        "processes_executor_options_list",
        "processes_templates_list",
        "processes_template_get",
        "processes_template_mermaid_get",
        "processes_template_import",
        "processes_template_baseline_scenarios_list",
        "processes_template_live_run_profiles_list"
    ];

    [Fact]
    public async Task ProjectStructureRuntimeToolProviderComposition_app_composition_registers_project_structure_provider_with_complete_tool_inventory()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();

        var projectStructureProvider = Assert.Single(
            scope.ServiceProvider.GetServices<IAgentRuntimeToolProvider>()
                .OfType<ProjectStructureAgentRuntimeToolProvider>());

        Assert.Equal(900, projectStructureProvider.Order);
        Assert.Equal("project-structure.runtime-tools", projectStructureProvider.Descriptor?.ProviderKey);

        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededAgent = seed.Agents[0];
        var provider = Assert.Single(seed.Providers, item => item.Id == seededAgent.ProviderProfileId);
        var agent = seededAgent with
        {
            Permissions = AgentPermissionsPolicy.Default,
            ConfigurationJson = AgentProjectStructureAccessMetadata.Write(
                seededAgent.ConfigurationJson,
                new AgentProjectStructureAccessSettings
                {
                    CanRead = true,
                    CanWrite = true,
                    AllowAllProjects = true
                })
        };

        var tools = await projectStructureProvider.CreateToolsAsync(
            new AgentRuntimeToolProviderContext(
                agent,
                provider,
                [],
                SuppressApprovalRequirements: false,
                AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
                RuntimeSessionKey: "sb04-runtime-smoke",
                Tags: new Dictionary<string, string>
                {
                    ["proof"] = "SB04"
                }),
            CancellationToken.None);
        var toolNames = tools
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expectedToolNames = AgentToolInvocationPolicyMetadata.ProjectStructureReadTools
            .Concat(AgentToolInvocationPolicyMetadata.ProjectStructureMutationTools)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(expectedToolNames.Count, toolNames.Count);
        foreach (var toolName in expectedToolNames)
        {
            Assert.Contains(toolName, toolNames);
        }
    }

    [Fact]
    public async Task ProcessRuntimeProvider_app_composition_preserves_process_tool_exact_name_parity()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();

        var processProvider = Assert.Single(
            scope.ServiceProvider.GetServices<IAgentRuntimeToolProvider>()
                .OfType<ProcessAgentRuntimeToolProvider>());

        Assert.Equal(1000, processProvider.Order);

        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededAgent = seed.Agents[0];
        var provider = Assert.Single(seed.Providers, item => item.Id == seededAgent.ProviderProfileId);
        var agent = seededAgent with
        {
            Permissions = AgentPermissionsPolicy.Default,
            ConfigurationJson = AgentProcessAccessMetadata.Write(
                seededAgent.ConfigurationJson,
                new AgentProcessAccessSettings
                {
                    CanRead = true,
                    CanWrite = true,
                    AllowAllDefinitions = true
                })
        };

        var tools = await processProvider.CreateToolsAsync(
            new AgentRuntimeToolProviderContext(
                agent,
                provider,
                [],
                SuppressApprovalRequirements: false,
                AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
                RuntimeSessionKey: "sb07-runtime-smoke",
                Tags: new Dictionary<string, string>
                {
                    ["proof"] = "SB07"
                }),
            CancellationToken.None);
        var toolNames = tools
            .Select(item => item.Name)
            .ToList();

        Assert.Equal(ExpectedProcessToolNames.Length, toolNames.Count);
        Assert.Equal(
            ExpectedProcessToolNames.Length,
            toolNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var toolName in ExpectedProcessToolNames)
        {
            Assert.Contains(toolNames, item => string.Equals(item, toolName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
