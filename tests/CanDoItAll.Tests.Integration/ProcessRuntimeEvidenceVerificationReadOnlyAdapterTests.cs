using System.Security.Cryptography;
using System.Text;
using System.Runtime.CompilerServices;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Core.Diagnostics;
using CanDoItAll.Processes.Core.Execution;
using CanDoItAll.Processes.Core.Finalization;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests
{
    [Fact]
    public void Process_runtime_evidence_readonly_adapter_SB030_INV_001_maps_supplied_core_descriptors_to_readonly_observation()
    {
        var adapter = new ProcessRuntimeEvidenceVerificationReadOnlyAdapter();
        var payload = CreatePayload(
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

        var observation = adapter.Verify(payload);

        Assert.True(observation.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, observation.DenialReason);
        Assert.True(observation.NoMutationPerformed);
        Assert.Equal(ProcessRuntimeEvidenceVerificationSourceLane.RuntimeEvidenceConsistency, observation.SourceLane);
        Assert.Equal(payload.ProcessRunId, observation.ProcessRunId);
        Assert.Equal(payload.StepRunId, observation.StepRunId);
        Assert.Equal(payload.ArtifactId, observation.ArtifactId);
        Assert.Equal(payload.RequestedAt, observation.ObservedAt);
        Assert.Equal(ProcessDriverContractVersion.Current, observation.ContractVersion);

        var categories = observation.Diagnostics.Select(diagnostic => diagnostic.Category).ToHashSet();
        Assert.Contains(ProcessDriverDiagnosticCategory.FinalizerContradiction, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.RetryContradiction, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ProviderRepairInconsistent, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.NoProgressFingerprintMissing, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ProjectionOrderDrift, categories);
        Assert.All(observation.AuditFacts, fact =>
        {
            Assert.Equal(ProcessDriverPermissionMode.ManagerReadonly, fact.PermissionMode);
            Assert.Equal(ProcessDriverCapabilityScopeKind.RuntimeFactsRead, fact.Scope.Kind);
            Assert.Matches("^[A-F0-9]{64}$", fact.OutputHash);
        });
    }

    [Fact]
    public void Process_runtime_evidence_readonly_adapter_SB030_INV_002_denies_mutation_and_untrusted_sources_without_mutation()
    {
        var adapter = new ProcessRuntimeEvidenceVerificationReadOnlyAdapter();

        var mutationObservation = adapter.Verify(CreatePayload(
            executionEvidence: CreateExecutionEvidence(ProcessAutomationRunOutcome.Succeeded),
            finalizerEvidence: CreateFinalizerEvidence(hasResult: true, shouldApplyTransition: true, ProcessStepRunStatus.Completed),
            requestedOperations: [ProcessDriverOperation.ApplyFinalizer]));
        var untrustedObservation = adapter.Verify(CreatePayload(
            executionEvidence: CreateExecutionEvidence(ProcessAutomationRunOutcome.Succeeded),
            finalizerEvidence: CreateFinalizerEvidence(hasResult: true, shouldApplyTransition: true, ProcessStepRunStatus.Completed),
            evidenceUri: "https://example.invalid/runtime-evidence.json"));

        Assert.False(mutationObservation.Accepted);
        Assert.True(mutationObservation.NoMutationPerformed);
        Assert.Equal(ProcessDriverDenialReason.MutationDenied, mutationObservation.DenialReason);
        Assert.Contains(
            mutationObservation.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.MutationAttemptDenied);

        Assert.False(untrustedObservation.Accepted);
        Assert.True(untrustedObservation.NoMutationPerformed);
        Assert.Equal(ProcessDriverDenialReason.MissingEvidence, untrustedObservation.DenialReason);
        Assert.Contains(
            untrustedObservation.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.TranscriptUntrusted);
        Assert.All(
            mutationObservation.AuditFacts.Concat(untrustedObservation.AuditFacts),
            fact => Assert.Equal(ProcessDriverAuditFactKind.OperationDenied, fact.Kind));
    }

    [Fact]
    public void Process_runtime_evidence_readonly_adapter_SB030_INV_003_keeps_driver_references_allowlisted_and_unregistered()
    {
        var root = FindRepositoryRoot();
        var moduleProject = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "CanDoItAll.Modules.Processes.csproj");
        var dispatchRoot = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var allowedDriverConsumerFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ProcessArtifactEvidenceReadOnlyAdapter.cs",
            "ProcessBusinessAnalysisReadOnlyAdapter.cs",
            "ProcessDryRunExecutionHost.cs",
            "ProcessExecutionCapableDriverFutureGate.cs",
            "ProcessDriverObservationAggregationReadOnlyAdapter.cs",
            "ProcessManagerReadOnlyVerificationCommandService.cs",
            "ProcessManagerReadOnlyVerificationProjection.cs",
            "ProcessOfficeEvidenceReadOnlyAdapter.cs",
            "ProcessReadOnlyVerificationAggregateObservation.cs",
            "ProcessReadOnlyVerificationBatchModels.cs",
            "ProcessReadOnlyVerificationBatchOrchestrator.cs",
            "ProcessReadOnlyVerificationJobModel.cs",
            "ProcessReadOnlyVerificationOperationPolicy.cs",
            "ProcessReadOnlyVerificationPayloadBuilder.cs",
            "ProcessReadOnlyVerificationRequestFactory.cs",
            "ProcessRuntimeEvidenceVerificationObservationMapper.cs",
            "ProcessRuntimeEvidenceVerificationReadOnlyAdapter.cs",
            "ProcessTranscriptVerificationObservationMapper.cs",
            "ProcessTranscriptVerificationPreflightPolicy.cs",
            "ProcessTranscriptVerificationReadOnlyAdapter.cs",
            "ProcessVerificationAuditStore.cs",
            "ProcessVerificationLaneRegistry.cs",
            "ProcessVerificationRuntimeHost.cs",
            "ProcessVerificationRuntimeHostModels.cs",
            "ProcessVerificationRuntimeHostOptions.cs",
            "ProcessVerificationRuntimeHostStatus.cs"
        };
        var actualDriverConsumerFiles = Directory
            .EnumerateFiles(dispatchRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => File.ReadAllText(path).Contains("CanDoItAll.Processes.Drivers.", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var allowedDriverConsumerFileNames = allowedDriverConsumerFiles
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var unapprovedDriverConsumers = actualDriverConsumerFiles
            .Where(fileName => !allowedDriverConsumerFiles.Contains(fileName))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var allowedSources = allowedDriverConsumerFiles
            .Select(fileName => File.ReadAllText(Path.Combine(dispatchRoot, fileName)))
            .ToArray();
        var combinedAllowedSource = string.Join(Environment.NewLine, allowedSources);

        Assert.Contains("CanDoItAll.Processes.Drivers.RuntimeEvidence.csproj", moduleProject, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.ArtifactEvidence.csproj", moduleProject, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.OfficeEvidence.csproj", moduleProject, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.BusinessAnalysis.csproj", moduleProject, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.ObservationAggregation.csproj", moduleProject, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.VerificationGateway.csproj", moduleProject, StringComparison.Ordinal);
        Assert.Equal(allowedDriverConsumerFileNames, actualDriverConsumerFiles);
        Assert.DoesNotContain("ProcessDomainEvidenceReadOnlyAdapters.cs", actualDriverConsumerFiles);
        Assert.Empty(unapprovedDriverConsumers);
        Assert.DoesNotContain("new TranscriptVerificationAlphaVerifier", combinedAllowedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeEvidenceConsistencyAlphaVerifier", combinedAllowedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ArtifactEvidenceAlphaVerifier", combinedAllowedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new OfficeEvidenceAlphaVerifier", combinedAllowedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new BusinessAnalysisAlphaVerifier", combinedAllowedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ProcessDriverObservationAggregator", combinedAllowedSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverVerificationGateway.CreateDefault()", combinedAllowedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceCollection", combinedAllowedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", combinedAllowedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", combinedAllowedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverRegistry", combinedAllowedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRuntimeSelector", combinedAllowedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverManagerCommand", combinedAllowedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverHost", combinedAllowedSource, StringComparison.Ordinal);
    }

    private static ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload CreatePayload(
        ProcessExecutionEvidenceDescriptor? executionEvidence,
        ProcessFinalizerEvidenceDescriptor? finalizerEvidence,
        ProcessRetryDiagnosticDescriptor? retryDiagnostic = null,
        ProcessProviderRepairDiagnosticDescriptor? providerRepairDiagnostic = null,
        ProcessNoProgressRetryDiagnosticDescriptor? noProgressDiagnostic = null,
        IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor>? projectionSourceOrder = null,
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string evidenceUri = "bundle://proof/SB030/runtime-evidence-consistency.json")
    {
        var effectiveProjectionSourceOrder = projectionSourceOrder ?? [];
        var suppliedPayloadMaterial = CreateDescriptorPayloadMaterial(
            executionEvidence,
            finalizerEvidence,
            retryDiagnostic,
            noProgressDiagnostic,
            providerRepairDiagnostic,
            effectiveProjectionSourceOrder);
        var evidenceReference = new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            evidenceUri,
            ComputeSha256(suppliedPayloadMaterial),
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);

        return new ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "process-runtime-consumer:readonly",
            ProcessDriverPermissionMode.ManagerReadonly,
            CreateRuntimeScope(),
            [evidenceReference],
            requestedOperations ?? [ProcessDriverOperation.ReadProcessFacts, ProcessDriverOperation.ReturnDiagnostics],
            executionEvidence,
            finalizerEvidence,
            retryDiagnostic,
            noProgressDiagnostic,
            providerRepairDiagnostic,
            effectiveProjectionSourceOrder,
            DateTimeOffset.Parse("2026-06-08T02:00:00Z"));
    }

    private static string CreateDescriptorPayloadMaterial(
        ProcessExecutionEvidenceDescriptor? executionEvidence,
        ProcessFinalizerEvidenceDescriptor? finalizerEvidence,
        ProcessRetryDiagnosticDescriptor? retryDiagnostic,
        ProcessNoProgressRetryDiagnosticDescriptor? noProgressDiagnostic,
        ProcessProviderRepairDiagnosticDescriptor? providerRepairDiagnostic,
        IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor> projectionSourceOrder)
    {
        return string.Join(
            "|",
            executionEvidence?.Run.ExecutionRunId,
            finalizerEvidence?.Intent.ProcessRunId,
            retryDiagnostic?.AttemptNumber,
            noProgressDiagnostic?.Fingerprint,
            providerRepairDiagnostic?.FailureSummary,
            projectionSourceOrder.Count);
    }

    private static ProcessDriverCapabilityScope CreateRuntimeScope()
    {
        return new ProcessDriverCapabilityScope(
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
            ProcessDriverPermissionMode.ManagerReadonly,
            AllowsProcessMutation: false,
            AllowsExternalCalls: false,
            AllowsWorkspaceWrites: false,
            AllowsStorageWrites: false);
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
                DateTimeOffset.Parse("2026-06-08T01:00:00Z"),
                DateTimeOffset.Parse("2026-06-08T01:01:00Z"),
                DateTimeOffset.Parse("2026-06-08T01:05:00Z"),
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

    private static string ComputeSha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
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
