using System.Runtime.CompilerServices;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.TranscriptVerification;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverTranscriptVerificationAlphaTests
{
    [Fact]
    public void Process_driver_transcript_alpha_SB010_INV_001_internals_are_split_without_runtime_or_io_surface()
    {
        var root = FindRepositoryRoot();
        var source = ReadProjectSource(root);

        Assert.Contains("internal readonly record struct TranscriptDiagnosticRule", source, StringComparison.Ordinal);
        Assert.Contains("internal static class TranscriptDiagnosticRules", source, StringComparison.Ordinal);
        Assert.Contains("internal static class TranscriptDiagnosticRuleEvaluator", source, StringComparison.Ordinal);
        Assert.Contains("internal static class TranscriptVerificationEvidencePolicy", source, StringComparison.Ordinal);
        Assert.Contains("internal static class TranscriptVerificationRedaction", source, StringComparison.Ordinal);
        Assert.Contains("internal static class TranscriptVerificationRequestPolicy", source, StringComparison.Ordinal);
        Assert.Contains("internal static class TranscriptVerificationAuditFactBuilder", source, StringComparison.Ordinal);
        AssertNoForbiddenAlphaPackageTokens(source);
    }

    [Fact]
    public void Process_driver_transcript_alpha_SB011_INV_001_malicious_corpus_is_supplied_text_only_redacted_and_readonly()
    {
        var verifier = new TranscriptVerificationAlphaVerifier();
        var corpus = new[]
        {
            ("malicious-prompt-path-secret-transcript.txt", ProcessDriverTranscriptLanguage.DotNet),
            ("malicious-mixed-dotnet-rust-transcript.txt", ProcessDriverTranscriptLanguage.DotNet),
            ("malicious-mixed-dotnet-rust-transcript.txt", ProcessDriverTranscriptLanguage.Rust),
            ("malicious-oversized-transcript.txt", ProcessDriverTranscriptLanguage.DotNet)
        };

        foreach (var (fileName, language) in corpus)
        {
            var transcript = ReadRepositoryFile(
                "tests",
                "CanDoItAll.Tests.Unit",
                "TestData",
                "ProcessDriverContractTranscripts",
                fileName);
            var result = verifier.Verify(CreateRequest(transcript, language));

            Assert.True(result.Accepted);
            Assert.Equal(ProcessDriverDenialReason.None, result.DenialReason);
            ProcessDriverVerificationTestHarness.AssertNoMutation(result);
            Assert.NotEmpty(result.Diagnostics);
            ProcessDriverVerificationTestHarness.AssertRedaction(result, ProcessDriverRedactionStatus.Redacted);
            Assert.All(result.Diagnostics, AssertDiagnosticDoesNotLeakMaliciousText);
            ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
                result,
                "fixture-password",
                "@example.invalid",
                "C:\\Users");
            Assert.All(result.AuditFacts, fact =>
            {
                Assert.Equal(ProcessDriverAuditFactKind.DiagnosticReturned, fact.Kind);
                Assert.DoesNotContain("fixture-password", fact.DiagnosticSummary, StringComparison.Ordinal);
                Assert.DoesNotContain("@example.invalid", fact.DiagnosticSummary, StringComparison.Ordinal);
                Assert.DoesNotContain("C:\\Users", fact.DiagnosticSummary, StringComparison.Ordinal);
            });
        }

        var oversized = ReadRepositoryFile(
            "tests",
            "CanDoItAll.Tests.Unit",
            "TestData",
            "ProcessDriverContractTranscripts",
            "malicious-oversized-transcript.txt");
        var redactedOversized = ProcessDriverRedactionPolicy.Redact(oversized);

        Assert.True(oversized.Length > ProcessDriverRedactionPolicy.DefaultMaxRedactedTextLength);
        Assert.True(redactedOversized.WasTruncated);
    }

    [Fact]
    public void Process_driver_transcript_alpha_SB012_INV_001_dotnet_semantic_diagnostics_are_readonly_redacted_and_audited()
    {
        const string transcript = """
CSC : warning CS8618: Non-nullable property 'Name' must contain a non-null value.
Program.cs(12,18): error CS1002: ; expected
Failed CanDoItAll.Tests.Unit.ProcessDriverTranscriptVerificationAlphaTests
NETSDK1045: The current .NET SDK does not support targeting .NET 11.0.
missing artifact proof/SB012/manifest.md
runtime proof gap: no source assertion was captured
warning CA1416: This call site is reachable on all platforms.
token=sk-live-secret lucy@example.com
""";
        var verifier = new TranscriptVerificationAlphaVerifier();
        var result = verifier.Verify(CreateRequest(transcript, ProcessDriverTranscriptLanguage.DotNet));

        Assert.True(result.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, result.DenialReason);
        Assert.True(result.NoMutationPerformed);
        Assert.Equal(ProcessDriverContractVersion.Current, result.ContractVersion);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            result,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification);
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            result,
            "sk-live-secret",
            "lucy@example.com");

        var categories = result.Diagnostics.Select(diagnostic => diagnostic.Category).ToHashSet();
        Assert.Contains(ProcessDriverDiagnosticCategory.BuildWarning, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.NullableWarning, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.BuildError, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.TestFailure, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.UnsupportedTargetFramework, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.MissingArtifact, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.RuntimeProofGap, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.PlatformCompatibilityWarning, categories);
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.Same(result.EvidenceReferences[0], diagnostic.EvidenceReference);
            Assert.DoesNotContain("sk-live-secret", diagnostic.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("lucy@example.com", diagnostic.Message, StringComparison.Ordinal);
        });
        Assert.Equal(ProcessDriverRedactionStatus.Redacted, result.Redaction.Status);
        ProcessDriverVerificationTestHarness.AssertRedaction(
            result,
            ProcessDriverRedactionStatus.Redacted,
            ProcessDriverRedactionKind.Secret,
            ProcessDriverRedactionKind.EmailAddress);
    }

    [Fact]
    public void Process_driver_transcript_alpha_SB015_INV_001_rust_semantic_diagnostics_are_readonly_redacted_and_audited()
    {
        const string transcript = """
error[E0425]: cannot find value `answer` in this scope
thread 'tests::panics' panicked at src/lib.rs:7:9: explicit panic
test result: FAILED. 1 passed; 1 failed
warning: this lint is denied by clippy::unwrap_used
missing cargo artifact target/debug/deps/candoitall.rlib
unsupported toolchain nightly-2099
password=hunter2 rust.user@example.com
""";
        var verifier = new TranscriptVerificationAlphaVerifier();
        var result = verifier.Verify(CreateRequest(transcript, ProcessDriverTranscriptLanguage.Rust));

        Assert.True(result.Accepted);
        Assert.True(result.NoMutationPerformed);
        Assert.Equal(ProcessDriverDenialReason.None, result.DenialReason);

        var categories = result.Diagnostics.Select(diagnostic => diagnostic.Category).ToHashSet();
        Assert.Contains(ProcessDriverDiagnosticCategory.CompileError, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.PanicDetected, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.CargoTestFailure, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ClippyWarning, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.MissingCargoArtifact, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.UnsupportedToolchain, categories);
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.DoesNotContain("hunter2", diagnostic.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("rust.user@example.com", diagnostic.Message, StringComparison.Ordinal);
        });
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            result,
            "hunter2",
            "rust.user@example.com");
        ProcessDriverVerificationTestHarness.AssertRedaction(
            result,
            ProcessDriverRedactionStatus.Redacted,
            ProcessDriverRedactionKind.Secret,
            ProcessDriverRedactionKind.EmailAddress);
    }

    [Fact]
    public void Process_driver_transcript_alpha_SB018_INV_001_permission_denials_and_response_mapping_reject_side_effects()
    {
        var verifier = new TranscriptVerificationAlphaVerifier();

        foreach (var operation in ProcessDriverVerificationTestHarness.SideEffectOperations)
        {
            var result = verifier.Verify(CreateRequest(
                "Build succeeded.",
                ProcessDriverTranscriptLanguage.DotNet,
                requestedOperations: [operation]));

            ProcessDriverVerificationTestHarness.AssertSideEffectDenied(result, operation);
        }

        var officeScopeResult = verifier.Verify(CreateRequest(
            "Build succeeded.",
            ProcessDriverTranscriptLanguage.DotNet,
            scope: ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
                ProcessDriverPermissionMode.VerificationOnly)));

        Assert.False(officeScopeResult.Accepted);
        Assert.Equal(ProcessDriverDenialReason.CapabilityScopeDenied, officeScopeResult.DenialReason);
    }

    [Fact]
    public void Process_driver_transcript_alpha_SB023_INV_001_supplied_content_policy_rejects_untrusted_mismatched_oversized_and_invalid_content_type()
    {
        const string transcript = "Build succeeded.";
        var verifier = new TranscriptVerificationAlphaVerifier();
        var wrongContentType = verifier.Verify(CreateRequest(
            transcript,
            ProcessDriverTranscriptLanguage.DotNet,
            suppliedContentFactory: (reference, text) => new ProcessDriverSuppliedEvidenceContent(
                ProcessDriverSuppliedEvidenceContentKind.TranscriptText,
                reference,
                ProcessDriverSuppliedEvidenceContentRules.JsonContentType,
                text.Length,
                ProcessDriverEvidencePolicy.ComputeSha256(text))));
        var untrustedEnvelopeUri = verifier.Verify(CreateRequest(
            transcript,
            ProcessDriverTranscriptLanguage.DotNet,
            suppliedContentFactory: (reference, text) => new ProcessDriverSuppliedEvidenceContent(
                ProcessDriverSuppliedEvidenceContentKind.TranscriptText,
                reference with { Uri = "file://C:/Users/lucys/secrets/transcript.txt" },
                ProcessDriverSuppliedEvidenceContentRules.PlainTextContentType,
                text.Length,
                ProcessDriverEvidencePolicy.ComputeSha256(text))));
        var mismatchedEnvelopeHash = verifier.Verify(CreateRequest(
            transcript,
            ProcessDriverTranscriptLanguage.DotNet,
            suppliedContentFactory: (reference, text) => new ProcessDriverSuppliedEvidenceContent(
                ProcessDriverSuppliedEvidenceContentKind.TranscriptText,
                reference,
                ProcessDriverSuppliedEvidenceContentRules.PlainTextContentType,
                text.Length,
                ProcessDriverEvidencePolicy.ComputeSha256("different supplied content"))));
        var oversizedEnvelope = verifier.Verify(CreateRequest(
            transcript,
            ProcessDriverTranscriptLanguage.DotNet,
            suppliedContentFactory: (reference, text) => new ProcessDriverSuppliedEvidenceContent(
                ProcessDriverSuppliedEvidenceContentKind.TranscriptText,
                reference,
                ProcessDriverSuppliedEvidenceContentRules.PlainTextContentType,
                ProcessDriverSuppliedEvidenceContentRules.MaxSuppliedEvidenceContentBytes + 1,
                ProcessDriverEvidencePolicy.ComputeSha256(text))));

        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            wrongContentType,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.InsufficientProof);
        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            untrustedEnvelopeUri,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.TranscriptUntrusted);
        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            mismatchedEnvelopeHash,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.EvidenceHashMismatch);
        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            oversizedEnvelope,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.InsufficientProof);
        Assert.All(untrustedEnvelopeUri.Diagnostics, diagnostic =>
            Assert.DoesNotContain("C:/Users", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Process_driver_transcript_alpha_SB024_INV_003_evidence_boundary_rejects_missing_supplied_content_envelope_without_parsing()
    {
        const string transcript = "Build succeeded.";
        var verifier = new TranscriptVerificationAlphaVerifier();
        var result = verifier.Verify(CreateRequest(
            transcript,
            ProcessDriverTranscriptLanguage.DotNet,
            suppliedContentFactory: (reference, text) => new ProcessDriverSuppliedEvidenceContent(
                ProcessDriverSuppliedEvidenceContentKind.TranscriptText,
                reference,
                ProcessDriverSuppliedEvidenceContentRules.PlainTextContentType,
                SizeBytes: 0,
                ContentHash: ProcessDriverEvidencePolicy.ComputeSha256(text))));

        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            result,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.InsufficientProof);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        Assert.All(result.Diagnostics, diagnostic =>
            Assert.DoesNotContain(transcript, diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Process_driver_transcript_alpha_SB025_INV_001_audit_facts_include_caller_lane_operation_evidence_denial_and_output_hash()
    {
        var verifier = new TranscriptVerificationAlphaVerifier();
        var result = verifier.Verify(CreateRequest(
            "Build succeeded.",
            ProcessDriverTranscriptLanguage.DotNet));

        Assert.True(result.Accepted);
        ProcessDriverVerificationTestHarness.AssertNormalizedAuditFacts(
            result,
            "manager:readonly",
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
            ProcessDriverVerificationTestHarness.TranscriptReadonlyOperations,
            ProcessDriverDenialReason.None);
        Assert.All(result.AuditFacts, fact =>
            Assert.Contains(
                fact.EvidenceReferences,
                evidenceReference => evidenceReference.Kind == ProcessDriverEvidenceReferenceKind.CommandTranscript));
    }

    [Fact]
    public void Process_driver_transcript_alpha_SB024_INV_001_evidence_hash_policy_rejects_mismatch_and_normalizes_references()
    {
        var verifier = new TranscriptVerificationAlphaVerifier();
        var mismatchResult = verifier.Verify(CreateRequest(
            "Build succeeded.",
            ProcessDriverTranscriptLanguage.DotNet,
            transcriptHash: new string('A', 64)));

        Assert.False(mismatchResult.Accepted);
        Assert.Equal(ProcessDriverDenialReason.MissingEvidence, mismatchResult.DenialReason);
        Assert.Contains(mismatchResult.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.EvidenceHashMismatch);

        var validResult = verifier.Verify(CreateRequest(
            "Build succeeded.",
            ProcessDriverTranscriptLanguage.DotNet,
            evidenceHash: "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd"));

        Assert.True(validResult.Accepted);
        Assert.All(validResult.EvidenceReferences, evidence =>
            Assert.Equal(evidence.ContentHash.ToUpperInvariant(), evidence.ContentHash));
        Assert.Contains(validResult.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);

        var missingTranscriptResult = verifier.Verify(CreateRequest(string.Empty, ProcessDriverTranscriptLanguage.DotNet));
        Assert.False(missingTranscriptResult.Accepted);
        Assert.Contains(missingTranscriptResult.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.TranscriptMissing);
    }

    [Fact]
    public void Process_driver_transcript_alpha_SB024_INV_002_evidence_uri_policy_rejects_unapproved_sources_without_mutation()
    {
        var verifier = new TranscriptVerificationAlphaVerifier();
        var localTranscriptResult = verifier.Verify(CreateRequest(
            "Build succeeded. token=sk-local-secret",
            ProcessDriverTranscriptLanguage.DotNet,
            transcriptUri: "file://C:/Users/lucys/secrets/transcript.txt"));
        var remoteEvidenceResult = verifier.Verify(CreateRequest(
            "Build succeeded. token=sk-remote-secret",
            ProcessDriverTranscriptLanguage.DotNet,
            evidenceUri: "https://example.invalid/transcript.txt"));

        AssertUntrustedEvidenceDenied(localTranscriptResult);
        AssertUntrustedEvidenceDenied(remoteEvidenceResult);
        Assert.All(
            localTranscriptResult.Diagnostics.Concat(remoteEvidenceResult.Diagnostics),
            diagnostic =>
            {
                Assert.DoesNotContain("sk-local-secret", diagnostic.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("sk-remote-secret", diagnostic.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("C:/Users", diagnostic.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("example.invalid", diagnostic.Message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Process_driver_transcript_alpha_SB033_INV_001_alpha_package_and_process_adapter_have_no_runtime_hook()
    {
        var root = FindRepositoryRoot();
        var solution = ReadRepositoryFile("CanDoItAll.slnx");
        var project = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.TranscriptVerification",
            "CanDoItAll.Processes.Drivers.TranscriptVerification.csproj");
        var source = ReadProjectSource(root);
        var modulesProcessesProject = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "CanDoItAll.Modules.Processes.csproj");
        var processAdapterSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessTranscriptVerificationReadOnlyAdapter.cs");

        Assert.Contains(
            "src/CanDoItAll.Processes.Drivers.TranscriptVerification/CanDoItAll.Processes.Drivers.TranscriptVerification.csproj",
            solution,
            StringComparison.Ordinal);
        Assert.Contains(
            @"<ProjectReference Include=""..\CanDoItAll.Processes.Drivers.Abstractions\CanDoItAll.Processes.Drivers.Abstractions.csproj"" />",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", project, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics.Process", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceCollection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", source, StringComparison.Ordinal);
        Assert.Contains(
            @"<ProjectReference Include=""..\CanDoItAll.Processes.Drivers.VerificationGateway\CanDoItAll.Processes.Drivers.VerificationGateway.csproj"" />",
            modulesProcessesProject,
            StringComparison.Ordinal);
        Assert.Contains("ProcessTranscriptVerificationReadOnlyAdapter", processAdapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverVerificationGateway.CreateDefault().VerifyTranscript", processAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new TranscriptVerificationAlphaVerifier", processAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceCollection", modulesProcessesProject + processAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", modulesProcessesProject + processAdapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", modulesProcessesProject + processAdapterSource, StringComparison.Ordinal);
        AssertNoForbiddenRuntimeTokens(modulesProcessesProject + processAdapterSource);
        AssertNoForbiddenRuntimeTokens(source);
        AssertNoForbiddenAlphaPackageTokens(source);
    }

    [Fact]
    public void Process_driver_transcript_alpha_SB039_INV_001_docs_and_roadmap_keep_runtime_deferred()
    {
        var packageReadme = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.TranscriptVerification",
            "README.md");
        var runtimeDeferral = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-driver-runtime-evidence-verifier-integration-hardening-v1",
            "architecture",
            "06-runtime-host-deferral.md");
        var domainRoadmap = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-driver-runtime-evidence-verifier-integration-hardening-v1",
            "architecture",
            "05-driver-domain-roadmap.md");
        var docs = string.Join(Environment.NewLine, packageReadme, runtimeDeferral, domainRoadmap);

        Assert.Contains(".NET/Rust Transcript Verifier", docs, StringComparison.Ordinal);
        Assert.Contains("read-only", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("business-analysis", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Office", docs, StringComparison.Ordinal);
        Assert.DoesNotContain("Run shell commands", docs, StringComparison.Ordinal);
        Assert.DoesNotContain("Workspace write allowed", docs, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", docs, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", docs, StringComparison.Ordinal);
    }

    private static TranscriptVerificationAlphaRequest CreateRequest(
        string transcriptText,
        ProcessDriverTranscriptLanguage language,
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        ProcessDriverPermissionMode permissionMode = ProcessDriverPermissionMode.VerificationOnly,
        ProcessDriverCapabilityScope? scope = null,
        string? transcriptHash = null,
        string evidenceHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        string? transcriptUri = null,
        string? evidenceUri = null,
        Func<ProcessDriverEvidenceReference, string, ProcessDriverSuppliedEvidenceContent>? suppliedContentFactory = null)
    {
        var contentHash = ProcessDriverEvidencePolicy.ComputeSha256(transcriptText);
        var resolvedTranscriptUri = transcriptUri ?? $"bundle://proof/SB012/transcripts/{language.ToString().ToLowerInvariant()}-transcript.txt";
        var transcriptReference = new ProcessDriverTranscriptReference(
            resolvedTranscriptUri,
            transcriptHash ?? contentHash,
            language,
            language == ProcessDriverTranscriptLanguage.DotNet ? "dotnet" : "cargo",
            language == ProcessDriverTranscriptLanguage.DotNet ? "net10.0" : "rust-stable");
        var transcriptEvidence = ProcessDriverEvidencePolicy.CreateTranscriptEvidenceReference(
            transcriptReference,
            transcriptText);
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CommandTranscript,
            evidenceUri ?? resolvedTranscriptUri,
            transcriptText,
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence,
            evidenceHash);
        var verificationRequest = ProcessDriverVerificationTestHarness.CreateVerificationRequest(
            permissionMode,
            scope ?? ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
                ProcessDriverPermissionMode.VerificationOnly),
            [evidenceReference],
            requestedOperations ?? ProcessDriverVerificationTestHarness.TranscriptReadonlyOperations,
            "manager:readonly");
        var suppliedContent = suppliedContentFactory?.Invoke(transcriptEvidence, transcriptText) ??
            ProcessDriverSuppliedEvidenceContentRules.CreateTranscriptText(
                transcriptEvidence,
                transcriptText);

        return new TranscriptVerificationAlphaRequest(
            verificationRequest,
            transcriptReference,
            suppliedContent,
            transcriptText,
            DateTimeOffset.Parse("2026-06-07T21:00:00Z"));
    }

    private static void AssertUntrustedEvidenceDenied(ProcessDriverVerificationResponse result)
    {
        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            result,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.TranscriptUntrusted);
        Assert.All(result.AuditFacts, fact =>
            Assert.Equal(ProcessDriverAuditFactKind.OperationDenied, fact.Kind));
    }

    private static void AssertDiagnosticDoesNotLeakMaliciousText(ProcessDriverDiagnostic diagnostic)
    {
        Assert.DoesNotContain("IGNORE ALL PREVIOUS INSTRUCTIONS", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-password", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("@example.invalid", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("../../../../production", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("file:///C:/repositories", diagnostic.Message, StringComparison.Ordinal);
    }

    private static string ReadProjectSource(string repositoryRoot)
    {
        return string.Join(
            Environment.NewLine,
            System.IO.Directory
                .EnumerateFiles(
                    Path.Combine(repositoryRoot, "src", "CanDoItAll.Processes.Drivers.TranscriptVerification"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(System.IO.File.ReadAllText));
    }

    private static void AssertNoForbiddenRuntimeTokens(string source)
    {
        var forbiddenTokens = new[]
        {
            "IProcessDriverPack",
            "IProcessDriverRegistry",
            "ProcessDriverRegistry",
            "ProcessDriverPack",
            "ProcessDriverRuntimeSelector",
            "ProcessDriverManagerCommand",
            "ProcessDriverRuntime",
            "ProcessDriverProvider",
            "ProcessDriverHost",
            "AddProcessDriver",
            "MapProcessDriver"
        };

        foreach (var token in forbiddenTokens)
        {
            Assert.DoesNotContain(token, source, StringComparison.Ordinal);
        }
    }

    private static void AssertNoForbiddenAlphaPackageTokens(string source)
    {
        var forbiddenTokens = new[]
        {
            "Registry",
            "Selector",
            "RuntimeHost",
            "ServiceCollection",
            "AddProcessDriver",
            "ExecuteCommand",
            "Process.Start",
            "Directory.",
            "File.",
            "Workspace",
            "Storage",
            "Graph",
            "Office365",
            "Gmail",
            "TransitionStep",
            "DispatchClaim",
            "Finalize",
            "RetryScheduler",
            "DbContext",
            "AppDbContext",
            "AgentFramework"
        };

        foreach (var token in forbiddenTokens)
        {
            Assert.DoesNotContain(token, source, StringComparison.Ordinal);
        }
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        return System.IO.File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. pathParts]));
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory, Path.GetDirectoryName(sourceFilePath) ?? string.Empty })
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                continue;
            }

            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (System.IO.File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
