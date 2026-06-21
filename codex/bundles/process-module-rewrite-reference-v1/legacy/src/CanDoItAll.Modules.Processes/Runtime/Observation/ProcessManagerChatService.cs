using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessManagerChatService(
    ProcessesService processesService,
    IAgentFrameworkWorkspaceService workspaceService,
    ProcessWorkspaceRunDetailsLoader runDetailsLoader,
    ILogger<ProcessManagerChatService> logger) : IProcessManagerChatService
{
    private const int RecentThreadLimit = 10;
    private const string ManagerChatSourceKind = "process-manager-chat";
    private const string ManagerChatRequester = "live-processes-manager-chat";
    private const string DefaultManagerLabel = "Default process manager";
    private const string DefaultRunStateTone = "neutral";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProcessManagerChatProjection> LoadAsync(
        ProcessManagerChatProjectionQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);

        var resolution = await ResolveManagerAsync(query, cancellationToken);
        if (resolution.ErrorMessage.Length > 0 || resolution.Agent is null)
        {
            return CreateUnavailableProjection(query, resolution.ManagerLabel, resolution.ErrorMessage, resolution.AgentResolution);
        }

        try
        {
            var workspace = await workspaceService.GetChatAgentWorkspaceAsync(
                resolution.Agent.Id,
                query.ChatSessionId,
                cancellationToken);
            var runtimeSnapshot = workspace.SelectedSessionId.HasValue
                ? await workspaceService.GetChatRuntimeSnapshotAsync(
                    resolution.Agent.Id,
                    workspace.SelectedSessionId,
                    cancellationToken)
                : new ChatRuntimeSnapshot([], []);
            var runState = ResolveManagerChatRunState(workspace.SelectedRun?.State);

            return new ProcessManagerChatProjection(
                query with
                {
                    ChatSessionId = workspace.SelectedSessionId
                },
                resolution.Agent,
                workspace,
                workspace.Sessions.Take(RecentThreadLimit).ToArray(),
                runtimeSnapshot.ExecutionLog,
                runtimeSnapshot.Metrics,
                resolution.ManagerLabel,
                BuildRunLabel(query),
                runState.Text,
                runState.Tone,
                resolution.AgentResolution.ReasonCode.ToString(),
                resolution.AgentResolution.Confidence,
                resolution.AgentResolution.Summary,
                ErrorMessage: string.Empty);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to load live process manager chat. ProcessRunId={ProcessRunId} ManagerAgentId={ManagerAgentId}.",
                query.ProcessRunId,
                resolution.Agent.Id);
            return CreateUnavailableProjection(query, resolution.ManagerLabel, exception.Message, resolution.AgentResolution);
        }
    }

    public async Task<ProcessManagerChatProjection> StartNewThreadAsync(
        ProcessManagerChatProjectionQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);

        var resolution = await ResolveManagerAsync(query, cancellationToken);
        if (resolution.ErrorMessage.Length > 0 || resolution.Agent is null)
        {
            return CreateUnavailableProjection(query, resolution.ManagerLabel, resolution.ErrorMessage, resolution.AgentResolution);
        }

        var session = await workspaceService.GetOrCreateChatSessionAsync(
            resolution.Agent.Id,
            chatSessionId: null,
            cancellationToken);
        return await LoadAsync(query with
        {
            ChatSessionId = session.Id
        }, cancellationToken);
    }

    public async Task<ProcessManagerChatProjection> SendMessageAsync(
        ProcessManagerChatMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateQuery(request.Query);

        var prompt = request.Prompt.Trim();
        if (prompt.Length == 0)
        {
            return CreateUnavailableProjection(
                request.Query,
                ResolveManagerLabel(request.Query),
                "Write a manager chat prompt before sending it.");
        }

        var resolution = await ResolveManagerAsync(request.Query, cancellationToken);
        if (resolution.ErrorMessage.Length > 0 || resolution.Agent is null)
        {
            return CreateUnavailableProjection(request.Query, resolution.ManagerLabel, resolution.ErrorMessage, resolution.AgentResolution);
        }

        var effectiveQuery = request.Query with
        {
            ChatSessionId = request.ChatSessionId ?? request.Query.ChatSessionId
        };
        var usageSummary = await LoadRunUsageSummaryAsync(effectiveQuery, cancellationToken);
        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                AgentId: resolution.Agent.Id,
                Prompt: BuildManagerChatPrompt(effectiveQuery, prompt, resolution.AgentResolution, usageSummary),
                ChatSessionId: effectiveQuery.ChatSessionId,
                Context: BuildManagerChatInvocationContext(effectiveQuery, resolution.AgentResolution),
                AutoApprovePendingToolCalls: false),
            cancellationToken);

        return await LoadAsync(effectiveQuery with
        {
            ChatSessionId = result.ChatSessionId ?? effectiveQuery.ChatSessionId
        }, cancellationToken);
    }

    public async Task<ProcessManagerChatProjection> RenameThreadAsync(
        ProcessManagerChatRenameRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateQuery(request.Query);

        var resolution = await ResolveManagerAsync(request.Query, cancellationToken);
        if (resolution.ErrorMessage.Length > 0 || resolution.Agent is null)
        {
            return CreateUnavailableProjection(request.Query, resolution.ManagerLabel, resolution.ErrorMessage, resolution.AgentResolution);
        }

        var session = await workspaceService.RenameChatSessionAsync(
            resolution.Agent.Id,
            request.ChatSessionId,
            request.Title,
            cancellationToken);
        return await LoadAsync(request.Query with
        {
            ChatSessionId = session.Id
        }, cancellationToken);
    }

    public async Task<ProcessManagerChatProjection> RespondToApprovalsAsync(
        ProcessManagerChatApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateQuery(request.Query);

        var resolution = await ResolveManagerAsync(request.Query, cancellationToken);
        if (resolution.ErrorMessage.Length > 0 || resolution.Agent is null)
        {
            return CreateUnavailableProjection(request.Query, resolution.ManagerLabel, resolution.ErrorMessage, resolution.AgentResolution);
        }

        await workspaceService.RespondToPendingApprovalsAsync(
            resolution.Agent.Id,
            request.ChatSessionId,
            request.Approved,
            request.AutoApprovePendingToolCalls,
            cancellationToken);
        return await LoadAsync(request.Query with
        {
            ChatSessionId = request.ChatSessionId
        }, cancellationToken);
    }

    private async Task<ManagerResolution> ResolveManagerAsync(
        ProcessManagerChatProjectionQuery query,
        CancellationToken cancellationToken)
    {
        var managerLabel = ResolveManagerLabel(query);
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var managerOptions = await processesService.ListManagerAgentOptionsAsync(cancellationToken);

        var agentResolution = ProcessManagerAgentResolver.ResolveConfiguredManager(
            query.ManagerAgentId,
            query.ManagerAgentName,
            managerOptions,
            agents);
        if (!agentResolution.IsResolved && !agentResolution.IsAmbiguous)
        {
            var assignedResolution = await ResolveAssignedManagerResolutionAsync(
                query.ProcessRunId,
                managerOptions,
                agents,
                cancellationToken);
            agentResolution = assignedResolution.IsResolved || assignedResolution.IsAmbiguous
                ? assignedResolution
                : ProcessManagerAgentResolver.ResolveFallbackManager(managerOptions, agents);
        }

        if (!agentResolution.IsResolved)
        {
            return new ManagerResolution(
                null,
                managerLabel,
                BuildManagerResolutionError(agentResolution),
                agentResolution);
        }

        var agent = agents.FirstOrDefault(item => item.Id == agentResolution.ResolvedTechnicalAgentId!.Value);
        if (agent is null)
        {
            return new ManagerResolution(
                null,
                managerLabel,
                "The resolved manager AI resource is not available in the Agent Framework catalog.",
                agentResolution);
        }

        return new ManagerResolution(agent, agent.Name, ErrorMessage: string.Empty, agentResolution);
    }

    private async Task<ProcessManagerAgentResolution> ResolveAssignedManagerResolutionAsync(
        Guid processRunId,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents,
        CancellationToken cancellationToken)
    {
        var details = await processesService.GetRunDetailsAsync(processRunId, cancellationToken);
        return ProcessManagerAgentResolver.ResolveAssignedManager(details.Assignments, managerOptions, agents);
    }

    private static string BuildManagerResolutionError(ProcessManagerAgentResolution resolution)
        => resolution.IsAmbiguous
            ? resolution.Summary
            : $"No bound technical manager agent could be resolved for this process run. {resolution.Summary}";

    private static string ResolveManagerLabel(ProcessManagerChatProjectionQuery query)
    {
        return string.IsNullOrWhiteSpace(query.ManagerAgentName)
            ? DefaultManagerLabel
            : query.ManagerAgentName.Trim();
    }

    private static string BuildRunLabel(ProcessManagerChatProjectionQuery query)
    {
        return $"{query.ProcessRunName} / {query.RunStatus}";
    }

    private static string BuildManagerChatPrompt(
        ProcessManagerChatProjectionQuery query,
        string prompt,
        ProcessManagerAgentResolution resolution,
        ProcessRunUsageSummaryViewModel usageSummary)
    {
        return $"""
Context:
- Workspace: live process manager chat.
- Process definition id: {query.ProcessDefinitionId:D}.
- Process definition name: {query.ProcessDefinitionName}.
- Process run id: {query.ProcessRunId:D}.
- Process run name: {query.ProcessRunName}.
- Process run status: {query.RunStatus}.
- Process run progress: {query.CompletedStepCount}/{query.TotalStepCount} steps completed, {query.BlockedStepCount} blocked, {query.CapabilityGapCount} capability gaps.
- Active execution count: {query.ActiveExecutionCount}.
- Pending approval count: {query.PendingApprovalCount}.
- Own run estimated cost: {usageSummary.OwnEstimatedCost}.
- Own run actual cost: {usageSummary.OwnActualCost}.
- Process tree estimated cost: {usageSummary.TreeEstimatedCost}.
- Process tree persisted actual cost: {usageSummary.TreeActualCost}.
- Process tree known provider usage cost: {usageSummary.KnownProviderUsageCostUsd}.
- Process tree reconciled actual cost: {usageSummary.ReconciledActualCostUsd}.
- Process tree run count: {usageSummary.ProcessRunCount}; descendant subprocess runs: {usageSummary.DescendantRunCount}.
- Process tree provider usage observations: {usageSummary.KnownProviderUsageObservationCount} known, {usageSummary.UnknownProviderUsageObservationCount} incomplete, {usageSummary.ProviderUsageObservationCount} total.
- Process tree tokens: input={usageSummary.InputTokens}; cachedInput={usageSummary.CachedInputTokens}; output={usageSummary.OutputTokens}; reasoning={usageSummary.ReasoningTokens}; total={usageSummary.TotalTokens}; toolCalls={usageSummary.ToolCallCount}.
- Last run activity UTC: {query.UpdatedAtUtc:O}.
- Selected process run manager: {ResolveManagerLabel(query)}.
- Manager resolution: {resolution.Summary} Reason={resolution.ReasonCode}; confidence={resolution.Confidence}.
- Treat "this process" as the process definition above.
- Treat "this run" as the selected process run above.
- Report like a human delivery manager: current status, main blockers, concrete unblock actions, and whether action is needed from user or agents.
- If asked to unblock work, prefer process runtime tools, manager directives, rework requests, or agent instructions. Do not rewrite dispatcher behavior unless the issue is generic across processes.

User request:
{prompt.Trim()}
""";
    }

    private async Task<ProcessRunUsageSummaryViewModel> LoadRunUsageSummaryAsync(
        ProcessManagerChatProjectionQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await runDetailsLoader.LoadAsync(query.ProcessRunId, cancellationToken)).UsageSummary;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to load process manager chat usage summary. ProcessRunId={ProcessRunId}.",
                query.ProcessRunId);
            return new ProcessRunUsageSummaryViewModel(
                ProcessRunCount: 1 + Math.Max(0, query.DescendantRunCount),
                DescendantRunCount: Math.Max(0, query.DescendantRunCount),
                ExecutionRunCount: 0,
                ProviderUsageObservationCount: 0,
                KnownProviderUsageObservationCount: 0,
                UnknownProviderUsageObservationCount: 0,
                InputTokens: 0,
                CachedInputTokens: 0,
                OutputTokens: 0,
                ReasoningTokens: 0,
                TotalTokens: 0,
                ToolCallCount: 0,
                KnownProviderUsageCostUsd: 0m,
                OwnEstimatedCost: query.EstimatedCost,
                OwnActualCost: query.ActualCost,
                TreeEstimatedCost: query.TreeEstimatedCost,
                TreeActualCost: query.TreeActualCost);
        }
    }

    private static ExecutionInvocationContext BuildManagerChatInvocationContext(
        ProcessManagerChatProjectionQuery query,
        ProcessManagerAgentResolution resolution)
    {
        var metadata = new Dictionary<string, string>
        {
            ["processDefinitionId"] = query.ProcessDefinitionId.ToString("D"),
            ["processDefinitionName"] = query.ProcessDefinitionName,
            ["selectedProcessRunId"] = query.ProcessRunId.ToString("D"),
            ["selectedProcessRunName"] = query.ProcessRunName,
            ["managerDisplayName"] = ResolveManagerLabel(query),
            ["managerResolutionReasonCode"] = resolution.ReasonCode.ToString(),
            ["managerResolutionConfidence"] = resolution.Confidence.ToString(),
            ["managerResolutionSummary"] = resolution.Summary
        };

        if (query.ProjectId.HasValue)
        {
            metadata["projectId"] = query.ProjectId.Value.ToString("D");
        }

        return new ExecutionInvocationContext(
            SourceKind: ManagerChatSourceKind,
            SourceId: query.ProcessDefinitionId.ToString("D"),
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: query.ChatSessionId?.ToString("N") ?? string.Empty,
            RequestedBy: ManagerChatRequester,
            RequestedByKind: "interactive",
            MetadataJson: JsonSerializer.Serialize(metadata, JsonOptions),
            ProcessRunId: query.ProcessRunId.ToString("D"));
    }

    private static (string Text, string Tone) ResolveManagerChatRunState(ExecutionState? state)
    {
        if (!state.HasValue)
        {
            return (string.Empty, DefaultRunStateTone);
        }

        return (
            state.Value.ToString(),
            state.Value switch
            {
                ExecutionState.Completed => "success",
                ExecutionState.WaitingOnTool => "warning",
                ExecutionState.Failed => "danger",
                ExecutionState.Running or ExecutionState.Preparing or ExecutionState.Persisting => "info",
                _ => "neutral"
            });
    }

    private static ProcessManagerChatProjection CreateUnavailableProjection(
        ProcessManagerChatProjectionQuery query,
        string managerLabel,
        string errorMessage,
        ProcessManagerAgentResolution? resolution = null)
    {
        var effectiveResolution = resolution ??
                                  ProcessManagerAgentResolution.NotEvaluated("Manager resolution was not evaluated for this projection.");
        return new ProcessManagerChatProjection(
            query,
            ManagerAgent: null,
            Workspace: null,
            RecentSessions: [],
            ExecutionLog: [],
            Metrics: [],
            managerLabel,
            BuildRunLabel(query),
            RunStateText: string.Empty,
            RunStateTone: DefaultRunStateTone,
            effectiveResolution.ReasonCode.ToString(),
            effectiveResolution.Confidence,
            effectiveResolution.Summary,
            errorMessage);
    }

    private static void ValidateQuery(ProcessManagerChatProjectionQuery query)
    {
        if (query.ProcessRunId == Guid.Empty)
        {
            throw new ArgumentException("Process run id is required.", nameof(query));
        }

        if (query.ProcessDefinitionId == Guid.Empty)
        {
            throw new ArgumentException("Process definition id is required.", nameof(query));
        }
    }

    private sealed record ManagerResolution(
        AgentDefinition? Agent,
        string ManagerLabel,
        string ErrorMessage,
        ProcessManagerAgentResolution AgentResolution);
}
