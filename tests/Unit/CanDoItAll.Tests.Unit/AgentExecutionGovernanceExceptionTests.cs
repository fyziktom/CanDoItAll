using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentExecutionGovernanceExceptionTests
{
    [Fact]
    public void Constructor_preserves_typed_kind_and_sanitizes_display_message()
    {
        const string secret = "governance-secret-must-not-escape";
        var exception = new AgentExecutionGovernanceException(
            AgentExecutionGovernanceFailureKind.FinalizerValidation,
            $"Finalizer failed validation with api_key={secret}.\nInspect the invocation sequence.");

        Assert.Equal(
            AgentExecutionGovernanceFailureKind.FinalizerValidation,
            exception.FailureKind);
        Assert.Contains("failed validation", exception.SanitizedDisplayMessage, StringComparison.Ordinal);
        Assert.Contains("Inspect the invocation sequence", exception.SanitizedDisplayMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, exception.SanitizedDisplayMessage, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', exception.SanitizedDisplayMessage);
    }
}
