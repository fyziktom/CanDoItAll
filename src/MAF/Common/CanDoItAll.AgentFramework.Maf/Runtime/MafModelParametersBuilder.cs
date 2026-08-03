using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;
using OpenAI.Chat;
using OpenAI.Responses;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafModelParametersBuilder
{
    private const string TemperatureParameterName = "temperature";

    private static readonly string[] UnsupportedTemperatureErrorSignals =
    [
        "unsupported_value",
        "unsupported value",
        "unsupported parameter",
        "does not support",
        "not supported"
    ];

    public static ChatOptions CreateModelCompatibleChatOptions(
        ProviderProfile provider,
        string model,
        float? requestedTemperature,
        bool forceOmitTemperature,
        string? agentConfigurationJson = null)
    {
        var options = new ChatOptions();
        if (!ShouldOmitTemperature(provider, model, forceOmitTemperature))
        {
            options.Temperature = requestedTemperature;
        }

        var reasoningEffort = ResolveEffectiveThinkingEffort(
            provider,
            model,
            agentConfigurationJson ?? string.Empty);
        if (reasoningEffort is not null && provider.Kind == ProviderKind.Ollama)
        {
            var thinkingCapability = AgentThinkingEffortPolicy.ResolveCapability(provider, model);
            options.AddOllamaOption(
                OllamaOption.Think,
                OllamaThinkingEffortAdapter.ToNativeValue(
                    thinkingCapability,
                    reasoningEffort.Value));
        }
        else if (reasoningEffort is not null)
        {
            if (reasoningEffort is AgentReasoningEffortLevel.Minimal or AgentReasoningEffortLevel.Max)
            {
                options.RawRepresentationFactory = _ => CreateRawReasoningOptions(
                    provider.Transport,
                    reasoningEffort.Value);
            }
            else
            {
                options.Reasoning = new ReasoningOptions
                {
                    Effort = MapReasoningEffort(reasoningEffort.Value)
                };
            }
        }

        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            provider.Kind,
            model,
            provider.ConfigurationJson,
            agentConfigurationJson ?? string.Empty);
        if (maxOutputTokens is not null)
        {
            options.MaxOutputTokens = maxOutputTokens.Value;
        }
        else if (provider.Kind == ProviderKind.Ollama)
        {
            options.MaxOutputTokens = AgentProviderModelParameterPolicy.DefaultOllamaMaxOutputTokens;
        }

        return options;
    }

    public static bool ShouldOmitTemperature(
        ProviderProfile provider,
        string model,
        bool forceOmitTemperature)
    {
        if (forceOmitTemperature)
        {
            return true;
        }

        if (provider.Kind == ProviderKind.AzureOpenAi &&
            AgentThinkingEffortPolicy.ResolveCapability(provider, model).Status ==
            AgentThinkingEffortSupportStatus.Supported)
        {
            return true;
        }

        return AgentProviderModelParameterPolicy.ShouldOmitTemperature(provider.Kind, model);
    }

    public static bool ShouldRetryWithoutTemperature(
        ProviderProfile provider,
        string model,
        Exception exception)
    {
        return AgentProviderModelParameterPolicy.IsOpenAiLikeProvider(provider.Kind) &&
               !ShouldOmitTemperature(provider, model, forceOmitTemperature: false) &&
               IsUnsupportedTemperatureException(exception);
    }

    public static string ResolveRuntimeModel(AgentDefinition agent, ProviderProfile provider)
    {
        return ManagedSeedProviderFallbacks.ResolveModel(agent, provider);
    }

    public static string BuildTemperatureRetryMessage(string model)
    {
        return $"Provider rejected the configured temperature for model '{model}'. Retrying once without the temperature parameter.";
    }

    public static string BuildTemperatureOmittedMessage(string model)
    {
        return $"The runtime will omit temperature for model '{model}' and use the provider default.";
    }

    public static AgentReasoningEffortLevel? ResolveEffectiveThinkingEffort(
        ProviderProfile provider,
        string model,
        string? agentConfigurationJson)
    {
        return AgentThinkingEffortPolicy.ResolveEffectiveEffort(
            provider,
            model,
            agentConfigurationJson ?? string.Empty);
    }

    private static bool IsUnsupportedTemperatureException(Exception exception)
    {
        return EnumerateExceptionMessages(exception).Any(message =>
            message.Contains(TemperatureParameterName, StringComparison.OrdinalIgnoreCase) &&
            UnsupportedTemperatureErrorSignals.Any(signal => message.Contains(signal, StringComparison.OrdinalIgnoreCase)));
    }

    private static ReasoningEffort MapReasoningEffort(AgentReasoningEffortLevel effort)
    {
        return effort switch
        {
            AgentReasoningEffortLevel.None => ReasoningEffort.None,
            AgentReasoningEffortLevel.Low => ReasoningEffort.Low,
            AgentReasoningEffortLevel.Medium => ReasoningEffort.Medium,
            AgentReasoningEffortLevel.High => ReasoningEffort.High,
            AgentReasoningEffortLevel.ExtraHigh => ReasoningEffort.ExtraHigh,
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unsupported reasoning effort.")
        };
    }

#pragma warning disable OPENAI001
    private static object CreateRawReasoningOptions(
        ProviderTransportKind transport,
        AgentReasoningEffortLevel effort)
    {
        var nativeEffort = AgentThinkingEffortPolicy.FormatEffort(effort);
        return transport switch
        {
            ProviderTransportKind.Responses => new CreateResponseOptions
            {
                ReasoningOptions = new ResponseReasoningOptions
                {
                    ReasoningEffortLevel = new ResponseReasoningEffortLevel(nativeEffort)
                }
            },
            ProviderTransportKind.ChatCompletions => new ChatCompletionOptions
            {
                ReasoningEffortLevel = new ChatReasoningEffortLevel(nativeEffort)
            },
            _ => throw new InvalidOperationException(
                $"The {transport} transport cannot apply {nativeEffort} reasoning effort.")
        };
    }
#pragma warning restore OPENAI001

    private static IEnumerable<string> EnumerateExceptionMessages(Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.Flatten().InnerExceptions)
            {
                foreach (var message in EnumerateExceptionMessages(innerException))
                {
                    yield return message;
                }
            }
        }

        for (var currentException = exception; currentException is not null; currentException = currentException.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(currentException.Message))
            {
                yield return currentException.Message;
            }
        }
    }
}
