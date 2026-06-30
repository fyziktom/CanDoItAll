using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Components;

public sealed class ContextualAgentAccessResolverTests
{
    [Fact]
    public void Project_structure_context_filters_to_active_agents_with_matching_project_access()
    {
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var matchingAgent = CreateAgent(
            "Project helper",
            AgentProjectStructureAccessMetadata.Write(
                null,
                new AgentProjectStructureAccessSettings
                {
                    CanRead = true,
                    AllowedProjectIds = [projectId]
                }),
            tags: ["project"]);
        var writeAgent = CreateAgent(
            "Project writer",
            AgentProjectStructureAccessMetadata.Write(
                null,
                new AgentProjectStructureAccessSettings
                {
                    CanWrite = true,
                    AllowAllProjects = true
                }),
            tags: ["write"]);
        var wrongProjectAgent = CreateAgent(
            "Other project helper",
            AgentProjectStructureAccessMetadata.Write(
                null,
                new AgentProjectStructureAccessSettings
                {
                    CanRead = true,
                    AllowedProjectIds = [otherProjectId]
                }));
        var templateAgent = CreateAgent(
            "Template",
            matchingAgent.ConfigurationJson,
            isTemplate: true);

        var result = ContextualAgentAccessResolver.Resolve(
            [matchingAgent, writeAgent, wrongProjectAgent, templateAgent],
            ContextualAgentWorkspaceKind.ProjectStructure,
            projectId: projectId);

        Assert.Collection(
            result,
            item =>
            {
                Assert.Equal(matchingAgent.Id, item.Agent.Id);
                Assert.True(item.CanRead);
                Assert.False(item.CanWrite);
                Assert.Equal("This project", item.ScopeLabel);
            },
            item =>
            {
                Assert.Equal(writeAgent.Id, item.Agent.Id);
                Assert.True(item.CanRead);
                Assert.True(item.CanWrite);
                Assert.Equal("All projects", item.ScopeLabel);
            });
    }

    [Fact]
    public void Processes_context_filters_to_active_agents_with_matching_definition_access()
    {
        var definitionId = Guid.NewGuid();
        var otherDefinitionId = Guid.NewGuid();
        var matchingAgent = CreateAgent(
            "Process helper",
            AgentProcessAccessMetadata.Write(
                null,
                new AgentProcessAccessSettings
                {
                    CanRead = true,
                    AllowedDefinitionIds = [definitionId]
                }));
        var allDefinitionsAgent = CreateAgent(
            "Process writer",
            AgentProcessAccessMetadata.Write(
                null,
                new AgentProcessAccessSettings
                {
                    CanWrite = true,
                    AllowAllDefinitions = true
                }));
        var wrongDefinitionAgent = CreateAgent(
            "Other process helper",
            AgentProcessAccessMetadata.Write(
                null,
                new AgentProcessAccessSettings
                {
                    CanRead = true,
                    AllowedDefinitionIds = [otherDefinitionId]
                }));
        var suspendedAgent = CreateAgent(
            "Suspended",
            allDefinitionsAgent.ConfigurationJson,
            status: AgentLifecycleStatus.Suspended);

        var result = ContextualAgentAccessResolver.Resolve(
            [matchingAgent, allDefinitionsAgent, wrongDefinitionAgent, suspendedAgent],
            ContextualAgentWorkspaceKind.Processes,
            processDefinitionId: definitionId);

        Assert.Collection(
            result,
            item =>
            {
                Assert.Equal(matchingAgent.Id, item.Agent.Id);
                Assert.True(item.CanRead);
                Assert.False(item.CanWrite);
                Assert.Equal("This process", item.ScopeLabel);
            },
            item =>
            {
                Assert.Equal(allDefinitionsAgent.Id, item.Agent.Id);
                Assert.True(item.CanRead);
                Assert.True(item.CanWrite);
                Assert.Equal("All processes", item.ScopeLabel);
            });
    }

    [Fact]
    public void BuildPrompt_includes_selected_project_structure_node_ids()
    {
        var projectId = Guid.NewGuid();

        var prompt = ContextualAgentWorkspaceContextBuilder.BuildPrompt(
            ContextualAgentWorkspaceKind.ProjectStructure,
            projectId,
            processDefinitionId: null,
            [" node:alpha ", "node:beta", "node:alpha"],
            "move selected nodes");

        Assert.Contains($"Selected project id: {projectId:D}", prompt);
        Assert.Contains("Selected project-structure node ids: node:alpha, node:beta.", prompt);
        Assert.Contains("Treat \"selected nodes\" as exactly the selected node ids listed above", prompt);
        Assert.Contains("When task ordering matters, create DependsOn dependency links", prompt);
    }

    private static AgentDefinition CreateAgent(
        string name,
        string configurationJson,
        IReadOnlyList<string>? tags = null,
        bool isTemplate = false,
        AgentLifecycleStatus status = AgentLifecycleStatus.Active)
    {
        var timestamp = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            name,
            "Technical agent",
            "Helps with contextual work.",
            "Assist with the current workspace.",
            status,
            ProviderProfileId: null,
            "test-model",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            configurationJson,
            isTemplate,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            tags ?? [],
            timestamp,
            timestamp);
    }
}
