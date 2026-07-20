using CanDoItAll.AgentFramework.Models;
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

        var reasoningEffort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            provider.Kind,
            provider.Transport,
            model,
            provider.ConfigurationJson,
            agentConfigurationJson ?? string.Empty);
        if (reasoningEffort is not null)
        {
            if (reasoningEffort == AgentReasoningEffortLevel.Max)
            {
                options.RawRepresentationFactory = _ => CreateMaxReasoningOptions(provider.Transport);
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

        if (provider.Kind == ProviderKind.Ollama)
        {
            var think = AgentProviderModelParameterPolicy.ResolveOllamaThinkOrDefault(
                provider.ConfigurationJson,
                agentConfigurationJson ?? string.Empty);
            options.AddOllamaOption(OllamaOption.Think, think);
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

    public static bool IsReasoningEffortConfiguredButTransportUnsupported(
        ProviderProfile provider,
        string model,
        string? agentConfigurationJson)
    {
        return AgentProviderModelParameterPolicy.ResolveConfiguredReasoningEffort(
                   provider.Kind,
                   model,
                   provider.ConfigurationJson,
                   agentConfigurationJson ?? string.Empty) is not null &&
               !AgentProviderModelParameterPolicy.CanApplyReasoningEffort(provider.Kind, provider.Transport, model);
    }

    public static string BuildReasoningEffortUnsupportedTransportMessage(
        ProviderProfile provider,
        string model)
    {
        return $"Provider '{provider.Name}' has reasoning effort configured for model '{model}', but the {provider.Transport} transport cannot apply it. Use the Responses or Chat Completions transport for reasoning-capable OpenAI runs.";
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
    private static object CreateMaxReasoningOptions(ProviderTransportKind transport)
    {
        return transport switch
        {
            ProviderTransportKind.Responses => new CreateResponseOptions
            {
                ReasoningOptions = new ResponseReasoningOptions
                {
                    ReasoningEffortLevel = new ResponseReasoningEffortLevel("max")
                }
            },
            ProviderTransportKind.ChatCompletions => new ChatCompletionOptions
            {
                ReasoningEffortLevel = new ChatReasoningEffortLevel("max")
            },
            _ => throw new InvalidOperationException(
                $"The {transport} transport cannot apply max reasoning effort.")
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
