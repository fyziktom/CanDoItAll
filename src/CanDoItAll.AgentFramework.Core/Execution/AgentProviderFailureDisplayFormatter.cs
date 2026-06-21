using System.Net;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentProviderFailureCategory
{
    ProviderError,
    QuotaOrBilling,
    RateLimit
}

public sealed record AgentProviderFailureDisplay(
    AgentProviderFailureCategory Category,
    string Message,
    string ProviderDetail);

public static class AgentProviderFailureDisplayFormatter
{
    private const int MaxProviderDetailLength = 480;

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

    public static AgentProviderFailureDisplay Format(ProviderProfile provider, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(exception);

        var messages = CollectMessages(exception);
        var providerDetail = SelectProviderDetail(messages);
        var category = ResolveCategory(messages, exception);

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
            _ => new AgentProviderFailureDisplay(
                category,
                $"The agent run failed while using provider '{provider.Name}'. Provider detail: {providerDetail}",
                providerDetail)
        };
    }

    private static AgentProviderFailureCategory ResolveCategory(
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

    private static string SelectProviderDetail(IReadOnlyList<string> messages)
    {
        var detail =
            messages.FirstOrDefault(IsQuotaOrBillingMessage) ??
            messages.FirstOrDefault(IsRateLimitMessage) ??
            messages.FirstOrDefault(message => !LooksLikeRuntimeWrapper(message)) ??
            messages.FirstOrDefault() ??
            "Provider returned no error detail.";

        return Truncate(WorkflowExecutorRedaction.RedactText(detail), MaxProviderDetailLength);
    }

    private static bool LooksLikeRuntimeWrapper(string message)
    {
        return message.StartsWith("Provider runtime failed", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Usage was captured when available", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> CollectMessages(Exception exception)
    {
        var messages = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<Exception>();
        stack.Push(exception);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
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
            : message.Trim().ReplaceLineEndings(" ");

    private static string ResolveProviderAccountLabel(ProviderProfile provider)
        => provider.Kind switch
        {
            ProviderKind.OpenAi => "OpenAI API account",
            ProviderKind.AzureOpenAi => "Azure OpenAI resource",
            _ => "Provider account"
        };

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..(maxLength - 3)]}...";
    }
}
