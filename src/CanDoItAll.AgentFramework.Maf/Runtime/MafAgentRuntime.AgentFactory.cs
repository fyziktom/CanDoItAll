using Azure.AI.OpenAI;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
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
        var managedSeedProvider = ManagedSeedProviderFallbacks.IsManagedSeedAgent(agent)
            ? ManagedSeedProviderFallbacks.ApplyForManagedSqliteSeedProvider(provider, isManagedSqliteProfile: true)
            : provider;
        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, managedSeedProvider, openAiCredentialOverride);
        PromoteResolvedProviderCredentialEnvironment(effectiveProvider);
        var model = ManagedSeedProviderFallbacks.ResolveModel(agent, effectiveProvider, openAiCredentialOverride);
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException($"Provider '{effectiveProvider.Name}' does not have a default model and the agent '{agent.Name}' does not override one.");
        }

        EnsureStructuredOutputCapability(effectiveProvider, runtimeOptions.StructuredOutput);
        var finalizerCapture = CreateFinalizerCapture(runtimeOptions.StructuredOutput, runtimeOptions.FinalizerMode);
        var capabilityState = await CreateCapabilityStateAsync(
            agent,
            effectiveProvider,
            capabilities,
            memory,
            progressCallback,
            cancellationToken,
            suppressApprovalRequirements);
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

        var frameworkManagedHistory = ShouldUseFrameworkManagedHistory(agent, effectiveProvider);
        var chatOptions = CreateModelCompatibleChatOptions(
            effectiveProvider,
            model,
            (float)agent.Temperature,
            forceOmitTemperature);
        chatOptions.Instructions = AppendFinalizerInstructions(
            agent.Instructions,
            finalizerCapture?.Policy,
            runtimeOptions.FinalizerMode,
            runtimeOptions.StructuredOutput is not null);
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
            toolInvocationTraceRecorder);
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
                snapshotFinalizerInvocations: () => participantBuilds
                    .SelectMany(item => item.SnapshotFinalizerInvocations())
                    .OrderBy(item => item.Sequence)
                    .ToList(),
                snapshotToolInvocationTraces: () => participantBuilds
                    .SelectMany(item => item.SnapshotToolInvocationTraces())
                    .OrderBy(item => item.Sequence)
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
        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrix(provider);
        var logger = services.GetService<ILogger<MafAgentRuntime>>();
        builder.UseLogging(
            services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance,
            logging => logging.JsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web));
        builder.Use(async (innerAgent, context, next, cancellationToken) =>
        {
            var functionName = context.Function?.Name ?? "unknown";
            var redactedArguments = AgentToolInvocationPolicyMetadata.RedactArguments(ResolveFunctionInvocationArguments(context));
            var classification = AgentToolInvocationPolicyMetadata.Classify(functionName);
            var auditScope = WorkspaceExecutionAuditContext.Current;
            var policyContext = new ToolInvocationPolicyContext(
                AgentId: agentDefinition.Id,
                AgentName: agentDefinition.Name,
                ToolName: functionName,
                RedactedArguments: redactedArguments,
                Classification: classification,
                IsKnownTool: knownToolNames.Contains(functionName),
                AutoApprovalAllowed: suppressApprovalRequirements,
                ApprovalWrapperAvailable: approvalWrappedToolNames.Contains(functionName),
                ExecutionRunId: auditScope?.ExecutionRunId.ToString("D") ?? string.Empty,
                SourceKind: auditScope?.SourceKind ?? string.Empty,
                ProcessRunId: auditScope?.ProcessRunId ?? string.Empty,
                ProcessStepId: auditScope?.ProcessStepId ?? string.Empty,
                AllowedExternalTargetAliases: auditScope?.AllowedExternalTargetAliases ?? [],
                ReadOnlyExternalTargetAliases: auditScope?.ReadOnlyExternalTargetAliases ?? [],
                ApprovalWrapperEffectiveForProvider: featureMatrix.SupportsApprovalRequiredAIFunction,
                ApplicationApprovalAvailable: false);
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

            logger?.LogInformation(
                "Agent tool policy decision {Decision} for tool {ToolName} on agent {AgentId}. ExecutionRunId={ExecutionRunId} SourceKind={SourceKind} ProcessRunId={ProcessRunId} ProcessStepId={ProcessStepId} Signature={Signature}",
                policyDecision.Kind,
                functionName,
                agentDefinition.Id,
                policyContext.ExecutionRunId,
                policyContext.SourceKind,
                policyContext.ProcessRunId,
                policyContext.ProcessStepId,
                policyDecision.Signature);

            var traceSequence = toolInvocationTraceRecorder.Start(functionName, classification);
            var succeeded = false;
            var failureMessage = string.Empty;
            try
            {
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

    private static bool IsRequiredFinalizerTool(
        string functionName,
        AgentFinalizerPolicy? finalizerPolicy,
        AgentFinalizerMode finalizerMode)
    {
        return finalizerMode == AgentFinalizerMode.Required &&
               finalizerPolicy is { IsRequired: true } &&
               string.Equals(functionName, finalizerPolicy.ToolName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuccessfulToolInvocationResult(object? result)
    {
        if (result is null)
        {
            return true;
        }

        var succeededProperty = result.GetType().GetProperty("Succeeded");
        return succeededProperty?.PropertyType == typeof(bool) &&
               succeededProperty.GetValue(result) is bool succeeded
            ? succeeded
            : true;
    }

    private static string ResolveToolInvocationFailureMessage(object? result)
    {
        if (result is null)
        {
            return "Tool invocation returned an unsuccessful result.";
        }

        var messageProperty = result.GetType().GetProperty("Message");
        if (messageProperty?.PropertyType == typeof(string) &&
            messageProperty.GetValue(result) is string message &&
            !string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        return "Tool invocation returned an unsuccessful result.";
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
              $"- The finalizer arguments are the authoritative machine output for `{finalizerPolicy.OutputContract.ContractKey}`.{Environment.NewLine}" +
              (hasStructuredResponseFormat
                  ? $"- After the tool call, return exactly one JSON object matching the same `{finalizerPolicy.OutputContract.ContractKey}` schema through the configured structured response format.{Environment.NewLine}" +
                    "- Do not use Markdown, prose, code fences, or any extra text around the JSON object."
                  : "- Do not emit separate machine output after the finalizer call.") + Environment.NewLine +
              "- Do not call any other `submit_*` finalizer tool for this contract.";
        return string.IsNullOrWhiteSpace(instructions)
            ? finalizerInstructions.Trim()
            : instructions.TrimEnd() + finalizerInstructions;
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
        Func<IReadOnlyList<AgentFinalizerInvocation>>? snapshotFinalizerInvocations = null,
        Func<IReadOnlyList<AgentToolInvocationTrace>>? snapshotToolInvocationTraces = null) : IAsyncDisposable
    {
        public AIAgent Agent { get; } = agent;

        public ProviderProfile Provider { get; } = provider;

        public string Model { get; } = model;

        public bool HasApprovalTools { get; } = hasApprovalTools;

        public bool IsTemperatureOmitted { get; } = isTemperatureOmitted;

        public IReadOnlyList<AgentFinalizerInvocation> SnapshotFinalizerInvocations()
            => snapshotFinalizerInvocations?.Invoke() ?? finalizerCapture?.Snapshot() ?? [];

        public IReadOnlyList<AgentToolInvocationTrace> SnapshotToolInvocationTraces()
            => snapshotToolInvocationTraces?.Invoke() ?? toolInvocationTraceRecorder?.Snapshot() ?? [];

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

        public List<AIContextProvider> ContextProviders { get; } = [];

        public HashSet<string> FrameworkToolNames { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<IAsyncDisposable> AsyncDisposables { get; } = [];

        public List<IDisposable> Disposables { get; } = [];

        public bool HasApprovalTools { get; set; }
    }

    private sealed class ToolInvocationTraceRecorder
    {
        private readonly object gate = new();
        private readonly List<AgentToolInvocationTrace> traces = [];
        private int nextSequence;

        public int Start(
            string toolName,
            ToolInvocationClassification classification)
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
                    FailureMessage: string.Empty));
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

        public string SubmitProcessStepOutcome(ProcessStepOutcomeResult result)
            => Capture(result, "Process step outcome finalizer captured.");

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
