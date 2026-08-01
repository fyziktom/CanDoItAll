using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentFrameworkModuleChatContextBuilderTests
{
    [Fact]
    public void Agents_builder_exposes_only_selections_relevant_to_the_active_view()
    {
        var agentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var catalog = AgentFrameworkAgentsChatContextBuilder.Build(
            AgentFrameworkAgentsChatView.Agents,
            agentId,
            teamId,
            technicalAgentCount: 12,
            providerCount: 3,
            boundResourceCount: 5,
            capabilityCount: 18,
            activeRunCount: 2,
            failedRunCount: 1);

        Assert.Equal("agents", catalog.Position.View);
        Assert.Equal(agentId.ToString("D"), catalog.Position.PrimarySelection?.Id);
        Assert.Equal(teamId.ToString("D"), Assert.Single(catalog.Position.SelectedEntities).Id);
        Assert.Contains(catalog.Position.Facts, fact =>
            fact.Name == "technical-agent-count" && fact.Value == "12");

        var providers = AgentFrameworkAgentsChatContextBuilder.Build(
            AgentFrameworkAgentsChatView.Providers,
            agentId,
            teamId,
            technicalAgentCount: 12,
            providerCount: 3,
            boundResourceCount: 5,
            capabilityCount: 18,
            activeRunCount: 2,
            failedRunCount: 1);

        Assert.Null(providers.Position.PrimarySelection);
        Assert.Empty(providers.Position.SelectedEntities);
        Assert.Equal("providers", providers.Source.Id.Value);
    }

    [Fact]
    public void Agents_builder_uses_validated_component_selection_labels()
    {
        var agent = CreateAgent("Portfolio Architect");
        var team = new AgentTeamDefinition(
            Guid.NewGuid(),
            "Architecture",
            string.Empty,
            [agent.Id],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var surface = AgentFrameworkAgentsChatContextBuilder.Build(
            AgentFrameworkAgentsChatView.Agents,
            agent.Id,
            team.Id,
            technicalAgentCount: 1,
            providerCount: 1,
            boundResourceCount: 0,
            capabilityCount: 0,
            activeRunCount: 0,
            failedRunCount: 0,
            selectedAgent: agent,
            selectedTeam: team);

        Assert.Equal(agent.Name, surface.Position.PrimarySelection?.DisplayName);
        Assert.Equal(team.Name, Assert.Single(surface.Position.SelectedEntities).DisplayName);
    }

    [Fact]
    public void Workflows_builder_exposes_matching_definition_and_run_without_payload_text()
    {
        var workflowId = WorkflowId.New();
        var versionId = WorkflowVersionId.New();
        var run = new WorkflowRunSnapshot(
            WorkflowRunId.New(),
            workflowId,
            versionId,
            WorkflowRunState.WaitingForInput,
            WorkflowRuntimeBackendKind.InProcess,
            "backend-run-secret",
            "prompt and transcript must not be included",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var summary = new WorkflowCatalogItem(
            workflowId,
            versionId,
            "Machine review",
            "sensitive workflow description",
            WorkflowLifecycleStatus.Active,
            WorkflowRuntimeBackendKind.InProcess,
            DateTimeOffset.UtcNow);

        var surface = AgentFrameworkWorkflowsChatContextBuilder.Build(
            view: AgentFrameworkWorkflowsChatView.History,
            definitionCount: 7,
            selectedDefinitionId: workflowId,
            selectedDefinitionSummary: summary,
            selectedDefinition: null,
            selectedRun: run,
            historyLoaded: true,
            historyRunTotalCount: 4,
            pendingRequestCount: 1,
            artifactCount: 2,
            validationIssueCount: 0);

        Assert.Equal("history", surface.Position.View);
        Assert.Equal(workflowId.ToString(), surface.Position.PrimarySelection?.Id);
        Assert.Equal("Machine review", surface.Position.PrimarySelection?.DisplayName);
        Assert.Equal(run.RunId.ToString(), Assert.Single(surface.Position.SelectedEntities).Id);
        Assert.Contains(surface.Position.Facts, fact =>
            fact.Name == "run-state" && fact.Value == WorkflowRunState.WaitingForInput.ToString());
        var serialized = JsonSerializer.Serialize(surface);
        Assert.DoesNotContain("backend-run-secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("prompt and transcript", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive workflow description", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Workflows_builder_drops_stale_or_child_owned_selections()
    {
        var workflowId = WorkflowId.New();
        var staleRun = new WorkflowRunSnapshot(
            WorkflowRunId.New(),
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRunState.Running,
            WorkflowRuntimeBackendKind.InProcess,
            "runtime-id",
            "summary",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var history = AgentFrameworkWorkflowsChatContextBuilder.Build(
            view: AgentFrameworkWorkflowsChatView.History,
            definitionCount: 1,
            selectedDefinitionId: workflowId,
            selectedDefinitionSummary: null,
            selectedDefinition: null,
            selectedRun: staleRun,
            historyLoaded: true,
            historyRunTotalCount: 1,
            pendingRequestCount: 0,
            artifactCount: 0,
            validationIssueCount: 0);
        var analytics = AgentFrameworkWorkflowsChatContextBuilder.Build(
            view: AgentFrameworkWorkflowsChatView.Analytics,
            definitionCount: 1,
            selectedDefinitionId: workflowId,
            selectedDefinitionSummary: null,
            selectedDefinition: null,
            selectedRun: staleRun,
            historyLoaded: true,
            historyRunTotalCount: 1,
            pendingRequestCount: 0,
            artifactCount: 0,
            validationIssueCount: 0);

        Assert.NotNull(history.Position.PrimarySelection);
        Assert.Empty(history.Position.SelectedEntities);
        Assert.DoesNotContain(history.Position.Facts, fact => fact.Name == "run-state");
        Assert.Null(analytics.Position.PrimarySelection);
        Assert.Empty(analytics.Position.SelectedEntities);
    }

    [Fact]
    public void Workflows_editor_includes_only_a_node_from_the_current_definition()
    {
        var workflowId = WorkflowId.New();
        var currentNode = new WorkflowAgentChatNodeSelection(
            workflowId,
            new WorkflowNodeId("machine-details"),
            "Machine details",
            WorkflowNodeKind.Executor);
        var staleNode = currentNode with
        {
            DefinitionId = WorkflowId.New()
        };

        var current = BuildEditorSurface(workflowId, currentNode);
        var stale = BuildEditorSurface(workflowId, staleNode);

        var nodeReference = Assert.Single(current.Position.SelectedEntities);
        Assert.Equal("workflow-node", nodeReference.Kind);
        Assert.Equal("machine-details", nodeReference.Id);
        Assert.Equal("Machine details", nodeReference.DisplayName);
        Assert.Contains(current.Position.Facts, fact =>
            fact.Name == "workflow-node-kind" && fact.Value == WorkflowNodeKind.Executor.ToString());
        Assert.Empty(stale.Position.SelectedEntities);
        Assert.DoesNotContain(stale.Position.Facts, fact => fact.Name == "workflow-node-kind");
    }

    [Fact]
    public void Workflows_builder_bounds_page_owned_display_names()
    {
        var workflowId = WorkflowId.New();
        var summary = new WorkflowCatalogItem(
            workflowId,
            WorkflowVersionId.New(),
            new string('x', AgentChatPositionLimits.MaximumLabelLength + 20),
            string.Empty,
            WorkflowLifecycleStatus.Active,
            WorkflowRuntimeBackendKind.InProcess,
            DateTimeOffset.UtcNow);

        var surface = AgentFrameworkWorkflowsChatContextBuilder.Build(
            AgentFrameworkWorkflowsChatView.Workflows,
            definitionCount: 1,
            selectedDefinitionId: workflowId,
            selectedDefinitionSummary: summary,
            selectedDefinition: null,
            selectedRun: null,
            historyLoaded: false,
            historyRunTotalCount: 0,
            pendingRequestCount: 0,
            artifactCount: 0,
            validationIssueCount: 0);

        Assert.Equal(
            AgentChatPositionLimits.MaximumLabelLength,
            surface.Position.PrimarySelection?.DisplayName.Length);
    }

    private static AgentChatContextSurface BuildEditorSurface(
        WorkflowId workflowId,
        WorkflowAgentChatNodeSelection selection)
        => AgentFrameworkWorkflowsChatContextBuilder.Build(
            AgentFrameworkWorkflowsChatView.Editor,
            definitionCount: 1,
            selectedDefinitionId: workflowId,
            selectedDefinitionSummary: null,
            selectedDefinition: null,
            selectedRun: null,
            historyLoaded: false,
            historyRunTotalCount: 0,
            pendingRequestCount: 0,
            artifactCount: 0,
            validationIssueCount: 0,
            selectedNode: selection);

    private static AgentDefinition CreateAgent(string name)
    {
        var timestamp = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            name,
            "Technical agent",
            string.Empty,
            string.Empty,
            AgentLifecycleStatus.Active,
            null,
            "test-model",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            0.2,
            false,
            false,
            "{}",
            false,
            string.Empty,
            AgentPermissionsPolicy.Default,
            [],
            [],
            timestamp,
            timestamp);
    }

    [Fact]
    public void Builders_reject_unknown_views_and_negative_counts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AgentFrameworkAgentsChatContextBuilder.ResolveView("unknown"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AgentFrameworkAgentsChatContextBuilder.ResolveView("scenarios"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AgentFrameworkWorkflowsChatContextBuilder.Build(
                view: AgentFrameworkWorkflowsChatView.Dashboard,
                definitionCount: -1,
                selectedDefinitionId: null,
                selectedDefinitionSummary: null,
                selectedDefinition: null,
                selectedRun: null,
                historyLoaded: false,
                historyRunTotalCount: 0,
                pendingRequestCount: 0,
                artifactCount: 0,
                validationIssueCount: 0));
    }
}
