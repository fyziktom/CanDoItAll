using Azure.AI.OpenAI;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OllamaSharp;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private const string OpenAiApiKeyEnvironmentVariable = "OPENAI_API_KEY";

    private static readonly IAgentProviderCredentialResolver FallbackProviderCredentialResolver = new EnvironmentVariableAgentProviderCredentialResolver();
    private static readonly TimeSpan ModelNetworkTimeout = TimeSpan.FromMinutes(10);
    private const long MaxPolicyInspectedScriptBytes = 128 * 1024;

    public async Task<AIAgent> CreateHostedAgentAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false,
        bool forceOmitTemperature = false,
        AgentRuntimeExecutionOptions? executionOptions = null)
    {
        var runtimeBuild = await CreateRuntimeBuildAsync(
            agent,
            provider,
            capabilities,
            memory,
            static (_, _, _) => Task.CompletedTask,
            cancellationToken,
            suppressApprovalRequirements,
            forceOmitTemperature,
            executionOptions);

        return new HostedRuntimeAgent(runtimeBuild);
    }

    private async Task<RuntimeBuildResult> CreateRuntimeBuildAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements = false,
        bool forceOmitTemperature = false,
        AgentRuntimeExecutionOptions? executionOptions = null)
    {
        var runtimeOptions = executionOptions ?? CreateDisabledRuntimeExecutionOptions(null);
        if (runtimeOptions.Handoff is not null)
        {
            return await CreateHandoffRuntimeBuildAsync(
                agent,
                provider,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                forceOmitTemperature,
                runtimeOptions);
        }

        var toolInvocationTraceRecorder = new ToolInvocationTraceRecorder();
        var openAiCredentialOverride = ResolveOpenAiCredentialOverride(provider);
        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiCredentialOverride);
        PromoteResolvedProviderCredentialEnvironment(effectiveProvider);
        var requestedModel = ManagedSeedProviderFallbacks.ResolveModel(agent, effectiveProvider, openAiCredentialOverride);
        var model = ResolveRuntimeModelForInputAttachments(effectiveProvider, requestedModel, runtimeOptions);
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException($"Provider '{effectiveProvider.Name}' does not have a default model and the agent '{agent.Name}' does not override one.");
        }

        if (!string.Equals(requestedModel, model, StringComparison.OrdinalIgnoreCase))
        {
            await progressCallback(
                ExecutionState.Preparing,
                "Input attachments",
                $"Using provider image-analysis model '{model}' for request-scoped image attachment(s) because runtime model '{requestedModel}' is not vision-capable.");
        }

        if (IsReasoningEffortConfiguredButTransportUnsupported(effectiveProvider, model, agent.ConfigurationJson))
        {
            throw new InvalidOperationException(BuildReasoningEffortUnsupportedTransportMessage(effectiveProvider, model));
        }

        EnsureStructuredOutputCapability(effectiveProvider, runtimeOptions);
        var finalizerCapture = CreateFinalizerCapture(runtimeOptions.StructuredOutput, runtimeOptions.FinalizerMode);
        var capabilityState = await CreateCapabilityStateCoreAsync(
            agent,
            effectiveProvider,
            model,
            capabilities,
            memory,
            progressCallback,
            cancellationToken,
            suppressApprovalRequirements,
            runtimeOptions.ContextWorkspaceScope ?? workspaceScope,
            runtimeOptions.ContextIntent ?? AgentRuntimeContextIntent.Empty);
        await FilterUnusableApprovalToolsAsync(
            capabilityState,
            effectiveProvider,
            suppressApprovalRequirements,
            progressCallback);
        if (finalizerCapture is not null)
        {
            capabilityState.Tools.AddRange(finalizerCapture.Tools);
            await progressCallback(
                ExecutionState.Preparing,
                "Finalizer policy",
                $"Attached {runtimeOptions.FinalizerMode} finalizer tool '{finalizerCapture.Policy.ToolName}' for structured output contract '{finalizerCapture.Policy.OutputContract.ContractKey}'.");
        }

        var frameworkManagedHistory = ShouldUseFrameworkManagedHistory(agent, effectiveProvider, runtimeOptions);
        var chatOptions = CreateModelCompatibleChatOptions(
            effectiveProvider,
            model,
            (float)agent.Temperature,
            forceOmitTemperature,
            agent.ConfigurationJson);
        chatOptions.Instructions = AppendFinalizerInstructions(
            agent.Instructions,
            finalizerCapture?.Policy,
            runtimeOptions.FinalizerMode,
            ShouldApplyStructuredOutputResponseFormat(runtimeOptions));
        chatOptions.AllowMultipleToolCalls = !capabilityState.HasApprovalTools;

        if (capabilityState.Tools.Count > 0)
        {
            chatOptions.Tools = [.. capabilityState.Tools];
        }

        var options = new ChatClientAgentOptions
        {
            Id = agent.Id.ToString("D"),
            Name = agent.Name,
            Description = agent.Summary,
            ChatOptions = chatOptions,
            AIContextProviders = capabilityState.ContextProviders,
            ChatHistoryProvider = frameworkManagedHistory ? CreateChatHistoryProvider() : null,
            RequirePerServiceCallChatHistoryPersistence = agent.RequirePerServiceCallChatHistoryPersistence
        };

        var runtimeAgent = CreateInstrumentedAgent(
            CreateFrameworkAgent(effectiveProvider, model, options, frameworkManagedHistory),
            effectiveProvider,
            agent,
            capabilityState,
            suppressApprovalRequirements,
            toolInvocationTraceRecorder,
            finalizerCapture?.Policy,
            runtimeOptions.FinalizerMode);
        return new RuntimeBuildResult(
            runtimeAgent,
            effectiveProvider,
            model,
            capabilityState.AsyncDisposables,
            capabilityState.Disposables,
            capabilityState.HasApprovalTools,
            ShouldOmitTemperature(effectiveProvider, model, forceOmitTemperature),
            finalizerCapture,
            toolInvocationTraceRecorder,
            capabilityState.ContextContributionTraceCollector,
            runtimeCapabilityState: capabilityState);
    }

    private async Task<RuntimeBuildResult> CreateHandoffRuntimeBuildAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements,
        bool forceOmitTemperature,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        var handoffOptions = runtimeOptions.Handoff
            ?? throw new InvalidOperationException("Handoff runtime build requires handoff execution options.");
        var settings = AgentHandoffMetadata.Normalize(handoffOptions.Settings);
        var validation = AgentHandoffMetadata.Validate(settings);
        if (!validation.Succeeded)
        {
            throw new InvalidOperationException("Agent handoff configuration is invalid: " + string.Join(" ", validation.Errors));
        }

        var participantIds = AgentHandoffMetadata.ResolveParticipantAgentIds(settings, handoffOptions.EntryAgentId);
        var missingParticipantIds = participantIds
            .Where(participantId => handoffOptions.Participants.All(item => item.Agent.Id != participantId))
            .Select(participantId => participantId.ToString("D"))
            .ToList();
        if (missingParticipantIds.Count > 0)
        {
            throw new InvalidOperationException("Handoff participants are incomplete: " + string.Join(", ", missingParticipantIds));
        }

        await progressCallback(
            ExecutionState.Preparing,
            "Handoff",
            $"Composing a Microsoft Agent Framework handoff workflow with {participantIds.Count} local participant agent(s).");

        var participantExecutionOptions = runtimeOptions with
        {
            Handoff = null
        };
        var participantBuilds = new List<RuntimeBuildResult>();
        try
        {
            var participantAgents = new Dictionary<Guid, AIAgent>();
            foreach (var participant in handoffOptions.Participants.Where(item => participantIds.Contains(item.Agent.Id)))
            {
                var participantBuild = await CreateRuntimeBuildAsync(
                    participant.Agent,
                    participant.Provider,
                    participant.Capabilities,
                    participant.Memory,
                    progressCallback,
                    cancellationToken,
                    suppressApprovalRequirements,
                    forceOmitTemperature,
                    participantExecutionOptions);
                participantBuilds.Add(participantBuild);
                participantAgents[participant.Agent.Id] = participantBuild.Agent;
            }

            var entryBuild = participantBuilds.FirstOrDefault(item =>
                    string.Equals(item.Agent.Id, handoffOptions.EntryAgentId.ToString("D"), StringComparison.OrdinalIgnoreCase))
                ?? participantBuilds.FirstOrDefault(item => item.Agent.Id == agent.Id.ToString("D"))
                ?? throw new InvalidOperationException($"Handoff entry agent '{handoffOptions.EntryAgentId:D}' was not built.");
            var buildResult = MafHandoffWorkflowFactory.Build(
                settings,
                participantAgents,
                handoffOptions.EntryAgentId,
                handoffOptions.CorrelationId);

            return new RuntimeBuildResult(
                buildResult.Agent,
                entryBuild.Provider,
                entryBuild.Model,
                participantBuilds,
                [],
                participantBuilds.Any(item => item.HasApprovalTools),
                entryBuild.IsTemperatureOmitted,
                finalizerCapture: null,
                toolInvocationTraceRecorder: null,
                contextContributionTraceCollector: null,
                snapshotFinalizerInvocations: () => participantBuilds
                    .SelectMany(item => item.SnapshotFinalizerInvocations())
                    .OrderBy(item => item.Sequence)
                    .ToList(),
                snapshotToolInvocationTraces: () => participantBuilds
                    .SelectMany(item => item.SnapshotToolInvocationTraces())
                    .OrderBy(item => item.Sequence)
                    .ToList(),
                snapshotContextContributionTraces: () => participantBuilds
                    .SelectMany(item => item.SnapshotContextContributionTraces())
                    .ToList());
        }
        catch
        {
            foreach (var participantBuild in participantBuilds)
            {
                await participantBuild.DisposeAsync();
            }

            throw;
        }
    }

    private AIAgent CreateFrameworkAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory)
    {
        return provider.Kind switch
        {
            ProviderKind.OpenAi => CreateOpenAiAgent(provider, model, options, frameworkManagedHistory),
            ProviderKind.AzureOpenAi => CreateAzureOpenAiAgent(provider, model, options, frameworkManagedHistory),
            ProviderKind.Ollama => CreateOllamaAgent(provider, model, options),
            _ => throw new InvalidOperationException($"Unsupported provider kind '{provider.Kind}'.")
        };
    }

    private AIAgent CreateOpenAiAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory)
    {
        var credential = ResolveProviderCredential(provider);
        if (!credential.IsResolved)
        {
            throw new InvalidOperationException(credential.FailureMessage);
        }

        PromoteProviderCredentialEnvironment(provider, credential);
        var clientOptions = CreateOpenAiClientOptions(provider);
        var client = new OpenAIClient(
            credential: new System.ClientModel.ApiKeyCredential(credential.ApiKey),
            options: clientOptions);

        return provider.Transport switch
        {
            ProviderTransportKind.ChatCompletions => client
                .GetChatClient(model)
                .AsAIAgent(options: options, services: services),
            ProviderTransportKind.Responses when frameworkManagedHistory => client
                .GetResponsesClient()
                .AsIChatClientWithStoredOutputDisabled(
                    model: model,
                    includeReasoningEncryptedContent: ShouldIncludeReasoningEncryptedContentForStoredOutputDisabledResponses(provider, frameworkManagedHistory))
                .AsAIAgent(options: options, services: services),
            ProviderTransportKind.Responses => client
                .GetResponsesClient()
                .AsAIAgent(options: options, model: model, services: services),
            _ => throw new InvalidOperationException($"Unsupported transport '{provider.Transport}' for provider '{provider.Name}'.")
        };
    }

    private AIAgent CreateAzureOpenAiAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory)
    {
        var credential = ResolveProviderCredential(provider);
        if (!credential.IsResolved)
        {
            throw new InvalidOperationException(credential.FailureMessage);
        }

        PromoteProviderCredentialEnvironment(provider, credential);
        var client = new AzureOpenAIClient(
            new Uri(provider.BaseUrl, UriKind.Absolute),
            new System.ClientModel.ApiKeyCredential(credential.ApiKey),
            new AzureOpenAIClientOptions
            {
                NetworkTimeout = ResolveProviderNetworkTimeout(provider)
            });

        return provider.Transport switch
        {
            ProviderTransportKind.ChatCompletions => client
                .GetChatClient(model)
                .AsAIAgent(options: options, services: services),
            ProviderTransportKind.Responses when frameworkManagedHistory => client
                .GetResponsesClient()
                .AsIChatClientWithStoredOutputDisabled(
                    model: model,
                    includeReasoningEncryptedContent: ShouldIncludeReasoningEncryptedContentForStoredOutputDisabledResponses(provider, frameworkManagedHistory))
                .AsAIAgent(options: options, services: services),
            ProviderTransportKind.Responses => client
                .GetResponsesClient()
                .AsAIAgent(options: options, model: model, services: services),
            _ => throw new InvalidOperationException($"Unsupported transport '{provider.Transport}' for provider '{provider.Name}'.")
        };
    }

    private static AIAgent CreateOllamaAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(provider.BaseUrl, UriKind.Absolute),
            Timeout = ResolveProviderNetworkTimeout(provider)
        };
        IChatClient chatClient = new OllamaApiClient(httpClient, model, jsonSerializerContext: null);
        return chatClient.AsAIAgent(options: options);
    }

    private static OpenAIClientOptions CreateOpenAiClientOptions(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var options = new OpenAIClientOptions
        {
            NetworkTimeout = ResolveProviderNetworkTimeout(provider)
        };
        if (!ShouldUseDefaultOpenAiEndpoint(provider.BaseUrl))
        {
            options.Endpoint = new Uri(provider.BaseUrl, UriKind.Absolute);
        }

        return options;
    }

    private static TimeSpan ResolveProviderNetworkTimeout(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrWhiteSpace(provider.ConfigurationJson))
        {
            return ModelNetworkTimeout;
        }

        try
        {
            using var document = JsonDocument.Parse(provider.ConfigurationJson);
            if (!document.RootElement.TryGetProperty("timeoutSeconds", out var timeoutElement) ||
                !timeoutElement.TryGetInt32(out var timeoutSeconds))
            {
                return ModelNetworkTimeout;
            }

            return TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 3600));
        }
        catch (JsonException)
        {
            return ModelNetworkTimeout;
        }
    }

    private static bool ShouldIncludeReasoningEncryptedContentForStoredOutputDisabledResponses(
        ProviderProfile provider,
        bool frameworkManagedHistory)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!frameworkManagedHistory)
        {
            return false;
        }

        if (provider.Transport != ProviderTransportKind.Responses)
        {
            return false;
        }

        return false;
    }

    private AIAgent CreateInstrumentedAgent(
        AIAgent agent,
        ProviderProfile provider,
        AgentDefinition agentDefinition,
        RuntimeCapabilityState capabilityState,
        bool suppressApprovalRequirements,
        ToolInvocationTraceRecorder toolInvocationTraceRecorder,
        AgentFinalizerPolicy? finalizerPolicy,
        AgentFinalizerMode finalizerMode)
    {
        var builder = agent.AsBuilder();
        var toolPolicy = new DefaultAgentToolInvocationPolicy();
        var knownToolNames = capabilityState.Tools
            .Select(tool => tool.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var toolName in capabilityState.FrameworkToolNames)
        {
            knownToolNames.Add(toolName);
        }

        var approvalWrappedToolNames = capabilityState.Tools
            .Where(tool => tool is ApprovalRequiredAIFunction)
            .Select(tool => tool.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var runtimeToolOwnershipByToolName = CreateRuntimeToolOwnershipByToolName(capabilityState);
        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrix(provider);
        var logger = services.GetService<ILogger<MafAgentRuntime>>();
        builder.UseLogging(
            services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance,
            logging => logging.JsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web));
        builder.Use(async (innerAgent, context, next, cancellationToken) =>
        {
            var functionName = context.Function?.Name ?? "unknown";
            var invocationArguments = ResolveFunctionInvocationArguments(context).ToArray();
            var redactedArguments = AgentToolInvocationPolicyMetadata.RedactArguments(invocationArguments);
            var isRequiredFinalizerTool = IsRequiredFinalizerTool(functionName, finalizerPolicy, finalizerMode);
            var classification = isRequiredFinalizerTool
                ? ToolInvocationClassification.Read
                : AgentToolInvocationPolicyMetadata.Classify(functionName);
            var auditScope = WorkspaceExecutionAuditContext.Current;
            var scriptSideEffectManifestJson = TryGetStringArgument(
                invocationArguments,
                GovernedScriptSideEffectManifest.ArgumentName) ?? string.Empty;
            var scriptInspection = ResolveScriptContentInspectionForPolicy(
                functionName,
                invocationArguments,
                auditScope,
                scriptSideEffectManifestJson);
            var policyContext = new ToolInvocationPolicyContext(
                AgentId: agentDefinition.Id,
                AgentName: agentDefinition.Name,
                ToolName: functionName,
                RedactedArguments: redactedArguments,
                Classification: classification,
                IsKnownTool: isRequiredFinalizerTool || knownToolNames.Contains(functionName),
                AutoApprovalAllowed: suppressApprovalRequirements,
                ApprovalWrapperAvailable: approvalWrappedToolNames.Contains(functionName),
                ExecutionRunId: auditScope?.ExecutionRunId.ToString("D") ?? string.Empty,
                SourceKind: auditScope?.SourceKind ?? string.Empty,
                ProcessRunId: auditScope?.ProcessRunId ?? string.Empty,
                ProcessStepId: auditScope?.ProcessStepId ?? string.Empty,
                AllowedExternalTargetAliases: auditScope?.AllowedExternalTargetAliases ?? [],
                ReadOnlyExternalTargetAliases: auditScope?.ReadOnlyExternalTargetAliases ?? [],
                ApprovalWrapperEffectiveForProvider: featureMatrix.SupportsApprovalRequiredAIFunction,
                ApplicationApprovalAvailable: false,
                ProcessScaffoldToolOnly: auditScope?.ProcessScaffoldToolOnly == true,
                ProcessAllowsProductMutation: auditScope?.ProcessAllowsProductMutation != false,
                ProcessStepAllowedOperations: auditScope?.ProcessStepAllowedOperations ?? [],
                ProcessStepTargetScope: auditScope?.ProcessStepTargetScope ?? string.Empty,
                ContextWorkspaceScopeKind: auditScope?.ContextWorkspaceScope?.Kind.ToString() ?? string.Empty,
                ContextWorkspaceScopeKey: auditScope?.ContextWorkspaceScope?.Key ?? string.Empty,
                InspectedScriptContent: scriptInspection.Content,
                ScriptInspectionFailure: scriptInspection.FailureMessage,
                ScriptSideEffectManifestJson: scriptSideEffectManifestJson,
                ToolInvocationTraces: toolInvocationTraceRecorder.Snapshot())
            {
                SourceId = auditScope?.SourceId ?? string.Empty
            };
            var policyDecision = await toolPolicy.EvaluateAsync(policyContext, cancellationToken);
            using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("maf.function.invoke", ActivityKind.Internal);
            AgentFrameworkTelemetry.ApplyCurrentAuditScope(activity);
            activity?.SetTag("agentframework.tool_name", functionName);
            activity?.SetTag("agentframework.tool_policy_decision", policyDecision.Kind.ToString());
            activity?.SetTag("agentframework.tool_policy_signature", policyDecision.Signature);
            activity?.SetTag("agentframework.tool_policy_reason", policyDecision.Reason);
            activity?.SetTag("agentframework.tool_approval_effective", policyContext.HasEffectiveApprovalPath);
            activity?.SetTag("agentframework.provider_supports_tool_approval", featureMatrix.SupportsApprovalRequiredAIFunction);
            activity?.SetTag("agentframework.tool_call_index", context.FunctionCallIndex);
            activity?.SetTag("agentframework.tool_iteration", context.Iteration);
            activity?.SetTag("agentframework.tool_count", context.FunctionCount);
            activity?.SetTag("agentframework.tool_is_streaming", context.IsStreaming);
            runtimeToolOwnershipByToolName.TryGetValue(functionName, out var runtimeToolOwnership);
            if (runtimeToolOwnership is not null)
            {
                activity?.SetTag("agentframework.runtime_tool_provider_key", runtimeToolOwnership.ProviderKey);
                activity?.SetTag("agentframework.runtime_tool_provider_name", runtimeToolOwnership.ProviderName);
            }

            logger?.LogInformation(
                "Agent tool policy decision {Decision} for tool {ToolName} on agent {AgentId}. ExecutionRunId={ExecutionRunId} SourceKind={SourceKind} ProcessRunId={ProcessRunId} ProcessStepId={ProcessStepId} RuntimeToolProviderKey={RuntimeToolProviderKey} Signature={Signature}",
                policyDecision.Kind,
                functionName,
                agentDefinition.Id,
                policyContext.ExecutionRunId,
                policyContext.SourceKind,
                policyContext.ProcessRunId,
                policyContext.ProcessStepId,
                runtimeToolOwnership?.ProviderKey ?? string.Empty,
                policyDecision.Signature);

            var traceSequence = toolInvocationTraceRecorder.Start(functionName, classification, policyDecision.Signature, runtimeToolOwnership);
            var succeeded = false;
            var failureMessage = string.Empty;
            using var runtimeToolOwnershipScope = AgentRuntimeToolOwnershipContext.BeginScope(runtimeToolOwnership);
            try
            {
                if (AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
                        functionName,
                        policyDecision,
                        policyContext,
                        out var policyDeniedResult))
                {
                    failureMessage = policyDeniedResult;
                    activity?.SetTag("agentframework.tool_policy_recoverable_denial", true);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    logger?.LogInformation(
                        "Returning recoverable policy denial for tool {ToolName} on governed run {ProcessRunId}, step {ProcessStepId}. Reason={Reason}",
                        functionName,
                        policyContext.ProcessRunId,
                        policyContext.ProcessStepId,
                        policyDecision.Reason);
                    return policyDeniedResult;
                }

                AgentToolPolicyBlockGuard.ThrowIfBlocked(
                    functionName,
                    policyDecision,
                    policyContext.HasEffectiveApprovalPath);

                var result = await next(context, cancellationToken);
                succeeded = IsSuccessfulToolInvocationResult(result);
                if (succeeded)
                {
                    toolPolicy.RecordSuccessfulInvocation(policyContext);
                    if (IsRequiredFinalizerTool(functionName, finalizerPolicy, finalizerMode))
                    {
                        logger?.LogInformation(
                            "Required finalizer tool {ToolName} was captured for agent {AgentId}. Ending the Microsoft Agent Framework turn without waiting for post-finalizer prose.",
                            functionName,
                            agentDefinition.Id);
                        throw new RequiredFinalizerCapturedException(functionName);
                    }
                }
                else
                {
                    failureMessage = ResolveToolInvocationFailureMessage(result);
                    activity?.SetStatus(ActivityStatusCode.Error, failureMessage);
                }

                return result;
            }
            catch (RequiredFinalizerCapturedException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failureMessage = exception.Message;
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                throw;
            }
            finally
            {
                toolInvocationTraceRecorder.Complete(traceSequence, succeeded, failureMessage);
            }
        });
        builder.UseOpenTelemetry(
            $"{AgentFrameworkTelemetry.SourceName}.Maf.{provider.Kind}",
            telemetry => telemetry.EnableSensitiveData = false);
        return builder.Build(services);
    }

    private static IReadOnlyDictionary<string, AgentRuntimeToolOwnership> CreateRuntimeToolOwnershipByToolName(
        RuntimeCapabilityState capabilityState)
    {
        if (capabilityState.RuntimeToolMetadata.Count == 0)
        {
            return new Dictionary<string, AgentRuntimeToolOwnership>(StringComparer.OrdinalIgnoreCase);
        }

        var descriptorsByKey = capabilityState.RuntimeToolProviderDescriptors
            .ToDictionary(
                descriptor => descriptor.ProviderKey,
                StringComparer.OrdinalIgnoreCase);
        var ownershipByToolName = new Dictionary<string, AgentRuntimeToolOwnership>(StringComparer.OrdinalIgnoreCase);
        foreach (var metadata in capabilityState.RuntimeToolMetadata)
        {
            descriptorsByKey.TryGetValue(metadata.ProviderKey, out var descriptor);
            ownershipByToolName[metadata.ToolName] = new AgentRuntimeToolOwnership(
                metadata.ProviderKey,
                descriptor?.DisplayName ?? metadata.ProviderKey,
                metadata.ToolName);
        }

        return ownershipByToolName;
    }

    private static bool IsRequiredFinalizerTool(
        string functionName,
        AgentFinalizerPolicy? finalizerPolicy,
        AgentFinalizerMode finalizerMode)
    {
        return finalizerMode == AgentFinalizerMode.Required &&
               finalizerPolicy is { IsRequired: true } &&
               string.Equals(functionName, finalizerPolicy.ToolName, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsSuccessfulToolInvocationResult(object? result)
    {
        return TryResolveToolInvocationSuccess(result, [], out var succeeded)
            ? succeeded
            : true;
    }

    internal static string ResolveToolInvocationFailureMessage(object? result)
    {
        return TryResolveToolInvocationFailureMessage(result, [], out var message)
            ? message
            : "Tool invocation returned an unsuccessful result.";
    }

    private static bool TryResolveToolInvocationSuccess(
        object? result,
        HashSet<object> visited,
        out bool succeeded)
    {
        succeeded = true;
        if (result is null)
        {
            return false;
        }

        if (result is string text)
        {
            if (TextIndicatesToolFailure(text))
            {
                succeeded = false;
                return true;
            }

            return false;
        }

        var type = result.GetType();
        if (!type.IsValueType && !visited.Add(result))
        {
            return false;
        }

        if (TryReadBooleanProperty(result, "Succeeded", out succeeded) ||
            TryReadBooleanProperty(result, "Success", out succeeded) ||
            TryReadBooleanProperty(result, "IsSuccess", out succeeded))
        {
            return true;
        }

        if (TryReadFailureExitSummary(result, out succeeded))
        {
            return true;
        }

        foreach (var propertyName in ResultEnvelopePropertyNames)
        {
            if (TryReadObjectProperty(result, propertyName, out var propertyValue) &&
                TryResolveToolInvocationSuccess(propertyValue, visited, out succeeded))
            {
                return true;
            }
        }

        if (result is System.Collections.IEnumerable enumerable)
        {
            var sawResolvedResult = false;
            foreach (var item in enumerable)
            {
                if (!TryResolveToolInvocationSuccess(item, visited, out var itemSucceeded))
                {
                    continue;
                }

                sawResolvedResult = true;
                if (!itemSucceeded)
                {
                    succeeded = false;
                    return true;
                }
            }

            if (sawResolvedResult)
            {
                succeeded = true;
                return true;
            }
        }

        var resultText = result.ToString();
        if (TextIndicatesToolFailure(resultText))
        {
            succeeded = false;
            return true;
        }

        return false;
    }

    private static bool TryResolveToolInvocationFailureMessage(
        object? result,
        HashSet<object> visited,
        out string message)
    {
        message = string.Empty;
        if (result is null)
        {
            return false;
        }

        if (result is string text)
        {
            message = text;
            return !string.IsNullOrWhiteSpace(message);
        }

        var type = result.GetType();
        if (!type.IsValueType && !visited.Add(result))
        {
            return false;
        }

        if (TryReadStringProperty(result, "Message", out message) ||
            TryReadStringProperty(result, "ErrorMessage", out message) ||
            TryReadStringProperty(result, "FailureMessage", out message))
        {
            return true;
        }

        if (TryReadStringProperty(result, "StderrPreview", out message))
        {
            return true;
        }

        foreach (var propertyName in ResultEnvelopePropertyNames)
        {
            if (TryReadObjectProperty(result, propertyName, out var propertyValue) &&
                TryResolveToolInvocationFailureMessage(propertyValue, visited, out message))
            {
                return true;
            }
        }

        if (result is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (TryResolveToolInvocationFailureMessage(item, visited, out message))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static readonly string[] ResultEnvelopePropertyNames =
    [
        "Result",
        "Value",
        "Content",
        "Contents",
        "Data"
    ];

    private static bool TryReadBooleanProperty(object instance, string propertyName, out bool value)
    {
        value = false;
        var property = instance.GetType().GetProperty(propertyName);
        if (property?.PropertyType == typeof(bool) &&
            property.GetIndexParameters().Length == 0 &&
            property.GetValue(instance) is bool propertyValue)
        {
            value = propertyValue;
            return true;
        }

        return false;
    }

    private static bool TryReadStringProperty(object instance, string propertyName, out string value)
    {
        value = string.Empty;
        var property = instance.GetType().GetProperty(propertyName);
        if (property?.PropertyType == typeof(string) &&
            property.GetIndexParameters().Length == 0 &&
            property.GetValue(instance) is string propertyValue &&
            !string.IsNullOrWhiteSpace(propertyValue))
        {
            value = propertyValue;
            return true;
        }

        return false;
    }

    private static bool TryReadObjectProperty(object instance, string propertyName, out object? value)
    {
        value = null;
        var property = instance.GetType().GetProperty(propertyName);
        if (property is null ||
            property.GetIndexParameters().Length != 0)
        {
            return false;
        }

        value = property.GetValue(instance);
        return value is not null;
    }

    private static bool TryReadFailureExitSummary(object instance, out bool succeeded)
    {
        succeeded = true;
        if (!TryReadStringProperty(instance, "ExitSummary", out var exitSummary))
        {
            return false;
        }

        if (exitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
            exitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase))
        {
            succeeded = false;
            return true;
        }

        return false;
    }

    private static bool TextIndicatesToolFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Succeeded = False", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Succeeded=False", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("\"succeeded\":false", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("succeeded: false", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ExitSummary = Denied", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ExitSummary: Denied", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ExitSummary = Failed", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ExitSummary: Failed", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureStructuredOutputCapability(
        ProviderProfile provider,
        AgentStructuredOutputContract? structuredOutput)
    {
        if (structuredOutput is null)
        {
            return;
        }

        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrix(provider);
        if (featureMatrix.SupportsStructuredOutput)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Provider '{provider.Name}' using transport '{provider.Transport}' cannot enforce structured output contract '{structuredOutput.ContractKey}'. Choose a structured-output capable OpenAI/Azure OpenAI provider or disable the machine-critical structured-output request.");
    }

    private static void EnsureStructuredOutputCapability(
        ProviderProfile provider,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        if (runtimeOptions.StructuredOutput is not null)
        {
            if (runtimeOptions.FinalizerMode == AgentFinalizerMode.Required &&
                AgentFinalizerPolicies.TryResolveForStructuredOutput(runtimeOptions.StructuredOutput, out _))
            {
                return;
            }

            EnsureStructuredOutputCapability(provider, runtimeOptions.StructuredOutput);
            return;
        }

        if (!runtimeOptions.RequireJsonResponseFormat)
        {
            return;
        }

        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrix(provider);
        if (featureMatrix.SupportsStructuredOutput)
        {
            return;
        }

        var schemaName = string.IsNullOrWhiteSpace(runtimeOptions.ResponseFormatSchemaName)
            ? "JSON"
            : runtimeOptions.ResponseFormatSchemaName;
        throw new InvalidOperationException(
            $"Provider '{provider.Name}' using transport '{provider.Transport}' cannot enforce workflow JSON response format '{schemaName}'. Choose a structured-output capable OpenAI/Azure OpenAI provider or use a non-JSON workflow component.");
    }

    private static AgentRuntimeExecutionOptions NormalizeRuntimeExecutionOptions(
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeExecutionOptions? executionOptions)
    {
        if (executionOptions is null)
        {
            return CreateDisabledRuntimeExecutionOptions(structuredOutput);
        }

        if (structuredOutput is not null &&
            executionOptions.StructuredOutput is not null &&
            !string.Equals(executionOptions.StructuredOutput.ContractKey, structuredOutput.ContractKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Runtime execution options contract '{executionOptions.StructuredOutput.ContractKey}' does not match the requested structured output contract '{structuredOutput.ContractKey}'.");
        }

        return executionOptions.StructuredOutput is null && structuredOutput is not null
            ? executionOptions with { StructuredOutput = structuredOutput }
            : executionOptions;
    }

    private static AgentRuntimeExecutionOptions CreateDisabledRuntimeExecutionOptions(
        AgentStructuredOutputContract? structuredOutput)
    {
        return new AgentRuntimeExecutionOptions(
            StructuredOutput: structuredOutput,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: true,
            MaxStructuredOutputRepairAttempts: 0);
    }

    private async Task FilterUnusableApprovalToolsAsync(
        RuntimeCapabilityState capabilityState,
        ProviderProfile provider,
        bool suppressApprovalRequirements,
        Func<ExecutionState, string, string, Task> progressCallback)
    {
        if (suppressApprovalRequirements)
        {
            capabilityState.HasApprovalTools = capabilityState.Tools.Any(tool => tool is ApprovalRequiredAIFunction);
            return;
        }

        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrix(provider);
        if (featureMatrix.SupportsApprovalRequiredAIFunction)
        {
            return;
        }

        var unusableMutationTools = capabilityState.Tools
            .Where(tool => tool is ApprovalRequiredAIFunction)
            .Where(tool => AgentToolInvocationPolicyMetadata.Classify(tool.Name) == ToolInvocationClassification.Mutation)
            .ToList();
        if (unusableMutationTools.Count == 0)
        {
            return;
        }

        var toolNames = unusableMutationTools
            .Select(tool => tool.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var toolList = string.Join(", ", toolNames);
        if (IsGovernedProcessAutomationRun())
        {
            throw new InvalidOperationException(
                $"Provider '{provider.Name}' using transport '{provider.Transport}' cannot expose mutation tools that require MAF approval because no effective approval path is available. Unusable tools: {toolList}.");
        }

        capabilityState.Tools.RemoveAll(unusableMutationTools.Contains);
        capabilityState.HasApprovalTools = capabilityState.Tools.Any(tool => tool is ApprovalRequiredAIFunction);
        await progressCallback(
            ExecutionState.Preparing,
            "Approval policy",
            $"Omitted mutation tool(s) that require MAF approval because provider '{provider.Name}' using transport '{provider.Transport}' has no effective approval path: {toolList}.");
    }

    private static FinalizerCapture? CreateFinalizerCapture(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode)
    {
        if (finalizerMode == AgentFinalizerMode.Disabled)
        {
            return null;
        }

        if (!AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var policy))
        {
            return null;
        }

        var capture = new FinalizerCapture(policy);
        var tool = policy.OutputType switch
        {
            Type type when type == typeof(ProcessStepOutcomeResult) => AIFunctionFactory.Create(
                capture.SubmitProcessStepOutcome,
                policy.ToolName,
                "Submits the final process-step outcome exactly once as typed machine-readable arguments."),
            Type type when type == typeof(CodeReviewResult) => AIFunctionFactory.Create(
                capture.SubmitCodeReviewResult,
                policy.ToolName,
                "Submits the final code-review result exactly once as typed machine-readable arguments."),
            Type type when type == typeof(ArchitectureReviewResult) => AIFunctionFactory.Create(
                capture.SubmitArchitectureReviewResult,
                policy.ToolName,
                "Submits the final architecture-review result exactly once as typed machine-readable arguments."),
            Type type when type == typeof(ImplementationPlanResult) => AIFunctionFactory.Create(
                capture.SubmitImplementationPlan,
                policy.ToolName,
                "Submits the final implementation plan exactly once as typed machine-readable arguments."),
            Type type when type == typeof(TestPlanResult) => AIFunctionFactory.Create(
                capture.SubmitTestPlan,
                policy.ToolName,
                "Submits the final test plan exactly once as typed machine-readable arguments."),
            Type type when type == typeof(ToolExecutionDecisionResult) => AIFunctionFactory.Create(
                capture.SubmitToolExecutionDecision,
                policy.ToolName,
                "Submits the final tool-execution decision exactly once as typed machine-readable arguments."),
            Type type when type == typeof(ProcessStatePatch) => AIFunctionFactory.Create(
                capture.SubmitProcessStatePatch,
                policy.ToolName,
                "Submits the final process-state patch exactly once as typed machine-readable arguments."),
            Type type when type == typeof(HumanEscalationRequest) => AIFunctionFactory.Create(
                capture.SubmitHumanEscalationRequest,
                policy.ToolName,
                "Submits the final human-escalation request exactly once as typed machine-readable arguments."),
            _ => null
        };
        if (tool is null)
        {
            return null;
        }

        capture.Tools.Add(tool);
        return capture;
    }

    private static string AppendFinalizerInstructions(
        string instructions,
        AgentFinalizerPolicy? finalizerPolicy,
        AgentFinalizerMode finalizerMode,
        bool hasStructuredResponseFormat)
    {
        if (finalizerPolicy is null || finalizerMode == AgentFinalizerMode.Disabled)
        {
            return instructions;
        }

        var header = finalizerMode == AgentFinalizerMode.Shadow
            ? "Finalizer tool shadow policy:"
            : "Finalizer tool policy:";
        var finalizerInstructions = finalizerMode == AgentFinalizerMode.Shadow
            ? $"{Environment.NewLine}{Environment.NewLine}{header}{Environment.NewLine}" +
              $"- You may call `{finalizerPolicy.ToolName}` at most once before finishing to produce a comparison copy.{Environment.NewLine}" +
              (hasStructuredResponseFormat
                  ? $"- The final assistant response JSON is the source of truth. Return exactly one JSON object matching `{finalizerPolicy.OutputContract.ContractKey}` through the configured structured response format.{Environment.NewLine}" +
                    "- Do not use Markdown, prose, code fences, or any extra text around the JSON object."
                  : "- The final assistant response remains the source of truth because no structured response format is attached.")
            : $"{Environment.NewLine}{Environment.NewLine}{header}{Environment.NewLine}" +
              $"- Call `{finalizerPolicy.ToolName}` exactly once after all other significant tool work is complete.{Environment.NewLine}" +
              "- A normal assistant response without that finalizer tool is invalid for this run and will fail the execution even if the work itself succeeded." + Environment.NewLine +
              $"- The finalizer arguments are the authoritative machine output for `{finalizerPolicy.OutputContract.ContractKey}`.{Environment.NewLine}" +
              BuildRequiredFinalizerArgumentInstructions(finalizerPolicy) +
              (hasStructuredResponseFormat
                  ? $"- After the tool call, return exactly one JSON object matching the same `{finalizerPolicy.OutputContract.ContractKey}` schema through the configured structured response format.{Environment.NewLine}" +
                    "- Do not use Markdown, prose, code fences, or any extra text around the JSON object."
                  : "- Do not emit separate machine output after the finalizer call.") + Environment.NewLine +
              "- Do not call any other `submit_*` finalizer tool for this contract.";
        return string.IsNullOrWhiteSpace(instructions)
            ? finalizerInstructions.Trim()
            : instructions.TrimEnd() + finalizerInstructions;
    }

    private static string BuildRequiredFinalizerArgumentInstructions(AgentFinalizerPolicy finalizerPolicy)
    {
        if (!string.Equals(
                finalizerPolicy.OutputContract.ContractKey,
                AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return "- Pass exactly one `result` object argument to `submit_process_step_outcome`; do not pass scalar `result`, `status`, `reason`, or `evidenceRefs` as sibling arguments." + Environment.NewLine +
               "- The `result` object must include `status`, `reason`, `branchOutcomeKey`, `branchOutcomeTitle`, `evidenceRefs`, `nextActions`, and `humanReadableSummaryMarkdown`. Use `Completed`, `Blocked`, `Failed`, `WaitingApproval`, or `Refused` for `status`." + Environment.NewLine +
               "- Do not copy placeholder evidence values. Evidence refs must be exact current-run refs already created or observed during this turn." + Environment.NewLine +
               "- If `status` is `Completed`, `evidenceRefs` must contain at least one concrete current-run evidence reference. If no such evidence exists, return `Blocked` or `Failed` with a concrete `nextActions` entry instead of claiming completion." + Environment.NewLine;
    }

    private ScriptContentInspection ResolveScriptContentInspectionForPolicy(
        string functionName,
        IReadOnlyList<KeyValuePair<string, object?>> arguments,
        WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope,
        string scriptSideEffectManifestJson)
    {
        if (!IsWorkspaceScriptExecutionTool(functionName))
        {
            return ScriptContentInspection.Empty;
        }

        var scriptPath = TryGetStringArgument(arguments, "path");
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return new ScriptContentInspection(
                string.Empty,
                "script invocation did not provide a path argument.");
        }

        if (!TryResolvePolicyReadableScriptPath(scriptPath, auditScope, out var fullPath, out var failureMessage))
        {
            return new ScriptContentInspection(string.Empty, failureMessage);
        }

        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists)
            {
                return new ScriptContentInspection(
                    string.Empty,
                    $"script path '{scriptPath}' does not exist.");
            }

            if (fileInfo.Length > MaxPolicyInspectedScriptBytes)
            {
                return new ScriptContentInspection(
                    string.Empty,
                    $"script path '{scriptPath}' is larger than the {MaxPolicyInspectedScriptBytes} byte policy inspection limit.");
            }

            var inspectedContent = File.ReadAllText(fullPath);
            if (GovernedScriptSideEffectManifest.TryParse(
                    scriptSideEffectManifestJson,
                    out var manifest,
                    out _) &&
                manifest.DeclaredChildScripts.Length > 0)
            {
                var childInspection = ResolveDeclaredChildScriptInspection(manifest, auditScope);
                if (!string.IsNullOrWhiteSpace(childInspection.FailureMessage))
                {
                    return childInspection;
                }

                inspectedContent = string.Join(
                    Environment.NewLine,
                    inspectedContent,
                    childInspection.Content);
            }

            return new ScriptContentInspection(inspectedContent, string.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new ScriptContentInspection(
                string.Empty,
                $"script path '{scriptPath}' could not be read for policy inspection: {exception.Message}");
        }
    }

    private ScriptContentInspection ResolveDeclaredChildScriptInspection(
        GovernedScriptSideEffectManifest manifest,
        WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope)
    {
        var inspectedChildScripts = new List<string>();
        foreach (var childScript in manifest.DeclaredChildScripts)
        {
            if (!TryResolvePolicyReadableScriptPath(childScript, auditScope, out var childFullPath, out var failureMessage))
            {
                return new ScriptContentInspection(
                    string.Empty,
                    $"declared child script '{childScript}' could not be resolved for policy inspection: {failureMessage}");
            }

            try
            {
                var childFileInfo = new FileInfo(childFullPath);
                if (!childFileInfo.Exists)
                {
                    return new ScriptContentInspection(
                        string.Empty,
                        $"declared child script '{childScript}' does not exist.");
                }

                if (childFileInfo.Length > MaxPolicyInspectedScriptBytes)
                {
                    return new ScriptContentInspection(
                        string.Empty,
                        $"declared child script '{childScript}' is larger than the {MaxPolicyInspectedScriptBytes} byte policy inspection limit.");
                }

                inspectedChildScripts.Add(DefaultAgentToolInvocationPolicy.BuildInspectedChildScriptMarker(childScript));
                inspectedChildScripts.Add(File.ReadAllText(childFullPath));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return new ScriptContentInspection(
                    string.Empty,
                    $"declared child script '{childScript}' could not be read for policy inspection: {exception.Message}");
            }
        }

        return new ScriptContentInspection(
            string.Join(Environment.NewLine, inspectedChildScripts),
            string.Empty);
    }

    private bool TryResolvePolicyReadableScriptPath(
        string scriptPath,
        WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope,
        out string fullPath,
        out string failureMessage)
    {
        fullPath = string.Empty;
        failureMessage = string.Empty;

        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(scriptPath);
        if (!string.IsNullOrWhiteSpace(normalizedAlias) &&
            normalizedAlias.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
        {
            if (auditScope is not null)
            {
                var readableAliases = auditScope.AllowedExternalTargetAliases
                    .Concat(auditScope.ReadOnlyExternalTargetAliases)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (!AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(normalizedAlias, readableAliases))
                {
                    failureMessage = $"script path '{normalizedAlias}' is outside the current run external-target boundary.";
                    return false;
                }
            }

            return TryMapExternalTargetAliasToFullPath(normalizedAlias, out fullPath, out failureMessage);
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(scriptPath.Trim());
        if (Path.IsPathRooted(expandedPath))
        {
            fullPath = Path.GetFullPath(expandedPath);
            if (!IsPathWithinRoot(fullPath, workspaceRoot))
            {
                failureMessage = $"absolute script path '{scriptPath}' is outside the workspace root.";
                fullPath = string.Empty;
                return false;
            }

            return true;
        }

        var scopedRelativePath = ApplyManagedRootScopeForPolicy(WorkspaceScopeDescriptor.NormalizeRelativePath(expandedPath));
        if (string.IsNullOrWhiteSpace(scopedRelativePath))
        {
            failureMessage = "script path resolved to an empty workspace-relative path.";
            return false;
        }

        fullPath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            scopedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (IsPathWithinRoot(fullPath, workspaceRoot))
        {
            return true;
        }

        failureMessage = $"script path '{scriptPath}' resolves outside the workspace root.";
        fullPath = string.Empty;
        return false;
    }

    private static bool TryMapExternalTargetAliasToFullPath(
        string alias,
        out string fullPath,
        out string failureMessage)
    {
        fullPath = string.Empty;
        failureMessage = string.Empty;

        var segments = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2 ||
            !string.Equals(segments[0], "external-target", StringComparison.OrdinalIgnoreCase) ||
            segments[1].Length != 1 ||
            !char.IsLetter(segments[1][0]))
        {
            failureMessage = $"script path '{alias}' uses invalid external-target syntax.";
            return false;
        }

        var driveRoot = $"{char.ToUpperInvariant(segments[1][0])}:{Path.DirectorySeparatorChar}";
        fullPath = segments.Length == 2
            ? driveRoot
            : Path.GetFullPath(Path.Combine(driveRoot, Path.Combine(segments.Skip(2).ToArray())));
        return true;
    }

    private string ApplyManagedRootScopeForPolicy(string relativePath)
    {
        if (workspaceScope.IsDefaultSandbox)
        {
            return relativePath;
        }

        return TryMapManagedRootForPolicy(relativePath, "artifacts", workspaceScope.ArtifactRootRelativePath)
            ?? TryMapManagedRootForPolicy(relativePath, "output", workspaceScope.OutputRootRelativePath)
            ?? TryMapManagedRootForPolicy(relativePath, "integration-map", workspaceScope.IntegrationMapRootRelativePath)
            ?? TryMapManagedRootForPolicy(relativePath, "data", workspaceScope.DataRootRelativePath)
            ?? relativePath;
    }

    private static string? TryMapManagedRootForPolicy(
        string relativePath,
        string rootName,
        string scopedRootRelativePath)
    {
        if (!MatchesPolicyPathRoot(relativePath, rootName) ||
            MatchesPolicyPathRoot(relativePath, scopedRootRelativePath) ||
            relativePath.StartsWith($"{rootName}/scopes/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var suffix = string.Equals(relativePath, rootName, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : relativePath[(rootName.Length + 1)..];
        return string.IsNullOrWhiteSpace(suffix)
            ? scopedRootRelativePath
            : WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(scopedRootRelativePath, suffix));
    }

    private static bool MatchesPolicyPathRoot(string relativePath, string rootRelativePath)
    {
        return string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith(rootRelativePath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetStringArgument(
        IEnumerable<KeyValuePair<string, object?>> arguments,
        string argumentName)
    {
        foreach (var argument in arguments)
        {
            if (!string.Equals(argument.Key, argumentName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return argument.Value switch
            {
                string value => value,
                JsonElement { ValueKind: JsonValueKind.String } value => value.GetString(),
                null => null,
                _ => argument.Value.ToString()
            };
        }

        return null;
    }

    private static bool IsWorkspaceScriptExecutionTool(string functionName)
    {
        return string.Equals(functionName, AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(functionName, AgentToolInvocationPolicyMetadata.WorkspacePythonRunFile, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<KeyValuePair<string, object?>> ResolveFunctionInvocationArguments(object context)
    {
        foreach (var propertyName in new[] { "Arguments", "FunctionArguments", "FunctionCallArguments" })
        {
            var property = context.GetType().GetProperty(propertyName);
            if (property?.GetValue(context) is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                return pairs;
            }
        }

        return [];
    }

    private static ChatHistoryProvider CreateChatHistoryProvider()
    {
        return new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions
        {
            StorageInputRequestMessageFilter = messages => messages.Where(message =>
                message.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.AIContextProvider &&
                message.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.ChatHistory)
                .ToList()
        });
    }

    private ProviderCredentialResolution ResolveProviderCredential(ProviderProfile provider)
    {
        var primaryResolution = services.GetService<IAgentProviderCredentialResolver>()?.Resolve(provider);
        if (primaryResolution is { IsResolved: true })
        {
            return primaryResolution;
        }

        var configurationFallback = TryResolveConfigurationCredential(provider);
        if (configurationFallback.IsResolved)
        {
            return configurationFallback;
        }

        var environmentFallback = FallbackProviderCredentialResolver.Resolve(provider);
        if (environmentFallback.IsResolved)
        {
            return environmentFallback;
        }

        if (primaryResolution is null)
        {
            return environmentFallback;
        }

        return BuildUnresolvedCredentialResult(provider, primaryResolution, environmentFallback);
    }

    private ProviderCredentialResolution TryResolveConfigurationCredential(ProviderProfile provider)
    {
        var configuration = services.GetService<IConfiguration>();
        if (configuration is null)
        {
            return new ProviderCredentialResolution(string.Empty, "application configuration", "Application configuration is not available.");
        }

        foreach (var key in EnumerateCredentialConfigurationKeys(provider))
        {
            var configuredValue = configuration[key];
            if (string.IsNullOrWhiteSpace(configuredValue))
            {
                continue;
            }

            var trimmedValue = configuredValue.Trim();
            PromoteResolvedConfigurationCredential(provider, key, trimmedValue);
            return new ProviderCredentialResolution(
                trimmedValue,
                $"application configuration key '{key}'",
                string.Empty);
        }

        return new ProviderCredentialResolution(
            string.Empty,
            "application configuration",
            "No matching application configuration key contained a usable API key.");
    }

    private IEnumerable<string> EnumerateCredentialConfigurationKeys(ProviderProfile provider)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable))
        {
            var providerKey = provider.ApiKeyEnvironmentVariable.Trim();
            if (seen.Add(providerKey))
            {
                yield return providerKey;
            }
        }

        if (provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi &&
            seen.Add(OpenAiApiKeyEnvironmentVariable))
        {
            yield return OpenAiApiKeyEnvironmentVariable;
        }
    }

    private static ProviderCredentialResolution BuildUnresolvedCredentialResult(
        ProviderProfile provider,
        ProviderCredentialResolution primaryResolution,
        ProviderCredentialResolution environmentFallback)
    {
        var failureMessages = new[]
            {
                primaryResolution.FailureMessage,
                environmentFallback.FailureMessage
            }
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var failureMessage = failureMessages.Count == 0
            ? $"Provider '{provider.Name}' did not resolve a usable API key."
            : string.Join(" ", failureMessages);

        return new ProviderCredentialResolution(
            string.Empty,
            primaryResolution.ResolutionSource,
            failureMessage);
    }

    private static void PromoteResolvedConfigurationCredential(
        ProviderProfile provider,
        string configurationKey,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable))
        {
            AgentProviderEnvironmentCredential.PromoteProcessValue(provider.ApiKeyEnvironmentVariable, value);
        }

        if (provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi ||
            string.Equals(configurationKey, OpenAiApiKeyEnvironmentVariable, StringComparison.OrdinalIgnoreCase))
        {
            AgentProviderEnvironmentCredential.PromoteProcessValue(OpenAiApiKeyEnvironmentVariable, value);
        }
    }

    private static void PromoteProviderCredentialEnvironment(
        ProviderProfile provider,
        ProviderCredentialResolution credential)
    {
        if (!credential.IsResolved)
        {
            return;
        }

        if (!credential.ShouldPromoteToProcessEnvironment)
        {
            return;
        }

        AgentProviderEnvironmentCredential.PromoteProcessValue(provider.ApiKeyEnvironmentVariable, credential.ApiKey);
        if (provider.Kind == ProviderKind.OpenAi)
        {
            AgentProviderEnvironmentCredential.PromoteProcessValue(OpenAiApiKeyEnvironmentVariable, credential.ApiKey);
        }
    }

    private string ResolveOpenAiCredentialOverride(ProviderProfile provider)
    {
        if (provider.Kind is not (ProviderKind.OpenAi or ProviderKind.AzureOpenAi))
        {
            return "resolved";
        }

        var credential = ResolveProviderCredential(provider);
        if (!credential.IsResolved)
        {
            return string.Empty;
        }

        PromoteProviderCredentialEnvironment(provider, credential);
        return "resolved";
    }

    private void PromoteResolvedProviderCredentialEnvironment(
        ProviderProfile provider)
    {
        if (provider.Kind is not (ProviderKind.OpenAi or ProviderKind.AzureOpenAi))
        {
            return;
        }

        var credential = ResolveProviderCredential(provider);
        if (credential.IsResolved)
        {
            PromoteProviderCredentialEnvironment(provider, credential);
        }
    }

    private static string ResolveHealthCheckModel(
        ProviderProfile provider,
        IEnumerable<string> candidateModels,
        string fallbackModel)
    {
        if (!string.IsNullOrWhiteSpace(provider.DefaultModel))
        {
            return provider.DefaultModel;
        }

        var discoveredModel = candidateModels.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        return string.IsNullOrWhiteSpace(discoveredModel) ? fallbackModel : discoveredModel;
    }

    private static bool ShouldUseDefaultOpenAiEndpoint(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return true;
        }

        var normalized = baseUrl.Trim().TrimEnd('/');
        return normalized.Equals("https://api.openai.com", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("https://api.openai.com/v1", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RuntimeBuildResult(
        AIAgent agent,
        ProviderProfile provider,
        string model,
        IReadOnlyList<IAsyncDisposable> asyncDisposables,
        IReadOnlyList<IDisposable> disposables,
        bool hasApprovalTools,
        bool isTemperatureOmitted,
        FinalizerCapture? finalizerCapture,
        ToolInvocationTraceRecorder? toolInvocationTraceRecorder,
        AgentContextContributionTraceCollector? contextContributionTraceCollector,
        Func<IReadOnlyList<AgentFinalizerInvocation>>? snapshotFinalizerInvocations = null,
        Func<IReadOnlyList<AgentToolInvocationTrace>>? snapshotToolInvocationTraces = null,
        Func<IReadOnlyList<AgentContextContributionTrace>>? snapshotContextContributionTraces = null,
        RuntimeCapabilityState? runtimeCapabilityState = null) : IAsyncDisposable
    {
        public AIAgent Agent { get; } = agent;

        public ProviderProfile Provider { get; } = provider;

        public string Model { get; } = model;

        public bool HasApprovalTools { get; } = hasApprovalTools;

        public bool IsTemperatureOmitted { get; } = isTemperatureOmitted;

        public RuntimeCapabilityState? CapabilityState { get; } = runtimeCapabilityState;

        public IReadOnlyList<AITool> FinalizerTools { get; } = finalizerCapture?.Tools ?? [];

        public ToolInvocationTraceRecorder? ToolInvocationTraceRecorder { get; } = toolInvocationTraceRecorder;

        public IReadOnlyList<AgentFinalizerInvocation> SnapshotFinalizerInvocations()
            => snapshotFinalizerInvocations?.Invoke() ?? finalizerCapture?.Snapshot() ?? [];

        public IReadOnlyList<AgentToolInvocationTrace> SnapshotToolInvocationTraces()
            => snapshotToolInvocationTraces?.Invoke() ?? ToolInvocationTraceRecorder?.Snapshot() ?? [];

        public IReadOnlyList<AgentContextContributionTrace> SnapshotContextContributionTraces()
            => snapshotContextContributionTraces?.Invoke() ?? contextContributionTraceCollector?.Snapshot() ?? [];

        public async ValueTask DisposeAsync()
        {
            foreach (var disposable in asyncDisposables)
            {
                await disposable.DisposeAsync();
            }

            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }

            if (Agent is IAsyncDisposable asyncDisposableAgent)
            {
                await asyncDisposableAgent.DisposeAsync();
            }
            else if (Agent is IDisposable disposableAgent)
            {
                disposableAgent.Dispose();
            }
        }
    }

    private sealed class HostedRuntimeAgent(RuntimeBuildResult runtimeBuild) : DelegatingAIAgent(runtimeBuild.Agent), IAsyncDisposable, IDisposable
    {
        public ValueTask DisposeAsync()
        {
            return runtimeBuild.DisposeAsync();
        }

        public void Dispose()
        {
            _ = DisposeAsync();
        }
    }

    private sealed class RuntimeCapabilityState
    {
        public List<AITool> Tools { get; } = [];

        public List<AgentRuntimeToolProviderDescriptor> RuntimeToolProviderDescriptors { get; } = [];

        public List<AgentRuntimeToolMetadata> RuntimeToolMetadata { get; } = [];

        public List<AIContextProvider> ContextProviders { get; } = [];

        public AgentContextContributionTraceCollector ContextContributionTraceCollector { get; } = new();

        public List<AgentRuntimeContextManifestSource> ContextSources { get; } = [];

        public HashSet<string> FrameworkToolNames { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<CapabilityExposureDescriptor> EffectiveCapabilityDescriptors { get; } = [];

        public List<SuppressedCapabilityDiagnostic> CapabilityAccessDiagnostics { get; } = [];

        public EffectiveCapabilitySet EffectiveCapabilities => new(
            EffectiveCapabilityDescriptors,
            CapabilityAccessDiagnostics);

        public List<IAsyncDisposable> AsyncDisposables { get; } = [];

        public List<IDisposable> Disposables { get; } = [];

        public bool HasApprovalTools { get; set; }
    }

    private sealed record ScriptContentInspection(string Content, string FailureMessage)
    {
        public static ScriptContentInspection Empty { get; } = new(string.Empty, string.Empty);
    }

    private sealed class ToolInvocationTraceRecorder
    {
        private readonly object gate = new();
        private readonly List<AgentToolInvocationTrace> traces = [];
        private int nextSequence;

        public int Start(
            string toolName,
            ToolInvocationClassification classification,
            string signature,
            AgentRuntimeToolOwnership? runtimeToolOwnership)
        {
            lock (gate)
            {
                nextSequence++;
                traces.Add(new AgentToolInvocationTrace(
                    toolName,
                    classification,
                    nextSequence,
                    DateTimeOffset.UtcNow,
                    CompletedAtUtc: null,
                    Succeeded: false,
                    FailureMessage: string.Empty)
                {
                    RuntimeToolProviderKey = runtimeToolOwnership?.ProviderKey ?? string.Empty,
                    RuntimeToolProviderName = runtimeToolOwnership?.ProviderName ?? string.Empty,
                    Signature = signature
                });
                return nextSequence;
            }
        }

        public void Complete(
            int sequence,
            bool succeeded,
            string failureMessage)
        {
            lock (gate)
            {
                var index = traces.FindIndex(trace => trace.Sequence == sequence);
                if (index < 0)
                {
                    return;
                }

                traces[index] = traces[index] with
                {
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Succeeded = succeeded,
                    FailureMessage = succeeded ? string.Empty : failureMessage
                };
            }
        }

        public IReadOnlyList<AgentToolInvocationTrace> Snapshot()
        {
            lock (gate)
            {
                return traces.ToList();
            }
        }
    }

    private sealed class FinalizerCapture(AgentFinalizerPolicy policy)
    {
        private readonly object gate = new();
        private readonly List<AgentFinalizerInvocation> invocations = [];
        private int nextSequence;

        public AgentFinalizerPolicy Policy { get; } = policy;

        public List<AITool> Tools { get; } = [];

        public string SubmitProcessStepOutcome(JsonElement result)
            => CaptureJsonElement<ProcessStepOutcomeResult>(result, "Process step outcome finalizer captured.");

        public string SubmitCodeReviewResult(CodeReviewResult result)
            => Capture(result, "Code review result finalizer captured.");

        public string SubmitArchitectureReviewResult(ArchitectureReviewResult result)
            => Capture(result, "Architecture review result finalizer captured.");

        public string SubmitImplementationPlan(ImplementationPlanResult result)
            => Capture(result, "Implementation plan finalizer captured.");

        public string SubmitTestPlan(TestPlanResult result)
            => Capture(result, "Test plan finalizer captured.");

        public string SubmitToolExecutionDecision(ToolExecutionDecisionResult result)
            => Capture(result, "Tool execution decision finalizer captured.");

        public string SubmitProcessStatePatch(ProcessStatePatch result)
            => Capture(result, "Process state patch finalizer captured.");

        public string SubmitHumanEscalationRequest(HumanEscalationRequest result)
            => Capture(result, "Human escalation request finalizer captured.");

        public IReadOnlyList<AgentFinalizerInvocation> Snapshot()
        {
            lock (gate)
            {
                return invocations.ToList();
            }
        }

        private string Capture<TOutput>(TOutput result, string message)
        {
            ArgumentNullException.ThrowIfNull(result);

            var argumentsJson = JsonSerializer.Serialize(result, AgentOutputJson.SerializerOptions);
            return CaptureArgumentsJson(argumentsJson, message);
        }

        private string CaptureJsonElement<TOutput>(JsonElement result, string message)
        {
            var rawJson = result.ValueKind == JsonValueKind.String &&
                          TryUseRawJsonObjectOrArray(result.GetString(), out var parsedRawJson)
                ? parsedRawJson
                : result.GetRawText();
            if (!TryNormalizeKnownFinalizerOutput(Policy, rawJson, out var argumentsJson, out var failureMessage) &&
                !TryDeserializeFinalizerOutput(Policy, rawJson, out argumentsJson, out failureMessage))
            {
                throw new InvalidOperationException($"Finalizer payload for '{Policy.ToolName}' is invalid: {failureMessage}");
            }

            return CaptureArgumentsJson(argumentsJson, message);
        }

        private string CaptureArgumentsJson(string argumentsJson, string message)
        {
            lock (gate)
            {
                nextSequence++;
                invocations.Add(new AgentFinalizerInvocation(
                    Policy.ToolName,
                    argumentsJson,
                    nextSequence));
            }

            return message;
        }
    }
}
