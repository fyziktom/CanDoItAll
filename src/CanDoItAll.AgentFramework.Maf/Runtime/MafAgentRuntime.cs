using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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
            runtimeOptions);
        var inputMessages = CreatePromptInputMessages(agent, runtimeBuild.Provider, session, prompt).ToList();
        var contextManifest = CreateContextAssemblyManifest(
            agent,
            runtimeBuild.Provider,
            runtimeBuild.Model,
            runtimeOptions,
            runtimeBuild,
            inputMessages);

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
            runtimeOptions,
            forceOmitTemperature,
            runtimeBuild.SnapshotFinalizerInvocations,
            runtimeBuild.SnapshotToolInvocationTraces,
            runtimeBuild.SnapshotContextContributionTraces,
            contextManifest);
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
            runtimeOptions);
        var inputMessages = CreateApprovalInputMessages(session, approved).ToList();
        var contextManifest = CreateContextAssemblyManifest(
            agent,
            runtimeBuild.Provider,
            runtimeBuild.Model,
            runtimeOptions,
            runtimeBuild,
            inputMessages);

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
            runtimeOptions,
            forceOmitTemperature,
            runtimeBuild.SnapshotFinalizerInvocations,
            runtimeBuild.SnapshotToolInvocationTraces,
            runtimeBuild.SnapshotContextContributionTraces,
            contextManifest);
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
        AgentRuntimeExecutionOptions runtimeOptions,
        bool forceOmitTemperature,
        Func<IReadOnlyList<AgentFinalizerInvocation>> snapshotFinalizerInvocations,
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotToolInvocationTraces,
        Func<IReadOnlyList<AgentContextContributionTrace>> snapshotContextContributionTraces,
        AgentRuntimeContextAssemblyManifest contextManifest)
    {
        var updates = new List<AgentResponseUpdate>();
        var announcedStreaming = false;
        var announcedToolCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var guardedToolCallIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var repeatedToolInvocationGuard = new RepeatedToolInvocationGuard();
        var streamedFinalizerRecorder = new StreamedFinalizerInvocationRecorder(structuredOutput, finalizerMode);
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotEffectiveToolInvocationTraces = () =>
            CreateEffectiveToolInvocationTraces(
                snapshotToolInvocationTraces(),
                streamedFinalizerRecorder.SnapshotToolInvocationTraces());
        Func<IReadOnlyList<AgentFinalizerInvocation>> snapshotEffectiveFinalizerInvocations = () =>
            CreateEffectiveFinalizerInvocations(
                structuredOutput,
                finalizerMode,
                snapshotFinalizerInvocations(),
                snapshotToolInvocationTraces(),
                streamedFinalizerRecorder.SnapshotFinalizerInvocations());
        var pollCount = 0;
        var resolvedModel = model;

        AgentRuntimeResponse AttachContextDiagnostics(AgentRuntimeResponse response)
            => response with
            {
                ContextAssemblyManifest = contextManifest,
                ContextContributionTraces = snapshotContextContributionTraces()
            };

        async Task<AgentRuntimeResponse?> RecordStreamingUpdateAsync(
            AgentResponseUpdate update,
            string usageSourcePhase)
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

                streamedFinalizerRecorder.Record(toolCall);
                await progressCallback(ExecutionState.WaitingOnTool, "Tool", DescribeToolInvocation(toolCall));
            }

            return await TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
                provider,
                resolvedModel,
                structuredOutput,
                finalizerMode,
                runtimeAgent,
                runtimeSession,
                runtimeSessionKey,
                updates,
                usageSourcePhase,
                progressCallback,
                cancellationToken,
                snapshotEffectiveFinalizerInvocations,
                snapshotEffectiveToolInvocationTraces);
        }

        async Task<AgentRuntimeResponse?> TryRunMissingRequiredFinalizerRepairAsync(
            AgentResponse response,
            IReadOnlyCollection<ToolApprovalRequestContent> approvalRequests)
        {
            if (!ShouldRequestMissingRequiredFinalizerRepair(
                    structuredOutput,
                    finalizerMode,
                    runtimeOptions,
                    approvalRequests,
                    snapshotEffectiveFinalizerInvocations(),
                    out var finalizerPolicy))
            {
                return null;
            }

            await progressCallback(
                ExecutionState.Running,
                "Finalizer repair",
                $"Required finalizer tool '{finalizerPolicy.ToolName}' was missing after the provider completed. Requesting one bounded repair turn.");

            var repairRunOptions = CreateRequiredFinalizerRepairRunOptions(runOptions, finalizerPolicy);
            var repairMessages = new[]
            {
                CreateRequiredFinalizerRepairMessage(finalizerPolicy, response)
            };

            try
            {
                await foreach (var repairUpdate in RunStreamingAsync(runtimeAgent, runtimeSession, repairMessages, repairRunOptions, cancellationToken))
                {
                    var finalizerResponse = await RecordStreamingUpdateAsync(
                        repairUpdate,
                        ProviderUsageSourcePhases.FinalizerRecovery);
                    if (finalizerResponse is not null)
                    {
                        return AttachContextDiagnostics(finalizerResponse);
                    }
                }

                var requiredFinalizerResponse = await TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
                    provider,
                    resolvedModel,
                    structuredOutput,
                    finalizerMode,
                    runtimeAgent,
                    runtimeSession,
                    runtimeSessionKey,
                    updates,
                    ProviderUsageSourcePhases.FinalizerRecovery,
                    progressCallback,
                    cancellationToken,
                    snapshotEffectiveFinalizerInvocations,
                    snapshotEffectiveToolInvocationTraces);
                return requiredFinalizerResponse is null
                    ? null
                    : AttachContextDiagnostics(requiredFinalizerResponse);
            }
            catch (RequiredFinalizerCapturedException exception)
            {
                var finalizerResponse = await TryCreateFinalizerResponseAfterEarlyFinalizerAsync(
                    provider,
                    resolvedModel,
                    structuredOutput,
                    finalizerMode,
                    runtimeAgent,
                    runtimeSession,
                    runtimeSessionKey,
                    updates,
                    ProviderUsageSourcePhases.FinalizerRecovery,
                    progressCallback,
                    cancellationToken,
                    snapshotEffectiveFinalizerInvocations,
                    snapshotEffectiveToolInvocationTraces);
                if (finalizerResponse is not null)
                {
                    return AttachContextDiagnostics(finalizerResponse);
                }

                throw new AgentRuntimeUsageException(
                    $"Required finalizer repair captured '{exception.ToolName}' but the governed result could not be validated.",
                    exception,
                    CreateProviderUsageObservations(
                        provider,
                        resolvedModel,
                        runtimeSession,
                        runtimeSessionKey,
                        updates,
                        ProviderUsageSourcePhases.FinalizerRecovery,
                        $"Required finalizer repair captured '{exception.ToolName}' but validation failed."));
            }
            catch (Exception exception)
            {
                var finalizerResponse = await TryCreateFinalizerResponseAfterProviderFailureAsync(
                    provider,
                    resolvedModel,
                    structuredOutput,
                    runtimeAgent,
                    runtimeSession,
                    runtimeSessionKey,
                    finalizerMode,
                    exception,
                    updates,
                    progressCallback,
                    cancellationToken,
                    snapshotEffectiveFinalizerInvocations,
                    snapshotEffectiveToolInvocationTraces);
                if (finalizerResponse is not null)
                {
                    return AttachContextDiagnostics(finalizerResponse);
                }

                throw new AgentRuntimeUsageException(
                    "Provider runtime failed during the bounded required-finalizer repair turn. Usage was captured when available.",
                    exception,
                    CreateProviderUsageObservations(
                        provider,
                        resolvedModel,
                        runtimeSession,
                        runtimeSessionKey,
                        updates,
                        ProviderUsageSourcePhases.FinalizerRecovery,
                        BuildProviderFailureDiagnostic(exception)));
            }
        }

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
                        var finalizerResponse = await RecordStreamingUpdateAsync(
                            update,
                            ProviderUsageSourcePhases.FinalizerShortCircuit);
                        if (finalizerResponse is not null)
                        {
                            return AttachContextDiagnostics(finalizerResponse);
                        }
                    }

                    var postStreamingFinalizerResponse = await TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
                        provider,
                        resolvedModel,
                        structuredOutput,
                        finalizerMode,
                        runtimeAgent,
                        runtimeSession,
                        runtimeSessionKey,
                        updates,
                        ProviderUsageSourcePhases.FinalizerShortCircuit,
                        progressCallback,
                        cancellationToken,
                        snapshotEffectiveFinalizerInvocations,
                        snapshotEffectiveToolInvocationTraces);
                    if (postStreamingFinalizerResponse is not null)
                    {
                        return AttachContextDiagnostics(postStreamingFinalizerResponse);
                    }
                }
                catch (RequiredFinalizerCapturedException exception)
                {
                    providerActivity?.SetTag("agentframework.required_finalizer_tool_name", exception.ToolName);
                    var finalizerResponse = await TryCreateFinalizerResponseAfterEarlyFinalizerAsync(
                        provider,
                        resolvedModel,
                        structuredOutput,
                        finalizerMode,
                        runtimeAgent,
                        runtimeSession,
                        runtimeSessionKey,
                        updates,
                        ProviderUsageSourcePhases.FinalizerShortCircuit,
                        progressCallback,
                        cancellationToken,
                        snapshotEffectiveFinalizerInvocations,
                        snapshotEffectiveToolInvocationTraces);
                    if (finalizerResponse is not null)
                    {
                        return AttachContextDiagnostics(finalizerResponse);
                    }

                    throw;
                }
                catch (Exception exception)
                {
                    AgentFrameworkTelemetry.RecordProviderError(provider, resolvedModel);
                    providerActivity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                    var finalizerResponse = await TryCreateFinalizerResponseAfterProviderFailureAsync(
                        provider,
                        resolvedModel,
                        structuredOutput,
                        runtimeAgent,
                        runtimeSession,
                        runtimeSessionKey,
                        finalizerMode,
                        exception,
                        updates,
                        progressCallback,
                        cancellationToken,
                        snapshotEffectiveFinalizerInvocations,
                        snapshotEffectiveToolInvocationTraces);
                    if (finalizerResponse is not null)
                    {
                        return AttachContextDiagnostics(finalizerResponse);
                    }

                    throw new AgentRuntimeUsageException(
                        "Provider runtime failed after provider activity. Usage was captured when available.",
                        exception,
                        CreateProviderUsageObservations(
                            provider,
                            resolvedModel,
                            runtimeSession,
                            runtimeSessionKey,
                            updates,
                            ProviderUsageSourcePhases.AgentRuntime,
                            BuildProviderFailureDiagnostic(exception)));
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
                var repairedFinalizerResponse = await TryRunMissingRequiredFinalizerRepairAsync(response, approvalRequests);
                if (repairedFinalizerResponse is not null)
                {
                    return AttachContextDiagnostics(repairedFinalizerResponse);
                }

                await progressCallback(ExecutionState.Persisting, "Session", "Serializing the Microsoft Agent Framework session.");
                var serializedSession = await runtimeAgent.SerializeSessionAsync(runtimeSession, cancellationToken: cancellationToken);
                var serializedSessionJson = JsonSerializer.Serialize(serializedSession, SerializerOptions);
                var pendingApprovals = approvalRequests.Select(MapPendingApproval).ToList();

                if (pendingApprovals.Count > 0)
                {
                    await progressCallback(ExecutionState.WaitingOnTool, "Approval", "The run is waiting for a tool approval response before it can continue.");
                }

                return AttachContextDiagnostics(new AgentRuntimeResponse(
                    ResponseText: ResolveResponseText(response, pendingApprovals),
                    InputTokens: ClampTokenCount(response.Usage?.InputTokenCount),
                    OutputTokens: ClampTokenCount(response.Usage?.OutputTokenCount),
                    ToolCalls: CountToolCalls(response),
                    RuntimeSessionKey: ResolveRuntimeSessionKey(runtimeSession, response, runtimeSessionKey),
                    SerializedSessionStateJson: serializedSessionJson,
                    PendingApprovals: pendingApprovals)
                {
                    CachedInputTokens = ClampTokenCount(response.Usage?.CachedInputTokenCount),
                    FinalizerInvocations = snapshotEffectiveFinalizerInvocations(),
                    ToolInvocationTraces = snapshotEffectiveToolInvocationTraces(),
                    UsageObservations = CreateProviderUsageObservations(
                        provider,
                        resolvedModel,
                        runtimeSession,
                        runtimeSessionKey,
                        updates,
                        ProviderUsageSourcePhases.AgentRuntime,
                        "Microsoft Agent Framework returned a runtime response.")
                });
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
                runtimeOptions);
            inputMessages = [];
        }
    }

    internal static bool ShouldRequestMissingRequiredFinalizerRepair(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        AgentRuntimeExecutionOptions runtimeOptions,
        IReadOnlyCollection<ToolApprovalRequestContent> approvalRequests,
        IReadOnlyList<AgentFinalizerInvocation> finalizerInvocations,
        out AgentFinalizerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        ArgumentNullException.ThrowIfNull(approvalRequests);
        ArgumentNullException.ThrowIfNull(finalizerInvocations);

        policy = AgentFinalizerPolicy.NotRequired;
        if (finalizerMode != AgentFinalizerMode.Required ||
            runtimeOptions.MaxStructuredOutputRepairAttempts <= 0 ||
            approvalRequests.Count > 0 ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out policy))
        {
            return false;
        }

        var toolName = policy.ToolName;
        return !finalizerInvocations.Any(invocation =>
            string.Equals(invocation.ToolName, toolName, StringComparison.OrdinalIgnoreCase));
    }

    internal static ChatClientAgentRunOptions CreateRequiredFinalizerRepairRunOptions(
        ChatClientAgentRunOptions source,
        AgentFinalizerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(policy);

        var chatOptions = source.ChatOptions?.Clone() ?? new ChatOptions();
        chatOptions.AllowMultipleToolCalls = false;
        chatOptions.ToolMode = null;

        return new ChatClientAgentRunOptions(chatOptions)
        {
            AllowBackgroundResponses = false,
            ChatClientFactory = source.ChatClientFactory,
            ContinuationToken = null
        };
    }

    internal static ChatMessage CreateRequiredFinalizerRepairMessage(
        AgentFinalizerPolicy policy,
        AgentResponse previousResponse)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(previousResponse);

        return new ChatMessage(
            ChatRole.User,
            BuildRequiredFinalizerRepairPrompt(policy, previousResponse.Text));
    }

    internal static string BuildRequiredFinalizerRepairPrompt(
        AgentFinalizerPolicy policy,
        string? previousAssistantText)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var previousTextSummary = string.IsNullOrWhiteSpace(previousAssistantText)
            ? "The previous turn returned no assistant text."
            : $"Previous assistant text:{Environment.NewLine}{previousAssistantText.Trim()}";

        return
            $"The previous turn ended without the required `{policy.ToolName}` finalizer tool call.{Environment.NewLine}" +
            $"Call `{policy.ToolName}` exactly once now to submit the final governed `{policy.OutputContract.ContractKey}` outcome.{Environment.NewLine}" +
            "Use only the current session context, prior tool results, and process artifacts. If the available evidence is insufficient for a successful outcome, submit the contract's failure or blocking state with actionable next actions where the contract supports them." + Environment.NewLine +
            "Do not call any other tool. Do not emit Markdown, prose, or machine JSON outside the finalizer tool call." + Environment.NewLine +
            previousTextSummary;
    }

    private static IReadOnlyList<AgentFinalizerInvocation> CreateEffectiveFinalizerInvocations(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        IReadOnlyList<AgentFinalizerInvocation> capturedInvocations,
        IReadOnlyList<AgentToolInvocationTrace> capturedToolInvocationTraces,
        IReadOnlyList<AgentFinalizerInvocation> streamedInvocations)
    {
        if (capturedInvocations.Count > 0 ||
            streamedInvocations.Count == 0 ||
            finalizerMode != AgentFinalizerMode.Required ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var policy))
        {
            return capturedInvocations;
        }

        return streamedInvocations;
    }

    private static IReadOnlyList<AgentToolInvocationTrace> CreateEffectiveToolInvocationTraces(
        IReadOnlyList<AgentToolInvocationTrace> capturedToolInvocationTraces,
        IReadOnlyList<AgentToolInvocationTrace> streamedToolInvocationTraces)
    {
        if (streamedToolInvocationTraces.Count == 0)
        {
            return capturedToolInvocationTraces;
        }

        var capturedToolNames = capturedToolInvocationTraces
            .Select(trace => trace.ToolName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingStreamedTraces = streamedToolInvocationTraces
            .Where(trace => !capturedToolNames.Contains(trace.ToolName))
            .ToList();
        if (missingStreamedTraces.Count == 0)
        {
            return capturedToolInvocationTraces;
        }

        return capturedToolInvocationTraces
            .Concat(missingStreamedTraces)
            .OrderBy(trace => trace.Sequence)
            .ToList();
    }

    private static AgentFinalizerInvocation? TryCreateStreamedFinalizerInvocation(
        AgentFinalizerPolicy policy,
        ToolCallContent toolCall,
        int sequence)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(toolCall);

        if (!policy.IsRequired ||
            !string.Equals(ResolveToolName(toolCall), policy.ToolName, StringComparison.OrdinalIgnoreCase) ||
            toolCall is not FunctionCallContent functionCall ||
            functionCall.Arguments is null)
        {
            return null;
        }

        var payload = functionCall.Arguments.Count == 1 &&
                      functionCall.Arguments.TryGetValue("result", out var result)
            ? result
            : functionCall.Arguments;
        var argumentsJson = SerializeStreamedFinalizerPayload(payload);
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return null;
        }

        return new AgentFinalizerInvocation(
            policy.ToolName,
            argumentsJson,
            sequence);
    }

    private static string SerializeStreamedFinalizerPayload(object? payload)
    {
        return payload switch
        {
            null => "null",
            JsonElement jsonElement => SerializeStreamedFinalizerJsonElement(jsonElement),
            string text when TryUseRawJsonObjectOrArray(text, out var rawJson) => rawJson,
            string text => JsonSerializer.Serialize(text, AgentOutputJson.SerializerOptions),
            _ => JsonSerializer.Serialize(payload, AgentOutputJson.SerializerOptions)
        };
    }

    private static string SerializeStreamedFinalizerJsonElement(JsonElement jsonElement)
    {
        if (jsonElement.ValueKind == JsonValueKind.String &&
            TryUseRawJsonObjectOrArray(jsonElement.GetString(), out var rawJson))
        {
            return rawJson;
        }

        return jsonElement.GetRawText();
    }

    private static bool TryUseRawJsonObjectOrArray(string? text, out string rawJson)
    {
        rawJson = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
            (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
        {
            rawJson = trimmed;
            return true;
        }

        return false;
    }

    private sealed class StreamedFinalizerInvocationRecorder(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode)
    {
        private readonly object gate = new();
        private readonly AgentFinalizerPolicy? policy = finalizerMode == AgentFinalizerMode.Required &&
                                                        AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var resolvedPolicy)
            ? resolvedPolicy
            : null;
        private readonly List<AgentFinalizerInvocation> finalizerInvocations = [];
        private readonly List<AgentToolInvocationTrace> toolInvocationTraces = [];
        private int nextSequence;

        public void Record(ToolCallContent toolCall)
        {
            var sequence = Interlocked.Increment(ref nextSequence);
            if (policy is null)
            {
                return;
            }

            var invocation = TryCreateStreamedFinalizerInvocation(policy, toolCall, sequence);
            if (invocation is null)
            {
                return;
            }

            lock (gate)
            {
                finalizerInvocations.Add(invocation);
                toolInvocationTraces.Add(new AgentToolInvocationTrace(
                    policy.ToolName,
                    ToolInvocationClassification.Read,
                    sequence,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    Succeeded: true,
                    FailureMessage: "Captured from a streamed required-finalizer tool call."));
            }
        }

        public IReadOnlyList<AgentFinalizerInvocation> SnapshotFinalizerInvocations()
        {
            lock (gate)
            {
                return finalizerInvocations.ToList();
            }
        }

        public IReadOnlyList<AgentToolInvocationTrace> SnapshotToolInvocationTraces()
        {
            lock (gate)
            {
                return toolInvocationTraces.ToList();
            }
        }
    }

    private static async Task<AgentRuntimeResponse?> TryCreateFinalizerResponseAfterEarlyFinalizerAsync(
        ProviderProfile provider,
        string model,
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        IReadOnlyList<AgentResponseUpdate> updates,
        string usageSourcePhase,
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
            toolInvocationTraces,
            CreateProviderUsageObservations(
                provider,
                model,
                runtimeSession,
                runtimeSessionKey,
                updates,
                usageSourcePhase,
                "Required finalizer was captured before the provider emitted final assistant prose."));
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
        ProviderProfile provider,
        string model,
        AgentStructuredOutputContract? structuredOutput,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        AgentFinalizerMode finalizerMode,
        Exception exception,
        IReadOnlyList<AgentResponseUpdate> updates,
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
            ToolInvocationTraces = toolInvocationTraces,
            UsageObservations = CreateProviderUsageObservations(
                provider,
                model,
                runtimeSession,
                runtimeSessionKey,
                updates,
                ProviderUsageSourcePhases.FinalizerRecovery,
                "Provider streaming failed after a valid required finalizer was captured.")
        };
    }

    private static async Task<AgentRuntimeResponse?> TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
        ProviderProfile provider,
        string model,
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        IReadOnlyList<AgentResponseUpdate> updates,
        string usageSourcePhase,
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
            toolInvocationTraces,
            CreateProviderUsageObservations(
                provider,
                model,
                runtimeSession,
                runtimeSessionKey,
                updates,
                usageSourcePhase,
                "Required finalizer short-circuited the provider response."));
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
        IReadOnlyList<AgentToolInvocationTrace> toolInvocationTraces,
        IReadOnlyList<ProviderUsageObservation> usageObservations)
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
            ToolInvocationTraces = toolInvocationTraces,
            UsageObservations = usageObservations
        };
    }

    private static IReadOnlyList<ProviderUsageObservation> CreateProviderUsageObservations(
        ProviderProfile provider,
        string model,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        IReadOnlyList<AgentResponseUpdate> updates,
        string sourcePhase,
        string diagnostic)
    {
        if (updates.Count == 0)
        {
            return
            [
                CreateMissingProviderUsageObservation(
                    provider,
                    model,
                    ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
                    sourcePhase,
                    diagnostic)
            ];
        }

        var response = updates.ToAgentResponse();
        var runtimeKey = ResolveRuntimeSessionKey(runtimeSession, response, runtimeSessionKey);
        var rawUsageJson = response.Usage is null
            ? string.Empty
            : JsonSerializer.Serialize(response.Usage, SerializerOptions);

        return
        [
            DefaultProviderUsageNormalizer.Instance.Normalize(new ProviderUsageNormalizationRequest(
                Provider: provider,
                Model: model,
                SourcePhase: sourcePhase,
                UsageStatus: response.Usage is null
                    ? ProviderUsageObservationStatus.UsageUnavailable
                    : ProviderUsageObservationStatus.Observed,
                InputTokens: ClampTokenCount(response.Usage?.InputTokenCount),
                CachedInputTokens: ClampTokenCount(response.Usage?.CachedInputTokenCount),
                OutputTokens: ClampTokenCount(response.Usage?.OutputTokenCount),
                ReasoningTokens: 0,
                TotalTokens: ClampTokenCount(response.Usage?.TotalTokenCount),
                ToolCallCount: CountToolCalls(response),
                ProviderResponseId: response.ResponseId ?? response.ContinuationToken?.ToString() ?? string.Empty,
                ProviderRequestId: string.Empty,
                RuntimeSessionKey: runtimeKey,
                RawUsageJson: rawUsageJson,
                DiagnosticsJson: JsonSerializer.Serialize(
                    new Dictionary<string, string>
                    {
                        ["diagnostic"] = diagnostic
                    },
                    SerializerOptions)))
        ];
    }

    private static string BuildProviderFailureDiagnostic(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var builder = new StringBuilder("Provider streaming failed before a successful runtime response.");
        var current = exception;
        var depth = 0;
        while (current is not null && depth < 4)
        {
            builder
                .Append(" Exception")
                .Append(depth)
                .Append('=')
                .Append(current.GetType().FullName ?? current.GetType().Name)
                .Append(": ")
                .Append(WorkflowExecutorRedaction.RedactText(current.Message))
                .Append('.');

            current = current.InnerException;
            depth++;
        }

        return builder.ToString();
    }

    private static ProviderUsageObservation CreateMissingProviderUsageObservation(
        ProviderProfile provider,
        string model,
        string runtimeSessionKey,
        string sourcePhase,
        string diagnostic)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: provider.Name,
            ProviderKind: provider.Kind,
            Model: model,
            TransportKind: provider.Transport,
            SourcePhase: sourcePhase,
            UsageStatus: ProviderUsageObservationStatus.MissingAfterProviderActivity,
            InputTokens: 0,
            CachedInputTokens: 0,
            OutputTokens: 0,
            ReasoningTokens: 0,
            TotalTokens: 0,
            ToolCallCount: 0)
        {
            RuntimeSessionKey = runtimeSessionKey,
            DiagnosticsJson = JsonSerializer.Serialize(
                new Dictionary<string, string>
                {
                    ["diagnostic"] = diagnostic
                },
                SerializerOptions)
        };
    }

    private static int ClampTokenCount(long? tokenCount)
    {
        if (!tokenCount.HasValue || tokenCount.Value <= 0)
        {
            return 0;
        }

        return tokenCount.Value > int.MaxValue
            ? int.MaxValue
            : (int)tokenCount.Value;
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
            text = text[..120] + $"...#{ComputeStableHash(text)}";
        }

        return $"\"{text}\"";
    }

    private static string ComputeStableHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes, 0, 6).ToLowerInvariant();
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
