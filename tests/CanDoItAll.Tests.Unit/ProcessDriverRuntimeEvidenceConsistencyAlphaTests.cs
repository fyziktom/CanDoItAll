using System.Runtime.CompilerServices;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Core.Diagnostics;
using CanDoItAll.Processes.Core.Execution;
using CanDoItAll.Processes.Core.Finalization;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.RuntimeEvidence;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverRuntimeEvidenceConsistencyAlphaTests
{
    [Fact]
    public void Runtime_evidence_consistency_alpha_internals_are_split_without_runtime_or_io_surface()
    {
        var root = FindRepositoryRoot();
        var source = ReadProjectSource(root);

        Assert.Contains("internal static class RuntimeEvidenceDescriptorNormalizer", source, StringComparison.Ordinal);
        Assert.Contains("internal static class RuntimeEvidenceVerificationRequestPolicy", source, StringComparison.Ordinal);
        Assert.Contains("internal static class RuntimeEvidenceContradictionRules", source, StringComparison.Ordinal);
        Assert.Contains("internal static class RuntimeEvidenceDiagnosticFactory", source, StringComparison.Ordinal);
        Assert.Contains("internal static class RuntimeEvidenceAuditFactMapper", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics.Process", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceCollection", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_evidence_consistency_alpha_expands_contradiction_matrix_across_descriptor_families()
    {
        var verifier = new RuntimeEvidenceConsistencyAlphaVerifier();
        var request = CreateRequest(
            executionEvidence: CreateInternallyContradictoryExecutionEvidence(),
            finalizerEvidence: CreateInternallyContradictoryFinalizerEvidence(),
            retryDiagnostic: new ProcessRetryDiagnosticDescriptor(
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
            providerRepairDiagnostic: new ProcessProviderRepairDiagnosticDescriptor(
                HasRecoverableProviderFailure: true,
                HasRepairOutcome: true,
                FailureSummary: string.Empty,
                FailedProviderName: string.Empty,
                FallbackProviderName: string.Empty,
                FallbackModel: string.Empty,
                AffectedAgentCount: 0),
            noProgressDiagnostic: new ProcessNoProgressRetryDiagnosticDescriptor(
                HasSignal: true,
                Fingerprint: "no-progress",
                ExecutionRunId: null,
                ToolSignature: string.Empty,
                ArtifactValidationFingerprint: string.Empty,
                MutationDelta: string.Empty,
                ProofDelta: string.Empty),
            projectionSourceOrder:
            [
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite),
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite)
            ]);

        var result = verifier.Verify(request);
        var categories = result.Diagnostics.Select(diagnostic => diagnostic.Category).ToHashSet();

        Assert.True(result.Accepted);
        Assert.True(result.NoMutationPerformed);
        Assert.Contains(ProcessDriverDiagnosticCategory.RuntimeEvidenceInconsistent, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.FinalizerContradiction, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.RetryContradiction, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ProviderRepairInconsistent, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.NoProgressFingerprintMissing, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ProjectionOrderDrift, categories);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("terminal run that is still active", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("maximum execution attempts", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("duplicate source kinds", StringComparison.Ordinal));
    }

    [Fact]
    public void Runtime_evidence_consistency_alpha_detects_contradictory_core_descriptors_without_mutation()
    {
        var verifier = new RuntimeEvidenceConsistencyAlphaVerifier();
        var request = CreateRequest(
            executionEvidence: CreateExecutionEvidence(ProcessAutomationRunOutcome.Succeeded, unresolvedCriticalToolFailures: 2),
            finalizerEvidence: CreateFinalizerEvidence(hasResult: true, shouldApplyTransition: true, ProcessStepRunStatus.Failed),
            retryDiagnostic: CreateRetryDiagnostic(shouldRetry: false, unresolvedCriticalToolFailures: 2),
            providerRepairDiagnostic: new ProcessProviderRepairDiagnosticDescriptor(
                HasRecoverableProviderFailure: false,
                HasRepairOutcome: true,
                "repair claimed without provider failure",
                "openai",
                "fallback",
                "gpt-5.5",
                AffectedAgentCount: 1),
            noProgressDiagnostic: new ProcessNoProgressRetryDiagnosticDescriptor(
                HasSignal: true,
                Fingerprint: string.Empty,
                ExecutionRunId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ToolSignature: "workspace_dotnet_test",
                ArtifactValidationFingerprint: "artifact",
                MutationDelta: "none",
                ProofDelta: "none"),
            projectionSourceOrder:
            [
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.CompletedDecision),
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite)
            ]);

        var result = verifier.Verify(request);

        Assert.True(result.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, result.DenialReason);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            result,
            ProcessDriverPermissionMode.ManagerReadonly,
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead);

        var categories = result.Diagnostics.Select(diagnostic => diagnostic.Category).ToHashSet();
        Assert.Contains(ProcessDriverDiagnosticCategory.FinalizerContradiction, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.RetryContradiction, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ProviderRepairInconsistent, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.NoProgressFingerprintMissing, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ProjectionOrderDrift, categories);
    }

    [Fact]
    public void Runtime_evidence_consistency_alpha_accepts_consistent_descriptors_and_rejects_mutation_operations()
    {
        var verifier = new RuntimeEvidenceConsistencyAlphaVerifier();
        var consistent = verifier.Verify(CreateRequest(
            executionEvidence: CreateExecutionEvidence(ProcessAutomationRunOutcome.Succeeded),
            finalizerEvidence: CreateFinalizerEvidence(hasResult: true, shouldApplyTransition: true, ProcessStepRunStatus.Completed),
            retryDiagnostic: CreateRetryDiagnostic(shouldRetry: false),
            providerRepairDiagnostic: ProcessRetryDiagnosticDescriptorRules.DescribeProviderRepair(
                hasRecoverableProviderFailure: false,
                failureSummary: string.Empty,
                failedProviderName: string.Empty,
                fallbackProviderName: string.Empty,
                fallbackModel: string.Empty,
                affectedAgentCount: 0),
            noProgressDiagnostic: ProcessRetryDiagnosticDescriptorRules.DescribeNoProgressSignalAbsent(),
            projectionSourceOrder:
            [
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite),
                ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.CompletedDecision)
            ]));

        Assert.True(consistent.Accepted);
        ProcessDriverVerificationTestHarness.AssertNoMutation(consistent);
        Assert.Contains(consistent.Diagnostics, diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);

        var denied = verifier.Verify(CreateRequest(
            executionEvidence: CreateExecutionEvidence(ProcessAutomationRunOutcome.Succeeded),
            finalizerEvidence: CreateFinalizerEvidence(hasResult: true, shouldApplyTransition: true, ProcessStepRunStatus.Completed),
            requestedOperations: [ProcessDriverOperation.ApplyFinalizer]));

        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            denied,
            ProcessDriverDenialReason.MutationDenied,
            ProcessDriverDiagnosticCategory.MutationAttemptDenied);
    }

    [Fact]
    public void Runtime_evidence_consistency_alpha_rejects_untrusted_descriptor_evidence_without_mutation()
    {
        var verifier = new RuntimeEvidenceConsistencyAlphaVerifier();
        var result = verifier.Verify(CreateRequest(
            executionEvidence: CreateExecutionEvidence(ProcessAutomationRunOutcome.Succeeded),
            finalizerEvidence: CreateFinalizerEvidence(hasResult: true, shouldApplyTransition: true, ProcessStepRunStatus.Completed),
            evidenceUri: "https://example.invalid/core-descriptor.json"));

        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            result,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.TranscriptUntrusted);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("example.invalid", StringComparison.Ordinal));
        Assert.All(result.AuditFacts, fact =>
            Assert.Equal(ProcessDriverAuditFactKind.OperationDenied, fact.Kind));
    }

    [Fact]
    public void Runtime_evidence_consistency_alpha_supplied_content_policy_rejects_untrusted_mismatched_oversized_and_invalid_content_type()
    {
        var verifier = new RuntimeEvidenceConsistencyAlphaVerifier();
        var executionEvidence = CreateExecutionEvidence(ProcessAutomationRunOutcome.Succeeded);
        var finalizerEvidence = CreateFinalizerEvidence(
            hasResult: true,
            shouldApplyTransition: true,
            ProcessStepRunStatus.Completed);
        var wrongContentType = verifier.Verify(CreateRequest(
            executionEvidence,
            finalizerEvidence,
            suppliedContentFactory: reference => new ProcessDriverSuppliedEvidenceContent(
                ProcessDriverSuppliedEvidenceContentKind.TranscriptText,
                reference,
                ProcessDriverSuppliedEvidenceContentRules.PlainTextContentType,
                "runtime-evidence-consistency".Length,
                reference.ContentHash)));
        var untrustedEnvelopeUri = verifier.Verify(CreateRequest(
            executionEvidence,
            finalizerEvidence,
            suppliedContentFactory: reference => new ProcessDriverSuppliedEvidenceContent(
                ProcessDriverSuppliedEvidenceContentKind.CoreDescriptorPayload,
                reference with { Uri = "https://example.invalid/core-descriptor.json" },
                ProcessDriverSuppliedEvidenceContentRules.JsonContentType,
                "runtime-evidence-consistency".Length,
                reference.ContentHash)));
        var mismatchedEnvelopeHash = verifier.Verify(CreateRequest(
            executionEvidence,
            finalizerEvidence,
            suppliedContentFactory: reference => new ProcessDriverSuppliedEvidenceContent(
                ProcessDriverSuppliedEvidenceContentKind.CoreDescriptorPayload,
                reference,
                ProcessDriverSuppliedEvidenceContentRules.JsonContentType,
                "runtime-evidence-consistency".Length,
                ProcessDriverEvidencePolicy.ComputeSha256("different runtime evidence"))));
        var oversizedEnvelope = verifier.Verify(CreateRequest(
            executionEvidence,
            finalizerEvidence,
            suppliedContentFactory: reference => new ProcessDriverSuppliedEvidenceContent(
                ProcessDriverSuppliedEvidenceContentKind.CoreDescriptorPayload,
                reference,
                ProcessDriverSuppliedEvidenceContentRules.JsonContentType,
                ProcessDriverSuppliedEvidenceContentRules.MaxSuppliedEvidenceContentBytes + 1,
                reference.ContentHash)));

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
            Assert.DoesNotContain("example.invalid", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Runtime_evidence_consistency_alpha_evidence_boundary_rejects_missing_supplied_content_envelope_without_descriptor_analysis()
    {
        var verifier = new RuntimeEvidenceConsistencyAlphaVerifier();
        var result = verifier.Verify(CreateRequest(
            executionEvidence: CreateExecutionEvidence(ProcessAutomationRunOutcome.Succeeded),
            finalizerEvidence: CreateFinalizerEvidence(
                hasResult: true,
                shouldApplyTransition: true,
                ProcessStepRunStatus.Completed),
            suppliedContentFactory: reference => new ProcessDriverSuppliedEvidenceContent(
                ProcessDriverSuppliedEvidenceContentKind.CoreDescriptorPayload,
                reference,
                ProcessDriverSuppliedEvidenceContentRules.JsonContentType,
                SizeBytes: 0,
                ContentHash: reference.ContentHash)));

        ProcessDriverVerificationTestHarness.AssertMutationFreeDenial(
            result,
            ProcessDriverDenialReason.MissingEvidence,
            ProcessDriverDiagnosticCategory.InsufficientProof);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Category == ProcessDriverDiagnosticCategory.RuntimeEvidenceInconsistent);
    }

    [Fact]
    public void Runtime_evidence_consistency_alpha_audit_facts_include_caller_lane_operation_evidence_denial_and_output_hash()
    {
        var verifier = new RuntimeEvidenceConsistencyAlphaVerifier();
        var result = verifier.Verify(CreateRequest(
            executionEvidence: CreateExecutionEvidence(ProcessAutomationRunOutcome.Succeeded),
            finalizerEvidence: CreateFinalizerEvidence(
                hasResult: true,
                shouldApplyTransition: true,
                ProcessStepRunStatus.Completed)));

        Assert.True(result.Accepted);
        ProcessDriverVerificationTestHarness.AssertNormalizedAuditFacts(
            result,
            "manager:runtime-readonly",
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
            ProcessDriverVerificationTestHarness.RuntimeReadonlyOperations,
            ProcessDriverDenialReason.None);
        Assert.All(result.AuditFacts, fact =>
            Assert.Contains(
                fact.EvidenceReferences,
                evidenceReference => evidenceReference.Kind == ProcessDriverEvidenceReferenceKind.CoreDescriptor));
    }

    [Fact]
    public void Runtime_evidence_consistency_alpha_package_is_solution_bound_and_runtime_free()
    {
        var root = FindRepositoryRoot();
        var solution = ReadRepositoryFile("CanDoItAll.slnx");
        var project = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.RuntimeEvidence",
            "CanDoItAll.Processes.Drivers.RuntimeEvidence.csproj");
        var source = ReadProjectSource(root);

        Assert.Contains(
            "src/CanDoItAll.Processes.Drivers.RuntimeEvidence/CanDoItAll.Processes.Drivers.RuntimeEvidence.csproj",
            solution,
            StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Core.csproj", project, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanDoItAll.Modules.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceCollection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics.Process", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRuntimeSelector", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverManagerCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverHost", source, StringComparison.Ordinal);
    }

    private static RuntimeEvidenceConsistencyVerificationRequest CreateRequest(
        ProcessExecutionEvidenceDescriptor? executionEvidence,
        ProcessFinalizerEvidenceDescriptor? finalizerEvidence,
        ProcessRetryDiagnosticDescriptor? retryDiagnostic = null,
        ProcessProviderRepairDiagnosticDescriptor? providerRepairDiagnostic = null,
        ProcessNoProgressRetryDiagnosticDescriptor? noProgressDiagnostic = null,
        IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor>? projectionSourceOrder = null,
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string evidenceUri = "bundle://proof/SB018/runtime-evidence-consistency.json",
        Func<ProcessDriverEvidenceReference, ProcessDriverSuppliedEvidenceContent>? suppliedContentFactory = null)
    {
        const string suppliedPayload = "runtime-evidence-consistency";
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            evidenceUri,
            suppliedPayload,
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
        var suppliedContent = suppliedContentFactory?.Invoke(evidenceReference) ??
            ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
                evidenceReference,
                suppliedPayload);
        var verificationRequest = ProcessDriverVerificationTestHarness.CreateVerificationRequest(
            ProcessDriverPermissionMode.ManagerReadonly,
            ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
                ProcessDriverPermissionMode.ManagerReadonly),
            [evidenceReference],
            requestedOperations ?? ProcessDriverVerificationTestHarness.RuntimeReadonlyOperations,
            "manager:runtime-readonly");

        return new RuntimeEvidenceConsistencyVerificationRequest(
            verificationRequest,
            suppliedContent,
            executionEvidence,
            finalizerEvidence,
            retryDiagnostic,
            noProgressDiagnostic,
            providerRepairDiagnostic,
            projectionSourceOrder ?? [],
            DateTimeOffset.Parse("2026-06-08T12:00:00Z"));
    }

    private static ProcessExecutionEvidenceDescriptor CreateExecutionEvidence(
        ProcessAutomationRunOutcome outcome,
        int unresolvedCriticalToolFailures = 0)
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
                HasUnresolvedCriticalToolFailures: unresolvedCriticalToolFailures > 0,
                UnresolvedCriticalToolFailureCount: unresolvedCriticalToolFailures,
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
        bool shouldRetry,
        int unresolvedCriticalToolFailures = 0)
    {
        return new ProcessRetryDiagnosticDescriptor(
            shouldRetry,
            AttemptNumber: 1,
            MaxExecutionAttempts: 3,
            RetryReasons: [],
            RetryReasonSummary: string.Empty,
            MissingRequiredTools: [],
            FailedToolNames: [],
            unresolvedCriticalToolFailures,
            HasMissingRequiredTools: false,
            HasUnresolvedCriticalToolFailures: unresolvedCriticalToolFailures > 0,
            HasBuildFailure: false,
            HasTestFailure: false,
            HasRecoverableProviderFailure: false,
            HasRecoverableExecutionInterruption: false,
            HasRecoverableFinalizerFailure: false,
            PrimaryFailureKind: unresolvedCriticalToolFailures > 0
                ? ProcessRetryDiagnosticFailureKind.CriticalToolFailure
                : ProcessRetryDiagnosticFailureKind.None);
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

    private static string ReadProjectSource(string repositoryRoot)
    {
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(
                    Path.Combine(repositoryRoot, "src", "CanDoItAll.Processes.Drivers.RuntimeEvidence"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. pathParts]));
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
