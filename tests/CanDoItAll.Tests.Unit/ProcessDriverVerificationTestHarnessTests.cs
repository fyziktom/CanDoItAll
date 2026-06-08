using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverVerificationTestHarnessTests
{
    [Fact]
    public void Process_driver_shared_harness_SB016_INV_001_exposes_readonly_scopes_side_effect_denials_and_evidence_references()
    {
        var transcriptScope = ProcessDriverVerificationTestHarness.CreateReadonlyScope(
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
            ProcessDriverPermissionMode.VerificationOnly);
        var runtimeScope = ProcessDriverVerificationTestHarness.CreateReadonlyScope(
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
            ProcessDriverPermissionMode.ManagerReadonly);
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CommandTranscript,
            "bundle://proof/SB016/transcripts/shared-harness.txt",
            "shared harness proof",
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);

        AssertReadonlyScope(transcriptScope, ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification);
        AssertReadonlyScope(runtimeScope, ProcessDriverCapabilityScopeKind.RuntimeFactsRead);
        Assert.Equal(ProcessDriverPermissionMode.ManagerReadonly, runtimeScope.RequiredPermissionMode);
        Assert.All(ProcessDriverVerificationTestHarness.TranscriptReadonlyOperations, operation =>
            Assert.True(ProcessDriverOperationRules.IsReadonlyVerificationOperation(operation)));
        Assert.All(ProcessDriverVerificationTestHarness.RuntimeReadonlyOperations, operation =>
            Assert.True(ProcessDriverOperationRules.IsReadonlyVerificationOperation(operation)));
        Assert.All(ProcessDriverVerificationTestHarness.SideEffectOperations, operation =>
        {
            Assert.True(ProcessDriverOperationRules.IsSideEffectOperation(operation));
            Assert.NotEqual(ProcessDriverDenialReason.None, ProcessDriverOperationRules.ResolveReadonlyDenialReason(operation));
        });
        Assert.Equal(ProcessDriverEvidenceReferenceKind.CommandTranscript, evidenceReference.Kind);
        Assert.Equal(ProcessDriverCoreDescriptorFamily.ExecutionEvidence, evidenceReference.CoreDescriptorFamily);
        Assert.Matches("^[A-F0-9]{64}$", evidenceReference.ContentHash);
    }

    [Fact]
    public void Process_driver_shared_harness_SB017_INV_001_asserts_audit_redaction_and_no_mutation_contracts()
    {
        var scope = ProcessDriverVerificationTestHarness.CreateReadonlyScope(
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
            ProcessDriverPermissionMode.VerificationOnly);
        var redaction = new ProcessDriverRedactionDescriptor(
            ProcessDriverRedactionStatus.Redacted,
            [ProcessDriverRedactionKind.Secret, ProcessDriverRedactionKind.EmailAddress],
            ProcessDriverEvidencePolicy.ComputeSha256("[redacted]"));
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CommandTranscript,
            "bundle://proof/SB017/transcripts/audit-redaction.txt",
            "audit redaction",
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
        var auditFact = new ProcessDriverAuditFact(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DateTimeOffset.Parse("2026-06-08T12:00:00Z"),
            ProcessDriverAuditFactKind.DiagnosticReturned,
            "manager:readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            scope,
            scope.Kind,
            ProcessDriverOperation.ReturnDiagnostics,
            [evidenceReference],
            ProcessDriverDenialReason.None,
            redaction,
            "diagnostic summary [redacted-secret]",
            ProcessDriverEvidencePolicy.ComputeSha256("output"));
        var response = new ProcessDriverVerificationResponse(
            Accepted: true,
            ProcessDriverDenialReason.None,
            [
                new ProcessDriverDiagnostic(
                    ProcessDriverDiagnosticSeverity.Warning,
                    ProcessDriverDiagnosticCategory.AnalyzerWarning,
                    "warning [redacted-email]",
                    evidenceReference)
            ],
            [],
            redaction,
            NoMutationPerformed: true,
            [auditFact],
            ProcessDriverContractVersion.Current);

        ProcessDriverVerificationTestHarness.AssertNoMutation(response);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            response,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification);
        ProcessDriverVerificationTestHarness.AssertRedaction(
            response,
            ProcessDriverRedactionStatus.Redacted,
            ProcessDriverRedactionKind.Secret,
            ProcessDriverRedactionKind.EmailAddress);
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            response,
            "sk-live-secret",
            "person@example.invalid");
    }

    [Fact]
    public void Process_driver_shared_harness_SB026_INV_001_central_redaction_policy_redacts_secret_email_connection_string_and_bounds_summaries()
    {
        var sensitiveSummary = string.Join(
            Environment.NewLine,
            "ConnectionString=Host=localhost;Password=fixture-password;Username=admin",
            "token=plain-token",
            "reviewer@example.invalid",
            "secret=hidden",
            new string('x', ProcessDriverRedactionPolicy.DefaultMaxAuditSummaryLength + 128));

        var redaction = ProcessDriverRedactionPolicy.RedactDiagnosticSummary(sensitiveSummary);

        Assert.Equal(ProcessDriverRedactionStatus.Redacted, redaction.Descriptor.Status);
        Assert.Contains(ProcessDriverRedactionKind.ConnectionString, redaction.Descriptor.AppliedKinds);
        Assert.Contains(ProcessDriverRedactionKind.Secret, redaction.Descriptor.AppliedKinds);
        Assert.Contains(ProcessDriverRedactionKind.EmailAddress, redaction.Descriptor.AppliedKinds);
        Assert.True(redaction.WasTruncated);
        Assert.Equal(ProcessDriverRedactionPolicy.DefaultMaxAuditSummaryLength, redaction.RedactedText.Length);
        Assert.Contains("[redacted-connection-string]", redaction.RedactedText, StringComparison.Ordinal);
        Assert.Contains("[redacted-secret]", redaction.RedactedText, StringComparison.Ordinal);
        Assert.Contains("[redacted-email]", redaction.RedactedText, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-password", redaction.RedactedText, StringComparison.Ordinal);
        Assert.DoesNotContain("plain-token", redaction.RedactedText, StringComparison.Ordinal);
        Assert.DoesNotContain("reviewer@example.invalid", redaction.RedactedText, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden", redaction.RedactedText, StringComparison.Ordinal);
    }

    private static void AssertReadonlyScope(
        ProcessDriverCapabilityScope scope,
        ProcessDriverCapabilityScopeKind expectedKind)
    {
        Assert.Equal(expectedKind, scope.Kind);
        Assert.False(scope.AllowsProcessMutation);
        Assert.False(scope.AllowsExternalCalls);
        Assert.False(scope.AllowsWorkspaceWrites);
        Assert.False(scope.AllowsStorageWrites);
    }
}
