using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class OpenAiChatCompletionsCompatibilityChatClient(
    IChatClient innerClient,
    ProviderProfile provider,
    string model,
    ILogger? logger) : DelegatingChatClient(innerClient)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetResponseAsync(
            NormalizeToolHistory(messages),
            NormalizeOptions(options),
            cancellationToken);
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetStreamingResponseAsync(
            NormalizeToolHistory(messages),
            NormalizeOptions(options),
            cancellationToken);
    }

    internal static IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> NormalizeToolHistory(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages) {
        var normalized = new List<Microsoft.Extensions.AI.ChatMessage>();
        var pendingCalls = new HashSet<string>(StringComparer.Ordinal);
        var callMessageIndex = 0;
        foreach (var message in messages) {
            if (pendingCalls.Count > 0 &&
                message.GetAgentRequestMessageSourceType() == AgentRequestMessageSourceType.AIContextProvider) {
                normalized.Insert(callMessageIndex++, message);
                continue;
            }

            if (message.Role == ChatRole.Assistant) {
                foreach (var call in message.Contents.OfType<FunctionCallContent>().Where(call => !call.InformationalOnly)) {
                    if (pendingCalls.Count == 0) {
                        callMessageIndex = normalized.Count;
                    }
                    pendingCalls.Add(call.CallId);
                }
            } else if (message.Role == ChatRole.Tool) {
                foreach (var result in message.Contents.OfType<FunctionResultContent>()) {
                    pendingCalls.Remove(result.CallId);
                }
            }
            normalized.Add(message);
        }
        return normalized;
    }

    private ChatOptions? NormalizeOptions(ChatOptions? options)
    {
        var selectedModel = string.IsNullOrWhiteSpace(options?.ModelId)
            ? model
            : options.ModelId.Trim();
        var effectiveModel = provider.IsSourceManaged
            ? provider.ModelCatalog.Single(item => string.Equals(item.Id, selectedModel, StringComparison.Ordinal)).DisplayName
            : selectedModel;
        var functionToolCount = options?.Tools?.Count(static tool => tool is AIFunctionDeclaration) ?? 0;
        var requiresExplicitNone = OpenAiRequestCompatibilityPolicy.RequiresExplicitReasoningNone(
            provider.Kind,
            provider.Transport,
            effectiveModel,
            ProviderInvocationFeatures.FunctionTools);
        if (!requiresExplicitNone)
        {
            return options;
        }

        if (functionToolCount == 0 && options?.RawRepresentationFactory is null)
        {
            return options;
        }

        var requestedEffort = options?.RawRepresentationFactory is null
            ? MapReasoningEffort(options?.Reasoning?.Effort)
            : null;
        var resolution = OpenAiRequestCompatibilityPolicy.ResolveReasoningEffort(
            provider.Kind,
            provider.Transport,
            effectiveModel,
            ProviderInvocationFeatures.FunctionTools,
            requestedEffort);
        var alreadyExplicitNone = options?.Reasoning?.Effort == ReasoningEffort.None &&
                                  options.RawRepresentationFactory is null;
        if (functionToolCount > 0 && alreadyExplicitNone)
        {
            return options;
        }

        var normalizedOptions = options?.Clone() ?? new ChatOptions();
        if (functionToolCount > 0)
        {
            SetExplicitNone(normalizedOptions);
            ReportAdjustment(
                effectiveModel,
                resolution.RequestedEffort?.ToString() ?? "provider-default-or-transport-native",
                functionToolCount);
        }

        WrapRawRepresentationFactory(
            normalizedOptions,
            effectiveModel,
            functionToolCount);
        return normalizedOptions;
    }

    private static AgentReasoningEffortLevel? MapReasoningEffort(ReasoningEffort? effort)
    {
        return effort switch
        {
            null => null,
            ReasoningEffort.None => AgentReasoningEffortLevel.None,
            ReasoningEffort.Low => AgentReasoningEffortLevel.Low,
            ReasoningEffort.Medium => AgentReasoningEffortLevel.Medium,
            ReasoningEffort.High => AgentReasoningEffortLevel.High,
            ReasoningEffort.ExtraHigh => AgentReasoningEffortLevel.ExtraHigh,
            _ => null
        };
    }

    private void WrapRawRepresentationFactory(
        ChatOptions options,
        string effectiveModel,
        int frameworkFunctionToolCount)
    {
        var rawRepresentationFactory = options.RawRepresentationFactory;
        if (rawRepresentationFactory is null)
        {
            return;
        }

        options.RawRepresentationFactory = serviceProvider =>
            NormalizeRawRepresentation(
                rawRepresentationFactory(serviceProvider),
                options,
                effectiveModel,
                frameworkFunctionToolCount);
    }

#pragma warning disable OPENAI001
    private object? NormalizeRawRepresentation(
        object? rawRepresentation,
        ChatOptions options,
        string effectiveModel,
        int frameworkFunctionToolCount)
    {
        if (rawRepresentation is not ChatCompletionOptions chatOptions)
        {
            return rawRepresentation;
        }

        var nativeFunctionToolCount = chatOptions.Tools.Count;
        var totalFunctionToolCount = frameworkFunctionToolCount + nativeFunctionToolCount;
        if (totalFunctionToolCount == 0)
        {
            return chatOptions;
        }

        var requestedEffort = chatOptions.ReasoningEffortLevel?.ToString();
        SetExplicitNone(options);
        chatOptions.ReasoningEffortLevel = new ChatReasoningEffortLevel("none");
        if (frameworkFunctionToolCount == 0)
        {
            ReportAdjustment(
                effectiveModel,
                string.IsNullOrWhiteSpace(requestedEffort)
                    ? "provider-default-or-transport-native"
                    : requestedEffort,
                totalFunctionToolCount);
        }

        return chatOptions;
    }
#pragma warning restore OPENAI001

    private static void SetExplicitNone(ChatOptions options)
    {
        options.Reasoning = new ReasoningOptions
        {
            Effort = ReasoningEffort.None,
            Output = options.Reasoning?.Output
        };
    }

    private void ReportAdjustment(
        string effectiveModel,
        string requestedEffort,
        int functionToolCount)
    {
        logger?.LogWarning(
            "Enforced provider request compatibility. ProviderId={ProviderId} Provider={ProviderName} Transport={Transport} Model={Model} Adjustment={Adjustment} RequestedReasoningEffort={RequestedReasoningEffort} EffectiveReasoningEffort={EffectiveReasoningEffort} FunctionToolCount={FunctionToolCount}.",
            provider.Id,
            provider.Name,
            provider.Transport,
            effectiveModel,
            ProviderModelParameterAdjustment.ReasoningDisabledForFunctionTools,
            requestedEffort,
            AgentReasoningEffortLevel.None,
            functionToolCount);
    }
}
