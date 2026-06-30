using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentProviderFailureDisplayFormatterTests
{
    [Fact]
    public void Format_reports_openai_quota_and_billing_failure_from_inner_exception()
    {
        var provider = CreateProvider();
        var exception = new AgentRuntimeUsageException(
            "Provider runtime failed after provider activity. Usage was captured when available.",
            new InvalidOperationException("Error code: insufficient_quota. You exceeded your current quota, please check your plan and billing details."),
            []);

        var display = AgentProviderFailureDisplayFormatter.Format(provider, exception);

        Assert.Equal(AgentProviderFailureCategory.QuotaOrBilling, display.Category);
        Assert.Contains("OpenAI API account", display.Message, StringComparison.Ordinal);
        Assert.Contains("no remaining credits or quota", display.Message, StringComparison.Ordinal);
        Assert.Contains("OpenAI default", display.Message, StringComparison.Ordinal);
        Assert.Contains("insufficient_quota", display.ProviderDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_reports_rate_limit_without_claiming_credit_exhaustion()
    {
        var provider = CreateProvider();
        var exception = new InvalidOperationException("rate_limit_exceeded: please retry later.");

        var display = AgentProviderFailureDisplayFormatter.Format(provider, exception);

        Assert.Equal(AgentProviderFailureCategory.RateLimit, display.Category);
        Assert.Contains("rate limiting", display.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("credits", display.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_redacts_provider_detail()
    {
        var provider = CreateProvider();
        var exception = new InvalidOperationException("OpenAI failed with api_key=unit-redaction-secret and status code 402.");

        var display = AgentProviderFailureDisplayFormatter.Format(provider, exception);

        Assert.Equal(AgentProviderFailureCategory.QuotaOrBilling, display.Category);
        Assert.Contains("[REDACTED]", display.ProviderDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("unit-redaction-secret", display.ProviderDetail, StringComparison.Ordinal);
    }

    private static ProviderProfile CreateProvider()
        => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "OpenAI default",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-4.1",
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            false,
            true,
            "{}",
            string.Empty,
            "Not checked",
            null,
            []);
}
