using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessAgentRuntimeToolProviderTests
{
    private static readonly string[] ExpectedProcessToolNames =
    [
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionsList,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionEditorGet,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionSave,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionRoleAdd,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionPublish,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionDelete,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionExport,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionImport,
        AgentToolInvocationPolicyMetadata.ProcessesRunsList,
        AgentToolInvocationPolicyMetadata.ProcessesRunDetailGet,
        AgentToolInvocationPolicyMetadata.ProcessesAnalyticsGet,
        AgentToolInvocationPolicyMetadata.ProcessesRunStart,
        AgentToolInvocationPolicyMetadata.ProcessesStepTransition,
        AgentToolInvocationPolicyMetadata.ProcessesAssignmentResolve,
        AgentToolInvocationPolicyMetadata.ProcessesArtifactRecord,
        AgentToolInvocationPolicyMetadata.ProcessesPartyOptionsList,
        AgentToolInvocationPolicyMetadata.ProcessesExecutorOptionsList,
        AgentToolInvocationPolicyMetadata.ProcessesTemplatesList,
        AgentToolInvocationPolicyMetadata.ProcessesTemplateGet,
        AgentToolInvocationPolicyMetadata.ProcessesTemplateMermaidGet,
        AgentToolInvocationPolicyMetadata.ProcessesTemplateImport,
        AgentToolInvocationPolicyMetadata.ProcessesTemplateBaselineScenariosList,
        AgentToolInvocationPolicyMetadata.ProcessesTemplateLiveRunProfilesList
    ];

    private static readonly string[] ExpectedReadToolNames = ExpectedProcessToolNames
        .Where(toolName => !AgentToolInvocationPolicyMetadata.IsMutationTool(toolName))
        .ToArray();

    private static readonly string[] ExpectedMutationToolNames = ExpectedProcessToolNames
        .Where(AgentToolInvocationPolicyMetadata.IsMutationTool)
        .ToArray();

    [Theory]
    [InlineData(AgentRuntimeToolProviderPurpose.InteractiveChat)]
    [InlineData(AgentRuntimeToolProviderPurpose.GovernedProcessAutomation)]
    [InlineData(AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive)]
    [InlineData(AgentRuntimeToolProviderPurpose.A2AEndpoint)]
    public async Task ProcessAgentRuntimeToolProvider_purpose_matrix_exposes_read_tools_without_write_access(
        AgentRuntimeToolProviderPurpose purpose)
    {
        var toolNames = await CreateToolNamesAsync(
            purpose,
            new AgentProcessAccessSettings
            {
                CanRead = true,
                CanWrite = false,
                AllowAllDefinitions = true
            });

        AssertToolNames(ExpectedReadToolNames, toolNames);
        Assert.DoesNotContain(toolNames, AgentToolInvocationPolicyMetadata.IsMutationTool);
    }

    [Theory]
    [InlineData(AgentRuntimeToolProviderPurpose.InteractiveChat)]
    [InlineData(AgentRuntimeToolProviderPurpose.GovernedProcessAutomation)]
    [InlineData(AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive)]
    [InlineData(AgentRuntimeToolProviderPurpose.A2AEndpoint)]
    public async Task ProcessAgentRuntimeToolProvider_purpose_matrix_preserves_mutation_tools_with_explicit_write_access(
        AgentRuntimeToolProviderPurpose purpose)
    {
        var toolNames = await CreateToolNamesAsync(
            purpose,
            new AgentProcessAccessSettings
            {
                CanRead = true,
                CanWrite = true,
                AllowAllDefinitions = true
            });

        AssertToolNames(ExpectedProcessToolNames, toolNames);
        foreach (var mutationToolName in ExpectedMutationToolNames)
        {
            Assert.Contains(mutationToolName, toolNames);
        }
    }

    [Fact]
    public async Task ProcessAgentRuntimeToolProvider_no_process_read_access_exposes_no_process_tools()
    {
        var toolNames = await CreateToolNamesAsync(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            new AgentProcessAccessSettings());

        Assert.Empty(toolNames);
    }

    [Fact]
    public async Task ProcessAgentRuntimeToolProvider_unknown_purpose_exposes_no_process_tools()
    {
        var toolNames = await CreateToolNamesAsync(
            (AgentRuntimeToolProviderPurpose)999,
            new AgentProcessAccessSettings
            {
                CanRead = true,
                CanWrite = true,
                AllowAllDefinitions = true
            });

        Assert.Empty(toolNames);
    }

    private static async Task<IReadOnlyList<string>> CreateToolNamesAsync(
        AgentRuntimeToolProviderPurpose purpose,
        AgentProcessAccessSettings accessSettings)
    {
        var provider = new ProcessAgentRuntimeToolProvider(null!, null!, null!, null!, null!);
        var tools = await provider.CreateToolsAsync(
            CreateContext(purpose, accessSettings),
            CancellationToken.None);

        return tools
            .Select(tool => tool.Name)
            .ToList();
    }

    private static AgentRuntimeToolProviderContext CreateContext(
        AgentRuntimeToolProviderPurpose purpose,
        AgentProcessAccessSettings accessSettings)
    {
        return new AgentRuntimeToolProviderContext(
            CreateAgent(accessSettings),
            CreateProviderProfile(),
            [],
            SuppressApprovalRequirements: false,
            purpose,
            RuntimeSessionKey: "process-provider-purpose-test",
            Tags: new Dictionary<string, string>
            {
                ["workspaceScopeKind"] = "Organization"
            });
    }

    private static AgentDefinition CreateAgent(AgentProcessAccessSettings accessSettings)
        => new(
            Id: Guid.NewGuid(),
            Name: "Process Provider Test Agent",
            RoleTitle: "Tester",
            Summary: "Tests process runtime tools.",
            Instructions: "Use process tools.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: Guid.NewGuid(),
            Model: string.Empty,
            Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: AgentProcessAccessMetadata.Write("{}", accessSettings),
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default with
            {
                CanUseTools = true
            },
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);

    private static ProviderProfile CreateProviderProfile()
        => new(
            Guid.NewGuid(),
            "Unit Provider",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-4.1",
            ProviderTransportKind.ChatCompletions,
            true,
            true,
            true,
            false,
            true,
            "{}",
            string.Empty,
            "Not checked",
            null,
            []);

    private static void AssertToolNames(
        IReadOnlyList<string> expectedToolNames,
        IReadOnlyList<string> actualToolNames)
    {
        Assert.Equal(expectedToolNames.Count, actualToolNames.Count);
        foreach (var expectedToolName in expectedToolNames)
        {
            Assert.Contains(actualToolNames, toolName =>
                string.Equals(toolName, expectedToolName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
