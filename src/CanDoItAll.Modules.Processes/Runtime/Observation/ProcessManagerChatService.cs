using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessManagerChatService(
    ProcessesService processesService,
    IAgentFrameworkWorkspaceService workspaceService,
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
            return CreateUnavailableProjection(query, resolution.ManagerLabel, resolution.ErrorMessage);
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
                ErrorMessage: string.Empty);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to load live process manager chat. ProcessRunId={ProcessRunId} ManagerAgentId={ManagerAgentId}.",
                query.ProcessRunId,
                resolution.Agent.Id);
            return CreateUnavailableProjection(query, resolution.ManagerLabel, exception.Message);
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
            return CreateUnavailableProjection(query, resolution.ManagerLabel, resolution.ErrorMessage);
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
            return CreateUnavailableProjection(request.Query, resolution.ManagerLabel, resolution.ErrorMessage);
        }

        var effectiveQuery = request.Query with
        {
            ChatSessionId = request.ChatSessionId ?? request.Query.ChatSessionId
        };
        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                AgentId: resolution.Agent.Id,
                Prompt: BuildManagerChatPrompt(effectiveQuery, prompt),
                ChatSessionId: effectiveQuery.ChatSessionId,
                Context: BuildManagerChatInvocationContext(effectiveQuery),
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
            return CreateUnavailableProjection(request.Query, resolution.ManagerLabel, resolution.ErrorMessage);
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
            return CreateUnavailableProjection(request.Query, resolution.ManagerLabel, resolution.ErrorMessage);
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
        var technicalAgentId = ResolveConfiguredManagerTechnicalAgentId(query, managerOptions, agents)
            ?? await ResolveAssignedManagerTechnicalAgentIdAsync(query.ProcessRunId, managerOptions, agents, cancellationToken)
            ?? ResolveFallbackManagerTechnicalAgentId(managerOptions, agents);
        if (!technicalAgentId.HasValue)
        {
            return new ManagerResolution(
                null,
                managerLabel,
                "No bound technical manager agent could be resolved for this process run.");
        }

        var agent = agents.FirstOrDefault(item => item.Id == technicalAgentId.Value);
        if (agent is null)
        {
            return new ManagerResolution(
                null,
                managerLabel,
                "The resolved manager AI resource is not available in the Agent Framework catalog.");
        }

        return new ManagerResolution(agent, agent.Name, ErrorMessage: string.Empty);
    }

    private static Guid? ResolveConfiguredManagerTechnicalAgentId(
        ProcessManagerChatProjectionQuery query,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
    {
        var runManagerOption = ResolveManagerOptionByIdentifier(query.ManagerAgentId, managerOptions);
        if (runManagerOption?.TechnicalAgentId is Guid runManagerTechnicalAgentId)
        {
            return runManagerTechnicalAgentId;
        }

        if (query.ManagerAgentId.HasValue && agents.Any(item => item.Id == query.ManagerAgentId.Value))
        {
            return query.ManagerAgentId.Value;
        }

        var namedRunManagerOption = ResolveManagerOptionByName(query.ManagerAgentName, managerOptions);
        if (namedRunManagerOption?.TechnicalAgentId is Guid namedRunManagerTechnicalAgentId)
        {
            return namedRunManagerTechnicalAgentId;
        }

        return null;
    }

    private async Task<Guid?> ResolveAssignedManagerTechnicalAgentIdAsync(
        Guid processRunId,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents,
        CancellationToken cancellationToken)
    {
        var details = await processesService.GetRunDetailsAsync(processRunId, cancellationToken);
        return details.Assignments
            .Where(item => !item.IsCapabilityGap)
            .Select(item => new
            {
                Assignment = item,
                Option = ResolveManagerOptionByIdentifier(item.PartyId, managerOptions),
                Agent = item.PartyId.HasValue
                    ? agents.FirstOrDefault(agent => agent.Id == item.PartyId.Value)
                    : null,
                Score = ResolveManagerAssignmentScore(item)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .Select(item => item.Option?.TechnicalAgentId ?? item.Agent?.Id)
            .FirstOrDefault(item => item.HasValue);
    }

    private static Guid? ResolveFallbackManagerTechnicalAgentId(
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
    {
        var fallbackManagerOptions = managerOptions
            .Where(option => option.TechnicalAgentId.HasValue)
            .Where(IsManagerLikeOption)
            .ToList();
        if (fallbackManagerOptions.Count == 1)
        {
            return fallbackManagerOptions[0].TechnicalAgentId;
        }

        var fallbackAgents = agents
            .Select(agent => new
            {
                Agent = agent,
                Score = ResolveAgentManagerScore(agent)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Agent.UpdatedAtUtc)
            .ToList();
        if (fallbackAgents.Count == 0)
        {
            return null;
        }

        var topScore = fallbackAgents[0].Score;
        return fallbackAgents.Count(item => item.Score == topScore) == 1
            ? fallbackAgents[0].Agent.Id
            : null;
    }

    private static ProcessManagerAgentOption? ResolveManagerOptionByIdentifier(
        Guid? managerId,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions)
    {
        if (!managerId.HasValue)
        {
            return null;
        }

        return managerOptions.FirstOrDefault(option =>
            option.PartyId == managerId.Value ||
            option.TechnicalAgentId == managerId.Value);
    }

    private static ProcessManagerAgentOption? ResolveManagerOptionByName(
        string managerName,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions)
    {
        if (string.IsNullOrWhiteSpace(managerName))
        {
            return null;
        }

        var normalizedName = managerName.Trim();
        return managerOptions.FirstOrDefault(option =>
            string.Equals(option.DisplayName, normalizedName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsManagerLikeOption(ProcessManagerAgentOption option)
    {
        return ContainsManagerToken(option.DisplayName) ||
               ContainsManagerToken(option.BindingSummary);
    }

    private static int ResolveManagerAssignmentScore(ProcessRunAssignmentViewModel assignment)
    {
        var score = 0;
        score = Math.Max(score, ResolveManagerTextScore(assignment.RoleDisplayName));
        score = Math.Max(score, ResolveManagerTextScore(assignment.DisplayName));
        score = Math.Max(score, ResolveManagerTextScore(assignment.BindingReason));
        return assignment.AllowsDirectMessaging
            ? score + 5
            : score;
    }

    private static int ResolveAgentManagerScore(AgentDefinition agent)
    {
        var score = 0;
        score = Math.Max(score, ResolveManagerTextScore(agent.Name));
        score = Math.Max(score, ResolveManagerTextScore(agent.RoleTitle));
        score = Math.Max(score, agent.Tags.Any(tag => ContainsManagerToken(tag)) ? 20 : 0);
        return score;
    }

    private static int ResolveManagerTextScore(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (value.Contains("process manager", StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (value.Contains("delivery manager", StringComparison.OrdinalIgnoreCase))
        {
            return 90;
        }

        if (value.Contains("manager", StringComparison.OrdinalIgnoreCase))
        {
            return 70;
        }

        if (value.Contains("orchestrator", StringComparison.OrdinalIgnoreCase))
        {
            return 60;
        }

        return value.Contains("lead", StringComparison.OrdinalIgnoreCase)
            ? 50
            : 0;
    }

    private static bool ContainsManagerToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var tokens = value.Split(
            [' ', '-', '_', '/', '\\', '.', ':', ';', ',', '(', ')', '[', ']'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Any(token =>
            string.Equals(token, "manager", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "lead", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "orchestrator", StringComparison.OrdinalIgnoreCase));
    }

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
        string prompt)
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
- Estimated cost: {query.EstimatedCost}.
- Actual cost: {query.ActualCost}.
- Last run activity UTC: {query.UpdatedAtUtc:O}.
- Selected process run manager: {ResolveManagerLabel(query)}.
- Treat "this process" as the process definition above.
- Treat "this run" as the selected process run above.
- Report like a human delivery manager: current status, main blockers, concrete unblock actions, and whether action is needed from user or agents.
- If asked to unblock work, prefer process runtime tools, manager directives, rework requests, or agent instructions. Do not rewrite dispatcher behavior unless the issue is generic across processes.

User request:
{prompt.Trim()}
""";
    }

    private static ExecutionInvocationContext BuildManagerChatInvocationContext(
        ProcessManagerChatProjectionQuery query)
    {
        var metadata = new Dictionary<string, string>
        {
            ["processDefinitionId"] = query.ProcessDefinitionId.ToString("D"),
            ["processDefinitionName"] = query.ProcessDefinitionName,
            ["selectedProcessRunId"] = query.ProcessRunId.ToString("D"),
            ["selectedProcessRunName"] = query.ProcessRunName,
            ["managerDisplayName"] = ResolveManagerLabel(query)
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
        string errorMessage)
    {
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
        string ErrorMessage);
}
