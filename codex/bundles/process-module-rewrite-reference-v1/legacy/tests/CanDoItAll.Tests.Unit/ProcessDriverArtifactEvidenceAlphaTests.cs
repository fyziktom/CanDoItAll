using System.Runtime.CompilerServices;
using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.ArtifactEvidence;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverArtifactEvidenceAlphaTests
{
    [Fact]
    public void Artifact_evidence_alpha_verifies_supplied_projection_and_validation_descriptors_without_mutation()
    {
        var verifier = new ArtifactEvidenceAlphaVerifier();
        var result = verifier.Verify(CreateRequest(
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

        Assert.True(result.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, result.DenialReason);
        Assert.Equal(ProcessDriverContractVersion.Current, result.ContractVersion);
        ProcessDriverVerificationTestHarness.AssertNoMutation(result);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            result,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead);
        ProcessDriverVerificationTestHarness.AssertNormalizedAuditFacts(
            result,
            "manager:artifact-readonly",
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
            ProcessDriverVerificationTestHarness.ArtifactEvidenceReadonlyOperations,
            ProcessDriverDenialReason.None);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        Assert.Contains(result.EvidenceReferences, evidenceReference =>
            evidenceReference is
            {
                Kind: ProcessDriverEvidenceReferenceKind.CoreDescriptor,
                CoreDescriptorFamily: ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence
            });
        Assert.Contains(result.EvidenceReferences, evidenceReference =>
            evidenceReference.CoreDescriptorFamily == ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation);
    }

    [Fact]
    public void Artifact_evidence_alpha_reports_missing_descriptor_metadata_without_raw_reference_leakage()
    {
        var verifier = new ArtifactEvidenceAlphaVerifier();
        var result = verifier.Verify(CreateRequest(
            projectionLineage:
            [
                new ProcessArtifactProjectionLineageDescriptor(
                    SourceKind: ProcessCoreArtifactProjectionSourceKind.Unknown,
                    SourceExecutionRunId: null,
                    RecoveryExecutionRunId: null,
                    RecoveredForExecutionRunId: null,
                    ProjectedExecutionRunId: null,
                    WorkflowRunId: null,
                    WorkflowArtifactId: null,
                    SubprocessRunId: null,
                    SourceArtifactId: null,
                    ReworkPacketId: null,
                    SourceExternalReferenceKey: "fixture-secret reviewer@example.invalid",
                    ContentHash: "not-a-hash",
                    ProjectionIdentityHash: string.Empty,
                    HasRuntimeSource: false,
                    HasRecordOnlySource: false,
                    HasRecoveryLineage: false,
                    HasSourceArtifact: false,
                    IsProviderNativeBrowserEvidence: false)
            ],
            projectionSourceOrder:
            [
                new ProcessArtifactProjectionSourceOrderDescriptor(
                    SourceKind: ProcessCoreArtifactProjectionSourceKind.Unknown,
                    ProducerKind: ProcessCoreArtifactProducerKind.Unknown,
                    ProjectionOrder: int.MaxValue,
                    IsRuntimeEvidenceSource: false,
                    IsRecordOnlySource: false,
                    RunsBeforeRecordOnlySources: false,
                    IsProviderNativeBrowserEvidence: false)
            ],
            providerNativeBrowserEvidence:
            [
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeProviderNativeBrowserEvidence(
                    toolName: string.Empty,
                    hasDeclaredPath: false,
                    hasMatchedOutput: false)
            ],
            validationRequirements:
            [
                new ProcessArtifactValidationRequirementDescriptor(
                    ExpectationId: Guid.Empty,
                    ArtifactKind: ProcessCoreArtifactKind.Deliverable,
                    Title: string.Empty,
                    IsRequired: true,
                    ValidationRequirementSummary: string.Empty,
                    AllowedFutureUsageSummary: "fixture-secret reviewer@example.invalid",
                    Mode: ProcessCoreArtifactExpectationMode.Deliverable)
            ]));

        Assert.True(result.Accepted);
        ProcessDriverVerificationTestHarness.AssertNoMutation(result);
        ProcessDriverVerificationTestHarness.AssertNormalizedAuditFacts(
            result,
            "manager:artifact-readonly",
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
            ProcessDriverVerificationTestHarness.ArtifactEvidenceReadonlyOperations,
            ProcessDriverDenialReason.None);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == ProcessDriverDiagnosticSeverity.Warning &&
            diagnostic.Category == ProcessDriverDiagnosticCategory.InsufficientProof);
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            result,
            "fixture-secret",
            "reviewer@example.invalid");
    }

    [Fact]
    public void Artifact_evidence_alpha_detects_projection_order_drift_duplicate_sources_and_missing_lineage()
    {
        var verifier = new ArtifactEvidenceAlphaVerifier();
        var result = verifier.Verify(CreateRequest(
            projectionLineage: [CreateValidLineage(ProcessCoreArtifactProjectionSourceKind.FileWrite)],
            projectionSourceOrder:
            [
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.CompletedDecision),
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite),
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite)
            ],
            validationRequirements: [CreateValidationRequirement()]));
        var categories = result.Diagnostics.Select(diagnostic => diagnostic.Category).ToHashSet();

        Assert.True(result.Accepted);
        ProcessDriverVerificationTestHarness.AssertNoMutation(result);
        Assert.Contains(ProcessDriverDiagnosticCategory.ProjectionOrderDrift, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactLineageMissing, categories);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
    }

    [Fact]
    public void Artifact_evidence_alpha_detects_trust_sensitivity_and_satisfaction_inconsistencies_without_raw_text_leakage()
    {
        var verifier = new ArtifactEvidenceAlphaVerifier();
        var expectedSensitiveDeliverable = CreateExpectedArtifact(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            ProcessCoreArtifactKind.Deliverable,
            "fixture-secret release evidence reviewer@example.invalid",
            ProcessCoreArtifactTrustRequirement.HumanApproved,
            ProcessCoreSensitivityLevel.Restricted);
        var expectedEvidence = CreateExpectedArtifact(
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            ProcessCoreArtifactKind.Evidence,
            "Evidence log",
            ProcessCoreArtifactTrustRequirement.None,
            ProcessCoreSensitivityLevel.Public);
        var result = verifier.Verify(CreateRequest(
            projectionLineage:
            [
                CreateValidLineage(ProcessCoreArtifactProjectionSourceKind.FileWrite),
                CreateValidLineage(ProcessCoreArtifactProjectionSourceKind.CompletedDecision)
            ],
            projectionSourceOrder:
            [
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite),
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.CompletedDecision)
            ],
            validationRequirements: [CreateValidationRequirement()],
            expectedArtifacts: [expectedSensitiveDeliverable, expectedEvidence],
            artifactRecords:
            [
                new ProcessArtifactRecordSnapshot(
                    Id: Guid.Parse("11111111-2222-3333-4444-555555555555"),
                    ArtifactExpectationId: expectedSensitiveDeliverable.Id,
                    ArtifactKind: ProcessCoreArtifactKind.Deliverable,
                    Title: expectedSensitiveDeliverable.Title,
                    TrustStatus: ProcessCoreArtifactTrustStatus.ReviewRequired,
                    SensitivityLevel: ProcessCoreSensitivityLevel.Confidential,
                    CreatedAtUtc: DateTimeOffset.Parse("2026-06-08T14:10:00Z")),
                new ProcessArtifactRecordSnapshot(
                    Id: Guid.Parse("66666666-7777-8888-9999-000000000000"),
                    ArtifactExpectationId: expectedEvidence.Id,
                    ArtifactKind: ProcessCoreArtifactKind.Deliverable,
                    Title: expectedEvidence.Title,
                    TrustStatus: ProcessCoreArtifactTrustStatus.TrustedSource,
                    SensitivityLevel: ProcessCoreSensitivityLevel.Public,
                    CreatedAtUtc: DateTimeOffset.Parse("2026-06-08T14:12:00Z"))
            ]));
        var categories = result.Diagnostics.Select(diagnostic => diagnostic.Category).ToHashSet();

        Assert.True(result.Accepted);
        ProcessDriverVerificationTestHarness.AssertNoMutation(result);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactTrustSensitivityMismatch, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactSatisfactionInconsistent, categories);
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            result,
            "fixture-secret",
            "reviewer@example.invalid");
    }

    [Fact]
    public void Artifact_evidence_alpha_rejects_invalid_envelopes_wrong_families_empty_descriptors_and_mutation()
    {
        var verifier = new ArtifactEvidenceAlphaVerifier();
        var lineage = CreateValidLineage(ProcessCoreArtifactProjectionSourceKind.FileWrite);
        var requirement = CreateValidationRequirement();
        var wrongContentType = verifier.Verify(CreateRequest(
            projectionLineage: [lineage],
            validationRequirements: [requirement],
            suppliedContentFactory: reference => new ProcessDriverSuppliedEvidenceContent(
                ProcessDriverSuppliedEvidenceContentKind.TranscriptText,
                reference,
                ProcessDriverSuppliedEvidenceContentRules.PlainTextContentType,
                SizeBytes: 32,
                reference.ContentHash)));
        var untrustedUri = verifier.Verify(CreateRequest(
            projectionLineage: [lineage],
            validationRequirements: [requirement],
            projectionEvidenceUri: "https://example.invalid/artifact-projection.json"));
        var mismatchedEnvelope = verifier.Verify(CreateRequest(
            projectionLineage: [lineage],
            validationRequirements: [requirement],
            suppliedContentFactory: reference => ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
                reference with { Uri = "artifact://proof/scenario034/different-artifact-projection.json" },
                ArtifactEvidencePayload)));
        var wrongFamily = verifier.Verify(CreateRequest(
            projectionLineage: [lineage],
            validationRequirements: [requirement],
            projectionFamily: ProcessDriverCoreDescriptorFamily.ExecutionEvidence));
        var emptyDescriptors = verifier.Verify(CreateRequest());
        var mutation = verifier.Verify(CreateRequest(
            projectionLineage: [lineage],
            validationRequirements: [requirement],
            requestedOperations: [ProcessDriverOperation.WriteArtifact]));

        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            wrongContentType,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.InsufficientProof);
        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            untrustedUri,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.TranscriptUntrusted);
        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            mismatchedEnvelope,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.EvidenceHashMismatch);
        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            wrongFamily,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.EvidenceHashMismatch);
        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            emptyDescriptors,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.InsufficientProof);
        ProcessDriverVerificationTestHarness.AssertSideEffectDenied(
            mutation,
            ProcessDriverOperation.WriteArtifact);
        Assert.DoesNotContain(untrustedUri.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("example.invalid", StringComparison.Ordinal));
        Assert.DoesNotContain(mutation.Diagnostics, diagnostic =>
            diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
    }

    [Fact]
    public void Artifact_evidence_alpha_package_is_solution_bound_dependency_clean_and_runtime_free()
    {
        var root = FindRepositoryRoot();
        var solution = ReadRepositoryFile("CanDoItAll.slnx");
        var project = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.ArtifactEvidence",
            "CanDoItAll.Processes.Drivers.ArtifactEvidence.csproj");
        var source = ReadProjectSource(root);

        Assert.Contains(
            "src/CanDoItAll.Processes.Drivers.ArtifactEvidence/CanDoItAll.Processes.Drivers.ArtifactEvidence.csproj",
            solution,
            StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Core.csproj", project, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanDoItAll.Modules.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceCollection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics.Process", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRuntimeSelector", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverManagerCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverHost", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverProvider", source, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionLineageDescriptor", source, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactValidationRequirementDescriptor", source, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactExpectationSatisfactionRules.Diagnose", source, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactExpectationMatcher.DiagnoseStrongExpectedArtifactMatch", source, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverCapabilityScopeRules.IsArtifactEvidenceReadScope", source, StringComparison.Ordinal);
    }

    private const string ArtifactEvidencePayload = """{"projection":[{"source":"file-write"}],"validation":[{"kind":"deliverable"}]}""";

    private static ArtifactEvidenceVerificationRequest CreateRequest(
        IReadOnlyList<ProcessArtifactProjectionLineageDescriptor>? projectionLineage = null,
        IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor>? projectionSourceOrder = null,
        IReadOnlyList<ProcessProviderNativeBrowserEvidenceDescriptor>? providerNativeBrowserEvidence = null,
        IReadOnlyList<ProcessArtifactValidationRequirementDescriptor>? validationRequirements = null,
        IReadOnlyList<ProcessArtifactExpectationSnapshot>? expectedArtifacts = null,
        IReadOnlyList<ProcessArtifactRecordSnapshot>? artifactRecords = null,
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string projectionEvidenceUri = "artifact://proof/scenario034/artifact-projection-evidence.json",
        ProcessDriverCoreDescriptorFamily projectionFamily = ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence,
        Func<ProcessDriverEvidenceReference, ProcessDriverSuppliedEvidenceContent>? suppliedContentFactory = null)
    {
        var projectionReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            projectionEvidenceUri,
            ArtifactEvidencePayload,
            projectionFamily);
        var validationReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            "artifact://proof/scenario034/artifact-projection-validation.json",
            ArtifactEvidencePayload,
            ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation);
        var suppliedContent = suppliedContentFactory?.Invoke(projectionReference) ??
            ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
                projectionReference,
                ArtifactEvidencePayload);
        var verificationRequest = ProcessDriverVerificationTestHarness.CreateVerificationRequest(
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
                ProcessDriverPermissionMode.VerificationOnly),
            [projectionReference, validationReference],
            requestedOperations ?? ProcessDriverVerificationTestHarness.ArtifactEvidenceReadonlyOperations,
            "manager:artifact-readonly");

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
            sourceExternalReferenceKey: "repo://tests/artifacts/release-notes.md",
            contentHash: ProcessDriverEvidencePolicy.ComputeSha256("release notes"),
            projectionIdentityHash: ProcessDriverEvidencePolicy.ComputeSha256("release notes projection"));
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
            Title: "Release notes",
            IsRequired: true,
            ValidationRequirementSummary: "Runtime proof transcript required.",
            AllowedFutureUsageSummary: "May be used by final closure.",
            Mode: ProcessCoreArtifactExpectationMode.RuntimeProof);
    }

    private static string ReadProjectSource(string repositoryRoot)
    {
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(
                    Path.Combine(repositoryRoot, "src", "CanDoItAll.Processes.Drivers.ArtifactEvidence"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. pathParts]));
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
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
