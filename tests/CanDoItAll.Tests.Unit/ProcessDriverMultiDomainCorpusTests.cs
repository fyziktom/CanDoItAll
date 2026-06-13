using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Core.Diagnostics;
using CanDoItAll.Processes.Core.Execution;
using CanDoItAll.Processes.Core.Finalization;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.ArtifactEvidence;
using CanDoItAll.Processes.Drivers.BusinessAnalysis;
using CanDoItAll.Processes.Drivers.OfficeEvidence;
using CanDoItAll.Processes.Drivers.RuntimeEvidence;
using CanDoItAll.Processes.Drivers.TranscriptVerification;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverMultiDomainCorpusTests
{
    private const string CorpusDirectoryName = "ProcessDriverMultiDomainCorpus";
    private const string SecretPattern = @"sk-[A-Za-z0-9_-]{20,}|gh[pousr]_[A-Za-z0-9_]{30,}|github_pat_[A-Za-z0-9_]{20,}|AccountKey=[A-Za-z0-9+/]{60,}={0,2}";

    [Fact]
    public void Process_driver_multi_domain_corpus_transcript_fixtures_drive_positive_negative_dotnet_and_rust_paths()
    {
        var verifier = new TranscriptVerificationAlphaVerifier();
        var dotnetPositiveText = ReadCorpusFile("transcript", "dotnet-positive-clean-build.txt");
        var dotnetNegativeText = ReadCorpusFile("transcript", "dotnet-negative-diagnostics-and-redaction.txt");
        var rustPositiveText = ReadCorpusFile("transcript", "rust-positive-clean-test.txt");
        var rustNegativeText = ReadCorpusFile("transcript", "rust-negative-diagnostics-and-redaction.txt");

        var dotnetPositive = verifier.Verify(CreateTranscriptRequest(
            dotnetPositiveText,
            ProcessDriverTranscriptLanguage.DotNet,
            "dotnet-positive-clean-build.txt"));
        var dotnetNegative = verifier.Verify(CreateTranscriptRequest(
            dotnetNegativeText,
            ProcessDriverTranscriptLanguage.DotNet,
            "dotnet-negative-diagnostics-and-redaction.txt"));
        var rustPositive = verifier.Verify(CreateTranscriptRequest(
            rustPositiveText,
            ProcessDriverTranscriptLanguage.Rust,
            "rust-positive-clean-test.txt"));
        var rustNegative = verifier.Verify(CreateTranscriptRequest(
            rustNegativeText,
            ProcessDriverTranscriptLanguage.Rust,
            "rust-negative-diagnostics-and-redaction.txt"));

        AssertAcceptedNoIssue(dotnetPositive);
        AssertAcceptedNoIssue(rustPositive);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            dotnetPositive,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            rustPositive,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification);

        AssertAcceptedWithCategories(
            dotnetNegative,
            ProcessDriverDiagnosticCategory.BuildWarning,
            ProcessDriverDiagnosticCategory.NullableWarning,
            ProcessDriverDiagnosticCategory.BuildError,
            ProcessDriverDiagnosticCategory.TestFailure,
            ProcessDriverDiagnosticCategory.MissingArtifact,
            ProcessDriverDiagnosticCategory.RuntimeProofGap);
        AssertAcceptedWithCategories(
            rustNegative,
            ProcessDriverDiagnosticCategory.CompileError,
            ProcessDriverDiagnosticCategory.PanicDetected,
            ProcessDriverDiagnosticCategory.CargoTestFailure,
            ProcessDriverDiagnosticCategory.MissingCargoArtifact,
            ProcessDriverDiagnosticCategory.UnsupportedToolchain);
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            dotnetNegative,
            "fixture-password",
            "qa.owner@example.invalid");
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            rustNegative,
            "fixture-password",
            "rust.owner@example.invalid");
    }

    [Fact]
    public void Process_driver_multi_domain_corpus_runtime_fixtures_drive_consistent_and_contradictory_descriptor_paths()
    {
        var verifier = new RuntimeEvidenceConsistencyAlphaVerifier();
        var positivePayload = ReadCorpusFile("runtime", "runtime-positive-consistent-descriptors.json");
        var negativePayload = ReadCorpusFile("runtime", "runtime-negative-contradictory-descriptors.json");

        var positive = verifier.Verify(CreateRuntimeRequest(
            positivePayload,
            "runtime-positive-consistent-descriptors.json",
            CreateExecutionEvidence(ProcessAutomationRunOutcome.Succeeded),
            CreateFinalizerEvidence(hasResult: true, shouldApplyTransition: true, ProcessStepRunStatus.Completed),
            CreateRetryDiagnostic(shouldRetry: false),
            ProcessRetryDiagnosticDescriptorRules.DescribeProviderRepair(
                hasRecoverableProviderFailure: false,
                failureSummary: string.Empty,
                failedProviderName: string.Empty,
                fallbackProviderName: string.Empty,
                fallbackModel: string.Empty,
                affectedAgentCount: 0),
            ProcessRetryDiagnosticDescriptorRules.DescribeNoProgressSignalAbsent(),
            [
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite),
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.CompletedDecision)
            ]));
        var negative = verifier.Verify(CreateRuntimeRequest(
            negativePayload,
            "runtime-negative-contradictory-descriptors.json",
            CreateInternallyContradictoryExecutionEvidence(),
            CreateInternallyContradictoryFinalizerEvidence(),
            new ProcessRetryDiagnosticDescriptor(
                ShouldRetry: true,
                AttemptNumber: 3,
                MaxExecutionAttempts: 3,
                RetryReasons: [],
                RetryReasonSummary: string.Empty,
                MissingRequiredTools: [],
                FailedToolNames: [],
                UnresolvedCriticalToolFailureCount: 0,
                HasMissingRequiredTools: false,
                HasUnresolvedCriticalToolFailures: false,
                HasBuildFailure: false,
                HasTestFailure: false,
                HasRecoverableProviderFailure: false,
                HasRecoverableExecutionInterruption: false,
                HasRecoverableFinalizerFailure: false,
                ProcessRetryDiagnosticFailureKind.None),
            new ProcessProviderRepairDiagnosticDescriptor(
                HasRecoverableProviderFailure: true,
                HasRepairOutcome: true,
                FailureSummary: string.Empty,
                FailedProviderName: string.Empty,
                FallbackProviderName: string.Empty,
                FallbackModel: string.Empty,
                AffectedAgentCount: 0),
            new ProcessNoProgressRetryDiagnosticDescriptor(
                HasSignal: true,
                Fingerprint: string.Empty,
                ExecutionRunId: null,
                ToolSignature: string.Empty,
                ArtifactValidationFingerprint: string.Empty,
                MutationDelta: string.Empty,
                ProofDelta: string.Empty),
            [
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.CompletedDecision),
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite),
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite)
            ]));

        AssertAcceptedNoIssue(positive);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            positive,
            ProcessDriverPermissionMode.ManagerReadonly,
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead);
        AssertAcceptedWithCategories(
            negative,
            ProcessDriverDiagnosticCategory.RuntimeEvidenceInconsistent,
            ProcessDriverDiagnosticCategory.FinalizerContradiction,
            ProcessDriverDiagnosticCategory.RetryContradiction,
            ProcessDriverDiagnosticCategory.ProviderRepairInconsistent,
            ProcessDriverDiagnosticCategory.NoProgressFingerprintMissing,
            ProcessDriverDiagnosticCategory.ProjectionOrderDrift);
        ProcessDriverVerificationTestHarness.AssertNoMutation(negative);
    }

    [Fact]
    public void Process_driver_multi_domain_corpus_office_fixtures_drive_complete_and_missing_metadata_paths()
    {
        var verifier = new OfficeEvidenceAlphaVerifier();
        var positivePayload = ReadCorpusFile("office", "office-positive-escalation.json");
        var negativePayload = ReadCorpusFile("office", "office-negative-missing-metadata.json");

        var positive = verifier.Verify(CreateOfficeRequest(
            positivePayload,
            "office-positive-escalation.json",
            [
                new OfficeEvidenceItem(
                    OfficeEvidenceItemKind.EmailMessage,
                    "mail-sb043-positive-001",
                    "Release readiness follow-up",
                    "program.manager@example.invalid",
                    ["process.owner@example.invalid", "release.lead@example.invalid"],
                    DateTimeOffset.Parse("2026-06-08T12:00:00Z"),
                    "Follow-up confirms the release readiness review action item is assigned to the process owner."),
                new OfficeEvidenceItem(
                    OfficeEvidenceItemKind.Document,
                    "doc-sb043-positive-001",
                    "Release readiness notes",
                    "program.manager@example.invalid",
                    [],
                    DateTimeOffset.Parse("2026-06-08T12:05:00Z"),
                    "Document body records the same action item, due date, and read-only evidence boundary.")
            ]));
        var negative = verifier.Verify(CreateOfficeRequest(
            negativePayload,
            "office-negative-missing-metadata.json",
            [
                new OfficeEvidenceItem(
                    OfficeEvidenceItemKind.EmailMessage,
                    "mail-sb043-negative-001",
                    string.Empty,
                    string.Empty,
                    [],
                    null,
                    negativePayload)
            ]));

        AssertAcceptedNoIssue(positive);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            positive,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        AssertAcceptedWithCategories(negative, ProcessDriverDiagnosticCategory.InsufficientProof);
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            negative,
            "fixture-password",
            "reviewer@example.invalid");
    }

    [Fact]
    public void Process_driver_multi_domain_corpus_business_fixtures_drive_supported_and_unsupported_analysis_paths()
    {
        var verifier = new BusinessAnalysisAlphaVerifier();
        var positiveText = ReadCorpusFile("business", "business-positive-churn-analysis.md");
        var negativeText = ReadCorpusFile("business", "business-negative-unsupported-assumption.md");

        var positive = verifier.Verify(CreateBusinessAnalysisRequest(
            positiveText,
            "business-positive-churn-analysis.md",
            [
                new BusinessAnalysisEvidenceItem(
                    BusinessAnalysisEvidenceItemKind.Deliverable,
                    "analysis-sb043-positive-001",
                    "Customer churn risk analysis",
                    positiveText,
                    DateTimeOffset.Parse("2026-06-08T13:00:00Z")),
                new BusinessAnalysisEvidenceItem(
                    BusinessAnalysisEvidenceItemKind.SupportingEvidence,
                    "evidence-sb043-positive-001",
                    "Renewal-team interview summary",
                    positiveText,
                    DateTimeOffset.Parse("2026-06-08T13:05:00Z"))
            ]));
        var negative = verifier.Verify(CreateBusinessAnalysisRequest(
            negativeText,
            "business-negative-unsupported-assumption.md",
            [
                new BusinessAnalysisEvidenceItem(
                    BusinessAnalysisEvidenceItemKind.Deliverable,
                    "analysis-sb043-negative-001",
                    "Enterprise onboarding recommendation",
                    negativeText,
                    DateTimeOffset.Parse("2026-06-08T13:10:00Z"))
            ]));

        AssertAcceptedNoIssue(positive);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            positive,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);
        AssertAcceptedWithCategories(
            negative,
            ProcessDriverDiagnosticCategory.BusinessRequirementMissing,
            ProcessDriverDiagnosticCategory.BusinessEvidenceGap,
            ProcessDriverDiagnosticCategory.BusinessUnsupportedAssumption,
            ProcessDriverDiagnosticCategory.BusinessContradictionMarker);
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            negative,
            "fixture-password",
            "analyst@example.invalid",
            "conflicts with the conclusion");
    }

    [Fact]
    public void Process_driver_multi_domain_corpus_artifact_fixtures_drive_valid_and_drifted_projection_paths()
    {
        var verifier = new ArtifactEvidenceAlphaVerifier();
        var positivePayload = ReadCorpusFile("artifact", "artifact-positive-release-notes.json");
        var negativePayload = ReadCorpusFile("artifact", "artifact-negative-projection-drift.json");
        var expectedArtifact = CreateExpectedArtifact(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            ProcessCoreArtifactKind.Deliverable,
            "Restricted release decision",
            ProcessCoreArtifactTrustRequirement.HumanApproved,
            ProcessCoreSensitivityLevel.Restricted);

        var positive = verifier.Verify(CreateArtifactRequest(
            positivePayload,
            "artifact-positive-release-notes.json",
            projectionLineage:
            [
                CreateValidLineage(ProcessCoreArtifactProjectionSourceKind.FileWrite),
                CreateValidLineage(ProcessCoreArtifactProjectionSourceKind.ProviderNativeBrowser),
                CreateValidLineage(ProcessCoreArtifactProjectionSourceKind.CompletedDecision)
            ],
            projectionSourceOrder:
            [
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite),
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.ProviderNativeBrowser),
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.CompletedDecision)
            ],
            providerNativeBrowserEvidence:
            [
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeProviderNativeBrowserEvidence(
                    "browser_take_screenshot",
                    hasDeclaredPath: true,
                    hasMatchedOutput: true)
            ],
            validationRequirements: [CreateValidationRequirement()]));
        var negative = verifier.Verify(CreateArtifactRequest(
            negativePayload,
            "artifact-negative-projection-drift.json",
            projectionLineage: [CreateValidLineage(ProcessCoreArtifactProjectionSourceKind.FileWrite)],
            projectionSourceOrder:
            [
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.CompletedDecision),
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite),
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite)
            ],
            validationRequirements: [CreateValidationRequirement()],
            expectedArtifacts: [expectedArtifact],
            artifactRecords:
            [
                new ProcessArtifactRecordSnapshot(
                    Id: Guid.Parse("11111111-2222-3333-4444-555555555555"),
                    ArtifactExpectationId: expectedArtifact.Id,
                    ArtifactKind: ProcessCoreArtifactKind.Deliverable,
                    Title: expectedArtifact.Title,
                    TrustStatus: ProcessCoreArtifactTrustStatus.ReviewRequired,
                    SensitivityLevel: ProcessCoreSensitivityLevel.Confidential,
                    CreatedAtUtc: DateTimeOffset.Parse("2026-06-08T14:12:00Z"))
            ]));

        AssertAcceptedNoIssue(positive);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            positive,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead);
        AssertAcceptedWithCategories(
            negative,
            ProcessDriverDiagnosticCategory.ProjectionOrderDrift,
            ProcessDriverDiagnosticCategory.ArtifactLineageMissing,
            ProcessDriverDiagnosticCategory.ArtifactTrustSensitivityMismatch);
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            negative,
            "fixture-password",
            "artifact.reviewer@example.invalid");
    }

    [Fact]
    public void Process_driver_multi_domain_corpus_fixture_inventory_is_source_backed_secret_safe_and_runtime_free()
    {
        var expectedFixtures = new[]
        {
            "transcript/dotnet-positive-clean-build.txt",
            "transcript/dotnet-negative-diagnostics-and-redaction.txt",
            "transcript/rust-positive-clean-test.txt",
            "transcript/rust-negative-diagnostics-and-redaction.txt",
            "runtime/runtime-positive-consistent-descriptors.json",
            "runtime/runtime-negative-contradictory-descriptors.json",
            "office/office-positive-escalation.json",
            "office/office-negative-missing-metadata.json",
            "business/business-positive-churn-analysis.md",
            "business/business-negative-unsupported-assumption.md",
            "artifact/artifact-positive-release-notes.json",
            "artifact/artifact-negative-projection-drift.json"
        };

        foreach (var relativePath in expectedFixtures)
        {
            var content = ReadCorpusFile(relativePath.Split('/'));

            Assert.Contains("SB043", content, StringComparison.Ordinal);
            Assert.True(content.Length > 120);
            Assert.DoesNotMatch(SecretPattern, content);
            AssertNoRuntimeHostOrSideEffectTokens(content);
        }

        var corpusRoot = Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "CanDoItAll.Tests.Unit",
            "TestData",
            CorpusDirectoryName);
        var domainDirectoryNames = Directory
            .EnumerateDirectories(corpusRoot)
            .Select(path => Path.GetFileName(path) ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["artifact", "business", "office", "runtime", "transcript"],
            domainDirectoryNames);
    }

    private static TranscriptVerificationAlphaRequest CreateTranscriptRequest(
        string transcriptText,
        ProcessDriverTranscriptLanguage language,
        string fixtureName)
    {
        var contentHash = ProcessDriverEvidencePolicy.ComputeSha256(transcriptText);
        var fixtureUri = $"bundle://testdata/SB043/transcript/{fixtureName}";
        var transcriptReference = new ProcessDriverTranscriptReference(
            fixtureUri,
            contentHash,
            language,
            language == ProcessDriverTranscriptLanguage.DotNet ? "dotnet" : "cargo",
            language == ProcessDriverTranscriptLanguage.DotNet ? "net10.0" : "rust-stable");
        var transcriptEvidence = ProcessDriverEvidencePolicy.CreateTranscriptEvidenceReference(
            transcriptReference,
            transcriptText);
        var verificationRequest = ProcessDriverVerificationTestHarness.CreateVerificationRequest(
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
                ProcessDriverPermissionMode.VerificationOnly),
            [transcriptEvidence],
            ProcessDriverVerificationTestHarness.TranscriptReadonlyOperations,
            "manager:sb043-transcript-corpus");
        var suppliedContent = ProcessDriverSuppliedEvidenceContentRules.CreateTranscriptText(
            transcriptEvidence,
            transcriptText);

        return new TranscriptVerificationAlphaRequest(
            verificationRequest,
            transcriptReference,
            suppliedContent,
            transcriptText,
            DateTimeOffset.Parse("2026-06-08T10:00:00Z"));
    }

    private static RuntimeEvidenceConsistencyVerificationRequest CreateRuntimeRequest(
        string suppliedPayload,
        string fixtureName,
        ProcessExecutionEvidenceDescriptor? executionEvidence,
        ProcessFinalizerEvidenceDescriptor? finalizerEvidence,
        ProcessRetryDiagnosticDescriptor? retryDiagnostic,
        ProcessProviderRepairDiagnosticDescriptor? providerRepairDiagnostic,
        ProcessNoProgressRetryDiagnosticDescriptor? noProgressDiagnostic,
        IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor> projectionSourceOrder)
    {
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            $"bundle://testdata/SB043/runtime/{fixtureName}",
            suppliedPayload,
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
        var suppliedContent = ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
            evidenceReference,
            suppliedPayload);
        var verificationRequest = ProcessDriverVerificationTestHarness.CreateVerificationRequest(
            ProcessDriverPermissionMode.ManagerReadonly,
            ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
                ProcessDriverPermissionMode.ManagerReadonly),
            [evidenceReference],
            ProcessDriverVerificationTestHarness.RuntimeReadonlyOperations,
            "manager:sb043-runtime-corpus");

        return new RuntimeEvidenceConsistencyVerificationRequest(
            verificationRequest,
            suppliedContent,
            executionEvidence,
            finalizerEvidence,
            retryDiagnostic,
            noProgressDiagnostic,
            providerRepairDiagnostic,
            projectionSourceOrder,
            DateTimeOffset.Parse("2026-06-08T11:00:00Z"));
    }

    private static OfficeEvidenceVerificationRequest CreateOfficeRequest(
        string suppliedPayload,
        string fixtureName,
        IReadOnlyList<OfficeEvidenceItem> items)
    {
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact,
            $"bundle://testdata/SB043/office/{fixtureName}",
            suppliedPayload,
            coreDescriptorFamily: null);
        var suppliedContent = ProcessDriverSuppliedEvidenceContentRules.CreateOfficeEvidencePayload(
            evidenceReference,
            suppliedPayload);
        var verificationRequest = ProcessDriverVerificationTestHarness.CreateVerificationRequest(
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
                ProcessDriverPermissionMode.VerificationOnly),
            [evidenceReference],
            ProcessDriverVerificationTestHarness.OfficeReadonlyOperations,
            "manager:sb043-office-corpus");

        return new OfficeEvidenceVerificationRequest(
            verificationRequest,
            suppliedContent,
            items,
            DateTimeOffset.Parse("2026-06-08T12:00:00Z"));
    }

    private static BusinessAnalysisVerificationRequest CreateBusinessAnalysisRequest(
        string suppliedPayload,
        string fixtureName,
        IReadOnlyList<BusinessAnalysisEvidenceItem> items)
    {
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact,
            $"bundle://testdata/SB043/business/{fixtureName}",
            suppliedPayload,
            coreDescriptorFamily: null);
        var suppliedContent = ProcessDriverSuppliedEvidenceContentRules.CreateBusinessAnalysisPayload(
            evidenceReference,
            suppliedPayload);
        var verificationRequest = ProcessDriverVerificationTestHarness.CreateVerificationRequest(
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
                ProcessDriverPermissionMode.VerificationOnly),
            [evidenceReference],
            ProcessDriverVerificationTestHarness.BusinessAnalysisReadonlyOperations,
            "manager:sb043-business-corpus");

        return new BusinessAnalysisVerificationRequest(
            verificationRequest,
            suppliedContent,
            items,
            DateTimeOffset.Parse("2026-06-08T13:00:00Z"));
    }

    private static ArtifactEvidenceVerificationRequest CreateArtifactRequest(
        string suppliedPayload,
        string fixtureName,
        IReadOnlyList<ProcessArtifactProjectionLineageDescriptor>? projectionLineage = null,
        IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor>? projectionSourceOrder = null,
        IReadOnlyList<ProcessProviderNativeBrowserEvidenceDescriptor>? providerNativeBrowserEvidence = null,
        IReadOnlyList<ProcessArtifactValidationRequirementDescriptor>? validationRequirements = null,
        IReadOnlyList<ProcessArtifactExpectationSnapshot>? expectedArtifacts = null,
        IReadOnlyList<ProcessArtifactRecordSnapshot>? artifactRecords = null)
    {
        var projectionReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            $"bundle://testdata/SB043/artifact/{fixtureName}",
            suppliedPayload,
            ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence);
        var validationReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            $"bundle://testdata/SB043/artifact/{fixtureName}#validation",
            suppliedPayload,
            ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation);
        var suppliedContent = ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
            projectionReference,
            suppliedPayload);
        var verificationRequest = ProcessDriverVerificationTestHarness.CreateVerificationRequest(
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
                ProcessDriverPermissionMode.VerificationOnly),
            [projectionReference, validationReference],
            ProcessDriverVerificationTestHarness.ArtifactEvidenceReadonlyOperations,
            "manager:sb043-artifact-corpus");

        return new ArtifactEvidenceVerificationRequest(
            verificationRequest,
            suppliedContent,
            projectionLineage ?? [],
            projectionSourceOrder ?? [],
            providerNativeBrowserEvidence ?? [],
            validationRequirements ?? [],
            expectedArtifacts ?? [],
            artifactRecords ?? [],
            DateTimeOffset.Parse("2026-06-08T14:00:00Z"));
    }

    private static ProcessExecutionEvidenceDescriptor CreateExecutionEvidence(
        ProcessAutomationRunOutcome outcome)
    {
        return new ProcessExecutionEvidenceDescriptor(
            new ProcessExecutionRunEvidenceDescriptor(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ProcessAutomationExecutionState.Completed,
                outcome,
                IsTerminal: true,
                IsActive: false,
                HasPendingToolApprovals: false,
                DateTimeOffset.Parse("2026-06-08T11:00:00Z"),
                DateTimeOffset.Parse("2026-06-08T11:01:00Z"),
                DateTimeOffset.Parse("2026-06-08T11:05:00Z"),
                outcome == ProcessAutomationRunOutcome.Succeeded
                    ? ProcessCoreExecutionRunObservationKind.Succeeded
                    : ProcessCoreExecutionRunObservationKind.Failed),
            new ProcessExecutionAttemptEvidenceDescriptor(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                AttemptNumber: 1,
                ProcessStepRunStatus.Completed,
                "completed",
                MissingRequiredTools: [],
                HasMissingRequiredTools: false,
                MissingRequiredToolCount: 0,
                HasUnresolvedCriticalToolFailures: false,
                UnresolvedCriticalToolFailureCount: 0,
                SelectedBranchOutcomeId: null),
            new ProcessExecutionCarriedProofDescriptor(
                HasConcreteImplementationProof: true,
                HasRunnableApplicationProof: true,
                HasConcreteProductMutation: false));
    }

    private static ProcessExecutionEvidenceDescriptor CreateInternallyContradictoryExecutionEvidence()
    {
        return new ProcessExecutionEvidenceDescriptor(
            new ProcessExecutionRunEvidenceDescriptor(
                Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                IsTerminal: true,
                IsActive: true,
                HasPendingToolApprovals: false,
                DateTimeOffset.Parse("2026-06-08T11:00:00Z"),
                DateTimeOffset.Parse("2026-06-08T11:01:00Z"),
                CompletedAtUtc: null,
                ProcessCoreExecutionRunObservationKind.Active),
            new ProcessExecutionAttemptEvidenceDescriptor(
                Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                AttemptNumber: 1,
                ProcessStepRunStatus.Completed,
                "completed",
                MissingRequiredTools: [],
                HasMissingRequiredTools: false,
                MissingRequiredToolCount: 0,
                HasUnresolvedCriticalToolFailures: false,
                UnresolvedCriticalToolFailureCount: 2,
                SelectedBranchOutcomeId: null),
            new ProcessExecutionCarriedProofDescriptor(
                HasConcreteImplementationProof: true,
                HasRunnableApplicationProof: true,
                HasConcreteProductMutation: false));
    }

    private static ProcessRetryDiagnosticDescriptor CreateRetryDiagnostic(
        bool shouldRetry)
    {
        return new ProcessRetryDiagnosticDescriptor(
            shouldRetry,
            AttemptNumber: 1,
            MaxExecutionAttempts: 3,
            RetryReasons: [],
            RetryReasonSummary: string.Empty,
            MissingRequiredTools: [],
            FailedToolNames: [],
            UnresolvedCriticalToolFailureCount: 0,
            HasMissingRequiredTools: false,
            HasUnresolvedCriticalToolFailures: false,
            HasBuildFailure: false,
            HasTestFailure: false,
            HasRecoverableProviderFailure: false,
            HasRecoverableExecutionInterruption: false,
            HasRecoverableFinalizerFailure: false,
            PrimaryFailureKind: ProcessRetryDiagnosticFailureKind.None);
    }

    private static ProcessFinalizerEvidenceDescriptor CreateFinalizerEvidence(
        bool hasResult,
        bool shouldApplyTransition,
        ProcessStepRunStatus completionStatus)
    {
        return new ProcessFinalizerEvidenceDescriptor(
            new ProcessFinalizerIntentEvidenceDescriptor(
                ProcessCoreFinalizerKind.DirectAgent,
                ProcessRunId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                StepRunId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                completionStatus,
                "finalizer completed",
                SelectedBranchOutcomeId: null,
                ExecutionRunId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                WorkflowRunId: null,
                SubprocessRunId: null,
                ProjectsExecutionArtifacts: true,
                AllowsManagerArtifactRecovery: false,
                Trigger: "test",
                RequiresLeaseRenewal: false,
                RecoveryExecutionRunId: null,
                RecoveredForExecutionRunId: null),
            new ProcessFinalizerResultEvidenceDescriptor(
                hasResult,
                shouldApplyTransition,
                completionStatus,
                "finalizer result",
                ProcessCoreFinalizerBlockCauseKind.None,
                SelectedBranchOutcomeId: null,
                StepRunConcurrencyToken: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                ArtifactValidationResultCount: 1,
                HasArtifactValidationResults: true));
    }

    private static ProcessFinalizerEvidenceDescriptor CreateInternallyContradictoryFinalizerEvidence()
    {
        return new ProcessFinalizerEvidenceDescriptor(
            new ProcessFinalizerIntentEvidenceDescriptor(
                ProcessCoreFinalizerKind.DirectAgent,
                ProcessRunId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                StepRunId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                ProcessStepRunStatus.Completed,
                "finalizer intent completed",
                SelectedBranchOutcomeId: null,
                ExecutionRunId: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                WorkflowRunId: null,
                SubprocessRunId: null,
                ProjectsExecutionArtifacts: true,
                AllowsManagerArtifactRecovery: false,
                Trigger: "test",
                RequiresLeaseRenewal: false,
                RecoveryExecutionRunId: null,
                RecoveredForExecutionRunId: null),
            new ProcessFinalizerResultEvidenceDescriptor(
                HasResult: true,
                ShouldApplyTransition: true,
                CompletionStatus: ProcessStepRunStatus.Failed,
                "finalizer result failed",
                ProcessCoreFinalizerBlockCauseKind.None,
                SelectedBranchOutcomeId: null,
                StepRunConcurrencyToken: null,
                ArtifactValidationResultCount: 1,
                HasArtifactValidationResults: true));
    }

    private static ProcessArtifactProjectionLineageDescriptor CreateValidLineage(
        ProcessCoreArtifactProjectionSourceKind sourceKind)
    {
        return ProcessArtifactProjectionEvidenceDescriptorRules.DescribeLineage(
            sourceKind,
            sourceExecutionRunId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            recoveryExecutionRunId: null,
            recoveredForExecutionRunId: null,
            projectedExecutionRunId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            workflowRunId: null,
            workflowArtifactId: null,
            subprocessRunId: null,
            sourceArtifactId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            reworkPacketId: null,
            sourceExternalReferenceKey: $"repo://tests/artifacts/{sourceKind.ToString().ToLowerInvariant()}-release-notes.md",
            contentHash: ProcessDriverEvidencePolicy.ComputeSha256($"{sourceKind}-release notes"),
            projectionIdentityHash: ProcessDriverEvidencePolicy.ComputeSha256($"{sourceKind}-release notes projection"));
    }

    private static ProcessArtifactExpectationSnapshot CreateExpectedArtifact(
        Guid id,
        ProcessCoreArtifactKind artifactKind,
        string title,
        ProcessCoreArtifactTrustRequirement trustRequirement,
        ProcessCoreSensitivityLevel sensitivityLevel)
    {
        return new ProcessArtifactExpectationSnapshot(
            Id: id,
            ArtifactKind: artifactKind,
            Title: title,
            IsRequired: true,
            TrustRequirement: trustRequirement,
            SensitivityLevel: sensitivityLevel,
            ValidationRequirementSummary: "Supplied validation requirement.",
            AllowedFutureUsageSummary: "Supplied future usage.");
    }

    private static ProcessArtifactValidationRequirementDescriptor CreateValidationRequirement()
    {
        return new ProcessArtifactValidationRequirementDescriptor(
            ExpectationId: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            ArtifactKind: ProcessCoreArtifactKind.Deliverable,
            Title: "Release readiness report",
            IsRequired: true,
            ValidationRequirementSummary: "Runtime proof transcript required.",
            AllowedFutureUsageSummary: "May be used by final closure.",
            Mode: ProcessCoreArtifactExpectationMode.RuntimeProof);
    }

    private static void AssertAcceptedNoIssue(
        ProcessDriverVerificationResponse response)
    {
        Assert.True(response.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, response.DenialReason);
        ProcessDriverVerificationTestHarness.AssertNoMutation(response);
        Assert.Contains(response.Diagnostics, diagnostic =>
            diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
    }

    private static void AssertAcceptedWithCategories(
        ProcessDriverVerificationResponse response,
        params ProcessDriverDiagnosticCategory[] expectedCategories)
    {
        var categories = response.Diagnostics.Select(diagnostic => diagnostic.Category).ToHashSet();

        Assert.True(response.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, response.DenialReason);
        ProcessDriverVerificationTestHarness.AssertNoMutation(response);
        Assert.DoesNotContain(ProcessDriverDiagnosticCategory.NoIssueDetected, categories);
        foreach (var expectedCategory in expectedCategories)
        {
            Assert.Contains(expectedCategory, categories);
        }
    }

    private static void AssertNoRuntimeHostOrSideEffectTokens(
        string content)
    {
        var forbiddenTokens = new[]
        {
            "IProcessDriverRegistry",
            "ProcessDriverRegistry",
            "ProcessDriverRuntimeSelector",
            "ProcessDriverManagerCommand",
            "ProcessDriverRuntime",
            "ProcessDriverProvider",
            "ProcessDriverHost",
            "AddProcessDriver",
            "MapProcessDriver",
            "System.Diagnostics.Process",
            "Process.Start",
            "IServiceCollection",
            "AddScoped",
            "AddSingleton",
            "DbContext"
        };

        foreach (var token in forbiddenTokens)
        {
            Assert.DoesNotContain(token, content, StringComparison.Ordinal);
        }
    }

    private static string ReadCorpusFile(
        params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine(
            [FindRepositoryRoot(), "tests", "CanDoItAll.Tests.Unit", "TestData", CorpusDirectoryName, .. pathParts]));
    }

    private static string FindRepositoryRoot(
        [CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(sourceFilePath) ?? string.Empty })
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                continue;
            }

            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
