using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentChatPanel
{
    [Parameter]
    public Guid? PreferredAgentId { get; set; }

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    private IReadOnlyList<AgentDefinition> agents = [];
    private ChatAgentWorkspaceSnapshot? workspace;
    private IReadOnlyList<ExecutionLogEntry> executionLog = [];
    private IReadOnlyList<AgentRunMetric> metrics = [];
    private AgentDefinition? selectedAgent;
    private Guid? selectedAgentId;
    private Guid? selectedSessionId;
    private string draftPrompt = string.Empty;
    private string pendingUserPrompt = string.Empty;
    private IReadOnlyList<string> draftAttachmentPaths = [];
    private bool isBusy;
    private int composerKey;
    private string message = string.Empty;
    private string messageTone = "info";
    private string messageLabel = "Info";
    private string runStateText = string.Empty;
    private string runStateTone = "neutral";

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (PreferredAgentId.HasValue &&
            PreferredAgentId != selectedAgentId &&
            agents.Any(item => item.Id == PreferredAgentId.Value))
        {
            await SelectAgentAsync(PreferredAgentId.Value);
        }
    }

    private async Task LoadAsync()
    {
        agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
        if (agents.Count == 0)
        {
            workspace = null;
            selectedAgent = null;
            selectedAgentId = null;
            selectedSessionId = null;
            executionLog = [];
            metrics = [];
            return;
        }

        var initialAgentId = PreferredAgentId.HasValue &&
                             agents.Any(item => item.Id == PreferredAgentId.Value)
            ? PreferredAgentId.Value
            : selectedAgentId is { } currentAgentId &&
              agents.Any(item => item.Id == currentAgentId)
                ? currentAgentId
                : agents[0].Id;

        await LoadWorkspaceAsync(initialAgentId, selectedSessionId);
    }

    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    private async Task SelectAgentAsync(Guid agentId)
    {
        await LoadWorkspaceAsync(agentId, preferredSessionId: null);
    }

    private async Task SelectSessionAsync(Guid sessionId)
    {
        if (!selectedAgentId.HasValue)
        {
            return;
        }

        await LoadWorkspaceAsync(selectedAgentId.Value, sessionId);
    }

    private async Task CreateThreadAsync()
    {
        if (!selectedAgentId.HasValue)
        {
            return;
        }

        isBusy = true;
        try
        {
            var session = await WorkspaceService.GetOrCreateChatSessionAsync(selectedAgentId.Value);
            await LoadWorkspaceAsync(selectedAgentId.Value, session.Id);
            SetMessage("Ready", "success", "New thread created.");
        }
        catch (Exception exception)
        {
            SetMessage("Attention", "danger", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private Task HandleDraftPromptChangedAsync(string value)
    {
        draftPrompt = value;
        return Task.CompletedTask;
    }

    private async Task SendMessageAsync()
    {
        if (!selectedAgentId.HasValue)
        {
            SetMessage("Heads up", "warning", "Select a technical agent before sending a prompt.");
            return;
        }

        var prompt = BuildPromptWithAttachments();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            SetMessage("Heads up", "warning", "Enter a prompt before sending it.");
            return;
        }

        isBusy = true;
        pendingUserPrompt = draftPrompt;
        var previousDraft = draftPrompt;
        draftPrompt = string.Empty;
        composerKey++;
        try
        {
            var result = await WorkspaceService.SendMessageAsync(
                selectedAgentId.Value,
                selectedSessionId,
                prompt);
            draftAttachmentPaths = [];
            await LoadWorkspaceAsync(selectedAgentId.Value, result.ChatSessionId);
            SetMessage("Ready", "success", "Prompt sent through the integrated runtime.");
        }
        catch (Exception exception)
        {
            draftPrompt = previousDraft;
            composerKey++;
            SetMessage("Attention", "danger", exception.Message);
        }
        finally
        {
            pendingUserPrompt = string.Empty;
            isBusy = false;
        }
    }

    private async Task HandleApprovalDecisionAsync(bool approved)
    {
        await ContinueApprovalAsync(approved, autoApprovePendingToolCalls: false);
    }

    private async Task ApproveConversationAsync()
    {
        await ContinueApprovalAsync(approved: true, autoApprovePendingToolCalls: true);
    }

    private async Task ContinueApprovalAsync(bool approved, bool autoApprovePendingToolCalls)
    {
        if (!selectedAgentId.HasValue || !selectedSessionId.HasValue)
        {
            return;
        }

        isBusy = true;
        try
        {
            await WorkspaceService.RespondToPendingApprovalsAsync(
                selectedAgentId.Value,
                selectedSessionId.Value,
                approved,
                autoApprovePendingToolCalls);
            await LoadWorkspaceAsync(selectedAgentId.Value, selectedSessionId.Value);
            SetMessage(
                approved ? "Ready" : "Heads up",
                approved ? "success" : "warning",
                approved
                    ? autoApprovePendingToolCalls
                        ? "Approval resumed the run and enabled remaining approvals for the active execution."
                        : "Approval resumed the run."
                    : "Approval was rejected and the thread was refreshed.");
        }
        catch (Exception exception)
        {
            SetMessage("Attention", "danger", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task StageAttachmentsAsync()
    {
        if (workspace?.SelectedRun is null)
        {
            SetMessage("Heads up", "warning", "Run a prompt first so the thread has execution artifacts to stage.");
            return;
        }

        var detail = await WorkspaceService.GetExecutionRunDetailAsync(workspace.SelectedRun.Id);
        var artifactPaths = detail.Artifacts
            .Select(item => item.RelativePath)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (artifactPaths.Count == 0)
        {
            SetMessage("Heads up", "warning", "The selected run does not have persisted artifacts yet.");
            return;
        }

        draftAttachmentPaths = artifactPaths;
        SetMessage("Ready", "success", $"Staged {artifactPaths.Count} artifact path(s) for the next prompt.");
    }

    private async Task LoadWorkspaceAsync(Guid agentId, Guid? preferredSessionId)
    {
        selectedAgentId = agentId;
        selectedAgent = agents.FirstOrDefault(item => item.Id == agentId);
        workspace = await WorkspaceService.GetChatAgentWorkspaceAsync(agentId, preferredSessionId);
        selectedSessionId = workspace.SelectedSessionId;

        var runtimeSnapshot = await WorkspaceService.GetChatRuntimeSnapshotAsync(agentId, workspace.SelectedSessionId);
        executionLog = runtimeSnapshot.ExecutionLog;
        metrics = runtimeSnapshot.Metrics;
        ResolveRunState();
    }

    private string BuildPromptWithAttachments()
    {
        if (draftAttachmentPaths.Count == 0)
        {
            return draftPrompt.Trim();
        }

        var attachmentText = string.Join(
            Environment.NewLine,
            draftAttachmentPaths.Select(item => $"- {item}"));

        return $"""
Use these workspace artifacts as input:
{attachmentText}

{draftPrompt.Trim()}
""";
    }

    private void ResolveRunState()
    {
        if (workspace?.SelectedRun is null)
        {
            runStateText = string.Empty;
            runStateTone = "neutral";
            return;
        }

        var run = workspace.SelectedRun;
        runStateText = run.State.ToString();
        runStateTone = run.State switch
        {
            ExecutionState.Completed => "success",
            ExecutionState.WaitingOnTool => "warning",
            ExecutionState.Failed => "danger",
            _ => "info"
        };
    }

    private void SetMessage(string label, string tone, string value)
    {
        messageLabel = label;
        messageTone = tone;
        message = value;
    }

    private static string ResolveAgentMeta(AgentDefinition agent)
    {
        return string.IsNullOrWhiteSpace(agent.Model)
            ? "No model configured"
            : agent.Model;
    }

    private static string BuildSessionMeta(ChatSessionSummaryRecord session)
    {
        return session.MessageCount == 0
            ? "Empty thread"
            : $"{session.MessageCount} message(s)";
    }

    private static string ResolveAgentTone(AgentLifecycleStatus status)
    {
        return status switch
        {
            AgentLifecycleStatus.Active => "success",
            AgentLifecycleStatus.Suspended => "warning",
            AgentLifecycleStatus.Archived => "neutral",
            _ => "info"
        };
    }
}
