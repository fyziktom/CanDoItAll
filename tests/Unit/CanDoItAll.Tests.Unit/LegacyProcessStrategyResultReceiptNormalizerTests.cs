using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class LegacyProcessStrategyResultReceiptNormalizerTests
{
    private const string LegacyHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData(ProcessRecoveryRouteKind.None)]
    [InlineData(ProcessRecoveryRouteKind.ChildRunPropagation)]
    public void Normalize_LegacyReceipt_CanonicalizesBoundedFields(ProcessRecoveryRouteKind legacyRouteKind)
    {
        var stepInstanceId = ProcessStepInstanceId.New();
        var receipt = new StrategyResultReceipt(
            stepInstanceId,
            new StrategyId("strategy.adapter.workflow.execute"),
            StrategyResultIdempotencyKey.New(),
            StrategyOutcome.NeedsManager,
            ProcessRuntimeStepStatus.Blocked,
            LegacyHash,
            [
                new StrategyResultDiagnosticReceipt(
                    "process.runtime.legacy_diagnostic",
                    StrategyDiagnosticSensitivity.Normal,
                    LegacyHash,
                    @"Diagnostic evidence is stored at C:\legacy\diagnostic.txt",
                    "file:///legacy/diagnostic.txt",
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Unknown)
            ],
            recoveryDecision: new ProcessRecoveryDecisionReceipt(
                ProcessFailureCategory.ProviderFailure,
                ProcessRecoveryDecisionKind.ManagerRequired,
                "legacy recovery code!",
                "legacy policy!",
                @"Recovery evidence is stored at C:\legacy\recovery.txt")
            {
                RouteKind = legacyRouteKind,
                DiagnosticFingerprint = LegacyHash,
                AutomaticRetryAttempt = 1,
                MaximumAutomaticRetryAttempts = 4,
                SameDiagnosticFingerprintAttempt = 1,
                MaximumSameDiagnosticFingerprintAttempts = 2
            })
        {
            UserSafeSummary = @"Workflow output is stored at C:\legacy\result.txt"
        };

        var normalized = LegacyProcessStrategyResultReceiptNormalizer.Normalize(receipt);

        Assert.Equal("sha256:" + LegacyHash, normalized.ResultHash);
        Assert.True(ProcessPublicReceiptTextPolicy.IsSafe(
            normalized.UserSafeSummary,
            ProcessStrategyResultLimits.MaximumUserSafeSummaryLength));
        var diagnostic = Assert.Single(normalized.Diagnostics);
        Assert.Equal("sha256:" + LegacyHash, diagnostic.EvidenceHash);
        Assert.True(ProcessPublicReceiptTextPolicy.IsSafe(
            diagnostic.SafeSummary,
            ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength));
        Assert.Null(diagnostic.RestrictedEvidenceReference);
        var recovery = Assert.IsType<ProcessRecoveryDecisionReceipt>(normalized.RecoveryDecision);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, recovery.RouteKind);
        Assert.Equal(stepInstanceId, recovery.ResponsibleStepInstanceId);
        Assert.Equal("sha256:" + LegacyHash, recovery.DiagnosticFingerprint);
        Assert.True(ProcessPublicReceiptTextPolicy.IsSafe(
            recovery.SafeReason,
            ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength));
    }

    [Fact]
    public void Normalize_UnsupportedResultHash_ThrowsWithoutDisclosingValue()
    {
        const string unsupportedHash = "secret=legacy-result-hash";
        var receipt = new StrategyResultReceipt(
            ProcessStepInstanceId.New(),
            new StrategyId("strategy.adapter.workflow.execute"),
            StrategyResultIdempotencyKey.New(),
            StrategyOutcome.Succeeded,
            ProcessRuntimeStepStatus.Completed,
            unsupportedHash);

        var exception = Assert.Throws<InvalidOperationException>(
            () => LegacyProcessStrategyResultReceiptNormalizer.Normalize(receipt));

        Assert.DoesNotContain(unsupportedHash, exception.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported digest shape", exception.Message, StringComparison.Ordinal);
    }
}
