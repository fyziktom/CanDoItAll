using System.Net;
using CanDoItAll.AgentFramework.Models;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.AgentFramework.Core;

public enum AgentProviderFailureCategory
{
    ProviderError,
    QuotaOrBilling,
    RateLimit,
    RequestCompatibility,
    ProviderConfiguration
}

public sealed record AgentProviderFailureDisplay(
    AgentProviderFailureCategory Category,
    string Message,
    string ProviderDetail);

public static class AgentProviderFailureDisplayFormatter
{
    private const string DefaultFailureDisplayMessage = "The agent run failed while contacting its configured provider.";
    private const int MaxProviderDetailLength = 480;
    private const int MaxInspectedExceptions = 32;
    private const int MaxInspectedMessageLength = 512;

    private static readonly string[] QuotaOrBillingMarkers =
    [
        "insufficient_quota",
        "billing_hard_limit_reached",
        "exceeded your current quota",
        "check your plan and billing details",
        "no remaining credit",
        "no remaining credits",
        "out of credit",
        "out of credits",
        "insufficient credit",
        "insufficient credits",
        "payment required",
        "http 402",
        "status code 402",
        "status code: 402"
    ];

    private static readonly string[] RateLimitMarkers =
    [
        "rate_limit_exceeded",
        "rate limit",
        "too many requests",
        "http 429",
        "status code 429",
        "status code: 429"
    ];

