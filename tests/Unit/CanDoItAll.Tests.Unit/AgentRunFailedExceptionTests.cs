using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentRunFailedExceptionTests
{
    [Fact]
    public void Run_failure_exposes_only_the_explicit_sanitized_display_message()
    {
        var exception = new AgentRunFailedException(
            Guid.NewGuid(),
            Guid.NewGuid(),
            chatSessionId: null,
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("api_key=provider-secret"),
            "The configured provider rejected the request.",
            AgentProviderFailureCategory.RequestCompatibility);

        Assert.Equal(
            "The configured provider rejected the request.",
            exception.SanitizedDisplayMessage);
        Assert.Equal(
            AgentProviderFailureCategory.RequestCompatibility,
            exception.FailureCategory);
        Assert.DoesNotContain("provider-secret", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("api_key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Chat_failure_uses_a_safe_message_when_the_explicit_display_is_empty()
    {
        var exception = new AgentChatRunFailedException(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("Bearer provider-secret"),
            "   ",
            AgentProviderFailureCategory.ProviderError);

        Assert.Equal(
            "The agent run failed while contacting its configured provider.",
            exception.SanitizedDisplayMessage);
        Assert.DoesNotContain("provider-secret", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Chat_failure_redacts_an_untrusted_explicit_display_message()
    {
        const string displaySecret = "untrusted-display-secret";
        var exception = new AgentChatRunFailedException(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("Synthetic provider failure."),
            $"Provider rejected credential={displaySecret}.",
            AgentProviderFailureCategory.ProviderError);

        Assert.DoesNotContain(displaySecret, exception.SanitizedDisplayMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(displaySecret, exception.Message, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", exception.SanitizedDisplayMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_failure_does_not_imply_a_provider_category_by_default()
    {
        var exception = new AgentRunFailedException(
            Guid.NewGuid(),
            Guid.NewGuid(),
            chatSessionId: null,
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("Tool or persistence failure."),
            "Inspect the persisted run using its execution-run ID.");

        Assert.Null(exception.FailureCategory);
    }
}
