using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

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
        bool forceOmitTemperature)
    {
        var options = new ChatOptions();
        if (!ShouldOmitTemperature(provider, model, forceOmitTemperature))
        {
            options.Temperature = requestedTemperature;
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
}
