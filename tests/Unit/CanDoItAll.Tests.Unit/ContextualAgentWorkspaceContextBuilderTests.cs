using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class ContextualAgentWorkspaceContextBuilderTests
{
    [Fact]
    public void BuildPrompt_includes_project_structure_asset_guidance()
    {
        var projectId = Guid.NewGuid();

        var prompt = ContextualAgentWorkspaceContextBuilder.BuildPrompt(
            ContextualAgentWorkspaceKind.ProjectStructure,
            projectId,
            processDefinitionId: null,
            [" node:alpha ", "node:beta", "node:alpha"],
            "extract prices from the quotation PDF");

        Assert.Contains($"Selected project id: {projectId:D}", prompt);
        Assert.Contains("Selected project-structure node ids: node:alpha, node:beta.", prompt);
        Assert.Contains("If none are listed, work at selected project scope", prompt);
        Assert.Contains("project_structure_asset_content_get", prompt);
        Assert.Contains("workspace_convert_document", prompt);
        Assert.Contains("searchPattern uses glob syntax, not regex", prompt);
    }

    [Fact]
    public void ShouldAutoApproveContextualRun_requires_write_agent_and_scoped_project()
    {
        var projectId = Guid.NewGuid();
        var readAgent = CreateAgent(
            "Project reader",
            AgentProjectStructureAccessMetadata.Write(
                null,
                new AgentProjectStructureAccessSettings
                {
                    CanRead = true,
                    AllowedProjectIds = [projectId]
                }));
        var writeAgent = CreateAgent(
            "Project writer",
            AgentProjectStructureAccessMetadata.Write(
                null,
                new AgentProjectStructureAccessSettings
                {
                    CanWrite = true,
                    AllowAllProjects = true
                }));
        var agents = ContextualAgentAccessResolver.Resolve(
            [readAgent, writeAgent],
            ContextualAgentWorkspaceKind.ProjectStructure,
            projectId: projectId);

        Assert.False(ContextualAgentAccessResolver.ShouldAutoApproveContextualRun(
            agents,
            ContextualAgentWorkspaceKind.ProjectStructure,
            readAgent.Id,
            projectId: projectId));
        Assert.True(ContextualAgentAccessResolver.ShouldAutoApproveContextualRun(
            agents,
            ContextualAgentWorkspaceKind.ProjectStructure,
            writeAgent.Id,
            projectId: projectId));
        Assert.False(ContextualAgentAccessResolver.ShouldAutoApproveContextualRun(
            agents,
            ContextualAgentWorkspaceKind.ProjectStructure,
            writeAgent.Id));
    }

    private static AgentDefinition CreateAgent(
        string name,
        string configurationJson)
    {
        var timestamp = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            name,
            "Technical agent",
            "Helps with contextual work.",
            "Assist with the current workspace.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            "test-model",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            configurationJson,
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: timestamp,
            UpdatedAtUtc: timestamp);
    }
}
