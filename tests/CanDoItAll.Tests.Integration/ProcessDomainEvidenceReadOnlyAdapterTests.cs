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
using CanDoItAll.Processes.Drivers.BusinessAnalysis;
using CanDoItAll.Processes.Drivers.OfficeEvidence;
using CoreArtifactExpectationSnapshot = CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot;
using CoreArtifactRecordSnapshot = CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessDomainEvidenceReadOnlyAdapterTests
{
    private const string ArtifactEvidencePayload = """{"projection":[{"source":"file-write"}],"validation":[{"kind":"deliverable"}]}""";
    private const string OfficeEvidencePayload = """{"items":[{"kind":"email","id":"message-1"}]}""";
    private const string OfficeBatchEvidencePayload = """{"items":[{"kind":"email","id":"message-1"},{"kind":"document","id":"document-1"}]}""";
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

    [Fact]
    public void Process_readonly_verification_batch_orchestrator_SB015_INV_001_runs_all_supplied_payload_lanes_without_runtime_host()
    {
        var orchestrator = new ProcessReadOnlyVerificationBatchOrchestrator();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            " process-consumer:batch-readonly ",
            RequestedAt,
            transcriptPayloads: [CreateTranscriptPayload()],
            runtimeEvidencePayloads: [CreateRuntimePayload()],
            artifactEvidencePayloads: [CreateArtifactPayload()],
            officeEvidencePayloads: [CreateOfficePayload()],
            businessAnalysisPayloads: [CreateBusinessPayload()]);

        var observation = orchestrator.Verify(payload);

        Assert.Equal(ProcessRunId, observation.ProcessRunId);
        Assert.Equal(StepRunId, observation.StepRunId);
        Assert.Equal("process-consumer:batch-readonly", observation.CallerContext);
        Assert.Equal(RequestedAt, observation.ObservedAt);
        Assert.Equal(5, observation.ResponseCount);
        Assert.Single(observation.TranscriptObservations);
        Assert.Single(observation.RuntimeEvidenceObservations);
        Assert.Single(observation.ArtifactEvidenceObservations);
        Assert.Single(observation.OfficeEvidenceObservations);
        Assert.Single(observation.BusinessAnalysisObservations);
        Assert.All(observation.Responses, response =>
        {
            Assert.True(response.Accepted);
            Assert.True(response.NoMutationPerformed);
            Assert.Equal(ProcessDriverContractVersion.Current, response.ContractVersion);
        });

        var aggregate = Assert.IsType<ProcessReadOnlyVerificationAggregateObservation>(
            observation.AggregateObservation);

        Assert.Equal(ProcessRunId, aggregate.ProcessRunId);
        Assert.Equal(StepRunId, aggregate.StepRunId);
        Assert.Equal("process-consumer:batch-readonly", aggregate.CallerContext);
        Assert.Equal(5, aggregate.ResponseCount);
        Assert.Equal(5, aggregate.AcceptedCount);
        Assert.Equal(0, aggregate.DeniedCount);
        Assert.True(aggregate.AggregationMutationFree);
        Assert.True(aggregate.AllResponsesMutationFree);
        Assert.Equal(ProcessDriverContractVersion.Current, aggregate.ContractVersion);
        Assert.Contains(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification);
        Assert.Contains(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.RuntimeFactsRead);
        Assert.Contains(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead);
        Assert.Contains(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        Assert.Contains(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);
        AssertReadOnlyList(observation.Responses, observation.Responses[0]);
        AssertReadOnlyList(observation.TranscriptObservations, observation.TranscriptObservations[0]);
        AssertReadOnlyList(aggregate.LaneSummaries, aggregate.LaneSummaries[0]);
    }

    [Fact]
    public void Process_readonly_verification_multi_domain_harness_SB037_SB038_INV_001_proves_current_lane_producers_and_orchestrator_consumer()
    {
        var harness = new ProcessReadOnlyVerificationMultiDomainHarness();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-consumer:harness-multidomain",
            RequestedAt,
            transcriptPayloads: [CreateTranscriptPayload()],
            runtimeEvidencePayloads: [CreateRuntimePayload()],
            artifactEvidencePayloads: [CreateArtifactPayload()],
            officeEvidencePayloads: [CreateOfficePayload()],
            businessAnalysisPayloads: [CreateBusinessPayload()]);

        var observation = harness.Verify(payload);
        var matrix = harness.AssertCurrentLaneProducerConsumerProof(observation);

        Assert.Equal("process-consumer:harness-multidomain", observation.CallerContext);
        Assert.Contains(matrix, entry =>
            entry.Lane == ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification &&
            entry.ProducerType == typeof(ProcessTranscriptVerificationReadOnlyAdapter));
        Assert.Contains(matrix, entry =>
            entry.Lane == ProcessDriverCapabilityScopeKind.RuntimeFactsRead &&
            entry.ProducerType == typeof(ProcessRuntimeEvidenceVerificationReadOnlyAdapter));
        Assert.Contains(matrix, entry =>
            entry.Lane == ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead &&
            entry.ProducerType == typeof(ProcessArtifactEvidenceReadOnlyAdapter));
        Assert.Contains(matrix, entry =>
            entry.Lane == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead &&
            entry.ProducerType == typeof(ProcessOfficeEvidenceReadOnlyAdapter));
        Assert.Contains(matrix, entry =>
            entry.Lane == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead &&
            entry.ProducerType == typeof(ProcessBusinessAnalysisReadOnlyAdapter));
        Assert.All(matrix, entry =>
            Assert.Equal(typeof(ProcessReadOnlyVerificationBatchOrchestrator), entry.ConsumerType));
    }

    [Fact]
    public void Process_readonly_verification_batch_orchestrator_SB027_INV_002_feeds_artifact_projection_validation_and_satisfaction_descriptors_without_mutation()
    {
        var orchestrator = new ProcessReadOnlyVerificationBatchOrchestrator();
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
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-consumer:artifact-rehearsal",
            RequestedAt,
            artifactEvidencePayloads:
            [
                CreateArtifactPayload(
                    projectionLineage: [CreateArtifactProjectionLineage()],
                    projectionSourceOrder:
                    [
                        ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.CompletedDecision),
                        ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite),
                        ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite)
                    ],
                    validationRequirements: [CreateArtifactValidationRequirement()],
                    expectedArtifacts: [expectedSensitiveDeliverable, expectedEvidence],
                    artifactRecords:
                    [
                        new CoreArtifactRecordSnapshot(
                            Id: Guid.Parse("11111111-2222-3333-4444-555555555555"),
                            ArtifactExpectationId: expectedSensitiveDeliverable.Id,
                            ArtifactKind: ProcessCoreArtifactKind.Deliverable,
                            Title: expectedSensitiveDeliverable.Title,
                            TrustStatus: ProcessCoreArtifactTrustStatus.ReviewRequired,
                            SensitivityLevel: ProcessCoreSensitivityLevel.Confidential,
                            CreatedAtUtc: DateTimeOffset.Parse("2026-06-08T14:10:00Z")),
                        new CoreArtifactRecordSnapshot(
                            Id: Guid.Parse("66666666-7777-8888-9999-000000000000"),
                            ArtifactExpectationId: expectedEvidence.Id,
                            ArtifactKind: ProcessCoreArtifactKind.Deliverable,
                            Title: expectedEvidence.Title,
                            TrustStatus: ProcessCoreArtifactTrustStatus.TrustedSource,
                            SensitivityLevel: ProcessCoreSensitivityLevel.Public,
                            CreatedAtUtc: DateTimeOffset.Parse("2026-06-08T14:12:00Z"))
                    ])
            ]);

        var observation = orchestrator.Verify(payload);
        var artifactObservation = Assert.Single(observation.ArtifactEvidenceObservations);
        var categories = artifactObservation.Diagnostics.Select(diagnostic => diagnostic.Category).ToHashSet();
        var aggregate = Assert.IsType<ProcessReadOnlyVerificationAggregateObservation>(
            observation.AggregateObservation);
        var artifactSummary = Assert.Single(aggregate.LaneSummaries);

        Assert.Equal(ProcessRunId, observation.ProcessRunId);
        Assert.Equal(StepRunId, observation.StepRunId);
        Assert.Equal("process-consumer:artifact-rehearsal", observation.CallerContext);
        Assert.Equal(1, observation.ResponseCount);
        Assert.True(artifactObservation.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, artifactObservation.DenialReason);
        Assert.True(artifactObservation.NoMutationPerformed);
        Assert.Equal(ProcessDriverContractVersion.Current, artifactObservation.ContractVersion);
        Assert.Contains(ProcessDriverDiagnosticCategory.ProjectionOrderDrift, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactLineageMissing, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactTrustSensitivityMismatch, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactSatisfactionInconsistent, categories);
        Assert.DoesNotContain(ProcessDriverDiagnosticCategory.NoIssueDetected, categories);
        Assert.Contains(
            artifactObservation.EvidenceReferences,
            evidenceReference => evidenceReference.CoreDescriptorFamily == ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence);
        Assert.Contains(
            artifactObservation.EvidenceReferences,
            evidenceReference => evidenceReference.CoreDescriptorFamily == ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation);
        AssertReadonlyAuditFacts(
            artifactObservation.AuditFacts,
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
            ProcessDriverDenialReason.None);
        Assert.Equal(ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead, artifactSummary.Lane);
        Assert.Equal(1, artifactSummary.ResponseCount);
        Assert.Equal(1, artifactSummary.AcceptedCount);
        Assert.Equal(0, artifactSummary.DeniedCount);
        Assert.True(artifactSummary.AllResponsesMutationFree);
        Assert.Contains(ProcessDriverDiagnosticCategory.ProjectionOrderDrift, artifactSummary.DiagnosticCategories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactLineageMissing, artifactSummary.DiagnosticCategories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactTrustSensitivityMismatch, artifactSummary.DiagnosticCategories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactSatisfactionInconsistent, artifactSummary.DiagnosticCategories);
        AssertDiagnosticsAndAuditDoNotContain(
            artifactObservation.Diagnostics,
            artifactObservation.AuditFacts,
            "fixture-secret",
            "reviewer@example.invalid");
        AssertReadOnlyList(observation.ArtifactEvidenceObservations, artifactObservation);
        AssertReadOnlyList(aggregate.LaneSummaries, artifactSummary);
    }

    [Fact]
    public void Process_readonly_verification_batch_orchestrator_SB030_INV_001_feeds_supplied_office_and_business_evidence_without_external_sources()
    {
        var orchestrator = new ProcessReadOnlyVerificationBatchOrchestrator();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-consumer:office-business-rehearsal",
            RequestedAt,
            officeEvidencePayloads:
            [
                CreateOfficePayload(
                    evidenceUri: "bundle://proof/SB030/office-evidence.json",
                    evidencePayload: OfficeBatchEvidencePayload,
                    items: [CreateOfficeItem(), CreateOfficeDocumentItem()])
            ],
            businessAnalysisPayloads:
            [
                CreateBusinessPayload(
                    evidenceUri: "bundle://proof/SB030/business-analysis.json",
                    items: [CreateBusinessDeliverable(), CreateBusinessSupportingEvidence()])
            ]);

        var observation = orchestrator.Verify(payload);
        var officeObservation = Assert.Single(observation.OfficeEvidenceObservations);
        var businessObservation = Assert.Single(observation.BusinessAnalysisObservations);
        var aggregate = Assert.IsType<ProcessReadOnlyVerificationAggregateObservation>(
            observation.AggregateObservation);
        var officeSummary = Assert.Single(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        var businessSummary = Assert.Single(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);
        var officeEvidence = Assert.Single(officeObservation.EvidenceReferences);
        var businessEvidence = Assert.Single(businessObservation.EvidenceReferences);

        Assert.Equal(ProcessRunId, observation.ProcessRunId);
        Assert.Equal(StepRunId, observation.StepRunId);
        Assert.Equal("process-consumer:office-business-rehearsal", observation.CallerContext);
        Assert.Equal(2, observation.ResponseCount);
        Assert.All(observation.Responses, response =>
        {
            Assert.True(response.Accepted);
            Assert.True(response.NoMutationPerformed);
            Assert.Equal(ProcessDriverDenialReason.None, response.DenialReason);
            Assert.Equal(ProcessDriverContractVersion.Current, response.ContractVersion);
        });

        Assert.True(officeObservation.Accepted);
        Assert.Equal(ProcessOfficeEvidenceSourceLane.OfficeEvidenceRead, officeObservation.SourceLane);
        Assert.Equal(ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact, officeEvidence.Kind);
        Assert.Equal("bundle://proof/SB030/office-evidence.json", officeEvidence.Uri);
        Assert.Equal(ProcessDriverEvidencePolicy.ComputeSha256(OfficeBatchEvidencePayload), officeEvidence.ContentHash);
        Assert.Null(officeEvidence.CoreDescriptorFamily);
        Assert.Contains(
            officeObservation.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        AssertReadonlyAuditFacts(
            officeObservation.AuditFacts,
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
            ProcessDriverDenialReason.None);

        Assert.True(businessObservation.Accepted);
        Assert.Equal(ProcessBusinessAnalysisSourceLane.BusinessAnalysisRead, businessObservation.SourceLane);
        Assert.Equal(ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact, businessEvidence.Kind);
        Assert.Equal("bundle://proof/SB030/business-analysis.json", businessEvidence.Uri);
        Assert.Equal(ProcessDriverEvidencePolicy.ComputeSha256(BusinessAnalysisPayload), businessEvidence.ContentHash);
        Assert.Null(businessEvidence.CoreDescriptorFamily);
        Assert.Contains(
            businessObservation.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        AssertReadonlyAuditFacts(
            businessObservation.AuditFacts,
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
            ProcessDriverDenialReason.None);

        Assert.Equal(2, aggregate.ResponseCount);
        Assert.Equal(2, aggregate.AcceptedCount);
        Assert.Equal(0, aggregate.DeniedCount);
        Assert.True(aggregate.AggregationMutationFree);
        Assert.True(aggregate.AllResponsesMutationFree);
        Assert.Equal(1, officeSummary.ResponseCount);
        Assert.Equal(1, officeSummary.AcceptedCount);
        Assert.Equal(0, officeSummary.DeniedCount);
        Assert.True(officeSummary.AllResponsesMutationFree);
        Assert.Equal(1, businessSummary.ResponseCount);
        Assert.Equal(1, businessSummary.AcceptedCount);
        Assert.Equal(0, businessSummary.DeniedCount);
        Assert.True(businessSummary.AllResponsesMutationFree);
        Assert.All(
            officeObservation.AuditFacts.Concat(businessObservation.AuditFacts),
            fact =>
            {
                Assert.False(fact.Scope.AllowsExternalCalls);
                Assert.False(fact.Scope.AllowsProcessMutation);
                Assert.False(fact.Scope.AllowsWorkspaceWrites);
                Assert.False(fact.Scope.AllowsStorageWrites);
            });
        AssertReadOnlyList(observation.OfficeEvidenceObservations, officeObservation);
        AssertReadOnlyList(observation.BusinessAnalysisObservations, businessObservation);
        AssertReadOnlyList(aggregate.LaneSummaries, officeSummary);
    }

    [Fact]
    public void Process_readonly_verification_batch_orchestrator_SB030_INV_002_denies_office_and_business_external_calls_without_mutation()
    {
        var orchestrator = new ProcessReadOnlyVerificationBatchOrchestrator();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-consumer:office-business-denial-rehearsal",
            RequestedAt,
            officeEvidencePayloads:
            [
                CreateOfficePayload(
                    requestedOperations: [ProcessDriverOperation.CallOfficeGraph],
                    evidenceUri: "bundle://proof/SB030/office-graph-call-denied.json",
                    evidencePayload: OfficeBatchEvidencePayload,
                    items: [CreateOfficeItemWithSensitiveText()])
            ],
            businessAnalysisPayloads:
            [
                CreateBusinessPayload(
                    requestedOperations: [ProcessDriverOperation.CallOfficeGraph],
                    evidenceUri: "bundle://proof/SB030/business-external-call-denied.json",
                    items: [CreateBusinessDeliverableWithSensitiveText(), CreateBusinessSupportingEvidence()]),
                CreateBusinessPayload(
                    requestedOperations: [ProcessDriverOperation.MutateBusinessRecord],
                    evidenceUri: "bundle://proof/SB030/business-record-mutation-denied.json",
                    items: [CreateBusinessDeliverableWithSensitiveText(), CreateBusinessSupportingEvidence()])
            ]);

        var observation = orchestrator.Verify(payload);
        var officeDenied = Assert.Single(observation.OfficeEvidenceObservations);
        var businessExternalDenied = Assert.Single(
            observation.BusinessAnalysisObservations,
            businessObservation => businessObservation.DenialReason == ProcessDriverDenialReason.ExternalCallDenied);
        var businessMutationDenied = Assert.Single(
            observation.BusinessAnalysisObservations,
            businessObservation => businessObservation.DenialReason == ProcessDriverDenialReason.MutationDenied);
        var aggregate = Assert.IsType<ProcessReadOnlyVerificationAggregateObservation>(
            observation.AggregateObservation);
        var officeSummary = Assert.Single(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        var businessSummary = Assert.Single(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);

        Assert.Equal(3, observation.ResponseCount);
        Assert.All(observation.Responses, response =>
        {
            Assert.False(response.Accepted);
            Assert.True(response.NoMutationPerformed);
            Assert.Equal(ProcessDriverContractVersion.Current, response.ContractVersion);
            Assert.Contains(
                response.Diagnostics,
                diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.MutationAttemptDenied);
            Assert.DoesNotContain(
                response.Diagnostics,
                diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        });

        Assert.False(officeDenied.Accepted);
        Assert.Equal(ProcessDriverDenialReason.ExternalCallDenied, officeDenied.DenialReason);
        Assert.True(officeDenied.NoMutationPerformed);
        AssertDeniedAuditFacts(
            officeDenied.AuditFacts,
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
            ProcessDriverOperation.CallOfficeGraph,
            ProcessDriverDenialReason.ExternalCallDenied);
        AssertDiagnosticsAndAuditDoNotContain(
            officeDenied.Diagnostics,
            officeDenied.AuditFacts,
            "fixture-secret",
            "reviewer@example.invalid");

        Assert.False(businessExternalDenied.Accepted);
        Assert.Equal(ProcessDriverDenialReason.ExternalCallDenied, businessExternalDenied.DenialReason);
        Assert.True(businessExternalDenied.NoMutationPerformed);
        AssertDeniedAuditFacts(
            businessExternalDenied.AuditFacts,
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
            ProcessDriverOperation.CallOfficeGraph,
            ProcessDriverDenialReason.ExternalCallDenied);
        AssertDiagnosticsAndAuditDoNotContain(
            businessExternalDenied.Diagnostics,
            businessExternalDenied.AuditFacts,
            "fixture-secret",
            "reviewer@example.invalid");

        Assert.False(businessMutationDenied.Accepted);
        Assert.Equal(ProcessDriverDenialReason.MutationDenied, businessMutationDenied.DenialReason);
        Assert.True(businessMutationDenied.NoMutationPerformed);
        AssertDeniedAuditFacts(
            businessMutationDenied.AuditFacts,
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
            ProcessDriverOperation.MutateBusinessRecord,
            ProcessDriverDenialReason.MutationDenied);
        AssertDiagnosticsAndAuditDoNotContain(
            businessMutationDenied.Diagnostics,
            businessMutationDenied.AuditFacts,
            "fixture-secret",
            "reviewer@example.invalid");

        Assert.Equal(3, aggregate.ResponseCount);
        Assert.Equal(0, aggregate.AcceptedCount);
        Assert.Equal(3, aggregate.DeniedCount);
        Assert.True(aggregate.AggregationMutationFree);
        Assert.True(aggregate.AllResponsesMutationFree);
        Assert.Equal(1, officeSummary.ResponseCount);
        Assert.Equal(0, officeSummary.AcceptedCount);
        Assert.Equal(1, officeSummary.DeniedCount);
        Assert.True(officeSummary.AllResponsesMutationFree);
        Assert.Contains(ProcessDriverDiagnosticCategory.MutationAttemptDenied, officeSummary.DiagnosticCategories);
        Assert.Equal(2, businessSummary.ResponseCount);
        Assert.Equal(0, businessSummary.AcceptedCount);
        Assert.Equal(2, businessSummary.DeniedCount);
        Assert.True(businessSummary.AllResponsesMutationFree);
        Assert.Contains(ProcessDriverDiagnosticCategory.MutationAttemptDenied, businessSummary.DiagnosticCategories);
        AssertReadOnlyList(observation.OfficeEvidenceObservations, officeDenied);
        AssertReadOnlyList(observation.BusinessAnalysisObservations, businessExternalDenied);
        AssertReadOnlyList(aggregate.LaneSummaries, officeSummary);
    }

    [Fact]
    public void Process_readonly_payload_builders_SB018_INV_001_create_hash_content_type_and_size_contracts_from_memory()
    {
        var transcriptPayload = CreateTranscriptPayload();
        var runtimePayload = CreateRuntimePayload();
        var artifactPayload = CreateArtifactPayload();
        var officePayload = CreateOfficePayload();
        var businessPayload = CreateBusinessPayload();

        Assert.Equal(ProcessDriverEvidencePolicy.ComputeSha256("Build succeeded."), transcriptPayload.TranscriptReference.TranscriptHash);
        Assert.Single(runtimePayload.EvidenceReferences);
        Assert.Equal(ProcessDriverEvidenceReferenceKind.CoreDescriptor, runtimePayload.EvidenceReferences[0].Kind);
        Assert.Equal(ProcessDriverCoreDescriptorFamily.ExecutionEvidence, runtimePayload.EvidenceReferences[0].CoreDescriptorFamily);
        Assert.True(ProcessDriverEvidencePolicy.IsSha256(runtimePayload.EvidenceReferences[0].ContentHash));
        AssertSuppliedContentContract(
            artifactPayload.SuppliedContent,
            ProcessDriverSuppliedEvidenceContentKind.CoreDescriptorPayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType,
            ArtifactEvidencePayload);
        AssertSuppliedContentContract(
            officePayload.SuppliedContent,
            ProcessDriverSuppliedEvidenceContentKind.OfficeEvidencePayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType,
            OfficeEvidencePayload);
        AssertSuppliedContentContract(
            businessPayload.SuppliedContent,
            ProcessDriverSuppliedEvidenceContentKind.BusinessAnalysisPayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType,
            BusinessAnalysisPayload);
    }

    private static ProcessTranscriptVerificationReadOnlyEvidencePayload CreateTranscriptPayload()
    {
        const string transcriptText = "Build succeeded.";

        return ProcessReadOnlyVerificationPayloadBuilder.CreateTranscriptPayload(
            new ProcessTranscriptVerificationPayloadFacts(
                CreateIdentity("process-consumer:transcript-readonly"),
                ProcessDriverTranscriptLanguage.DotNet,
                "dotnet",
                "net10.0",
                "bundle://proof/SB015/transcripts/process-supplied-transcript.txt",
                transcriptText,
                [ProcessDriverOperation.InspectExistingEvidence, ProcessDriverOperation.ReturnDiagnostics]));
    }

    private static ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload CreateRuntimePayload()
    {
        var executionEvidence = CreateExecutionEvidence(ProcessAutomationRunOutcome.Succeeded);
        var finalizerEvidence = CreateFinalizerEvidence(
            hasResult: true,
            shouldApplyTransition: true,
            ProcessStepRunStatus.Completed);

        return ProcessReadOnlyVerificationPayloadBuilder.CreateRuntimeEvidencePayload(
            new ProcessRuntimeEvidenceVerificationPayloadFacts(
                CreateIdentity("process-consumer:runtime-readonly"),
                "bundle://proof/SB015/runtime-evidence-consistency.json",
                executionEvidence,
                finalizerEvidence,
                RetryDiagnostic: null,
                NoProgressDiagnostic: null,
                ProviderRepairDiagnostic: null,
                ProjectionSourceOrder: [],
                [ProcessDriverOperation.ReadProcessFacts, ProcessDriverOperation.ReturnDiagnostics]));
    }

    private static ProcessArtifactEvidenceReadOnlyPayload CreateArtifactPayload(
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string projectionEvidenceUri = "bundle://proof/SB021/artifact-projection-evidence.json",
        IReadOnlyList<ProcessArtifactProjectionLineageDescriptor>? projectionLineage = null,
        IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor>? projectionSourceOrder = null,
        IReadOnlyList<ProcessProviderNativeBrowserEvidenceDescriptor>? providerNativeBrowserEvidence = null,
        IReadOnlyList<ProcessArtifactValidationRequirementDescriptor>? validationRequirements = null,
        IReadOnlyList<CoreArtifactExpectationSnapshot>? expectedArtifacts = null,
        IReadOnlyList<CoreArtifactRecordSnapshot>? artifactRecords = null)
    {
        return ProcessReadOnlyVerificationPayloadBuilder.CreateArtifactEvidencePayload(
            new ProcessArtifactEvidencePayloadFacts(
                CreateIdentity("process-consumer:artifact-readonly"),
                projectionEvidenceUri,
                ArtifactEvidencePayload,
                "bundle://proof/SB021/artifact-projection-validation.json",
                ArtifactEvidencePayload,
                projectionLineage ?? [CreateArtifactProjectionLineage()],
                projectionSourceOrder ?? [ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite)],
                providerNativeBrowserEvidence ?? [],
                validationRequirements ?? [CreateArtifactValidationRequirement()],
                expectedArtifacts ?? [],
                artifactRecords ?? [],
                requestedOperations));
    }

    private static ProcessOfficeEvidenceReadOnlyPayload CreateOfficePayload(
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string evidenceUri = "bundle://proof/SB024/office-evidence.json",
        string evidencePayload = OfficeEvidencePayload,
        IReadOnlyList<OfficeEvidenceItem>? items = null)
    {
        return ProcessReadOnlyVerificationPayloadBuilder.CreateOfficeEvidencePayload(
            new ProcessOfficeEvidencePayloadFacts(
                CreateIdentity("process-consumer:office-readonly"),
                evidenceUri,
                evidencePayload,
                items ?? [CreateOfficeItem()],
                requestedOperations));
    }

    private static ProcessBusinessAnalysisReadOnlyPayload CreateBusinessPayload(
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string evidenceUri = "bundle://proof/SB024/business-analysis.json",
        string evidencePayload = BusinessAnalysisPayload,
        IReadOnlyList<BusinessAnalysisEvidenceItem>? items = null)
    {
        return ProcessReadOnlyVerificationPayloadBuilder.CreateBusinessAnalysisPayload(
            new ProcessBusinessAnalysisPayloadFacts(
                CreateIdentity("process-consumer:business-readonly"),
                evidenceUri,
                evidencePayload,
                items ?? [CreateBusinessDeliverable(), CreateBusinessSupportingEvidence()],
                requestedOperations));
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

    private static CoreArtifactExpectationSnapshot CreateExpectedArtifact(
        Guid id,
        ProcessCoreArtifactKind artifactKind,
        string title,
        ProcessCoreArtifactTrustRequirement trustRequirement,
        ProcessCoreSensitivityLevel sensitivityLevel)
    {
        return new CoreArtifactExpectationSnapshot(
            Id: id,
            ArtifactKind: artifactKind,
            Title: title,
            IsRequired: true,
            TrustRequirement: trustRequirement,
            SensitivityLevel: sensitivityLevel,
            ValidationRequirementSummary: "Supplied validation requirement.",
            AllowedFutureUsageSummary: "Supplied future usage.");
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

    private static OfficeEvidenceItem CreateOfficeDocumentItem()
    {
        return new OfficeEvidenceItem(
            OfficeEvidenceItemKind.Document,
            "document-1",
            "Evidence review notes",
            "manager@example.invalid",
            [],
            DateTimeOffset.Parse("2026-06-08T12:20:00Z"),
            "Document evidence text was supplied by the caller.");
    }

    private static OfficeEvidenceItem CreateOfficeItemWithSensitiveText()
    {
        return new OfficeEvidenceItem(
            OfficeEvidenceItemKind.EmailMessage,
            "message-sensitive",
            "Sensitive evidence review",
            "manager@example.invalid",
            ["owner@example.invalid"],
            DateTimeOffset.Parse("2026-06-08T12:25:00Z"),
            "fixture-secret reviewer@example.invalid");
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

    private static BusinessAnalysisEvidenceItem CreateBusinessDeliverableWithSensitiveText()
    {
        return new BusinessAnalysisEvidenceItem(
            BusinessAnalysisEvidenceItemKind.Deliverable,
            "analysis-sensitive",
            "Sensitive evidence review",
            "Requirement: fixture-secret reviewer@example.invalid.",
            DateTimeOffset.Parse("2026-06-08T13:17:00Z"));
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

    private static void AssertDeniedAuditFacts(
        IReadOnlyList<ProcessDriverAuditFact> auditFacts,
        ProcessDriverCapabilityScopeKind expectedLane,
        ProcessDriverOperation expectedOperation,
        ProcessDriverDenialReason expectedDenialReason)
    {
        Assert.NotEmpty(auditFacts);
        Assert.All(auditFacts, fact =>
        {
            Assert.Equal(ProcessDriverAuditFactKind.OperationDenied, fact.Kind);
            Assert.Equal(expectedLane, fact.Lane);
            Assert.Equal(expectedLane, fact.Scope.Kind);
            Assert.Equal(expectedOperation, fact.RequestedOperation);
            Assert.Equal(expectedDenialReason, fact.DenialReason);
            Assert.False(fact.Scope.AllowsExternalCalls);
            Assert.False(fact.Scope.AllowsProcessMutation);
            Assert.False(fact.Scope.AllowsWorkspaceWrites);
            Assert.False(fact.Scope.AllowsStorageWrites);
            Assert.NotEmpty(fact.EvidenceReferences);
            Assert.Matches("^[A-F0-9]{64}$", fact.OutputHash);
        });
    }

    private static void AssertDiagnosticsAndAuditDoNotContain(
        IReadOnlyList<ProcessDriverDiagnostic> diagnostics,
        IReadOnlyList<ProcessDriverAuditFact> auditFacts,
        params string[] forbiddenFragments)
    {
        var diagnosticAndAuditText = diagnostics
            .Select(diagnostic => diagnostic.Message)
            .Concat(auditFacts.Select(fact => fact.DiagnosticSummary))
            .ToArray();

        foreach (var forbiddenFragment in forbiddenFragments.Where(static fragment => !string.IsNullOrWhiteSpace(fragment)))
        {
            Assert.All(diagnosticAndAuditText, value =>
                Assert.DoesNotContain(forbiddenFragment, value, StringComparison.Ordinal));
        }
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
                HasUnresolvedCriticalToolFailures: false,
                UnresolvedCriticalToolFailureCount: 0,
                SelectedBranchOutcomeId: null),
            new ProcessExecutionCarriedProofDescriptor(
                HasConcreteImplementationProof: true,
                HasRunnableApplicationProof: true,
                HasConcreteProductMutation: false));
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

    private static void AssertReadOnlyList<T>(IReadOnlyList<T> values, T sample)
    {
        Assert.False(values.GetType().IsArray);

        var collection = Assert.IsAssignableFrom<ICollection<T>>(values);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Add(sample));
    }

    private static void AssertSuppliedContentContract(
        ProcessDriverSuppliedEvidenceContent suppliedContent,
        ProcessDriverSuppliedEvidenceContentKind expectedKind,
        string expectedContentType,
        string expectedPayload)
    {
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasExpectedEnvelope(
            suppliedContent,
            expectedKind,
            expectedContentType));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasAllowedSize(suppliedContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasValidContentHash(suppliedContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasEvidenceReferenceHashBinding(suppliedContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HashMatchesSuppliedPayload(
            suppliedContent,
            expectedPayload));
    }

    private static ProcessReadOnlyPayloadIdentity CreateIdentity(string callerContext)
    {
        return new ProcessReadOnlyPayloadIdentity(
            ProcessRunId,
            StepRunId,
            ArtifactId,
            callerContext,
            RequestedAt);
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
