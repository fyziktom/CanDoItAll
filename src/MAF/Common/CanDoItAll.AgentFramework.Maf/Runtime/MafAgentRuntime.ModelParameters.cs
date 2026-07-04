using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
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

    private static ChatOptions CreateModelCompatibleChatOptions(
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
            options.Reasoning = new ReasoningOptions
            {
                Effort = MapReasoningEffort(reasoningEffort.Value)
            };
        }

        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            provider.Kind,
            provider.ConfigurationJson,
            agentConfigurationJson ?? string.Empty);
        if (maxOutputTokens is not null)
        {
            options.MaxOutputTokens = maxOutputTokens.Value;
        }

        if (provider.Kind == ProviderKind.Ollama)
        {
            var think = AgentProviderModelParameterPolicy.ResolveOllamaThink(
                provider.ConfigurationJson,
                agentConfigurationJson ?? string.Empty);
            if (think is not null)
            {
                options.AddOllamaOption(OllamaOption.Think, think.Value);
            }
        }

        return options;
    }

    private static bool ShouldOmitTemperature(
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

    private static bool ShouldRetryWithoutTemperature(
        ProviderProfile provider,
        string model,
        Exception exception)
    {
        return AgentProviderModelParameterPolicy.IsOpenAiLikeProvider(provider.Kind) &&
               !ShouldOmitTemperature(provider, model, forceOmitTemperature: false) &&
               IsUnsupportedTemperatureException(exception);
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

    private static string ResolveRuntimeModel(AgentDefinition agent, ProviderProfile provider)
    {
        return string.IsNullOrWhiteSpace(agent.Model)
            ? provider.DefaultModel
            : agent.Model;
    }

    private static string BuildTemperatureRetryMessage(string model)
    {
        return $"Provider rejected the configured temperature for model '{model}'. Retrying once without the temperature parameter.";
    }

    private static string BuildTemperatureOmittedMessage(string model)
    {
        return $"The runtime will omit temperature for model '{model}' and use the provider default.";
    }

    private static bool IsReasoningEffortConfiguredButTransportUnsupported(
        ProviderProfile provider,
        string model,
        string? agentConfigurationJson)
    {
        if (provider.Transport == ProviderTransportKind.ChatCompletions)
        {
            return false;
        }

        return AgentProviderModelParameterPolicy.ResolveConfiguredReasoningEffort(
                   provider.Kind,
                   model,
                   provider.ConfigurationJson,
                   agentConfigurationJson ?? string.Empty) is not null &&
               !AgentProviderModelParameterPolicy.CanApplyReasoningEffort(provider.Kind, provider.Transport, model);
    }

    private static string BuildReasoningEffortUnsupportedTransportMessage(
        ProviderProfile provider,
        string model)
    {
        return $"Provider '{provider.Name}' has reasoning effort configured for model '{model}', but the {provider.Transport} transport cannot apply it. Use the Responses transport for reasoning-capable OpenAI runs.";
    }
}
