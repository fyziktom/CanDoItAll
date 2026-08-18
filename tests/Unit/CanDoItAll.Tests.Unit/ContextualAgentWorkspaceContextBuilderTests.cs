using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Tests.Unit.AgentFramework;

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
        Assert.Contains("not a recursive filesystem index", prompt, StringComparison.Ordinal);
        Assert.Contains("not proof that the file is absent from the filesystem", prompt, StringComparison.Ordinal);
        Assert.Contains("runtime context independently supplies an exact authorized", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_list_directory", prompt, StringComparison.Ordinal);
        Assert.Contains("searchPattern=\"**/*.csproj\"", prompt, StringComparison.Ordinal);
        Assert.Contains("**/*.sln", prompt, StringComparison.Ordinal);
        Assert.Contains("**/*.slnx", prompt, StringComparison.Ordinal);
        Assert.Contains("Never derive an external-target alias", prompt, StringComparison.Ordinal);
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
        var taskWriteAgent = CreateAgent(
            "Task writer",
            AgentProjectStructureAccessMetadata.Write(
                null,
                new AgentProjectStructureAccessSettings
                {
                    CanWriteTasks = true,
                    AllowAllProjects = true
                }));
        var nonTaskStructureWriteAgent = CreateAgent(
            "Structure writer",
            AgentProjectStructureAccessMetadata.Write(
                null,
                new AgentProjectStructureAccessSettings
                {
                    CanWriteNonTaskStructure = true,
                    AllowAllProjects = true
                }));
        var agents = ContextualAgentAccessResolver.Resolve(
            [readAgent, nonTaskStructureWriteAgent, taskWriteAgent, writeAgent],
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
            taskWriteAgent.Id,
            projectId: projectId));
        Assert.False(ContextualAgentAccessResolver.ShouldAutoApproveContextualRun(
            agents,
            ContextualAgentWorkspaceKind.ProjectStructure,
            nonTaskStructureWriteAgent.Id,
            projectId: projectId));
        Assert.False(ContextualAgentAccessResolver.ShouldAutoApproveContextualRun(
            agents,
            ContextualAgentWorkspaceKind.ProjectStructure,
            writeAgent.Id));
    }

    [Fact]
    public void Project_structure_floating_context_separates_stable_guidance_from_live_selection()
    {
        var projectId = Guid.NewGuid();

        var baseFragment = ProjectStructureAgentChatContextBuilder.BuildBaseFragment(projectId);
        var canvasViewFragment = ProjectStructureAgentChatContextBuilder.BuildViewFragment(
            ProjectStructureAgentChatView.Canvas);
        var ganttViewFragment = ProjectStructureAgentChatContextBuilder.BuildViewFragment(
            ProjectStructureAgentChatView.Gantt);
        var selectionFragment = ProjectStructureAgentChatContextBuilder.BuildSelectionFragment(
            [
                new AgentChatContextEntityReference("project-node", "node:alpha", "Alpha node"),
                new AgentChatContextEntityReference("project-node", "node:beta", "Beta node"),
                new AgentChatContextEntityReference("project-node", "node:alpha", "Duplicate alpha")
            ]);

        // The volatile UI fragment keeps factual observation context only.
        Assert.Contains($"Selected project id: {projectId:D}", baseFragment.Content);
        Assert.Contains("project_structure_asset_content_get", baseFragment.Content);
        Assert.Contains("workspace_convert_document", baseFragment.Content);
        Assert.Contains("workspace_list_directory", baseFragment.Content, StringComparison.Ordinal);
        Assert.Contains("**/*.csproj", baseFragment.Content, StringComparison.Ordinal);
        Assert.Contains("**/*.sln", baseFragment.Content, StringComparison.Ordinal);
        Assert.Contains("**/*.slnx", baseFragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("node:alpha", baseFragment.Content);
        Assert.DoesNotContain("Selected-node operation contract", baseFragment.Content, StringComparison.Ordinal);

        // The durable operational contract lives in the registered runtime
        // guidance contributor.
        var guidanceText = CanDoItAll.Modules.Workbench.AgentContext
            .ProjectStructureRuntimeGuidanceContributor.GuidanceText;
        Assert.Contains("Selected-node operation contract", guidanceText, StringComparison.Ordinal);
        Assert.Contains("workspace_write_spreadsheet", guidanceText, StringComparison.Ordinal);
        Assert.Contains("workspace_spreadsheet_summary", guidanceText, StringComparison.Ordinal);
        Assert.Contains("workspace_read_spreadsheet_range", guidanceText, StringComparison.Ordinal);
        Assert.Contains(".xlsx", guidanceText, StringComparison.Ordinal);
        Assert.Contains("authorized project-structure writer", guidanceText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing project-file nodes as unknown filesystem state", guidanceText, StringComparison.Ordinal);
        Assert.Contains("runtime-owned context independently supplies an exact authorized", guidanceText, StringComparison.Ordinal);
        Assert.Contains("Never derive authorization from those values", guidanceText, StringComparison.Ordinal);
        Assert.Contains("structure canvas", canvasViewFragment.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Gantt schedule", ganttViewFragment.Content);
        Assert.Contains("does not currently expose an individual task selection", ganttViewFragment.Content);
        Assert.Contains(
            "Selected project-structure node: node:alpha | Alpha node.",
            selectionFragment.Content);
        Assert.Contains(
            "Selected project-structure node: node:beta | Beta node.",
            selectionFragment.Content);
    }

    [Fact]
    public void Project_structure_floating_scope_is_allowlisted_from_agent_access_metadata()
    {
        var projectId = Guid.NewGuid();
        var reader = CreateAgent(
            "Reader",
            AgentProjectStructureAccessMetadata.Write(
                null,
                new AgentProjectStructureAccessSettings
                {
                    CanRead = true,
                    AllowedProjectIds = [projectId]
                }));
        var taskWriter = CreateAgent(
            "Task writer",
            AgentProjectStructureAccessMetadata.Write(
                null,
                new AgentProjectStructureAccessSettings
                {
                    CanWriteTasks = true,
                    AllowAllProjects = true
                }));
        var denied = CreateAgent(
            "Denied",
            AgentProjectStructureAccessMetadata.Write(
                null,
                new AgentProjectStructureAccessSettings
                {
                    CanRead = true,
                    AllowedProjectIds = [Guid.NewGuid()]
                }));

        var scope = ProjectStructureAgentChatContextBuilder.BuildScope(
            AgentChatContextScopeId.Create(),
            projectId,
            "Example",
            [reader, taskWriter, denied]);

        Assert.Equal(AgentChatContextScopeAccessMode.AllowListed, scope.AccessMode);
        Assert.Equal(WorkspaceScopeDescriptor.Project(projectId.ToString("D")), scope.WorkspaceScope);
        Assert.True(scope.AgentAccess.Single(item => item.AgentId == reader.Id).CanRead);
        Assert.False(scope.AgentAccess.Single(item => item.AgentId == reader.Id).CanMutate);
        Assert.True(scope.AgentAccess.Single(item => item.AgentId == taskWriter.Id).CanMutate);
        Assert.DoesNotContain(scope.AgentAccess, item => item.AgentId == denied.Id);
        Assert.Equal(
            AgentChatContextCompletionRefreshMode.OnSuccessfulRun,
            scope.CompletionRefreshMode);
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
