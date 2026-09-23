using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using System.Net;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentProviderFailureDisplayFormatterTests
{
    [Fact]
    public void TryFormat_requires_explicit_provider_origin()
    {
        var provider = CreateProvider();
        var unclassified = new AgentRuntimeUsageException(
            "Runtime failed.",
            new HttpRequestException("A tool-owned HTTP request failed."),
            []);
        var toolFailure = new AgentRuntimeUsageException(
            "Tool failed.",
            new HttpRequestException("A tool-owned HTTP request failed."),
            [],
            failureOrigin: AgentRuntimeFailureOrigin.Tool);
        var providerFailure = new AgentRuntimeUsageException(
            "Provider failed.",
            new InvalidOperationException("Provider transport failed."),
            [],
            failureOrigin: AgentRuntimeFailureOrigin.Provider);

        Assert.False(AgentProviderFailureDisplayFormatter.TryFormat(
            provider,
            unclassified,
            out _));
        Assert.False(AgentProviderFailureDisplayFormatter.TryFormat(
            provider,
            toolFailure,
            out _));
        Assert.True(AgentProviderFailureDisplayFormatter.TryFormat(
            provider,
            providerFailure,
            out var display));
        Assert.Equal(AgentProviderFailureCategory.ProviderError, display.Category);

        var aggregate = new AggregateException(
            providerFailure,
            new InvalidOperationException("An unrelated runtime finalizer failed."));
        Assert.False(AgentProviderFailureDisplayFormatter.TryFormat(
            provider,
            aggregate,
            out _));

        var runtimeFailureWithProviderInner = new AgentRuntimeUsageException(
            "Runtime failed after the provider fault.",
            providerFailure,
            [],
            failureOrigin: AgentRuntimeFailureOrigin.Runtime);
        Assert.False(AgentProviderFailureDisplayFormatter.TryFormat(
            provider,
            runtimeFailureWithProviderInner,
            out _));
    }

    [Fact]
    public void Format_reports_openai_quota_and_billing_failure_from_inner_exception()
    {
        var provider = CreateProvider();
        var exception = CreateProviderFailure(
            "Provider runtime failed after provider activity. Usage was captured when available.",
            new InvalidOperationException("Error code: insufficient_quota. You exceeded your current quota, please check your plan and billing details."));

        Assert.True(AgentProviderFailureDisplayFormatter.TryFormat(provider, exception, out var display));

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
        var exception = CreateProviderFailure(
            "Provider request failed.",
            new InvalidOperationException("rate_limit_exceeded: please retry later."));

        Assert.True(AgentProviderFailureDisplayFormatter.TryFormat(provider, exception, out var display));

        Assert.Equal(AgentProviderFailureCategory.RateLimit, display.Category);
        Assert.Contains("rate limiting", display.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("credits", display.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_redacts_provider_detail()
    {
        var provider = CreateProvider();
        var exception = CreateProviderFailure(
            "Provider request failed.",
            new InvalidOperationException("OpenAI failed with api_key=unit-redaction-secret and status code 402."));

        Assert.True(AgentProviderFailureDisplayFormatter.TryFormat(provider, exception, out var display));

        Assert.Equal(AgentProviderFailureCategory.QuotaOrBilling, display.Category);
        Assert.Equal(
            "Provider reported a quota or billing restriction.",
            display.ProviderDetail);
        Assert.DoesNotContain("unit-redaction-secret", display.ProviderDetail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(OpenAiModelIds.Gpt56Terra)]
    [InlineData(OpenAiModelIds.Gpt56Luna)]
    [InlineData("gpt-5.4-mini")]
    public void Format_reports_reasoning_function_tool_request_compatibility_independently_of_model(
        string model)
    {
        var provider = CreateProvider(ProviderTransportKind.ChatCompletions);
        var exception = CreateProviderFailure(
            "Provider request failed.",
            new InvalidOperationException(
                $"HTTP 400 (invalid_request_error: ) Parameter: reasoning_effort Function tools with reasoning_effort are not supported for {model} in /v1/chat/completions. To use function tools, use /v1/responses or set reasoning_effort to 'none'."));

        Assert.True(AgentProviderFailureDisplayFormatter.TryFormat(provider, exception, out var display));

        Assert.Equal(AgentProviderFailureCategory.RequestCompatibility, display.Category);
        Assert.Contains("function tools", display.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Responses", display.Message, StringComparison.Ordinal);
        Assert.Contains("reasoning effort to 'none'", display.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_does_not_treat_unknown_bad_request_as_request_compatibility()
    {
        var provider = CreateProvider();
        var exception = CreateProviderFailure(
            "Provider request failed.",
            new InvalidOperationException(
                "HTTP 400 (invalid_request_error) Parameter: messages. Invalid message shape."));

        Assert.True(AgentProviderFailureDisplayFormatter.TryFormat(provider, exception, out var display));

        Assert.Equal(AgentProviderFailureCategory.ProviderError, display.Category);
    }

    [Fact]
    public void Format_does_not_apply_openai_chat_remediation_to_other_transports()
    {
        var provider = CreateProvider(ProviderTransportKind.Responses);
        var exception = CreateProviderFailure(
            "Provider request failed.",
            new InvalidOperationException(
                "HTTP 400 (invalid_request_error: ) Parameter: reasoning_effort Function tools with reasoning_effort are not supported."));

        Assert.True(AgentProviderFailureDisplayFormatter.TryFormat(provider, exception, out var display));

        Assert.Equal(AgentProviderFailureCategory.ProviderError, display.Category);
    }

    [Fact]
    public void Format_reports_provider_configuration_without_exposing_raw_exception_content()
    {
        var provider = CreateProvider();
        const string secret = "provider-secret-must-not-escape";
        var exception = new AgentRuntimeUsageException(
            "Provider configuration failed.",
            new InvalidOperationException($"Invalid endpoint containing {secret}."),
            [],
            failureOrigin: AgentRuntimeFailureOrigin.ProviderConfiguration);

        Assert.True(AgentProviderFailureDisplayFormatter.TryFormat(provider, exception, out var display));

        Assert.Equal(AgentProviderFailureCategory.ProviderConfiguration, display.Category);
        Assert.Contains("credential, endpoint, transport, and runtime settings", display.Message, StringComparison.Ordinal);
        Assert.Equal("Provider configuration validation failed.", display.ProviderDetail);
        Assert.DoesNotContain(secret, display.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, display.ProviderDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_bounds_large_exception_graphs()
    {
        var provider = CreateProvider();
        var failures = Enumerable.Range(0, 100)
            .Select(index => new InvalidOperationException(new string('x', 2_000) + index))
            .ToArray();
        var exception = CreateProviderFailure(
            "Provider request failed.",
            new AggregateException(failures));

        Assert.True(AgentProviderFailureDisplayFormatter.TryFormat(provider, exception, out var display));

        Assert.Equal(AgentProviderFailureCategory.ProviderError, display.Category);
        Assert.True(display.ProviderDetail.Length <= 480);
    }

    [Theory]
    [InlineData(401, "token")]
    [InlineData(403, "permissions")]
    public void Format_shared_access_rejection_preserves_status_and_safe_recovery(int status, string guidance) {
        var provider = CreateSharedProvider();
        const string remoteSecret = "private-upstream-body-must-not-escape";
        var boundary = ProviderFailureDisclosurePolicy.CreateBoundaryException(provider,
            ProviderFailureOperation.RuntimeRequest,
            new InvalidOperationException($"{remoteSecret} at {provider.BaseUrl}"), status);
        var exception = CreateProviderFailure("Provider failed.", new AggregateException(boundary));

        Assert.True(AgentProviderFailureDisplayFormatter.TryFormat(provider, exception, out var display));
        Assert.Equal(AgentProviderFailureCategory.ProviderConfiguration, display.Category);
        Assert.Contains($"HTTP {status}", display.ProviderDetail, StringComparison.Ordinal);
        Assert.Contains("shared", display.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(guidance, display.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Shared provider connections", display.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(remoteSecret, display.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(provider.BaseUrl, display.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderFailureBoundaryException", display.Message, StringComparison.Ordinal);
        Assert.Null(boundary.InnerException);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(502)]
    [InlineData(503)]
    public void Format_shared_other_status_is_not_reported_as_expired_token(int status) {
        var provider = CreateSharedProvider();
        var boundary = ProviderFailureDisclosurePolicy.CreateBoundaryException(provider,
            ProviderFailureOperation.RuntimeRequest, diagnosticStatusCode: status);

        Assert.True(AgentProviderFailureDisplayFormatter.TryFormat(provider,
            CreateProviderFailure("Provider failed.", boundary), out var display));
        Assert.Equal(AgentProviderFailureCategory.ProviderError, display.Category);
        Assert.Contains($"HTTP {status}", display.ProviderDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("expired", display.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_local_upstream_401_does_not_claim_shared_connection_failure() {
        var exception = CreateProviderFailure("Provider failed.",
            new HttpRequestException("Sensitive upstream body.", null, HttpStatusCode.Unauthorized));

        Assert.True(AgentProviderFailureDisplayFormatter.TryFormat(CreateProvider(), exception, out var display));
        Assert.Equal(AgentProviderFailureCategory.ProviderError, display.Category);
        Assert.Contains("HTTP 401", display.ProviderDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("Shared provider connections", display.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Sensitive upstream body", display.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(999)]
    public void Format_unknown_boundary_does_not_guess_authentication_failure(int? status) {
        var provider = CreateSharedProvider();
        var boundary = ProviderFailureDisclosurePolicy.CreateBoundaryException(provider,
            ProviderFailureOperation.RuntimeRequest, diagnosticStatusCode: status);

        Assert.True(AgentProviderFailureDisplayFormatter.TryFormat(provider,
            CreateProviderFailure("Provider failed.", boundary), out var display));
        Assert.Equal(AgentProviderFailureCategory.ProviderError, display.Category);
        Assert.DoesNotContain("expired", display.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HTTP 999", display.ProviderDetail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AgentRuntimeFailureOrigin.Tool)]
    [InlineData(AgentRuntimeFailureOrigin.Runtime)]
    public void Format_shared_tool_or_runtime_failure_is_not_reclassified(AgentRuntimeFailureOrigin origin) {
        var provider = CreateSharedProvider();
        var boundary = ProviderFailureDisclosurePolicy.CreateBoundaryException(provider,
            ProviderFailureOperation.RuntimeRequest, diagnosticStatusCode: 401);
        var exception = new AgentRuntimeUsageException("Non-provider failure.", boundary, [], failureOrigin: origin);

        Assert.False(AgentProviderFailureDisplayFormatter.TryFormat(provider, exception, out _));
    }

    private static ProviderProfile CreateSharedProvider() => CreateProvider() with {
        BaseUrl = "http://private-source.example.test/api/shared-providers/openai/v1",
        CredentialBinding = new ProviderCredentialBinding(Guid.NewGuid(),
            ProviderCredentialPurpose.SourceAccessToken, ProviderCredentialConsumerKind.Source, Guid.NewGuid())
    };

    private static ProviderProfile CreateProvider(
        ProviderTransportKind transport = ProviderTransportKind.Responses)
        => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "OpenAI default",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-4.1",
            transport,
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

    private static AgentRuntimeUsageException CreateProviderFailure(
        string message,
        Exception innerException)
        => new(
            message,
            innerException,
            [],
            failureOrigin: AgentRuntimeFailureOrigin.Provider);
}