    public static bool TryFormat(
        ProviderProfile provider,
        Exception exception,
        out AgentProviderFailureDisplay display)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is not AgentRuntimeUsageException
            {
                FailureOrigin: AgentRuntimeFailureOrigin.Provider or AgentRuntimeFailureOrigin.ProviderConfiguration
            })
        {
            display = default!;
            return false;
        }

        display = FormatCore(provider, exception);
        return true;
    }

    private static AgentProviderFailureDisplay FormatCore(
        ProviderProfile provider,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is AgentRuntimeUsageException
            {
                FailureOrigin: AgentRuntimeFailureOrigin.ProviderConfiguration
            })
        {
            return new AgentProviderFailureDisplay(
                AgentProviderFailureCategory.ProviderConfiguration,
                $"Provider profile '{provider.Name}' is not ready for agent execution. Verify its credential, endpoint, transport, and runtime settings, then retry.",
                "Provider configuration validation failed.");
        }

        var messages = CollectMessages(exception);
        var providerDetail = SelectProviderDetail(messages, exception);
        var category = ResolveCategory(provider, messages, exception);

        return category switch
        {
            AgentProviderFailureCategory.QuotaOrBilling => new AgentProviderFailureDisplay(
                category,
                $"{ResolveProviderAccountLabel(provider)} for provider '{provider.Name}' has no remaining credits or quota, or billing is blocked. Add billing or credits, or switch this agent to a provider profile with available quota, then retry. Provider detail: {providerDetail}",
                providerDetail),
            AgentProviderFailureCategory.RateLimit => new AgentProviderFailureDisplay(
                category,
                $"Provider '{provider.Name}' rejected the request because of rate limiting. Wait for the provider limit to reset or reduce concurrency, then retry. Provider detail: {providerDetail}",
                providerDetail),
            AgentProviderFailureCategory.RequestCompatibility => new AgentProviderFailureDisplay(
                category,
                $"Provider '{provider.Name}' rejected an incompatible function-tools request. To retain reasoning with tools, use an OpenAI Responses provider profile; otherwise set reasoning effort to 'none'. Provider detail: {providerDetail}",
                providerDetail),
            _ => new AgentProviderFailureDisplay(
                category,
                $"The agent run failed while using provider '{provider.Name}'. Provider detail: {providerDetail}",
                providerDetail)
        };
    }

    internal static string NormalizeDisplayMessage(string? displayMessage)
    {
        var sanitized = WorkflowExecutorRedaction.RedactText(displayMessage).Trim();
        return string.IsNullOrWhiteSpace(sanitized)
            ? DefaultFailureDisplayMessage
            : sanitized;
    }

    private static AgentProviderFailureCategory ResolveCategory(
        ProviderProfile provider,
        IReadOnlyList<string> messages,
        Exception exception)
    {
        if (messages.Any(IsQuotaOrBillingMessage) ||
            exception is HttpRequestException { StatusCode: HttpStatusCode.PaymentRequired })
        {
            return AgentProviderFailureCategory.QuotaOrBilling;
        }

        if (messages.Any(IsRateLimitMessage) ||
            exception is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests })
        {
            return AgentProviderFailureCategory.RateLimit;
        }

        if (provider.Kind == ProviderKind.OpenAi &&
            provider.Transport == ProviderTransportKind.ChatCompletions &&
            IsBadRequest(messages, exception) &&
            messages.Any(IsReasoningFunctionToolCompatibilityMessage))
        {
            return AgentProviderFailureCategory.RequestCompatibility;
        }

        return AgentProviderFailureCategory.ProviderError;
    }

    private static bool IsQuotaOrBillingMessage(string message)
    {
        if (ContainsAny(message, QuotaOrBillingMarkers))
        {
            return true;
        }

        return message.Contains("billing", StringComparison.OrdinalIgnoreCase) &&
               (message.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("limit", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("plan", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("payment", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRateLimitMessage(string message)
        => ContainsAny(message, RateLimitMarkers);

    private static bool IsReasoningFunctionToolCompatibilityMessage(string message)
    {
        return message.Contains("invalid_request_error", StringComparison.OrdinalIgnoreCase) &&
               message.Contains("reasoning_effort", StringComparison.OrdinalIgnoreCase) &&
               message.Contains("function tools", StringComparison.OrdinalIgnoreCase) &&
               message.Contains("not supported", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBadRequest(
        IReadOnlyList<string> messages,
        Exception exception)
    {
        return exception is HttpRequestException { StatusCode: HttpStatusCode.BadRequest } ||
               messages.Any(message =>
                   message.Contains("HTTP 400", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("status code 400", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("status code: 400", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(string message, IReadOnlyList<string> markers)
    {
        foreach (var marker in markers)
        {
            if (message.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string SelectProviderDetail(
        IReadOnlyList<string> messages,
        Exception exception)
    {
        if (messages.Any(IsQuotaOrBillingMessage))
        {
            return messages.Any(message =>
                    message.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase))
                ? "Provider error code: insufficient_quota."
                : "Provider reported a quota or billing restriction.";
        }

        if (messages.Any(IsRateLimitMessage))
        {
            return messages.Any(message =>
                    message.Contains("rate_limit_exceeded", StringComparison.OrdinalIgnoreCase))
                ? "Provider error code: rate_limit_exceeded."
                : "Provider reported rate limiting.";
        }

        if (messages.Any(IsReasoningFunctionToolCompatibilityMessage))
        {
            return "HTTP 400 invalid_request_error: reasoning_effort with function tools is unsupported for this transport.";
        }

        var statusCode = FindHttpStatusCode(exception);
        return statusCode.HasValue
            ? $"HTTP {(int)statusCode.Value} {statusCode.Value}."
            : $"Provider transport failure type: {ResolveInnermostFailureType(exception)}.";
    }

    private static IReadOnlyList<string> CollectMessages(Exception exception)
    {
        var messages = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<Exception>();
        stack.Push(exception);

        while (stack.Count > 0 && visited.Count < MaxInspectedExceptions)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            AddMessage(current.Message);

            if (current is HttpRequestException { StatusCode: { } statusCode })
            {
                AddMessage($"HTTP {(int)statusCode} {statusCode}");
            }

            if (current is AggregateException aggregateException)
            {
                foreach (var innerException in aggregateException.InnerExceptions.Reverse())
                {
                    stack.Push(innerException);
                }
            }

            if (current.InnerException is not null)
            {
                stack.Push(current.InnerException);
            }
        }

        return messages;

        void AddMessage(string? message)
        {
            var normalized = NormalizeMessage(message);
            if (!string.IsNullOrWhiteSpace(normalized) &&
                seen.Add(normalized))
            {
                messages.Add(normalized);
            }
        }
    }

    private static string NormalizeMessage(string? message)
        => string.IsNullOrWhiteSpace(message)
            ? string.Empty
            : Truncate(message.Trim().ReplaceLineEndings(" "), MaxInspectedMessageLength);

    private static HttpStatusCode? FindHttpStatusCode(Exception exception)
    {
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<Exception>();
        stack.Push(exception);
        while (stack.Count > 0 && visited.Count < MaxInspectedExceptions)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (current is HttpRequestException { StatusCode: { } statusCode })
            {
                return statusCode;
            }

            if (current is AggregateException aggregateException)
            {
                foreach (var innerException in aggregateException.InnerExceptions)
                {
                    stack.Push(innerException);
                }
            }

            if (current.InnerException is not null)
            {
                stack.Push(current.InnerException);
            }
        }

        return null;
    }

    private static string ResolveInnermostFailureType(Exception exception)
    {
        var current = exception;
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        for (var inspected = 0;
             inspected < MaxInspectedExceptions && current.InnerException is not null && visited.Add(current);
             inspected++)
        {
            current = current.InnerException;
        }

        return current.GetType().Name;
    }

    private static string ResolveProviderAccountLabel(ProviderProfile provider)
        => provider.Kind switch
        {
            ProviderKind.OpenAi => "OpenAI API account",
            ProviderKind.AzureOpenAi => "Azure OpenAI resource",
            _ => "Provider account"
        };

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength
            ? value
            : value[..maxLength];

}
