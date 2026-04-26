using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime(
    string workspaceRoot,
    IServiceProvider services,
    WorkspaceScopeDescriptor? workspaceScope = null) : IAgentRuntime
{
    private const string LocalHistoryConversationId = "_agent_local_chat_history";
    private const int MaxRepeatedToolInvocationCount = 3;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };
    private static readonly ProviderProfileService ProviderFeatureService = new();

    private readonly string workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly IServiceProvider services = services;
    private readonly WorkspaceScopeDescriptor workspaceScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<ToolApprovalRequestContent>> pendingApprovalCache = new();

    public async Task<AgentRuntimeResponse> RunAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        string prompt,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false)
    {
        var model = ResolveRuntimeModel(agent, provider);
        try
        {
            return await RunCoreAsync(
                agent,
                provider,
                session,
                capabilities,
                memory,
                prompt,
                runtimeSessionKey,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                forceOmitTemperature: false);
        }
        catch (Exception exception) when (ShouldRetryWithoutTemperature(provider, model, exception))
        {
            await progressCallback(ExecutionState.Preparing, "Model parameters", BuildTemperatureRetryMessage(model));
            return await RunCoreAsync(
                agent,
                provider,
                session,
                capabilities,
                memory,
                prompt,
                runtimeSessionKey,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                forceOmitTemperature: true);
        }
    }

    private async Task<AgentRuntimeResponse> RunCoreAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        string prompt,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements,
        bool forceOmitTemperature)
    {
        await progressCallback(ExecutionState.Preparing, "Framework", "Composing the Microsoft Agent Framework runtime for the selected provider and capabilities.");
        if (suppressApprovalRequirements)
        {
            await progressCallback(ExecutionState.Preparing, "Approval policy", "Auto-approve is active for this run, so future tool approval gates will be suppressed.");
        }

        await using var runtimeBuild = await CreateRuntimeBuildAsync(
            agent,
            provider,
            capabilities,
            memory,
            progressCallback,
            cancellationToken,
            suppressApprovalRequirements,
            forceOmitTemperature);

        if (runtimeBuild.IsTemperatureOmitted)
        {
            await progressCallback(ExecutionState.Preparing, "Model parameters", BuildTemperatureOmittedMessage(runtimeBuild.Model));
        }

        await progressCallback(ExecutionState.Preparing, "Session", ResolveSessionMessage(agent, runtimeBuild.Provider, session));
        var runtimeSession = await RestoreOrCreateSessionAsync(
            runtimeBuild.Agent,
            agent,
            runtimeBuild.Provider,
            session,
            cancellationToken,
            isApprovalContinuation: false);
        var runOptions = CreateRunOptions(
            agent,
            runtimeBuild.Provider,
            runtimeBuild.Model,
            runtimeBuild.HasApprovalTools,
            continuationToken: null,
            forceOmitTemperature: forceOmitTemperature);
        var inputMessages = CreatePromptInputMessages(agent, runtimeBuild.Provider, session, prompt);

        return await ExecuteRunAsync(
            agent,
            runtimeBuild.Provider,
            runtimeBuild.Model,
            session,
            runtimeBuild.Agent,
            runtimeSession,
            runOptions,
            inputMessages,
            runtimeSessionKey,
            progressCallback,
            cancellationToken,
            forceOmitTemperature);
    }

    public async Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        bool approved,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false)
    {
        var model = ResolveRuntimeModel(agent, provider);
        try
        {
            return await RespondToPendingApprovalsCoreAsync(
                agent,
                provider,
                session,
                capabilities,
                memory,
                approved,
                runtimeSessionKey,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                forceOmitTemperature: false);
        }
        catch (Exception exception) when (ShouldRetryWithoutTemperature(provider, model, exception))
        {
            await progressCallback(ExecutionState.Preparing, "Model parameters", BuildTemperatureRetryMessage(model));
            return await RespondToPendingApprovalsCoreAsync(
                agent,
                provider,
                session,
                capabilities,
                memory,
                approved,
                runtimeSessionKey,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                forceOmitTemperature: true);
        }
    }

    private async Task<AgentRuntimeResponse> RespondToPendingApprovalsCoreAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        bool approved,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements,
        bool forceOmitTemperature)
    {
        await progressCallback(ExecutionState.Preparing, "Framework", "Rehydrating the Microsoft Agent Framework runtime to continue from a pending approval.");
        if (suppressApprovalRequirements)
        {
            await progressCallback(ExecutionState.Preparing, "Approval policy", "Auto-approve remains active, so future tool approval gates will be suppressed after this decision is replayed.");
        }

        await using var runtimeBuild = await CreateRuntimeBuildAsync(
            agent,
            provider,
            capabilities,
            memory,
            progressCallback,
            cancellationToken,
            suppressApprovalRequirements,
            forceOmitTemperature);

        if (runtimeBuild.IsTemperatureOmitted)
        {
            await progressCallback(ExecutionState.Preparing, "Model parameters", BuildTemperatureOmittedMessage(runtimeBuild.Model));
        }

        await progressCallback(ExecutionState.Preparing, "Session", "Restoring the session state prior to replaying the approval response.");
        var runtimeSession = await RestoreOrCreateSessionAsync(
            runtimeBuild.Agent,
            agent,
            runtimeBuild.Provider,
            session,
            cancellationToken,
            isApprovalContinuation: true);
        var runOptions = CreateRunOptions(
            agent,
            runtimeBuild.Provider,
            runtimeBuild.Model,
            runtimeBuild.HasApprovalTools,
            continuationToken: null,
            forceOmitTemperature: forceOmitTemperature);
        var inputMessages = CreateApprovalInputMessages(session, approved);

        return await ExecuteRunAsync(
            agent,
            runtimeBuild.Provider,
            runtimeBuild.Model,
            session,
            runtimeBuild.Agent,
            runtimeSession,
            runOptions,
            inputMessages,
            runtimeSessionKey,
            progressCallback,
            cancellationToken,
            forceOmitTemperature);
    }

    private async Task<AgentRuntimeResponse> ExecuteRunAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        ChatSessionRecord session,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        ChatClientAgentRunOptions runOptions,
        IEnumerable<ChatMessage> inputMessages,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool forceOmitTemperature)
    {
        var updates = new List<AgentResponseUpdate>();
        var announcedStreaming = false;
        var announcedToolCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var guardedToolCallIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var repeatedToolInvocationGuard = new RepeatedToolInvocationGuard();
        var pollCount = 0;
        var resolvedModel = model;

        while (true)
        {
            if (pollCount == 0)
            {
                await progressCallback(ExecutionState.Running, "Run", "Executing the run through Microsoft Agent Framework streaming.");
            }
            else
            {
                await progressCallback(ExecutionState.Running, "Background", $"Polling background response progress (attempt {pollCount}).");
            }

            using (var providerActivity = AgentFrameworkTelemetry.ActivitySource.StartActivity("provider.call", ActivityKind.Internal))
            {
                AgentFrameworkTelemetry.ApplyCurrentAuditScope(providerActivity);
                providerActivity?.SetTag("agentframework.provider_name", provider.Name);
                providerActivity?.SetTag("agentframework.model", resolvedModel);
                providerActivity?.SetTag("agentframework.background_poll", pollCount);

                try
                {
                    await foreach (var update in RunStreamingAsync(runtimeAgent, runtimeSession, inputMessages, runOptions, cancellationToken))
                    {
                        var snapshot = SnapshotUpdate(update);
                        updates.Add(snapshot);

                        if (!announcedStreaming && !string.IsNullOrWhiteSpace(snapshot.Text))
                        {
                            announcedStreaming = true;
                            await progressCallback(ExecutionState.Running, "Streaming", "The agent is producing streamed output.");
                        }

                        foreach (var toolCall in snapshot.Contents.OfType<ToolCallContent>())
                        {
                            var toolKey = ResolveToolCallKey(toolCall);
                            if (toolCall.CallId is null || guardedToolCallIds.Add(toolCall.CallId))
                            {
                                repeatedToolInvocationGuard.Guard(toolCall);
                            }

                            if (!announcedToolCalls.Add(toolKey))
                            {
                                continue;
                            }

                            await progressCallback(ExecutionState.WaitingOnTool, "Tool", DescribeToolInvocation(toolCall));
                        }
                    }
                }
                catch (Exception exception)
                {
                    AgentFrameworkTelemetry.RecordProviderError(provider, resolvedModel);
                    providerActivity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                    throw;
                }
            }

            var response = updates.ToAgentResponse();
            var approvalRequests = response.Messages
                .SelectMany(message => message.Contents)
                .OfType<ToolApprovalRequestContent>()
                .ToList();

            if (approvalRequests.Count > 0)
            {
                pendingApprovalCache[session.Id] = approvalRequests;
            }
            else
            {
                pendingApprovalCache.TryRemove(session.Id, out _);
            }

            if (!ShouldContinueBackgroundRun(agent, provider, response, approvalRequests))
            {
                await progressCallback(ExecutionState.Persisting, "Session", "Serializing the Microsoft Agent Framework session.");
                var serializedSession = await runtimeAgent.SerializeSessionAsync(runtimeSession, cancellationToken: cancellationToken);
                var serializedSessionJson = JsonSerializer.Serialize(serializedSession, SerializerOptions);
                var pendingApprovals = approvalRequests.Select(MapPendingApproval).ToList();

                if (pendingApprovals.Count > 0)
                {
                    await progressCallback(ExecutionState.WaitingOnTool, "Approval", "The run is waiting for a tool approval response before it can continue.");
                }

                return new AgentRuntimeResponse(
                    ResponseText: ResolveResponseText(response, pendingApprovals),
                    InputTokens: (int)(response.Usage?.InputTokenCount ?? 0),
                    OutputTokens: (int)(response.Usage?.OutputTokenCount ?? 0),
                    ToolCalls: CountToolCalls(response),
                    RuntimeSessionKey: ResolveRuntimeSessionKey(runtimeSession, response, runtimeSessionKey),
                    SerializedSessionStateJson: serializedSessionJson,
                    PendingApprovals: pendingApprovals);
            }

            pollCount++;
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            runOptions = CreateRunOptions(
                agent,
                provider,
                resolvedModel,
                hasApprovalTools: false,
                continuationToken: response.ContinuationToken,
                forceOmitTemperature: forceOmitTemperature);
            inputMessages = [];
        }
    }

    private IEnumerable<ChatMessage> CreateApprovalInputMessages(ChatSessionRecord session, bool approved)
    {
        var approvals = GetCachedOrRehydratedApprovals(session);
        return approvals
            .Select(item => new ChatMessage(ChatRole.User, [item.CreateResponse(approved)]))
            .ToList();
    }

    private IReadOnlyList<ToolApprovalRequestContent> GetCachedOrRehydratedApprovals(ChatSessionRecord session)
    {
        if (pendingApprovalCache.TryGetValue(session.Id, out var cached))
        {
            return cached;
        }

        var compatibility = session.Compatibility;
        if (compatibility is null || compatibility.PendingApprovals.Count == 0)
        {
            throw new InvalidOperationException("This session does not have any cached approval requests to continue.");
        }

        var rehydrated = compatibility.PendingApprovals
            .Select(RehydratePendingApproval)
            .ToList();

        pendingApprovalCache[session.Id] = rehydrated;
        return rehydrated;
    }

    private static ToolApprovalRequestContent RehydratePendingApproval(PendingToolApprovalRecord record)
    {
        var arguments = DeserializeArguments(record.ArgumentsJson);
        ToolCallContent toolCall = record.ToolKind switch
        {
            "mcp" or "hosted-mcp" => new McpServerToolCallContent(record.CallId, record.ToolName, record.Details)
            {
                Arguments = arguments
            },
            _ => new FunctionCallContent(record.CallId, record.ToolName, arguments)
        };

        return new ToolApprovalRequestContent(record.ApprovalId, toolCall);
    }

    private static PendingToolApprovalRecord MapPendingApproval(ToolApprovalRequestContent request)
    {
        var toolCall = request.ToolCall;
        var toolKind = toolCall switch
        {
            McpServerToolCallContent => "mcp",
            FunctionCallContent => "function",
            _ => "tool"
        };

        var details = toolCall switch
        {
            McpServerToolCallContent mcp => mcp.ServerName,
            _ => string.Empty
        };

        var argumentsJson = toolCall switch
        {
            McpServerToolCallContent mcp when mcp.Arguments is not null => JsonSerializer.Serialize(mcp.Arguments, SerializerOptions),
            FunctionCallContent function when function.Arguments is not null => JsonSerializer.Serialize(function.Arguments, SerializerOptions),
            _ => "{}"
        };

        return new PendingToolApprovalRecord(
            ApprovalId: request.RequestId ?? toolCall.CallId ?? Guid.NewGuid().ToString("N"),
            CallId: toolCall.CallId ?? string.Empty,
            ToolName: ResolveToolName(toolCall),
            ToolKind: toolKind,
            Details: details ?? string.Empty,
            ArgumentsJson: argumentsJson);
    }

    private static string ResolveResponseText(
        AgentResponse response,
        IReadOnlyList<PendingToolApprovalRecord> pendingApprovals)
    {
        if (!string.IsNullOrWhiteSpace(response.Text))
        {
            return response.Text.Trim();
        }

        if (pendingApprovals.Count == 0)
        {
            return "The provider completed without returning text.";
        }

        var summary = string.Join(
            Environment.NewLine,
            pendingApprovals.Select(item =>
            {
                var argumentSummary = DescribeArguments(item.ArgumentsJson);
                return item.ToolKind == "mcp"
                    ? $"- Approval required for MCP tool '{item.ToolName}' on server '{item.Details}'{FormatInlineArgumentSummary(argumentSummary)}."
                    : $"- Approval required for tool '{item.ToolName}'{FormatInlineArgumentSummary(argumentSummary)}.";
            }));

        return $"Approval is required before the run can continue.{Environment.NewLine}{summary}";
    }

    private static bool ShouldContinueBackgroundRun(
        AgentDefinition agent,
        ProviderProfile provider,
        AgentResponse response,
        IReadOnlyCollection<ToolApprovalRequestContent> approvalRequests)
    {
        if (approvalRequests.Count > 0)
        {
            return false;
        }

        return agent.EnableBackgroundResponses
            && SupportsBackgroundResponses(provider)
            && response.ContinuationToken is not null;
    }

    private static int CountToolCalls(AgentResponse response)
    {
        return response.Messages
            .SelectMany(message => message.Contents)
            .Select(content => content switch
            {
                ToolApprovalRequestContent approval => approval.ToolCall?.CallId ?? approval.ToolCall?.ToString(),
                ToolCallContent toolCall => toolCall.CallId ?? ResolveToolName(toolCall),
                _ => null
            })
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static string ResolveRuntimeSessionKey(
        AgentSession runtimeSession,
        AgentResponse response,
        string? fallbackValue)
    {
        if (runtimeSession is ChatClientAgentSession chatSession && !string.IsNullOrWhiteSpace(chatSession.ConversationId))
        {
            return chatSession.ConversationId;
        }

        return response.ResponseId
            ?? response.ContinuationToken?.ToString()
            ?? fallbackValue
            ?? string.Empty;
    }

    private static string ResolveToolName(ToolCallContent toolCall)
    {
        return toolCall switch
        {
            FunctionCallContent functionCall when !string.IsNullOrWhiteSpace(functionCall.Name) => functionCall.Name,
            McpServerToolCallContent mcpToolCall when !string.IsNullOrWhiteSpace(mcpToolCall.Name) => mcpToolCall.Name,
            _ => "Unnamed tool"
        };
    }

    private static string ResolveToolCallKey(ToolCallContent toolCall)
    {
        return toolCall.CallId
            ?? $"{ResolveToolName(toolCall)}|{DescribeToolCallArguments(toolCall)}";
    }

    private sealed class RepeatedToolInvocationGuard
    {
        private readonly Dictionary<string, int> repeatedToolInvocationCounts = new(StringComparer.OrdinalIgnoreCase);
        private int mutationGeneration;

        public void Guard(ToolCallContent toolCall)
        {
            var toolName = ResolveToolName(toolCall);
            if (!ShouldGuardRepeatedToolInvocation(toolName))
            {
                return;
            }

            var signature = ResolveToolInvocationSignature(toolCall);
            if (IsValidationToolInvocation(toolName))
            {
                signature = $"{signature}|mutationGeneration={mutationGeneration}";
            }

            var repeatedToolInvocationCount = repeatedToolInvocationCounts.TryGetValue(signature, out var currentCount)
                ? currentCount + 1
                : 1;
            repeatedToolInvocationCounts[signature] = repeatedToolInvocationCount;
            if (repeatedToolInvocationCount > MaxRepeatedToolInvocationCount)
            {
                var recoveryHint = ResolveRepeatedToolInvocationRecoveryHint(signature);
                throw new InvalidOperationException(
                    $"Agent repeated identical tool invocation '{signature}' {repeatedToolInvocationCount} times in one run. Stop repeating the same tool call and either call the required next validation tool, inspect and change the underlying cause, or return a governed blocked/failed outcome.{recoveryHint}");
            }

            if (IsMutationToolInvocation(toolName))
            {
                mutationGeneration++;
            }
        }
    }

    private static bool ShouldGuardRepeatedToolInvocation(string toolName)
    {
        return IsValidationToolInvocation(toolName) || IsMutationToolInvocation(toolName);
    }

    private static bool IsValidationToolInvocation(string toolName)
    {
        return string.Equals(toolName, "workspace_dotnet_build", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_dotnet_test", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_dotnet_run", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMutationToolInvocation(string toolName)
    {
        return string.Equals(toolName, "workspace_dotnet_new", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_pwsh_run_script", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_create_directory", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_write_file", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_append_file", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_move_path", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_delete_path", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveToolInvocationSignature(ToolCallContent toolCall)
    {
        return $"{ResolveToolName(toolCall)}|{DescribeToolCallArguments(toolCall)}";
    }

    private static string ResolveRepeatedToolInvocationRecoveryHint(string signature)
    {
        if (signature.Contains("workspace_delete_path", StringComparison.OrdinalIgnoreCase) &&
            signature.Contains("Calculator.Tests", StringComparison.OrdinalIgnoreCase))
        {
            return " If this is the calculator process, do not keep deleting the sibling test project. Inspect the path shape first. If `Calculator.Tests/Calculator.Tests.csproj` is a directory, the prior scaffold was nested incorrectly; stop repair-by-delete and recreate the sibling test project from the output root on the next clean run.";
        }

        if (signature.Contains("workspace_move_path", StringComparison.OrdinalIgnoreCase) &&
            signature.Contains("Calculator.Tests/Calculator.Tests", StringComparison.OrdinalIgnoreCase) &&
            signature.Contains("Calculator.Tests.csproj", StringComparison.OrdinalIgnoreCase))
        {
            return " If this is the calculator process, never move a directory to `Calculator.Tests/Calculator.Tests.csproj`. That creates a directory named like a project file; use `workspace_dotnet_new` with parentDirectory set to the output root and name `Calculator.Tests`.";
        }

        if (signature.Contains("workspace_write_file", StringComparison.OrdinalIgnoreCase) &&
            signature.Contains("Calculator.Tests/Calculator.Tests.csproj", StringComparison.OrdinalIgnoreCase))
        {
            return " If this is the calculator process, do not keep rewriting the sibling test project when it already references the host. If that path is a directory, the test project was nested incorrectly; stop rewriting it and repair from a clean sibling project path. If the current compiler error is `CS1503` in `Home.razor`, the valid next mutation is the effective routed UI (`Calculator/Components/Pages/Home.razor`), not `Calculator.Tests.csproj`: either change `AppendToResult(string value)` to `AppendToResult(char value)` for char callbacks, or keep string handlers and use single-quoted Razor attributes such as `@onclick='() => AppendToResult(\"1\")'`.";
        }

        if (signature.Contains("workspace_write_file", StringComparison.OrdinalIgnoreCase) &&
            signature.Contains("Calculator/Components/Pages/Home.razor", StringComparison.OrdinalIgnoreCase))
        {
            return " If this is the calculator process, do not keep overwriting the same routed page. Inspect the latest build output and change the actual blocker: for Razor callback syntax or `CS1503` errors, either use char handlers (`AppendDigit(char digit)`, `ChooseOperator(char op)`) with callbacks like `@onclick=\"() => AppendDigit('1')\"`, or keep string handlers and wrap the whole Razor attribute in single quotes, for example `@onclick='() => AppendDigit(\"1\")'`. Do not leave `AppendToResult('1')` or `SetOperation('+')` calling methods that still accept `string`, and never write `@onclick=\"() => AppendDigit(\"1\")\"`. Also replace placeholder `CalculateResult` logic with `CalculatorEngine`-backed operations, history, and divide-by-zero feedback before validating again.";
        }

        if (signature.Contains("workspace_dotnet_test", StringComparison.OrdinalIgnoreCase) &&
            signature.Contains("Calculator.Tests/Calculator.Tests.csproj", StringComparison.OrdinalIgnoreCase))
        {
            return " If this is the calculator process, do not rerun the same sibling test command again until you inspect the compiler diagnostic and mutate the source that addresses it. For `Calculator.Domain`, `CalculatorEngine`, `CS0234`, or `CS0246` failures, repair `Calculator.Tests/Calculator.Tests.csproj` with a host ProjectReference and confirm `Calculator/Domain/CalculatorEngine.cs` exists before testing again.";
        }

        if (signature.Contains("workspace_dotnet_build", StringComparison.OrdinalIgnoreCase) &&
            signature.Contains("Calculator/Calculator.csproj", StringComparison.OrdinalIgnoreCase))
        {
            return " If this is the calculator process, do not rerun the same host build until you inspect the compiler diagnostic and mutate the source that addresses it. For duplicate `CalculatorEngine` failures (`CS0101` or `CS0111`), inspect `Calculator/CalculatorEngine.cs` and `Calculator/Domain/CalculatorEngine.cs`; delete the stale top-level engine file and keep one domain engine before rebuilding.";
        }

        if (signature.Contains("workspace_write_file", StringComparison.OrdinalIgnoreCase))
        {
            return " If the same content is already present, read a different relevant file or mutate the file that actually addresses the remaining validation failure instead of writing this unchanged file again.";
        }

        return string.Empty;
    }

    private static string DescribeToolInvocation(ToolCallContent toolCall)
    {
        var toolName = ResolveToolName(toolCall);
        var arguments = DescribeToolCallArguments(toolCall);
        return string.IsNullOrWhiteSpace(arguments)
            ? $"Invoking tool '{toolName}'."
            : $"Invoking tool '{toolName}' with {arguments}.";
    }

    private static string DescribeToolCallArguments(ToolCallContent toolCall)
    {
        return toolCall switch
        {
            FunctionCallContent functionCall => SummarizeArguments(functionCall.Arguments),
            McpServerToolCallContent mcpToolCall => SummarizeArguments(mcpToolCall.Arguments),
            _ => string.Empty
        };
    }

    private static string DescribeArguments(string? argumentsJson)
    {
        return string.IsNullOrWhiteSpace(argumentsJson)
            ? string.Empty
            : FormatArgumentSummary(DeserializeArguments(argumentsJson));
    }

    private static string FormatInlineArgumentSummary(string argumentSummary)
    {
        return string.IsNullOrWhiteSpace(argumentSummary)
            ? string.Empty
            : $" with {argumentSummary}";
    }

    private static string SummarizeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return string.Empty;
        }

        return FormatArgumentSummary(arguments);
    }

    private static string FormatArgumentSummary(IEnumerable<KeyValuePair<string, object?>> arguments)
    {
        var parts = arguments
            .Where(item => item.Value is not null)
            .Select(item => $"{item.Key}={FormatArgumentValue(item.Value)}")
            .ToList();

        return parts.Count == 0
            ? string.Empty
            : string.Join(", ", parts);
    }

    private static string FormatArgumentValue(object? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        var text = value switch
        {
            string stringValue => stringValue,
            JsonElement jsonValue => jsonValue.ToString(),
            _ => JsonSerializer.Serialize(value, SerializerOptions)
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return "\"\"";
        }

        text = text.ReplaceLineEndings(" ").Trim();
        if (text.Length > 120)
        {
            text = text[..120] + "...";
        }

        return $"\"{text}\"";
    }

    private static Dictionary<string, object?> DeserializeArguments(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return document.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => ConvertJsonValue(property.Value));
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static object? ConvertJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when value.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.Array => value.EnumerateArray().Select(ConvertJsonValue).ToList(),
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(property => property.Name, property => ConvertJsonValue(property.Value)),
            _ => value.ToString()
        };
    }
}
