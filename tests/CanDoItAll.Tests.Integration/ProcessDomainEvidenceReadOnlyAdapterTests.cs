using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.BusinessAnalysis;
using CanDoItAll.Processes.Drivers.OfficeEvidence;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessDomainEvidenceReadOnlyAdapterTests
{
    private const string ArtifactEvidencePayload = """{"projection":[{"source":"file-write"}],"validation":[{"kind":"deliverable"}]}""";
    private const string OfficeEvidencePayload = """{"items":[{"kind":"email","id":"message-1"}]}""";
    private const string BusinessAnalysisPayload = """{"items":[{"kind":"deliverable","id":"analysis-1"},{"kind":"evidence","id":"evidence-1"}]}""";

    [Fact]
    public void Process_artifact_evidence_readonly_adapter_SB021_INV_001_maps_supplied_descriptors_to_observation_without_mutation()
    {
        var adapter = new ProcessArtifactEvidenceReadOnlyAdapter();
        var payload = CreateArtifactPayload();

        var observation = adapter.Verify(payload);

        Assert.True(observation.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, observation.DenialReason);
        Assert.True(observation.NoMutationPerformed);
        Assert.Equal(ProcessArtifactEvidenceSourceLane.ArtifactEvidenceConsistency, observation.SourceLane);
        Assert.Equal(payload.ProcessRunId, observation.ProcessRunId);
        Assert.Equal(payload.StepRunId, observation.StepRunId);
        Assert.Equal(payload.ArtifactId, observation.ArtifactId);
        Assert.Equal(payload.RequestedAt, observation.ObservedAt);
        Assert.Equal(ProcessDriverContractVersion.Current, observation.ContractVersion);
        Assert.Contains(
            observation.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        Assert.Contains(
            observation.EvidenceReferences,
            evidenceReference => evidenceReference.CoreDescriptorFamily == ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence);
        Assert.Contains(
            observation.EvidenceReferences,
            evidenceReference => evidenceReference.CoreDescriptorFamily == ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation);
        AssertReadonlyAuditFacts(
            observation.AuditFacts,
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
            ProcessDriverDenialReason.None);
    }

    [Fact]
    public void Process_artifact_evidence_readonly_adapter_SB021_INV_002_denies_mutation_and_untrusted_sources_without_verifier_side_effects()
    {
        var adapter = new ProcessArtifactEvidenceReadOnlyAdapter();

        var mutationObservation = adapter.Verify(CreateArtifactPayload(
            requestedOperations: [ProcessDriverOperation.WriteArtifact]));
        var untrustedObservation = adapter.Verify(CreateArtifactPayload(
            projectionEvidenceUri: "https://example.invalid/artifact-projection.json"));

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
    public void Process_office_and_business_readonly_adapters_SB024_INV_001_map_supplied_items_and_deny_external_or_record_mutation()
    {
        var officeAdapter = new ProcessOfficeEvidenceReadOnlyAdapter();
        var businessAdapter = new ProcessBusinessAnalysisReadOnlyAdapter();

        var officeObservation = officeAdapter.Verify(CreateOfficePayload());
        var businessObservation = businessAdapter.Verify(CreateBusinessPayload());
        var officeGraphAttempt = officeAdapter.Verify(CreateOfficePayload(
            requestedOperations: [ProcessDriverOperation.CallOfficeGraph]));
        var businessMutationAttempt = businessAdapter.Verify(CreateBusinessPayload(
            requestedOperations: [ProcessDriverOperation.MutateBusinessRecord]));

        Assert.True(officeObservation.Accepted);
        Assert.Equal(ProcessOfficeEvidenceSourceLane.OfficeEvidenceRead, officeObservation.SourceLane);
        Assert.Equal(ProcessDriverCapabilityScopeKind.OfficeEvidenceRead, officeObservation.AuditFacts[0].Lane);
        Assert.True(officeObservation.NoMutationPerformed);
        Assert.True(businessObservation.Accepted);
        Assert.Equal(ProcessBusinessAnalysisSourceLane.BusinessAnalysisRead, businessObservation.SourceLane);
        Assert.Equal(ProcessDriverCapabilityScopeKind.BusinessAnalysisRead, businessObservation.AuditFacts[0].Lane);
        Assert.True(businessObservation.NoMutationPerformed);

        Assert.False(officeGraphAttempt.Accepted);
        Assert.Equal(ProcessDriverDenialReason.ExternalCallDenied, officeGraphAttempt.DenialReason);
        Assert.True(officeGraphAttempt.NoMutationPerformed);
        Assert.Contains(
            officeGraphAttempt.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.MutationAttemptDenied);

        Assert.False(businessMutationAttempt.Accepted);
        Assert.Equal(ProcessDriverDenialReason.MutationDenied, businessMutationAttempt.DenialReason);
        Assert.True(businessMutationAttempt.NoMutationPerformed);
        Assert.Contains(
            businessMutationAttempt.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.MutationAttemptDenied);
    }

    [Fact]
    public void Process_observation_aggregation_readonly_adapter_SB027_INV_001_combines_existing_responses_without_persistence()
    {
        var adapter = new ProcessDriverObservationAggregationReadOnlyAdapter();
        var payload = new ProcessDriverObservationAggregationReadOnlyPayload(
            ProcessRunId,
            StepRunId,
            "process-consumer:aggregate-readonly",
            [
                CreateVerificationResponse(ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead, accepted: true),
                CreateVerificationResponse(ProcessDriverCapabilityScopeKind.OfficeEvidenceRead, accepted: true),
                CreateVerificationResponse(ProcessDriverCapabilityScopeKind.BusinessAnalysisRead, accepted: false)
            ],
            RequestedAt);

        var observation = adapter.Aggregate(payload);

        Assert.Equal(payload.ProcessRunId, observation.ProcessRunId);
        Assert.Equal(payload.StepRunId, observation.StepRunId);
        Assert.Equal(payload.RequestedAt, observation.ObservedAt);
        Assert.Equal(3, observation.ResponseCount);
        Assert.Equal(2, observation.AcceptedCount);
        Assert.Equal(1, observation.DeniedCount);
        Assert.True(observation.AggregationMutationFree);
        Assert.True(observation.AllResponsesMutationFree);
        Assert.Equal(ProcessDriverContractVersion.Current, observation.ContractVersion);
        Assert.Contains(
            observation.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead);
        Assert.Contains(
            observation.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        Assert.Contains(
            observation.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);
    }

    private static ProcessArtifactEvidenceReadOnlyPayload CreateArtifactPayload(
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string projectionEvidenceUri = "bundle://proof/SB021/artifact-projection-evidence.json")
    {
        var projectionReference = CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            projectionEvidenceUri,
            ArtifactEvidencePayload,
            ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence);
        var validationReference = CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            "bundle://proof/SB021/artifact-projection-validation.json",
            ArtifactEvidencePayload,
            ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation);

        return new ProcessArtifactEvidenceReadOnlyPayload(
            ProcessRunId,
            StepRunId,
            ArtifactId,
            "process-consumer:artifact-readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            CreateScope(ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead),
            [projectionReference, validationReference],
            requestedOperations ?? [ProcessDriverOperation.InspectExistingEvidence, ProcessDriverOperation.ReturnDiagnostics],
            ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
                projectionReference,
                ArtifactEvidencePayload),
            [CreateArtifactProjectionLineage()],
            [ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite)],
            [],
            [CreateArtifactValidationRequirement()],
            [],
            [],
            RequestedAt);
    }

    private static ProcessOfficeEvidenceReadOnlyPayload CreateOfficePayload(
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null)
    {
        var evidenceReference = CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact,
            "bundle://proof/SB024/office-evidence.json",
            OfficeEvidencePayload,
            coreDescriptorFamily: null);

        return new ProcessOfficeEvidenceReadOnlyPayload(
            ProcessRunId,
            StepRunId,
            ArtifactId,
            "process-consumer:office-readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            CreateScope(ProcessDriverCapabilityScopeKind.OfficeEvidenceRead),
            [evidenceReference],
            requestedOperations ?? [ProcessDriverOperation.InspectExistingEvidence, ProcessDriverOperation.ReturnDiagnostics],
            ProcessDriverSuppliedEvidenceContentRules.CreateOfficeEvidencePayload(
                evidenceReference,
                OfficeEvidencePayload),
            [CreateOfficeItem()],
            RequestedAt);
    }

    private static ProcessBusinessAnalysisReadOnlyPayload CreateBusinessPayload(
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null)
    {
        var evidenceReference = CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact,
            "bundle://proof/SB024/business-analysis.json",
            BusinessAnalysisPayload,
            coreDescriptorFamily: null);

        return new ProcessBusinessAnalysisReadOnlyPayload(
            ProcessRunId,
            StepRunId,
            ArtifactId,
            "process-consumer:business-readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            CreateScope(ProcessDriverCapabilityScopeKind.BusinessAnalysisRead),
            [evidenceReference],
            requestedOperations ?? [ProcessDriverOperation.InspectExistingEvidence, ProcessDriverOperation.ReturnDiagnostics],
            ProcessDriverSuppliedEvidenceContentRules.CreateBusinessAnalysisPayload(
                evidenceReference,
                BusinessAnalysisPayload),
            [CreateBusinessDeliverable(), CreateBusinessSupportingEvidence()],
            RequestedAt);
    }

    private static ProcessDriverVerificationResponse CreateVerificationResponse(
        ProcessDriverCapabilityScopeKind lane,
        bool accepted)
    {
        var evidence = CreateEvidenceReference(lane);
        var denialReason = accepted
            ? ProcessDriverDenialReason.None
            : ProcessDriverDenialReason.MissingEvidence;
        var diagnosticCategory = accepted
            ? ProcessDriverDiagnosticCategory.NoIssueDetected
            : ProcessDriverDiagnosticCategory.InsufficientProof;

        return new ProcessDriverVerificationResponse(
            accepted,
            denialReason,
            [
                new ProcessDriverDiagnostic(
                    accepted ? ProcessDriverDiagnosticSeverity.Info : ProcessDriverDiagnosticSeverity.Error,
                    diagnosticCategory,
                    $"{lane} supplied observation",
                    evidence)
            ],
            [evidence],
            NoRedaction,
            NoMutationPerformed: true,
            [CreateAuditFact(lane, accepted, evidence)],
            ProcessDriverContractVersion.Current);
    }

    private static ProcessDriverAuditFact CreateAuditFact(
        ProcessDriverCapabilityScopeKind lane,
        bool accepted,
        ProcessDriverEvidenceReference evidence)
    {
        var denialReason = accepted
            ? ProcessDriverDenialReason.None
            : ProcessDriverDenialReason.MissingEvidence;

        return new ProcessDriverAuditFact(
            Guid.Parse($"00000000-0000-0000-0000-{(int)lane:000000000000}"),
            RequestedAt,
            accepted ? ProcessDriverAuditFactKind.DiagnosticReturned : ProcessDriverAuditFactKind.OperationDenied,
            "process-consumer:aggregate-readonly",
            CreatePermissionMode(lane),
            CreateScope(lane),
            lane,
            ProcessDriverOperation.InspectExistingEvidence,
            [evidence],
            denialReason,
            NoRedaction,
            $"{lane} supplied observation",
            ProcessDriverEvidencePolicy.ComputeSha256($"{lane} supplied observation"));
    }

    private static ProcessArtifactProjectionLineageDescriptor CreateArtifactProjectionLineage()
    {
        return ProcessArtifactProjectionEvidenceDescriptorRules.DescribeLineage(
            ProcessCoreArtifactProjectionSourceKind.FileWrite,
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

    private static ProcessArtifactValidationRequirementDescriptor CreateArtifactValidationRequirement()
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

    private static OfficeEvidenceItem CreateOfficeItem()
    {
        return new OfficeEvidenceItem(
            OfficeEvidenceItemKind.EmailMessage,
            "message-1",
            "Evidence review",
            "manager@example.invalid",
            ["owner@example.invalid"],
            DateTimeOffset.Parse("2026-06-08T12:15:00Z"),
            "Evidence review text was supplied by the caller.");
    }

    private static BusinessAnalysisEvidenceItem CreateBusinessDeliverable()
    {
        return new BusinessAnalysisEvidenceItem(
            BusinessAnalysisEvidenceItemKind.Deliverable,
            "analysis-1",
            "Evidence review",
            "Requirement: business analysis evidence review text was supplied by the caller.",
            DateTimeOffset.Parse("2026-06-08T13:15:00Z"));
    }

    private static BusinessAnalysisEvidenceItem CreateBusinessSupportingEvidence()
    {
        return new BusinessAnalysisEvidenceItem(
            BusinessAnalysisEvidenceItemKind.SupportingEvidence,
            "evidence-1",
            "Interview summary",
            "Evidence: supplied interview summary supports the deliverable.",
            DateTimeOffset.Parse("2026-06-08T13:16:00Z"));
    }

    private static ProcessDriverEvidenceReference CreateEvidenceReference(
        ProcessDriverEvidenceReferenceKind kind,
        string uri,
        string contentSeed,
        ProcessDriverCoreDescriptorFamily? coreDescriptorFamily)
    {
        return new ProcessDriverEvidenceReference(
            kind,
            uri,
            ProcessDriverEvidencePolicy.ComputeSha256(contentSeed),
            coreDescriptorFamily);
    }

    private static ProcessDriverEvidenceReference CreateEvidenceReference(ProcessDriverCapabilityScopeKind lane)
    {
        return lane switch
        {
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead => CreateEvidenceReference(
                ProcessDriverEvidenceReferenceKind.CoreDescriptor,
                "bundle://proof/SB027/artifact-observation.json",
                "artifact observation",
                ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence),
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead => CreateEvidenceReference(
                ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact,
                "bundle://proof/SB027/office-observation.json",
                "office observation",
                coreDescriptorFamily: null),
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead => CreateEvidenceReference(
                ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact,
                "bundle://proof/SB027/business-observation.json",
                "business observation",
                coreDescriptorFamily: null),
            _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unsupported test lane.")
        };
    }

    private static void AssertReadonlyAuditFacts(
        IReadOnlyList<ProcessDriverAuditFact> auditFacts,
        ProcessDriverCapabilityScopeKind expectedLane,
        ProcessDriverDenialReason expectedDenialReason)
    {
        Assert.NotEmpty(auditFacts);
        Assert.All(auditFacts, fact =>
        {
            Assert.Equal(expectedLane, fact.Lane);
            Assert.Equal(expectedLane, fact.Scope.Kind);
            Assert.Equal(expectedDenialReason, fact.DenialReason);
            Assert.NotEmpty(fact.EvidenceReferences);
            Assert.True(fact.Scope is { AllowsProcessMutation: false, AllowsExternalCalls: false });
            Assert.Matches("^[A-F0-9]{64}$", fact.OutputHash);
        });
    }

    private static ProcessDriverCapabilityScope CreateScope(ProcessDriverCapabilityScopeKind lane)
    {
        return new ProcessDriverCapabilityScope(
            lane,
            CreatePermissionMode(lane),
            AllowsProcessMutation: false,
            AllowsExternalCalls: false,
            AllowsWorkspaceWrites: false,
            AllowsStorageWrites: false);
    }

    private static ProcessDriverPermissionMode CreatePermissionMode(ProcessDriverCapabilityScopeKind lane)
    {
        return lane == ProcessDriverCapabilityScopeKind.RuntimeFactsRead
            ? ProcessDriverPermissionMode.ManagerReadonly
            : ProcessDriverPermissionMode.VerificationOnly;
    }

    private static ProcessDriverRedactionDescriptor NoRedaction { get; } = new(
        ProcessDriverRedactionStatus.None,
        [],
        ProcessDriverEvidencePolicy.ComputeSha256(string.Empty));

    private static Guid ProcessRunId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static Guid StepRunId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static Guid ArtifactId { get; } = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static DateTimeOffset RequestedAt { get; } = DateTimeOffset.Parse("2026-06-08T17:00:00Z");
}
