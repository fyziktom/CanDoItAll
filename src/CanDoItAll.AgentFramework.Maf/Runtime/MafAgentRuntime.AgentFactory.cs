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
        CancellationToken cancellationToken = default)
    {
        var runtimeBuild = await CreateRuntimeBuildAsync(
            agent,
            provider,
            capabilities,
            memory,
            static (_, _, _) => Task.CompletedTask,
            cancellationToken);

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
        AgentStructuredOutputContract? structuredOutput = null)
    {
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

        EnsureStructuredOutputCapability(effectiveProvider, structuredOutput);
        var finalizerCapture = CreateFinalizerCapture(structuredOutput);
        var capabilityState = await CreateCapabilityStateAsync(
            agent,
            effectiveProvider,
            capabilities,
            memory,
            progressCallback,
            cancellationToken,
            suppressApprovalRequirements);
        if (finalizerCapture is not null)
        {
            capabilityState.Tools.AddRange(finalizerCapture.Tools);
            await progressCallback(
                ExecutionState.Preparing,
                "Finalizer policy",
                $"Attached shadow finalizer tool '{finalizerCapture.Policy.ToolName}' for structured output contract '{finalizerCapture.Policy.OutputContract.ContractKey}'.");
        }

        var frameworkManagedHistory = ShouldUseFrameworkManagedHistory(agent, effectiveProvider);
        var chatOptions = CreateModelCompatibleChatOptions(
            effectiveProvider,
            model,
            (float)agent.Temperature,
            forceOmitTemperature);
        chatOptions.Instructions = AppendFinalizerInstructions(agent.Instructions, finalizerCapture?.Policy);
        chatOptions.AllowMultipleToolCalls = !capabilityState.HasApprovalTools;

        if (capabilityState.Tools.Count > 0)
        {
            chatOptions.Tools = [.. capabilityState.Tools];
        }

        var options = new ChatClientAgentOptions
        {
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
            suppressApprovalRequirements);
        return new RuntimeBuildResult(
            runtimeAgent,
            effectiveProvider,
            model,
            capabilityState.AsyncDisposables,
            capabilityState.Disposables,
            capabilityState.HasApprovalTools,
            ShouldOmitTemperature(effectiveProvider, model, forceOmitTemperature),
            finalizerCapture);
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
        bool suppressApprovalRequirements)
    {
        var builder = agent.AsBuilder();
        var toolPolicy = new DefaultAgentToolInvocationPolicy();
        var knownToolNames = capabilityState.Tools
            .Select(tool => tool.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var approvalWrappedToolNames = capabilityState.Tools
            .Where(tool => tool is ApprovalRequiredAIFunction)
            .Select(tool => tool.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var logger = services.GetService<ILogger<MafAgentRuntime>>();
        builder.UseLogging(
            services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance,
            logging => logging.JsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web));
        builder.Use(async (innerAgent, context, next, cancellationToken) =>
        {
            var functionName = context.Function?.Name ?? "unknown";
            var redactedArguments = AgentToolInvocationPolicyMetadata.RedactArguments(ResolveFunctionInvocationArguments(context));
            var auditScope = WorkspaceExecutionAuditContext.Current;
            var policyContext = new ToolInvocationPolicyContext(
                AgentId: agentDefinition.Id,
                AgentName: agentDefinition.Name,
                ToolName: functionName,
                RedactedArguments: redactedArguments,
                Classification: AgentToolInvocationPolicyMetadata.Classify(functionName),
                IsKnownTool: knownToolNames.Contains(functionName),
                AutoApprovalAllowed: suppressApprovalRequirements,
                ApprovalWrapperAvailable: approvalWrappedToolNames.Contains(functionName),
                ExecutionRunId: auditScope?.ExecutionRunId.ToString("D") ?? string.Empty,
                SourceKind: auditScope?.SourceKind ?? string.Empty,
                ProcessRunId: auditScope?.ProcessRunId ?? string.Empty,
                ProcessStepId: auditScope?.ProcessStepId ?? string.Empty);
            var policyDecision = await toolPolicy.EvaluateAsync(policyContext, cancellationToken);
            using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("maf.function.invoke", ActivityKind.Internal);
            AgentFrameworkTelemetry.ApplyCurrentAuditScope(activity);
            activity?.SetTag("agentframework.tool_name", functionName);
            activity?.SetTag("agentframework.tool_policy_decision", policyDecision.Kind.ToString());
            activity?.SetTag("agentframework.tool_policy_signature", policyDecision.Signature);
            activity?.SetTag("agentframework.tool_policy_reason", policyDecision.Reason);
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

            try
            {
                if (policyDecision.Kind is ToolInvocationDecisionKind.Deny or ToolInvocationDecisionKind.SkipExecution)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, policyDecision.Reason);
                    throw new InvalidOperationException(policyDecision.Reason);
                }

                return await next(context, cancellationToken);
            }
            catch (Exception exception) when (IsPolicyException(exception))
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                throw new InvalidOperationException($"Tool '{functionName}' was blocked by policy. {exception.Message}", exception);
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                throw;
            }
        });
        builder.UseOpenTelemetry(
            $"{AgentFrameworkTelemetry.SourceName}.Maf.{provider.Kind}",
            telemetry => telemetry.EnableSensitiveData = false);
        return builder.Build(services);
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
            $"Provider '{provider.Name}' using transport '{provider.Transport}' cannot enforce structured output contract '{structuredOutput.ContractKey}'. Choose a Responses-backed OpenAI/Azure OpenAI provider or disable the machine-critical structured-output request.");
    }

    private static FinalizerCapture? CreateFinalizerCapture(AgentStructuredOutputContract? structuredOutput)
    {
        if (!AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var policy))
        {
            return null;
        }

        var capture = new FinalizerCapture(policy);
        capture.Tools.Add(AIFunctionFactory.Create(
            capture.SubmitProcessStepOutcome,
            AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
            "Submits the final process-step outcome exactly once as typed machine-readable arguments."));
        return capture;
    }

    private static string AppendFinalizerInstructions(
        string instructions,
        AgentFinalizerPolicy? finalizerPolicy)
    {
        if (finalizerPolicy is null)
        {
            return instructions;
        }

        var finalizerInstructions =
            $"{Environment.NewLine}{Environment.NewLine}Finalizer tool policy:{Environment.NewLine}" +
            $"- If the tool `{finalizerPolicy.ToolName}` is available, call it exactly once with the same `{finalizerPolicy.OutputContract.ContractKey}` decision you return as structured output.{Environment.NewLine}" +
            "- Treat normal assistant text as display-only; workflow state must come from typed machine output.";
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

    private static bool IsPolicyException(Exception exception)
        => exception is InvalidOperationException or NotSupportedException;

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
        FinalizerCapture? finalizerCapture) : IAsyncDisposable
    {
        public AIAgent Agent { get; } = agent;

        public ProviderProfile Provider { get; } = provider;

        public string Model { get; } = model;

        public bool HasApprovalTools { get; } = hasApprovalTools;

        public bool IsTemperatureOmitted { get; } = isTemperatureOmitted;

        public IReadOnlyList<AgentFinalizerInvocation> SnapshotFinalizerInvocations()
            => finalizerCapture?.Snapshot() ?? [];

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

        public List<IAsyncDisposable> AsyncDisposables { get; } = [];

        public List<IDisposable> Disposables { get; } = [];

        public bool HasApprovalTools { get; set; }
    }

    private sealed class FinalizerCapture(AgentFinalizerPolicy policy)
    {
        private readonly object gate = new();
        private readonly List<AgentFinalizerInvocation> invocations = [];
        private int nextSequence;

        public AgentFinalizerPolicy Policy { get; } = policy;

        public List<AITool> Tools { get; } = [];

        public string SubmitProcessStepOutcome(ProcessStepOutcomeResult result)
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

            return "Process step outcome finalizer captured.";
        }

        public IReadOnlyList<AgentFinalizerInvocation> Snapshot()
        {
            lock (gate)
            {
                return invocations.ToList();
            }
        }
    }
}
