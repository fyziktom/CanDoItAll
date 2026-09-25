using System.Globalization;
using System.Net;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessProviderFailureRecoveryTests {
    [Theory]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(404, false)]
    [InlineData(408, true)]
    [InlineData(429, true)]
    [InlineData(500, true)]
    [InlineData(502, true)]
    [InlineData(503, true)]
    [InlineData(504, true)]
    public void Provider_status_controls_recovery_instead_of_generic_service_failure(int status, bool retryable) {
        var exception = new InvalidOperationException("Provider request failed",
            new HttpRequestException($"Service request failed. Status: {status.ToString(CultureInfo.InvariantCulture)}",
                inner: null, (HttpStatusCode)status));

        var recovered = ProcessAgentExecutionRecoveryPolicy.TryBuildRetryableAgentTransientExecutionIssue(
            Assignment(), exception, out var issue);

        Assert.Equal(retryable, recovered);
        if (retryable) {
            Assert.Equal(ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionRetry, issue.Code);
            Assert.Null(issue.ExecutionSafetyAttestation);
        } else {
            Assert.Null(issue);
        }
    }

    [Theory]
    [InlineData("Service request failed", false)]
    [InlineData("Service request failed. permission denied", false)]
    [InlineData("PolicyDenied: Service request failed. Status: 503", false)]
    [InlineData("Service request failed. Status: 401. transient", false)]
    [InlineData("Service request failed. Status: 403. timeout", false)]
    [InlineData("Service request failed. Status: 404. Model does not exist", false)]
    [InlineData("provider runtime temporarily unavailable", true)]
    [InlineData("connection reset", true)]
    public void Unstructured_fallback_preserves_permanent_and_policy_boundaries(string message, bool retryable) {
        Assert.Equal(retryable, ProcessRuntimeFailureClassifier.LooksLikeTransientAgentExecutionFailure(message));
        Assert.Equal(retryable, ProcessRuntimeFailureClassifier.LooksLikeTransientAgentExecutionFailure(message.ToUpperInvariant()));
    }

    [Fact]
    public void Caller_cancellation_is_never_retried_even_when_message_mentions_timeout() {
        var failure = new OperationCanceledException("Service request failed: timeout", new CancellationToken(true));

        Assert.False(ProcessAgentExecutionRecoveryPolicy.TryBuildRetryableAgentTransientExecutionIssue(
            Assignment(), failure, out var issue));
        Assert.Null(issue);
    }

    [Fact]
    public void Typed_provider_failure_is_not_misreported_as_a_finalizer_failure() {
        var failure = new AgentRuntimeUsageException("submit_process_step_outcome provider request failed",
            new HttpRequestException("Service request failed", null, HttpStatusCode.Unauthorized),
            [], failureOrigin: AgentRuntimeFailureOrigin.Provider);

        Assert.False(ProcessAgentExecutionRecoveryPolicy.TryBuildRetryableAgentOutputContractIssue(
            Assignment(), failure, out var issue));
        Assert.Null(issue);
    }

    [Fact]
    public void Policy_denial_in_a_wrapper_overrides_a_transient_transport_cause() {
        var failure = new InvalidOperationException("PolicyDenied: service request failed",
            new HttpRequestException("temporarily unavailable", null, HttpStatusCode.ServiceUnavailable));
        Assert.False(ProcessRuntimeFailureClassifier.LooksLikeTransientAgentExecutionFailure(failure));
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("ar-EG")]
    public void Provider_status_fallback_is_culture_independent(string cultureName) {
        var original = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            Assert.False(ProcessRuntimeFailureClassifier.LooksLikeTransientAgentExecutionFailure("SERVICE REQUEST FAILED. STATUS: 401. transient"));
            Assert.True(ProcessRuntimeFailureClassifier.LooksLikeTransientAgentExecutionFailure("SERVICE REQUEST FAILED. STATUS: 503"));
        } finally {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Timeout_and_network_causes_remain_transient_but_do_not_supply_effect_attestation() {
        foreach (var cause in new Exception[] { new TimeoutException("operation timeout"), new HttpRequestException("network failure") }) {
            Assert.True(ProcessAgentExecutionRecoveryPolicy.TryBuildRetryableAgentTransientExecutionIssue(Assignment(), cause, out var issue));
            Assert.Null(issue.ExecutionSafetyAttestation);
        }
    }

    private static ProcessRuntimeStepAssignment Assignment() => new(
        ProcessRunId.New(), ProcessInstancePlanId.New(), ProcessStepInstanceId.New(),
        "artifact", "worker", string.Empty, "Worker", "agent", Guid.NewGuid().ToString("D"), "Worker",
        "Create an artifact", "ready", "fixture", [], [], [], string.Empty,
        new Dictionary<string, string>(), null, DateTimeOffset.UtcNow);

    [Fact]
    public void Typed_policy_denial_keeps_its_identity_inside_a_tool_failure_without_authorizing_recovery() {
        var failure = new AgentRuntimeUsageException("Tool execution failed",
            new AgentToolPolicyBlockedException(ToolContractCatalog.WorkspaceWriteFile, ToolInvocationDecisionKind.Deny, "PRIVATE_TARGET"),
            [], failureOrigin: AgentRuntimeFailureOrigin.Tool);
        var kind = ProcessRuntimeFailureClassifier.Classify(failure);
        Assert.Equal(ProcessAgentFailureKind.RightsBoundary, kind);
        Assert.False(ProcessAgentExecutionRecoveryPolicy.TryBuildRetryableAgentTransientExecutionIssue(Assignment(), failure, out _));
        var result = ProcessExecutionResultFactory.TerminalAgentFailure(kind, Guid.NewGuid(), failure.Message);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ProcessExecutionAdapterDiagnosticCodes.AgentToolPermissionDenied, diagnostic.Code.Value);
        Assert.NotEqual(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
        Assert.Contains("permission or workspace boundary", result.UserSafeSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_TARGET", result.UserSafeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Unconfirmed_tool_failure_requires_reconciliation_without_claiming_a_committed_or_safe_effect() {
        var result = ProcessExecutionResultFactory.TerminalAgentFailure(ProcessAgentFailureKind.Unknown, Guid.NewGuid(), "PRIVATE_ENDPOINT_RESPONSE");
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ProcessDiagnosticRetrySafety.Unknown, diagnostic.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.Unknown, diagnostic.Idempotency);
        Assert.Contains("Reconcile any unconfirmed tool effects", result.UserSafeSummary, StringComparison.Ordinal);
        Assert.Contains("automatic replay is not authorized", result.UserSafeSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_ENDPOINT_RESPONSE", result.UserSafeSummary, StringComparison.Ordinal);
    }
}
