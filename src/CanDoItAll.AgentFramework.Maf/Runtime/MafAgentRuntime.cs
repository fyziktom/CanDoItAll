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
    private static readonly TimeSpan FinalizerSessionSerializationTimeout = TimeSpan.FromSeconds(5);
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
        bool suppressApprovalRequirements = false,
        AgentStructuredOutputContract? structuredOutput = null,
        AgentRuntimeExecutionOptions? executionOptions = null)
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
                structuredOutput,
                executionOptions,
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
                structuredOutput,
                executionOptions,
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
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeExecutionOptions? executionOptions,
        bool forceOmitTemperature)
    {
        var runtimeOptions = NormalizeRuntimeExecutionOptions(structuredOutput, executionOptions);
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
            forceOmitTemperature,
            runtimeOptions);

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
            forceOmitTemperature: forceOmitTemperature,
            structuredOutput: runtimeOptions.StructuredOutput);
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
            runtimeOptions.StructuredOutput,
            runtimeOptions.FinalizerMode,
            forceOmitTemperature,
            runtimeBuild.SnapshotFinalizerInvocations,
            runtimeBuild.SnapshotToolInvocationTraces);
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
        bool suppressApprovalRequirements = false,
        AgentStructuredOutputContract? structuredOutput = null,
        AgentRuntimeExecutionOptions? executionOptions = null)
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
                structuredOutput,
                executionOptions,
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
                structuredOutput,
                executionOptions,
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
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeExecutionOptions? executionOptions,
        bool forceOmitTemperature)
    {
        var runtimeOptions = NormalizeRuntimeExecutionOptions(structuredOutput, executionOptions);
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
            forceOmitTemperature,
            runtimeOptions);

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
            forceOmitTemperature: forceOmitTemperature,
            structuredOutput: runtimeOptions.StructuredOutput);
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
            runtimeOptions.StructuredOutput,
            runtimeOptions.FinalizerMode,
            forceOmitTemperature,
            runtimeBuild.SnapshotFinalizerInvocations,
            runtimeBuild.SnapshotToolInvocationTraces);
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
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        bool forceOmitTemperature,
        Func<IReadOnlyList<AgentFinalizerInvocation>> snapshotFinalizerInvocations,
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotToolInvocationTraces)
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

                        var finalizerResponse = await TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
                            structuredOutput,
                            finalizerMode,
                            runtimeAgent,
                            runtimeSession,
                            runtimeSessionKey,
                            progressCallback,
                            cancellationToken,
                            snapshotFinalizerInvocations,
                            snapshotToolInvocationTraces);
                        if (finalizerResponse is not null)
                        {
                            return finalizerResponse;
                        }
                    }

                    var postStreamingFinalizerResponse = await TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
                        structuredOutput,
                        finalizerMode,
                        runtimeAgent,
                        runtimeSession,
                        runtimeSessionKey,
                        progressCallback,
                        cancellationToken,
                        snapshotFinalizerInvocations,
                        snapshotToolInvocationTraces);
                    if (postStreamingFinalizerResponse is not null)
                    {
                        return postStreamingFinalizerResponse;
                    }
                }
                catch (RequiredFinalizerCapturedException exception)
                {
                    providerActivity?.SetTag("agentframework.required_finalizer_tool_name", exception.ToolName);
                    var finalizerResponse = await TryCreateFinalizerResponseAfterEarlyFinalizerAsync(
                        structuredOutput,
                        finalizerMode,
                        runtimeAgent,
                        runtimeSession,
                        runtimeSessionKey,
                        progressCallback,
                        cancellationToken,
                        snapshotFinalizerInvocations,
                        snapshotToolInvocationTraces);
                    if (finalizerResponse is not null)
                    {
                        return finalizerResponse;
                    }

                    throw;
                }
                catch (Exception exception)
                {
                    AgentFrameworkTelemetry.RecordProviderError(provider, resolvedModel);
                    providerActivity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                    var finalizerResponse = await TryCreateFinalizerResponseAfterProviderFailureAsync(
                        structuredOutput,
                        runtimeAgent,
                        runtimeSession,
                        runtimeSessionKey,
                        finalizerMode,
                        exception,
                        progressCallback,
                        cancellationToken,
                        snapshotFinalizerInvocations,
                        snapshotToolInvocationTraces);
                    if (finalizerResponse is not null)
                    {
                        return finalizerResponse;
                    }

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
                    PendingApprovals: pendingApprovals)
                {
                    FinalizerInvocations = snapshotFinalizerInvocations(),
                    ToolInvocationTraces = snapshotToolInvocationTraces()
                };
            }

            pollCount++;
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            runOptions = CreateRunOptions(
                agent,
                provider,
                resolvedModel,
                hasApprovalTools: false,
                continuationToken: response.ContinuationToken,
                forceOmitTemperature: forceOmitTemperature,
                structuredOutput: structuredOutput);
            inputMessages = [];
        }
    }

    private static async Task<AgentRuntimeResponse?> TryCreateFinalizerResponseAfterEarlyFinalizerAsync(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<AgentFinalizerInvocation>> snapshotFinalizerInvocations,
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotToolInvocationTraces)
    {
        var finalizerInvocations = snapshotFinalizerInvocations();
        var toolInvocationTraces = snapshotToolInvocationTraces();
        var serializedResponse = TryBuildRequiredFinalizerRuntimeResponse(
            structuredOutput,
            finalizerMode,
            ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
            serializedSessionStateJson: null,
            finalizerInvocations,
            toolInvocationTraces);
        if (serializedResponse is null)
        {
            return null;
        }

        await progressCallback(
            ExecutionState.Persisting,
            "Finalizer short-circuit",
            "Required finalizer tool produced a valid governed result. Persisting the typed result immediately without waiting for redundant post-finalizer assistant prose.");

        var serializedSessionStateJson = await TrySerializeRuntimeSessionAsync(
            runtimeAgent,
            runtimeSession,
            cancellationToken);
        return serializedResponse with
        {
            SerializedSessionStateJson = serializedSessionStateJson
        };
    }

    private static async Task<AgentRuntimeResponse?> TryCreateFinalizerResponseAfterProviderFailureAsync(
        AgentStructuredOutputContract? structuredOutput,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        AgentFinalizerMode finalizerMode,
        Exception exception,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<AgentFinalizerInvocation>> snapshotFinalizerInvocations,
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotToolInvocationTraces)
    {
        if (finalizerMode != AgentFinalizerMode.Required ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var policy))
        {
            return null;
        }

        var finalizerInvocations = snapshotFinalizerInvocations();
        var finalizerValidation = new DefaultAgentFinalizerValidator().Validate(policy, finalizerInvocations);
        if (!finalizerValidation.Succeeded || finalizerValidation.Output is null)
        {
            return null;
        }

        var toolInvocationTraces = snapshotToolInvocationTraces();
        var sequenceValidation = AgentFinalizerSequenceValidator.Validate(policy, toolInvocationTraces);
        if (!sequenceValidation.Succeeded)
        {
            return null;
        }

        await progressCallback(
            ExecutionState.Persisting,
            "Finalizer recovery",
            $"Provider streaming failed after required finalizer '{policy.ToolName}' was captured. Persisting the governed finalizer outcome and preserving the provider error for diagnostics: {exception.Message}");

        var serializedSessionStateJson = await TrySerializeRuntimeSessionAsync(
            runtimeAgent,
            runtimeSession,
            cancellationToken);
        return new AgentRuntimeResponse(
            JsonSerializer.Serialize(finalizerValidation.Output, policy.OutputType, AgentOutputJson.SerializerOptions),
            InputTokens: 0,
            OutputTokens: 0,
            ToolCalls: toolInvocationTraces
                .Where(trace => !string.IsNullOrWhiteSpace(trace.ToolName))
                .Select(trace => $"{trace.ToolName}|{trace.Sequence}")
                .Distinct(StringComparer.Ordinal)
                .Count(),
            RuntimeSessionKey: ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
            SerializedSessionStateJson: serializedSessionStateJson,
            PendingApprovals: [])
        {
            FinalizerInvocations = finalizerInvocations,
            ToolInvocationTraces = toolInvocationTraces
        };
    }

    private static async Task<AgentRuntimeResponse?> TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<AgentFinalizerInvocation>> snapshotFinalizerInvocations,
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotToolInvocationTraces)
    {
        var finalizerInvocations = snapshotFinalizerInvocations();
        var toolInvocationTraces = snapshotToolInvocationTraces();
        var serializedResponse = TryBuildRequiredFinalizerRuntimeResponse(
            structuredOutput,
            finalizerMode,
            ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
            serializedSessionStateJson: null,
            finalizerInvocations,
            toolInvocationTraces);
        if (serializedResponse is null)
        {
            return null;
        }

        await progressCallback(
            ExecutionState.Persisting,
            "Finalizer short-circuit",
            "Required finalizer tool produced a valid governed result. Persisting the typed result without waiting for redundant post-finalizer assistant prose.");

        var serializedSessionStateJson = await TrySerializeRuntimeSessionAsync(
            runtimeAgent,
            runtimeSession,
            cancellationToken);
        return serializedResponse with
        {
            SerializedSessionStateJson = serializedSessionStateJson
        };
    }

    private static AgentRuntimeResponse? TryBuildRequiredFinalizerRuntimeResponse(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        string runtimeSessionKey,
        string? serializedSessionStateJson,
        IReadOnlyList<AgentFinalizerInvocation> finalizerInvocations,
        IReadOnlyList<AgentToolInvocationTrace> toolInvocationTraces)
    {
        if (finalizerMode != AgentFinalizerMode.Required ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var policy))
        {
            return null;
        }

        var finalizerValidation = new DefaultAgentFinalizerValidator().Validate(policy, finalizerInvocations);
        if (!finalizerValidation.Succeeded || finalizerValidation.Output is null)
        {
            return null;
        }

        var sequenceValidation = AgentFinalizerSequenceValidator.Validate(policy, toolInvocationTraces);
        if (!sequenceValidation.Succeeded)
        {
            return null;
        }

        return new AgentRuntimeResponse(
            JsonSerializer.Serialize(finalizerValidation.Output, policy.OutputType, AgentOutputJson.SerializerOptions),
            InputTokens: 0,
            OutputTokens: 0,
            ToolCalls: toolInvocationTraces
                .Where(trace => !string.IsNullOrWhiteSpace(trace.ToolName))
                .Select(trace => $"{trace.ToolName}|{trace.Sequence}")
                .Distinct(StringComparer.Ordinal)
                .Count(),
            RuntimeSessionKey: runtimeSessionKey,
            SerializedSessionStateJson: serializedSessionStateJson,
            PendingApprovals: [])
        {
            FinalizerInvocations = finalizerInvocations,
            ToolInvocationTraces = toolInvocationTraces
        };
    }

    private static async Task<string?> TrySerializeRuntimeSessionAsync(
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        CancellationToken cancellationToken)
    {
        try
        {
            var serializedSession = await runtimeAgent.SerializeSessionAsync(
                runtimeSession,
                cancellationToken: cancellationToken).AsTask().WaitAsync(
                    FinalizerSessionSerializationTimeout,
                    cancellationToken);
            return JsonSerializer.Serialize(serializedSession, SerializerOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch
        {
            return null;
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

    private static string ResolveRuntimeSessionKey(
        AgentSession runtimeSession,
        string? fallbackValue)
    {
        if (runtimeSession is ChatClientAgentSession chatSession && !string.IsNullOrWhiteSpace(chatSession.ConversationId))
        {
            return chatSession.ConversationId;
        }

        return fallbackValue ?? string.Empty;
    }

    private sealed class RequiredFinalizerCapturedException(string toolName) : Exception(
        $"Required finalizer tool '{toolName}' was captured.")
    {
        public string ToolName { get; } = toolName;
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
                throw new InvalidOperationException(
                    $"Agent repeated identical tool invocation '{signature}' {repeatedToolInvocationCount} times in one run. Stop repeating the same tool call and either call the required next validation tool, inspect and change the underlying cause, or return a governed blocked/failed outcome.");
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
        => AgentToolInvocationPolicyMetadata.IsValidationTool(toolName);

    private static bool IsMutationToolInvocation(string toolName)
        => AgentToolInvocationPolicyMetadata.IsMutationTool(toolName);

    private static string ResolveToolInvocationSignature(ToolCallContent toolCall)
    {
        return $"{ResolveToolName(toolCall)}|{DescribeToolCallArguments(toolCall)}";
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
