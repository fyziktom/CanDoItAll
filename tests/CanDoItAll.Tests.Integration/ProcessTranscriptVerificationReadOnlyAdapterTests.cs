using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessTranscriptVerificationReadOnlyAdapterTests
{
    [Fact]
    public void Process_transcript_readonly_adapter_maps_supplied_dotnet_evidence_to_readonly_observation()
    {
        const string transcript = """
CSC : warning CS8618: Non-nullable property 'Name' must contain a non-null value.
Program.cs(12,18): error CS1002: ; expected
token=sk-test-secret process.owner@example.com
""";
        var adapter = new ProcessTranscriptVerificationReadOnlyAdapter();
        var payload = CreatePayload(transcript, ProcessDriverTranscriptLanguage.DotNet);

        var observation = adapter.Verify(payload);

        Assert.True(observation.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, observation.DenialReason);
        Assert.True(observation.NoMutationPerformed);
        Assert.Equal(ProcessTranscriptVerificationSourceLane.DotNetRustTranscriptVerification, observation.SourceLane);
        Assert.Equal(payload.ProcessRunId, observation.ProcessRunId);
        Assert.Equal(payload.StepRunId, observation.StepRunId);
        Assert.Equal(payload.ArtifactId, observation.ArtifactId);
        Assert.Equal(ProcessDriverContractVersion.Current, observation.ContractVersion);
        Assert.Equal(ProcessDriverRedactionStatus.Redacted, observation.Redaction.Status);
        Assert.All(observation.Diagnostics, diagnostic =>
        {
            Assert.DoesNotContain("sk-test-secret", diagnostic.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("process.owner@example.com", diagnostic.Message, StringComparison.Ordinal);
        });
        Assert.Contains(observation.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.BuildWarning);
        Assert.Contains(observation.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NullableWarning);
        Assert.Contains(observation.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.BuildError);
        Assert.All(observation.AuditFacts, fact =>
        {
            Assert.Equal(ProcessDriverPermissionMode.VerificationOnly, fact.PermissionMode);
            Assert.Equal(ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification, fact.Scope.Kind);
            Assert.True(ProcessDriverOperationRules.IsReadonlyVerificationOperation(fact.RequestedOperation));
            Assert.DoesNotContain("sk-test-secret", fact.DiagnosticSummary, StringComparison.Ordinal);
            Assert.DoesNotContain("process.owner@example.com", fact.DiagnosticSummary, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Process_transcript_readonly_adapter_rejects_hash_mismatch_before_verifier_invocation()
    {
        var verifierCalled = false;
        var adapter = new ProcessTranscriptVerificationReadOnlyAdapter(_ =>
        {
            verifierCalled = true;
            throw new InvalidOperationException("Verifier must not be invoked for hash-mismatch preflight denial.");
        });
        var payload = CreatePayload(
            "Build succeeded.",
            ProcessDriverTranscriptLanguage.DotNet,
            transcriptHash: new string('A', 64));

        var observation = adapter.Verify(payload);

        Assert.False(verifierCalled);
        Assert.False(observation.Accepted);
        Assert.True(observation.NoMutationPerformed);
        Assert.Equal(ProcessDriverDenialReason.MissingEvidence, observation.DenialReason);
        Assert.Contains(observation.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.EvidenceHashMismatch);
        Assert.All(observation.AuditFacts, fact =>
        {
            Assert.Equal(ProcessDriverAuditFactKind.OperationDenied, fact.Kind);
            Assert.Equal(ProcessDriverDenialReason.MissingEvidence, fact.DenialReason);
        });
    }

    [Fact]
    public void Process_transcript_readonly_adapter_denies_mutation_and_untrusted_sources_without_verifier_invocation()
    {
        var adapter = new ProcessTranscriptVerificationReadOnlyAdapter(_ =>
            throw new InvalidOperationException("Verifier must not be invoked for read-only adapter preflight denial."));

        var mutationObservation = adapter.Verify(CreatePayload(
            "Build succeeded.",
            ProcessDriverTranscriptLanguage.DotNet,
            requestedOperations: [ProcessDriverOperation.ExecuteCommand]));
        var untrustedObservation = adapter.Verify(CreatePayload(
            "Build succeeded.",
            ProcessDriverTranscriptLanguage.DotNet,
            uri: "file:///tmp/transcript.txt"));

        Assert.False(mutationObservation.Accepted);
        Assert.True(mutationObservation.NoMutationPerformed);
        Assert.Equal(ProcessDriverDenialReason.UnsafeCommand, mutationObservation.DenialReason);
        Assert.Contains(mutationObservation.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.MutationAttemptDenied);
        Assert.False(untrustedObservation.Accepted);
        Assert.True(untrustedObservation.NoMutationPerformed);
        Assert.Equal(ProcessDriverDenialReason.MissingEvidence, untrustedObservation.DenialReason);
        Assert.Contains(untrustedObservation.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.TranscriptUntrusted);
    }

    [Fact]
    public void Process_transcript_readonly_adapter_rehearses_process_consumer_evidence_flow_without_state_mutation()
    {
        const string transcript = """
error[E0425]: cannot find value `answer` in this scope
thread 'tests::panics' panicked at src/lib.rs:7:9: explicit panic
test result: FAILED. 1 passed; 1 failed
""";
        var adapter = new ProcessTranscriptVerificationReadOnlyAdapter();
        var payload = CreatePayload(
            transcript,
            ProcessDriverTranscriptLanguage.Rust,
            requestedOperations:
            [
                ProcessDriverOperation.InspectExistingEvidence,
                ProcessDriverOperation.ReturnDiagnostics,
                ProcessDriverOperation.ReadProcessFacts
            ]);

        var observation = adapter.Verify(payload);

        Assert.True(observation.Accepted);
        Assert.True(observation.NoMutationPerformed);
        Assert.Equal(payload.ProcessRunId, observation.ProcessRunId);
        Assert.Equal(payload.StepRunId, observation.StepRunId);
        Assert.Equal(payload.RequestedAt, observation.ObservedAt);
        Assert.All(observation.EvidenceReferences, evidence =>
            Assert.Equal(evidence.ContentHash.ToUpperInvariant(), evidence.ContentHash));
        Assert.Contains(observation.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.CompileError);
        Assert.Contains(observation.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.PanicDetected);
        Assert.Contains(observation.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.CargoTestFailure);
        Assert.Contains(observation.AuditFacts, fact => fact.RequestedOperation == ProcessDriverOperation.ReadProcessFacts);
    }

    [Theory]
    [InlineData(ProcessDriverCapabilityScopeKind.OfficeEvidenceRead)]
    [InlineData(ProcessDriverCapabilityScopeKind.BusinessAnalysisRead)]
    public void Process_transcript_readonly_adapter_denies_non_dotnet_rust_lanes(
        ProcessDriverCapabilityScopeKind scopeKind)
    {
        var adapter = new ProcessTranscriptVerificationReadOnlyAdapter(_ =>
            throw new InvalidOperationException("Verifier must not be invoked for unsupported domain lane."));
        var payload = CreatePayload(
            "Build succeeded.",
            ProcessDriverTranscriptLanguage.DotNet,
            scope: CreateReadonlyScope(scopeKind));

        var observation = adapter.Verify(payload);

        Assert.False(observation.Accepted);
        Assert.True(observation.NoMutationPerformed);
        Assert.Equal(ProcessDriverDenialReason.CapabilityScopeDenied, observation.DenialReason);
        Assert.Contains(observation.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat);
        Assert.All(observation.AuditFacts, fact =>
            Assert.Equal(scopeKind, fact.Scope.Kind));
    }

    private static ProcessTranscriptVerificationReadOnlyEvidencePayload CreatePayload(
        string transcriptText,
        ProcessDriverTranscriptLanguage language,
        string? transcriptHash = null,
        string uri = "bundle://proof/SB024/transcripts/process-supplied-transcript.txt",
        ProcessDriverCapabilityScope? scope = null,
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null)
    {
        var evidenceReference = new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CommandTranscript,
            uri,
            ComputeSha256(transcriptText),
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
        var transcriptReference = new ProcessDriverTranscriptReference(
            uri,
            transcriptHash ?? ComputeSha256(transcriptText),
            language,
            language == ProcessDriverTranscriptLanguage.DotNet ? "dotnet" : "cargo",
            language == ProcessDriverTranscriptLanguage.DotNet ? "net10.0" : "rust-stable");

        return new ProcessTranscriptVerificationReadOnlyEvidencePayload(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "process-consumer:readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            scope ?? CreateReadonlyScope(ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification),
            transcriptReference,
            transcriptText,
            [evidenceReference],
            requestedOperations ?? [ProcessDriverOperation.InspectExistingEvidence, ProcessDriverOperation.ReturnDiagnostics],
            DateTimeOffset.Parse("2026-06-08T01:00:00Z"));
    }

    private static ProcessDriverCapabilityScope CreateReadonlyScope(ProcessDriverCapabilityScopeKind kind)
    {
        return new ProcessDriverCapabilityScope(
            kind,
            ProcessDriverPermissionMode.VerificationOnly,
            AllowsProcessMutation: false,
            AllowsExternalCalls: false,
            AllowsWorkspaceWrites: false,
            AllowsStorageWrites: false);
    }

    private static string ComputeSha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
