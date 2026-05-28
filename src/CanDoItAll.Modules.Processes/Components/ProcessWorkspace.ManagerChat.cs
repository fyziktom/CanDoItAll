using System.Text.Json;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private const string ManagerChatSourceKind = "process-manager-chat";
    private const string ManagerChatRequester = "process-workspace-manager-chat";
    private const string ManagerChatDefaultRunStateTone = "neutral";

    private IReadOnlyList<AgentDefinition> managerChatAgents = [];
    private AgentDefinition? managerChatAgent;
    private ChatAgentWorkspaceSnapshot? managerChatWorkspace;
    private IReadOnlyList<ExecutionLogEntry> managerChatExecutionLog = [];
    private IReadOnlyList<AgentRunMetric> managerChatMetrics = [];
    private IReadOnlyList<string> managerChatDraftAttachmentPaths = [];
    private ProcessManagerAgentResolution managerChatAgentResolution =
        ProcessManagerAgentResolution.NotEvaluated("Manager chat has not resolved a manager agent yet.");
    private Guid? managerChatProcessId;
    private Guid? managerChatAgentId;
    private Guid? managerChatSessionId;
    private Guid? managerChatSelectedRunId;
    private string managerChatDraftPrompt = string.Empty;
    private string managerChatPendingPrompt = string.Empty;
    private string managerChatRunStateText = string.Empty;
    private string managerChatRunStateTone = ManagerChatDefaultRunStateTone;
    private string managerChatLoadError = string.Empty;
    private bool managerChatIsBusy;
    private bool managerChatIsLoading;
    private bool managerChatRunSelectorOpen;
    private int managerChatComposerKey;

    protected override void OnInitialized()
    {
        AgentWorkspaceService.ExecutionUpdated += HandleManagerChatExecutionUpdated;
    }

    private ProcessRunListItem? ManagerChatSelectedRun
        => managerChatSelectedRunId.HasValue
            ? runs.FirstOrDefault(run => run.Id == managerChatSelectedRunId.Value)
            : null;

    private string ManagerChatSelectedRunLabel
        => ManagerChatSelectedRun is { } run
            ? $"{run.Name} / {run.Status}"
            : "No specific run selected";

    private string ManagerChatManagerLabel
        => managerChatAgent is not null
            ? managerChatAgent.Name
            : ResolveConfiguredManagerName();

    private bool CanOpenManagerChatRuntimeDetails
        => managerChatWorkspace?.SelectedRun is not null ||
           managerChatExecutionLog.Count > 0 ||
           managerChatMetrics.Count > 0;

    private async Task LoadManagerChatAsync(CancellationToken cancellationToken = default)
    {
        if (!selectedProcessId.HasValue)
        {
            ResetManagerChatState(clearRunSelection: true);
            managerChatLoadError = "Select a process definition before opening manager chat.";
            return;
        }

        if (managerChatProcessId != selectedProcessId)
        {
            ResetManagerChatState(clearRunSelection: true);
            managerChatProcessId = selectedProcessId;
        }

        managerChatLoadError = string.Empty;
        managerChatIsLoading = true;
        try
        {
            await LoadManagerChatRunSummariesAsync(cancellationToken);
            managerChatAgents = await AgentWorkspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
            var managerResolution = await ResolveManagerChatAgentResolutionAsync(cancellationToken);
            managerChatAgentResolution = managerResolution;
            if (!managerResolution.IsResolved)
            {
                ResetManagerChatAgentState();
                managerChatAgentResolution = managerResolution;
                managerChatLoadError = BuildManagerResolutionError(managerResolution);
                return;
            }

            var nextAgent = managerChatAgents.FirstOrDefault(agent => agent.Id == managerResolution.ResolvedTechnicalAgentId!.Value);
            if (nextAgent is null)
            {
                ResetManagerChatAgentState();
                managerChatAgentResolution = managerResolution;
                managerChatLoadError = "The resolved manager AI resource is not available in the Agent Framework catalog.";
                return;
            }

            if (managerChatAgentId != nextAgent.Id)
            {
                managerChatSessionId = null;
                managerChatWorkspace = null;
                managerChatExecutionLog = [];
                managerChatMetrics = [];
            }

            managerChatAgentId = nextAgent.Id;
            managerChatAgent = nextAgent;
            var session = await AgentWorkspaceService.GetOrCreateChatSessionAsync(
                nextAgent.Id,
                managerChatSessionId,
                cancellationToken);
            await LoadManagerChatWorkspaceAsync(nextAgent.Id, session.Id, cancellationToken);
        }
        catch (Exception exception)
        {
            ResetManagerChatAgentState();
            managerChatLoadError = exception.Message;
        }
        finally
        {
            managerChatIsLoading = false;
        }
    }

    private async Task LoadManagerChatRunSummariesAsync(CancellationToken cancellationToken)
    {
        if (!selectedProcessId.HasValue)
        {
            runs = [];
            managerChatSelectedRunId = null;
            return;
        }

        runs = await ProcessesService.ListRunsAsync(selectedProcessId, ProjectId, cancellationToken);
        if (managerChatSelectedRunId.HasValue &&
            runs.All(run => run.Id != managerChatSelectedRunId.Value))
        {
            managerChatSelectedRunId = null;
        }

        if (!managerChatSelectedRunId.HasValue &&
            selectedRunId.HasValue &&
            runs.Any(run => run.Id == selectedRunId.Value))
        {
            managerChatSelectedRunId = selectedRunId;
        }
    }

    private async Task<ProcessManagerAgentResolution> ResolveManagerChatAgentResolutionAsync(CancellationToken cancellationToken)
    {
        var selectedRun = ManagerChatSelectedRun;
        var configuredRunManager = ProcessManagerAgentResolver.ResolveConfiguredManager(
            selectedRun?.ManagerAgentId,
            selectedRun?.ManagerAgentName,
            managerAgentOptions,
            managerChatAgents);
        if (configuredRunManager.IsResolved || configuredRunManager.IsAmbiguous)
        {
            return configuredRunManager;
        }

        var configuredOverrideManager = ProcessManagerAgentResolver.ResolveConfiguredManager(
            editor.ManagerAgentOverrideId,
            editor.ManagerAgentOverrideName,
            managerAgentOptions,
            managerChatAgents);
        if (configuredOverrideManager.IsResolved || configuredOverrideManager.IsAmbiguous)
        {
            return configuredOverrideManager;
        }

        if (managerChatSelectedRunId.HasValue)
        {
            var details = await RunDetailsLoader.LoadAsync(managerChatSelectedRunId.Value, cancellationToken);
            var assignedManager = ProcessManagerAgentResolver.ResolveAssignedManager(
                details.Assignments,
                managerAgentOptions,
                managerChatAgents);
            if (assignedManager.IsResolved || assignedManager.IsAmbiguous)
            {
                return assignedManager;
            }
        }

        return ProcessManagerAgentResolver.ResolveFallbackManager(managerAgentOptions, managerChatAgents);
    }

    private static string BuildManagerResolutionError(ProcessManagerAgentResolution resolution)
        => resolution.IsAmbiguous
            ? resolution.Summary
            : $"No bound technical manager agent could be resolved for this process. {resolution.Summary}";

    private string ResolveConfiguredManagerName()
    {
        if (!string.IsNullOrWhiteSpace(ManagerChatSelectedRun?.ManagerAgentName))
        {
            return ManagerChatSelectedRun.ManagerAgentName;
        }

        if (!string.IsNullOrWhiteSpace(editor.ManagerAgentOverrideName))
        {
            return editor.ManagerAgentOverrideName;
        }

        return "Default process manager";
    }

    private async Task LoadManagerChatWorkspaceAsync(
        Guid agentId,
        Guid? preferredSessionId,
        CancellationToken cancellationToken = default)
    {
        managerChatWorkspace = await AgentWorkspaceService.GetChatAgentWorkspaceAsync(
            agentId,
            preferredSessionId,
            cancellationToken);
        managerChatSessionId = managerChatWorkspace.SelectedSessionId;

        var runtimeSnapshot = await AgentWorkspaceService.GetChatRuntimeSnapshotAsync(
            agentId,
            managerChatWorkspace.SelectedSessionId,
            cancellationToken);
        managerChatExecutionLog = runtimeSnapshot.ExecutionLog;
        managerChatMetrics = runtimeSnapshot.Metrics;
        ResolveManagerChatRunState();
    }

    private Task HandleManagerChatDraftPromptChangedAsync(string value)
    {
        managerChatDraftPrompt = value;
        return Task.CompletedTask;
    }

    private async Task SendManagerChatMessageAsync()
    {
        if (!managerChatAgentId.HasValue)
        {
            SetError("Resolve a bound process manager agent before sending a manager chat prompt.");
            return;
        }

        var prompt = BuildManagerChatPrompt(BuildManagerChatPromptWithAttachments());
        if (string.IsNullOrWhiteSpace(prompt))
        {
            SetError("Write a manager chat prompt before sending it.");
            return;
        }

        managerChatIsBusy = true;
        managerChatPendingPrompt = managerChatDraftPrompt;
        var previousDraft = managerChatDraftPrompt;
        managerChatDraftPrompt = string.Empty;
        managerChatComposerKey++;

        try
        {
            var result = await AgentWorkspaceService.ExecuteRunAsync(
                new ExecutionRunRequest(
                    AgentId: managerChatAgentId.Value,
                    Prompt: prompt,
                    ChatSessionId: managerChatSessionId,
                    Context: BuildManagerChatInvocationContext(),
                    AutoApprovePendingToolCalls: false));
            managerChatDraftAttachmentPaths = [];
            await LoadManagerChatWorkspaceAsync(managerChatAgentId.Value, result.ChatSessionId ?? managerChatSessionId);
            SetMessage("Manager chat prompt sent.");
        }
        catch (Exception exception)
        {
            managerChatDraftPrompt = previousDraft;
            managerChatComposerKey++;
            SetError($"Manager chat prompt failed. {exception.Message}");
        }
        finally
        {
            managerChatPendingPrompt = string.Empty;
            managerChatIsBusy = false;
        }
    }

    private async Task HandleManagerChatApprovalDecisionAsync(bool approved)
    {
        await ContinueManagerChatApprovalsAsync(approved, autoApprovePendingToolCalls: false);
    }

    private async Task ApproveManagerChatConversationAsync()
    {
        await ContinueManagerChatApprovalsAsync(approved: true, autoApprovePendingToolCalls: true);
    }

    private async Task ContinueManagerChatApprovalsAsync(bool approved, bool autoApprovePendingToolCalls)
    {
        if (!managerChatAgentId.HasValue || !managerChatSessionId.HasValue)
        {
            return;
        }

        managerChatIsBusy = true;
        try
        {
            await AgentWorkspaceService.RespondToPendingApprovalsAsync(
                managerChatAgentId.Value,
                managerChatSessionId.Value,
                approved,
                autoApprovePendingToolCalls);
            await LoadManagerChatWorkspaceAsync(managerChatAgentId.Value, managerChatSessionId.Value);
            SetMessage(approved ? "Manager chat approval applied." : "Manager chat approval rejected.");
        }
        catch (Exception exception)
        {
            SetError($"Manager chat approval failed. {exception.Message}");
        }
        finally
        {
            managerChatIsBusy = false;
        }
    }

    private async Task RenameManagerChatSessionAsync(string title)
    {
        if (!managerChatAgentId.HasValue || !managerChatSessionId.HasValue)
        {
            return;
        }

        managerChatIsBusy = true;
        try
        {
            var session = await AgentWorkspaceService.RenameChatSessionAsync(
                managerChatAgentId.Value,
                managerChatSessionId.Value,
                title);
            await LoadManagerChatWorkspaceAsync(managerChatAgentId.Value, session.Id);
            SetMessage("Manager chat thread renamed.");
        }
        catch (Exception exception)
        {
            SetError($"Manager chat rename failed. {exception.Message}");
        }
        finally
        {
            managerChatIsBusy = false;
        }
    }

    private async Task StageManagerChatRunArtifactsAsync()
    {
        if (!managerChatSelectedRunId.HasValue)
        {
            SetError("Select a process run before staging manager chat artifacts.");
            return;
        }

        var details = await RunDetailsLoader.LoadAsync(managerChatSelectedRunId.Value);
        var artifactPaths = details.Artifacts
            .Select(artifact => artifact.ManagedStoragePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (artifactPaths.Count == 0)
        {
            SetError("The selected process run does not have managed artifacts to stage.");
            return;
        }

        managerChatDraftAttachmentPaths = artifactPaths;
        SetMessage($"Staged {artifactPaths.Count} process artifact path(s) for manager chat.");
    }

    private string BuildManagerChatPromptWithAttachments()
    {
        if (managerChatDraftAttachmentPaths.Count == 0)
        {
            return managerChatDraftPrompt.Trim();
        }

        var attachmentText = string.Join(
            Environment.NewLine,
            managerChatDraftAttachmentPaths.Select(path => $"- {path}"));
        return $"""
Use these process artifacts as input:
{attachmentText}

User request:
{managerChatDraftPrompt.Trim()}
""";
    }

    private string BuildManagerChatPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return string.Empty;
        }

        var selectedRun = ManagerChatSelectedRun;
        var projectContext = ProjectId.HasValue
            ? $"- Project id: {ProjectId.Value:D}.{Environment.NewLine}- Project name: {projectName}"
            : "- Project: Global process library";
        var runContext = selectedRun is null
            ? "- Selected process run: none. Discuss the process definition unless the user asks to choose a run."
            : $"""
- Selected process run id: {selectedRun.Id:D}.
- Selected process run name: {selectedRun.Name}.
- Selected process run status: {selectedRun.Status}.
- Selected process run progress: {selectedRun.CompletedStepCount}/{selectedRun.TotalStepCount} steps completed, {selectedRun.BlockedStepCount} blocked, {selectedRun.CapabilityGapCount} capability gaps.
- Selected process run manager: {(string.IsNullOrWhiteSpace(selectedRun.ManagerAgentName) ? "Default process manager" : selectedRun.ManagerAgentName)}.
""";

        return $"""
Context:
- Workspace: process manager chat.
- Selected process definition id: {selectedProcessId!.Value:D}.
- Selected process definition name: {EditorTitle}.
{projectContext}
{runContext}
- Manager resolution: {managerChatAgentResolution.Summary} Reason={managerChatAgentResolution.ReasonCode}; confidence={managerChatAgentResolution.Confidence}.
- Treat "this process" as the selected process definition above.
- Treat "this run" as the selected process run only when a run id is listed.
- Report like a human delivery manager: current status, main blockers, concrete unblock actions, and whether action is needed from user or agents.
- If asked to unblock work, prefer process runtime tools, manager directives, rework requests, or agent instructions. Do not rewrite dispatcher behavior unless the issue is generic across processes.

User request:
{prompt}
""";
    }

    private ExecutionInvocationContext BuildManagerChatInvocationContext()
    {
        var metadata = new Dictionary<string, string>
        {
            ["processDefinitionId"] = selectedProcessId?.ToString("D") ?? string.Empty,
            ["processDefinitionName"] = EditorTitle,
            ["selectedProcessRunId"] = managerChatSelectedRunId?.ToString("D") ?? string.Empty,
            ["selectedProcessRunName"] = ManagerChatSelectedRun?.Name ?? string.Empty,
            ["managerDisplayName"] = ManagerChatManagerLabel,
            ["managerResolutionReasonCode"] = managerChatAgentResolution.ReasonCode.ToString(),
            ["managerResolutionConfidence"] = managerChatAgentResolution.Confidence.ToString(),
            ["managerResolutionSummary"] = managerChatAgentResolution.Summary
        };

        if (ProjectId.HasValue)
        {
            metadata["projectId"] = ProjectId.Value.ToString("D");
            metadata["projectName"] = projectName;
        }

        return new ExecutionInvocationContext(
            SourceKind: ManagerChatSourceKind,
            SourceId: selectedProcessId?.ToString("D") ?? string.Empty,
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: managerChatSessionId?.ToString("N") ?? string.Empty,
            RequestedBy: ManagerChatRequester,
            RequestedByKind: "interactive",
            MetadataJson: JsonSerializer.Serialize(metadata, JsonOptions),
            ProcessRunId: managerChatSelectedRunId?.ToString("D") ?? string.Empty);
    }

    private Task OpenManagerChatRunSelectorAsync()
    {
        managerChatRunSelectorOpen = true;
        return Task.CompletedTask;
    }

    private Task CloseManagerChatRunSelectorAsync()
    {
        managerChatRunSelectorOpen = false;
        return Task.CompletedTask;
    }

    private async Task SelectManagerChatRunAsync(Guid? runId)
    {
        managerChatSelectedRunId = runId;
        managerChatRunSelectorOpen = false;
        await LoadManagerChatAsync();
    }

    private Task OpenManagerChatRuntimeDetailsAsync()
    {
        if (!CanOpenManagerChatRuntimeDetails)
        {
            SetError("Send a manager chat prompt before opening runtime details.");
            return Task.CompletedTask;
        }

        _ = DialogService.OpenAsync<AgentRuntimeDetailsDialog>(
            "Manager chat runtime details",
            new Dictionary<string, object?>
            {
                [nameof(AgentRuntimeDetailsDialog.Run)] = managerChatWorkspace?.SelectedRun,
                [nameof(AgentRuntimeDetailsDialog.ExecutionLog)] = managerChatExecutionLog,
                [nameof(AgentRuntimeDetailsDialog.Metrics)] = managerChatMetrics,
                [nameof(AgentRuntimeDetailsDialog.RunStateText)] = managerChatRunStateText,
                [nameof(AgentRuntimeDetailsDialog.RunStateTone)] = managerChatRunStateTone
            },
            new DialogOptions
            {
                Eyebrow = "Process manager",
                Subtitle = ManagerChatSelectedRunLabel,
                Size = ModalSize.Full,
                DenseChrome = true,
                TestId = "processes-manager-chat-runtime-details-dialog",
                AriaLabel = "Manager chat runtime details",
                Style = "max-height:calc(100vh - 2rem);"
            });

        return Task.CompletedTask;
    }

    private void ResolveManagerChatRunState()
    {
        if (managerChatWorkspace?.SelectedRun is null)
        {
            managerChatRunStateText = string.Empty;
            managerChatRunStateTone = ManagerChatDefaultRunStateTone;
            return;
        }

        managerChatRunStateText = managerChatWorkspace.SelectedRun.State.ToString();
        managerChatRunStateTone = ResolveManagerChatExecutionTone(managerChatWorkspace.SelectedRun.State);
    }

    private void HandleManagerChatExecutionUpdated(object? sender, ExecutionLogEntry entry)
    {
        if (!ShouldAcceptManagerChatExecutionEntry(entry))
        {
            return;
        }

        _ = InvokeAsync(() =>
        {
            if (!ShouldAcceptManagerChatExecutionEntry(entry))
            {
                return;
            }

            managerChatExecutionLog = UpsertManagerChatExecutionLogEntry(managerChatExecutionLog, entry);
            if (managerChatWorkspace?.SelectedRun?.Id == entry.ExecutionRunId)
            {
                managerChatRunStateText = entry.State.ToString();
                managerChatRunStateTone = ResolveManagerChatExecutionTone(entry.State);
            }

            StateHasChanged();
        });
    }

    private bool ShouldAcceptManagerChatExecutionEntry(ExecutionLogEntry entry)
    {
        if (managerChatAgentId != entry.AgentId)
        {
            return false;
        }

        return !managerChatSessionId.HasValue ||
               entry.ChatSessionId == managerChatSessionId.Value;
    }

    private static IReadOnlyList<ExecutionLogEntry> UpsertManagerChatExecutionLogEntry(
        IReadOnlyList<ExecutionLogEntry> entries,
        ExecutionLogEntry entry)
    {
        return entries
            .Where(item => item.Id != entry.Id)
            .Append(entry)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    private static string ResolveManagerChatExecutionTone(ExecutionState state)
    {
        return state switch
        {
            ExecutionState.Completed => "success",
            ExecutionState.WaitingOnTool => "warning",
            ExecutionState.Failed => "danger",
            ExecutionState.Running or ExecutionState.Preparing or ExecutionState.Persisting => "info",
            _ => "neutral"
        };
    }

    private void ResetManagerChatState(bool clearRunSelection)
    {
        ResetManagerChatAgentState();
        managerChatProcessId = null;
        managerChatDraftPrompt = string.Empty;
        managerChatPendingPrompt = string.Empty;
        managerChatDraftAttachmentPaths = [];
        managerChatRunSelectorOpen = false;
        if (clearRunSelection)
        {
            managerChatSelectedRunId = null;
        }
    }

    private void ResetManagerChatAgentState()
    {
        managerChatAgent = null;
        managerChatWorkspace = null;
        managerChatExecutionLog = [];
        managerChatMetrics = [];
        managerChatAgentId = null;
        managerChatSessionId = null;
        managerChatAgentResolution = ProcessManagerAgentResolution.NotEvaluated("Manager chat has not resolved a manager agent yet.");
        managerChatRunStateText = string.Empty;
        managerChatRunStateTone = ManagerChatDefaultRunStateTone;
    }
}
