using System.Runtime.CompilerServices;
using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.ArtifactEvidence;
using CanDoItAll.Processes.Drivers.BusinessAnalysis;
using CanDoItAll.Processes.Drivers.ObservationAggregation;
using CanDoItAll.Processes.Drivers.OfficeEvidence;
using CanDoItAll.Processes.Drivers.RuntimeEvidence;
using CanDoItAll.Processes.Drivers.TranscriptVerification;
using CanDoItAll.Processes.Drivers.VerificationGateway;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverVerificationGatewayTests
{
    private const string ArtifactEvidencePayload = """{"projection":[{"source":"file-write"}],"validation":[{"kind":"deliverable"}]}""";
    private const string OfficeEvidencePayload = """{"items":[{"kind":"email","id":"message-1"},{"kind":"document","id":"document-1"}]}""";
    private const string BusinessAnalysisPayload = """{"items":[{"kind":"deliverable","id":"analysis-1"},{"kind":"evidence","id":"evidence-1"}]}""";
    private const string MaliciousJsonPayload = """{"proof":"supplied","secret":"fixture-secret","reviewer":"reviewer@example.invalid","access_token":"plain-token"}""";
    private const string MaliciousTextPayload = "Requirement: supplied proof. Evidence: supplied source. secret=fixture-secret access_token=plain-token reviewer@example.invalid";

    [Fact]
    public void Process_driver_verification_gateway_explicitly_runs_all_approved_readonly_lanes()
    {
        var gateway = ProcessDriverVerificationGateway.CreateDefault();
        var implementedLanes = gateway.ImplementedLanes.Select(descriptor => descriptor.Lane).ToArray();

        Assert.Equal(
            [
                ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification,
                ProcessDriverVerificationGatewayLane.RuntimeEvidenceConsistency,
                ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency,
                ProcessDriverVerificationGatewayLane.OfficeEvidenceRead,
                ProcessDriverVerificationGatewayLane.BusinessAnalysisRead
            ],
            implementedLanes);

        var transcriptResult = gateway.VerifyTranscript(CreateTranscriptRequest("Build succeeded."));
        var runtimeResult = gateway.VerifyRuntimeEvidence(CreateRuntimeEvidenceRequest());
        var artifactResult = gateway.VerifyArtifactEvidence(CreateArtifactEvidenceRequest());
        var officeResult = gateway.VerifyOfficeEvidence(CreateOfficeEvidenceRequest());
        var businessResult = gateway.VerifyBusinessAnalysis(CreateBusinessAnalysisRequest());
        var aggregateResult = gateway.AggregateObservations(new ProcessDriverObservationAggregationRequest(
            [transcriptResult, runtimeResult, artifactResult, officeResult, businessResult],
            DateTimeOffset.Parse("2026-06-08T16:00:00Z"),
            "manager:gateway-aggregate"));

        AssertAcceptedReadonlyResponse(
            transcriptResult,
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification);
        AssertAcceptedReadonlyResponse(
            runtimeResult,
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead);
        AssertAcceptedReadonlyResponse(
            artifactResult,
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead);
        AssertAcceptedReadonlyResponse(
            officeResult,
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        AssertAcceptedReadonlyResponse(
            businessResult,
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);
        Assert.Equal(5, aggregateResult.ResponseCount);
        Assert.Equal(5, aggregateResult.AcceptedCount);
        Assert.Equal(0, aggregateResult.DeniedCount);
        Assert.True(aggregateResult.AggregationMutationFree);
        Assert.True(aggregateResult.AllResponsesMutationFree);
        Assert.Equal(ProcessDriverContractVersion.Current, aggregateResult.ContractVersion);
        Assert.Contains(
            aggregateResult.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead);
        Assert.Contains(
            aggregateResult.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        Assert.Contains(
            aggregateResult.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);
    }

    [Fact]
    public void Process_driver_verification_gateway_source_has_no_dynamic_registry_selector_di_or_manager_surface()
    {
        var gatewayProject = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.VerificationGateway",
            "CanDoItAll.Processes.Drivers.VerificationGateway.csproj");
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.VerificationGateway",
            "ProcessDriverVerificationGateway.cs");

        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.csproj", gatewayProject, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.TranscriptVerification.csproj", gatewayProject, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.RuntimeEvidence.csproj", gatewayProject, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.ArtifactEvidence.csproj", gatewayProject, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.OfficeEvidence.csproj", gatewayProject, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.BusinessAnalysis.csproj", gatewayProject, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.ObservationAggregation.csproj", gatewayProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", gatewayProject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VerifyTranscript", source, StringComparison.Ordinal);
        Assert.Contains("VerifyRuntimeEvidence", source, StringComparison.Ordinal);
        Assert.Contains("VerifyArtifactEvidence", source, StringComparison.Ordinal);
        Assert.Contains("VerifyOfficeEvidence", source, StringComparison.Ordinal);
        Assert.Contains("VerifyBusinessAnalysis", source, StringComparison.Ordinal);
        Assert.Contains("AggregateObservations", source, StringComparison.Ordinal);
        Assert.Contains("VerifyBatch", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Verify(ProcessDriverVerificationGatewayLane", source, StringComparison.Ordinal);
        Assert.DoesNotContain("object ", source, StringComparison.Ordinal);
        AssertNoForbiddenRuntimeSurface(source + gatewayProject);
    }

    [Fact]
    public void Process_driver_verification_gateway_runs_explicit_typed_batch_without_generic_dispatch()
    {
        var gateway = ProcessDriverVerificationGateway.CreateDefault();
        var gatewaySource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.VerificationGateway",
            "ProcessDriverVerificationGateway.cs");
        var batchSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.VerificationGateway",
            "ProcessDriverVerificationBatch.cs");
        var response = gateway.VerifyBatch(new ProcessDriverVerificationBatchRequest(
            transcriptRequests: [CreateTranscriptRequest("Build succeeded.")],
            runtimeEvidenceRequests: [CreateRuntimeEvidenceRequest()],
            artifactEvidenceRequests: [CreateArtifactEvidenceRequest()],
            officeEvidenceRequests: [CreateOfficeEvidenceRequest()],
            businessAnalysisRequests: [CreateBusinessAnalysisRequest()],
            aggregation: new ProcessDriverVerificationBatchAggregationRequest(
                DateTimeOffset.Parse("2026-06-08T18:00:00Z"),
                "manager:batch-readonly")));

        Assert.Single(response.TranscriptResponses);
        Assert.Single(response.RuntimeEvidenceResponses);
        Assert.Single(response.ArtifactEvidenceResponses);
        Assert.Single(response.OfficeEvidenceResponses);
        Assert.Single(response.BusinessAnalysisResponses);
        Assert.Equal(5, response.AllResponses.Count);
        Assert.NotNull(response.Aggregate);
        Assert.Equal(5, response.Aggregate.ResponseCount);
        Assert.Equal(5, response.Aggregate.AcceptedCount);
        Assert.True(response.Aggregate.AggregationMutationFree);
        Assert.True(response.Aggregate.AllResponsesMutationFree);
        AssertReadOnlyList(response.AllResponses, response.TranscriptResponses[0]);
        AssertReadOnlyList(response.TranscriptResponses, response.TranscriptResponses[0]);
        Assert.Contains("VerifyTranscriptBatch", gatewaySource, StringComparison.Ordinal);
        Assert.Contains("VerifyRuntimeEvidenceBatch", gatewaySource, StringComparison.Ordinal);
        Assert.Contains("VerifyArtifactEvidenceBatch", gatewaySource, StringComparison.Ordinal);
        Assert.Contains("VerifyOfficeEvidenceBatch", gatewaySource, StringComparison.Ordinal);
        Assert.Contains("VerifyBusinessAnalysisBatch", gatewaySource, StringComparison.Ordinal);
        Assert.DoesNotContain("VerifyEach<", gatewaySource, StringComparison.Ordinal);
        Assert.DoesNotContain("Func<", gatewaySource, StringComparison.Ordinal);
        Assert.DoesNotContain("Verify(object", gatewaySource + batchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("dynamic", gatewaySource + batchSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_driver_verification_gateway_rejects_side_effects_across_all_domain_lanes()
    {
        var gateway = ProcessDriverVerificationGateway.CreateDefault();

        foreach (var operation in ProcessDriverVerificationTestHarness.SideEffectOperations)
        {
            ProcessDriverVerificationTestHarness.AssertSideEffectDenied(
                gateway.VerifyTranscript(CreateTranscriptRequest(
                    "Build succeeded.",
                    requestedOperations: [operation])),
                operation);
            ProcessDriverVerificationTestHarness.AssertSideEffectDenied(
                gateway.VerifyRuntimeEvidence(CreateRuntimeEvidenceRequest([operation])),
                operation);
            ProcessDriverVerificationTestHarness.AssertSideEffectDenied(
                gateway.VerifyArtifactEvidence(CreateArtifactEvidenceRequest([operation])),
                operation);
            ProcessDriverVerificationTestHarness.AssertSideEffectDenied(
                gateway.VerifyOfficeEvidence(CreateOfficeEvidenceRequest(requestedOperations: [operation])),
                operation);
            ProcessDriverVerificationTestHarness.AssertSideEffectDenied(
                gateway.VerifyBusinessAnalysis(CreateBusinessAnalysisRequest(requestedOperations: [operation])),
                operation);
        }
    }

    [Fact]
    public void Process_driver_verification_gateway_audit_redaction_and_no_mutation_cover_accepted_and_denied_responses()
    {
        var gateway = ProcessDriverVerificationGateway.CreateDefault();
        var acceptedTranscript = gateway.VerifyTranscript(CreateTranscriptRequest("Build succeeded."));
        var deniedTranscript = gateway.VerifyTranscript(CreateTranscriptRequest(
            "Build succeeded. token=fixture-secret reviewer@example.invalid",
            requestedOperations: [ProcessDriverOperation.ExecuteCommand]));
        var acceptedRuntime = gateway.VerifyRuntimeEvidence(CreateRuntimeEvidenceRequest());
        var deniedRuntime = gateway.VerifyRuntimeEvidence(CreateRuntimeEvidenceRequest(
            [ProcessDriverOperation.ExecuteCommand]));
        var acceptedArtifact = gateway.VerifyArtifactEvidence(CreateArtifactEvidenceRequest());
        var deniedArtifact = gateway.VerifyArtifactEvidence(CreateArtifactEvidenceRequest(
            [ProcessDriverOperation.WriteArtifact]));
        var acceptedOffice = gateway.VerifyOfficeEvidence(CreateOfficeEvidenceRequest());
        var redactedOffice = gateway.VerifyOfficeEvidence(CreateOfficeEvidenceRequest(
            items:
            [
                new OfficeEvidenceItem(
                    OfficeEvidenceItemKind.EmailMessage,
                    "message-redacted",
                    string.Empty,
                    string.Empty,
                    [],
                    null,
                    "token=fixture-secret reviewer@example.invalid")
            ]));
        var acceptedBusiness = gateway.VerifyBusinessAnalysis(CreateBusinessAnalysisRequest());
        var redactedBusiness = gateway.VerifyBusinessAnalysis(CreateBusinessAnalysisRequest(
            items:
            [
                new BusinessAnalysisEvidenceItem(
                    BusinessAnalysisEvidenceItemKind.Deliverable,
                    "analysis-redacted",
                    "Risk summary",
                    "Assumption: fixture-secret reviewer@example.invalid. Contradiction: supplied evidence conflicts with the conclusion.",
                    DateTimeOffset.Parse("2026-06-08T13:20:00Z"))
            ]));

        AssertGatewayResponseEnvelope(
            acceptedTranscript,
            accepted: true,
            ProcessDriverDenialReason.None,
            "manager:readonly",
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
            ProcessDriverVerificationTestHarness.TranscriptReadonlyOperations);
        AssertGatewayResponseEnvelope(
            deniedTranscript,
            accepted: false,
            ProcessDriverDenialReason.UnsafeCommand,
            "manager:readonly",
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
            [ProcessDriverOperation.ExecuteCommand]);
        AssertGatewayResponseEnvelope(
            acceptedRuntime,
            accepted: true,
            ProcessDriverDenialReason.None,
            "manager:runtime-readonly",
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
            ProcessDriverVerificationTestHarness.RuntimeReadonlyOperations);
        AssertGatewayResponseEnvelope(
            deniedRuntime,
            accepted: false,
            ProcessDriverDenialReason.UnsafeCommand,
            "manager:runtime-readonly",
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
            [ProcessDriverOperation.ExecuteCommand]);
        AssertGatewayResponseEnvelope(
            acceptedArtifact,
            accepted: true,
            ProcessDriverDenialReason.None,
            "manager:artifact-readonly",
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
            ProcessDriverVerificationTestHarness.ArtifactEvidenceReadonlyOperations);
        AssertGatewayResponseEnvelope(
            deniedArtifact,
            accepted: false,
            ProcessDriverDenialReason.MutationDenied,
            "manager:artifact-readonly",
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
            [ProcessDriverOperation.WriteArtifact]);
        AssertGatewayResponseEnvelope(
            acceptedOffice,
            accepted: true,
            ProcessDriverDenialReason.None,
            "manager:office-readonly",
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
            ProcessDriverVerificationTestHarness.OfficeReadonlyOperations);
        AssertGatewayResponseEnvelope(
            acceptedBusiness,
            accepted: true,
            ProcessDriverDenialReason.None,
            "manager:business-readonly",
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
            ProcessDriverVerificationTestHarness.BusinessAnalysisReadonlyOperations);
        ProcessDriverVerificationTestHarness.AssertRedaction(
            deniedTranscript,
            ProcessDriverRedactionStatus.Redacted,
            ProcessDriverRedactionKind.Secret,
            ProcessDriverRedactionKind.EmailAddress);
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            deniedTranscript,
            "fixture-secret",
            "reviewer@example.invalid");
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            redactedOffice,
            "fixture-secret",
            "reviewer@example.invalid");
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            redactedBusiness,
            "fixture-secret",
            "reviewer@example.invalid",
            "conflicts with the conclusion");
    }

    [Fact]
    public void Process_driver_verification_gateway_closes_no_secret_no_mutation_and_hash_mismatch_gates_across_all_lanes()
    {
        var gateway = ProcessDriverVerificationGateway.CreateDefault();
        var forbiddenFragments = new[] { "fixture-secret", "plain-token", "reviewer@example.invalid" };
        var maliciousTranscript = gateway.VerifyTranscript(CreateTranscriptRequest(
            $"Build FAILED{Environment.NewLine}{MaliciousTextPayload}"));
        var maliciousRuntime = gateway.VerifyRuntimeEvidence(CreateRuntimeEvidenceRequest(
            suppliedPayload: MaliciousJsonPayload));
        var maliciousArtifact = gateway.VerifyArtifactEvidence(CreateArtifactEvidenceRequest(
            suppliedPayload: MaliciousJsonPayload));
        var maliciousOffice = gateway.VerifyOfficeEvidence(CreateOfficeEvidenceRequest(
            items:
            [
                new OfficeEvidenceItem(
                    OfficeEvidenceItemKind.EmailMessage,
                    "message-malicious",
                    "Evidence review",
                    "manager@example.invalid",
                    ["owner@example.invalid"],
                    DateTimeOffset.Parse("2026-06-08T12:15:00Z"),
                    MaliciousTextPayload)
            ],
            suppliedPayload: MaliciousJsonPayload));
        var maliciousBusiness = gateway.VerifyBusinessAnalysis(CreateBusinessAnalysisRequest(
            items:
            [
                new BusinessAnalysisEvidenceItem(
                    BusinessAnalysisEvidenceItemKind.Deliverable,
                    "analysis-malicious",
                    "Malicious supplied business analysis",
                    MaliciousTextPayload,
                    DateTimeOffset.Parse("2026-06-08T13:15:00Z")),
                new BusinessAnalysisEvidenceItem(
                    BusinessAnalysisEvidenceItemKind.SupportingEvidence,
                    "evidence-malicious",
                    "Malicious supplied supporting evidence",
                    MaliciousTextPayload,
                    DateTimeOffset.Parse("2026-06-08T13:16:00Z"))
            ],
            suppliedPayload: MaliciousJsonPayload));

        ProcessDriverVerificationTestHarness.AssertSealedReadonlyResponse(
            maliciousTranscript,
            expectedAccepted: true,
            ProcessDriverDenialReason.None,
            "manager:readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
            ProcessDriverVerificationTestHarness.TranscriptReadonlyOperations,
            ProcessDriverRedactionStatus.Redacted,
            forbiddenFragments);
        ProcessDriverVerificationTestHarness.AssertRedaction(
            maliciousTranscript,
            ProcessDriverRedactionStatus.Redacted,
            ProcessDriverRedactionKind.Secret,
            ProcessDriverRedactionKind.EmailAddress,
            ProcessDriverRedactionKind.AccessToken);
        ProcessDriverVerificationTestHarness.AssertSealedReadonlyResponse(
            maliciousRuntime,
            expectedAccepted: true,
            ProcessDriverDenialReason.None,
            "manager:runtime-readonly",
            ProcessDriverPermissionMode.ManagerReadonly,
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
            ProcessDriverVerificationTestHarness.RuntimeReadonlyOperations,
            expectedRedactionStatus: null,
            forbiddenFragments: forbiddenFragments);
        ProcessDriverVerificationTestHarness.AssertSealedReadonlyResponse(
            maliciousArtifact,
            expectedAccepted: true,
            ProcessDriverDenialReason.None,
            "manager:artifact-readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
            ProcessDriverVerificationTestHarness.ArtifactEvidenceReadonlyOperations,
            expectedRedactionStatus: null,
            forbiddenFragments: forbiddenFragments);
        ProcessDriverVerificationTestHarness.AssertSealedReadonlyResponse(
            maliciousOffice,
            expectedAccepted: true,
            ProcessDriverDenialReason.None,
            "manager:office-readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
            ProcessDriverVerificationTestHarness.OfficeReadonlyOperations,
            expectedRedactionStatus: null,
            forbiddenFragments: forbiddenFragments);
        ProcessDriverVerificationTestHarness.AssertSealedReadonlyResponse(
            maliciousBusiness,
            expectedAccepted: true,
            ProcessDriverDenialReason.None,
            "manager:business-readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
            ProcessDriverVerificationTestHarness.BusinessAnalysisReadonlyOperations,
            expectedRedactionStatus: null,
            forbiddenFragments: forbiddenFragments);

        var hashMismatchedTranscriptRequest = CreateTranscriptRequest("Build succeeded.");
        var hashMismatchedRuntimeRequest = CreateRuntimeEvidenceRequest();
        var hashMismatchedArtifactRequest = CreateArtifactEvidenceRequest();
        var hashMismatchedOfficeRequest = CreateOfficeEvidenceRequest();
        var hashMismatchedBusinessRequest = CreateBusinessAnalysisRequest();
        var hashMismatchedTranscript = gateway.VerifyTranscript(hashMismatchedTranscriptRequest with
        {
            SuppliedContent = CreateHashMismatchedSuppliedContent(hashMismatchedTranscriptRequest.SuppliedContent)
        });
        var hashMismatchedRuntime = gateway.VerifyRuntimeEvidence(hashMismatchedRuntimeRequest with
        {
            SuppliedContent = CreateHashMismatchedSuppliedContent(hashMismatchedRuntimeRequest.SuppliedContent)
        });
        var hashMismatchedArtifact = gateway.VerifyArtifactEvidence(hashMismatchedArtifactRequest with
        {
            SuppliedContent = CreateHashMismatchedSuppliedContent(hashMismatchedArtifactRequest.SuppliedContent)
        });
        var hashMismatchedOffice = gateway.VerifyOfficeEvidence(hashMismatchedOfficeRequest with
        {
            SuppliedContent = CreateHashMismatchedSuppliedContent(hashMismatchedOfficeRequest.SuppliedContent)
        });
        var hashMismatchedBusiness = gateway.VerifyBusinessAnalysis(hashMismatchedBusinessRequest with
        {
            SuppliedContent = CreateHashMismatchedSuppliedContent(hashMismatchedBusinessRequest.SuppliedContent)
        });

        ProcessDriverVerificationTestHarness.AssertEvidenceHashMismatchDenied(
            hashMismatchedTranscript,
            "manager:readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
            ProcessDriverVerificationTestHarness.TranscriptReadonlyOperations,
            forbiddenFragments);
        ProcessDriverVerificationTestHarness.AssertEvidenceHashMismatchDenied(
            hashMismatchedRuntime,
            "manager:runtime-readonly",
            ProcessDriverPermissionMode.ManagerReadonly,
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
            ProcessDriverVerificationTestHarness.RuntimeReadonlyOperations,
            forbiddenFragments);
        ProcessDriverVerificationTestHarness.AssertEvidenceHashMismatchDenied(
            hashMismatchedArtifact,
            "manager:artifact-readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
            ProcessDriverVerificationTestHarness.ArtifactEvidenceReadonlyOperations,
            forbiddenFragments);
        ProcessDriverVerificationTestHarness.AssertEvidenceHashMismatchDenied(
            hashMismatchedOffice,
            "manager:office-readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
            ProcessDriverVerificationTestHarness.OfficeReadonlyOperations,
            forbiddenFragments);
        ProcessDriverVerificationTestHarness.AssertEvidenceHashMismatchDenied(
            hashMismatchedBusiness,
            "manager:business-readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
            ProcessDriverVerificationTestHarness.BusinessAnalysisReadonlyOperations,
            forbiddenFragments);
    }

    private static void AssertAcceptedReadonlyResponse(
        ProcessDriverVerificationResponse response,
        ProcessDriverCapabilityScopeKind expectedLane)
    {
        Assert.True(response.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, response.DenialReason);
        Assert.True(response.NoMutationPerformed);
        Assert.Equal(ProcessDriverContractVersion.Current, response.ContractVersion);
        Assert.Contains(
            response.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            response,
            expectedLane == ProcessDriverCapabilityScopeKind.RuntimeFactsRead
                ? ProcessDriverPermissionMode.ManagerReadonly
                : ProcessDriverPermissionMode.VerificationOnly,
            expectedLane);
    }

    private static void AssertGatewayResponseEnvelope(
        ProcessDriverVerificationResponse response,
        bool accepted,
        ProcessDriverDenialReason denialReason,
        string callerContext,
        ProcessDriverCapabilityScopeKind lane,
        IReadOnlyList<ProcessDriverOperation> operations)
    {
        ProcessDriverVerificationTestHarness.AssertSealedReadonlyResponse(
            response,
            accepted,
            denialReason,
            callerContext,
            lane == ProcessDriverCapabilityScopeKind.RuntimeFactsRead
                ? ProcessDriverPermissionMode.ManagerReadonly
                : ProcessDriverPermissionMode.VerificationOnly,
            lane,
            operations);
    }

    private static TranscriptVerificationAlphaRequest CreateTranscriptRequest(
        string transcriptText,
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null)
    {
        var transcriptUri = "artifact://proof/scenario018/transcripts/dotnet-transcript.txt";
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CommandTranscript,
            transcriptUri,
            transcriptText,
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
        var transcriptReference = new ProcessDriverTranscriptReference(
            transcriptUri,
            ProcessDriverEvidencePolicy.ComputeSha256(transcriptText),
            ProcessDriverTranscriptLanguage.DotNet,
            "dotnet",
            "net10.0");
        var transcriptEvidence = ProcessDriverEvidencePolicy.CreateTranscriptEvidenceReference(
            transcriptReference,
            transcriptText);
        var verificationRequest = ProcessDriverVerificationTestHarness.CreateVerificationRequest(
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
                ProcessDriverPermissionMode.VerificationOnly),
            [evidenceReference],
            requestedOperations ?? ProcessDriverVerificationTestHarness.TranscriptReadonlyOperations,
            "manager:readonly");

        return new TranscriptVerificationAlphaRequest(
            verificationRequest,
            transcriptReference,
            ProcessDriverSuppliedEvidenceContentRules.CreateTranscriptText(
                transcriptEvidence,
                transcriptText),
            transcriptText,
            DateTimeOffset.Parse("2026-06-08T12:00:00Z"));
    }

    private static RuntimeEvidenceConsistencyVerificationRequest CreateRuntimeEvidenceRequest(
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string suppliedPayload = "runtime evidence gateway")
    {
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            "artifact://proof/scenario018/transcripts/runtime-evidence.json",
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
            requestedOperations ?? ProcessDriverVerificationTestHarness.RuntimeReadonlyOperations,
            "manager:runtime-readonly");

        return new RuntimeEvidenceConsistencyVerificationRequest(
            verificationRequest,
            suppliedContent,
            ExecutionEvidence: null,
            FinalizerEvidence: null,
            RetryDiagnostic: null,
            NoProgressDiagnostic: null,
            ProviderRepairDiagnostic: null,
            ProjectionSourceOrder: [],
            RequestedAt: DateTimeOffset.Parse("2026-06-08T12:00:00Z"));
    }

    private static ArtifactEvidenceVerificationRequest CreateArtifactEvidenceRequest(
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string suppliedPayload = ArtifactEvidencePayload)
    {
        var projectionReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            "artifact://proof/scenario018/artifact-projection-evidence.json",
            suppliedPayload,
            ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence);
        var validationReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            "artifact://proof/scenario018/artifact-projection-validation.json",
            suppliedPayload,
            ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation);
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
            ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
                projectionReference,
                suppliedPayload),
            [CreateArtifactProjectionLineage()],
            [ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite)],
            [],
            [CreateArtifactValidationRequirement()],
            [],
            [],
            DateTimeOffset.Parse("2026-06-08T14:00:00Z"));
    }

    private static OfficeEvidenceVerificationRequest CreateOfficeEvidenceRequest(
        IReadOnlyList<OfficeEvidenceItem>? items = null,
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string suppliedPayload = OfficeEvidencePayload)
    {
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact,
            "artifact://proof/scenario018/office-evidence.json",
            suppliedPayload,
            coreDescriptorFamily: null);
        var verificationRequest = ProcessDriverVerificationTestHarness.CreateVerificationRequest(
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
                ProcessDriverPermissionMode.VerificationOnly),
            [evidenceReference],
            requestedOperations ?? ProcessDriverVerificationTestHarness.OfficeReadonlyOperations,
            "manager:office-readonly");

        return new OfficeEvidenceVerificationRequest(
            verificationRequest,
            ProcessDriverSuppliedEvidenceContentRules.CreateOfficeEvidencePayload(
                evidenceReference,
                suppliedPayload),
            items ?? [CreateCompleteOfficeItem()],
            DateTimeOffset.Parse("2026-06-08T12:10:00Z"));
    }

    private static BusinessAnalysisVerificationRequest CreateBusinessAnalysisRequest(
        IReadOnlyList<BusinessAnalysisEvidenceItem>? items = null,
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string suppliedPayload = BusinessAnalysisPayload)
    {
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact,
            "artifact://proof/scenario018/business-analysis.json",
            suppliedPayload,
            coreDescriptorFamily: null);
        var verificationRequest = ProcessDriverVerificationTestHarness.CreateVerificationRequest(
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverVerificationTestHarness.CreateReadonlyScope(
                ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
                ProcessDriverPermissionMode.VerificationOnly),
            [evidenceReference],
            requestedOperations ?? ProcessDriverVerificationTestHarness.BusinessAnalysisReadonlyOperations,
            "manager:business-readonly");

        return new BusinessAnalysisVerificationRequest(
            verificationRequest,
            ProcessDriverSuppliedEvidenceContentRules.CreateBusinessAnalysisPayload(
                evidenceReference,
                suppliedPayload),
            items ?? [CreateCompleteBusinessItem(), CreateSupportingBusinessEvidenceItem()],
            DateTimeOffset.Parse("2026-06-08T13:10:00Z"));
    }

    private static ProcessDriverSuppliedEvidenceContent CreateHashMismatchedSuppliedContent(
        ProcessDriverSuppliedEvidenceContent suppliedContent)
    {
        return suppliedContent with
        {
            EvidenceReference = suppliedContent.EvidenceReference with
            {
                ContentHash = ProcessDriverEvidencePolicy.ComputeSha256("Scenario024 tampered supplied evidence reference")
            }
        };
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

    private static OfficeEvidenceItem CreateCompleteOfficeItem()
    {
        return new OfficeEvidenceItem(
            OfficeEvidenceItemKind.EmailMessage,
            "message-1",
            "Customer escalation follow-up",
            "manager@example.invalid",
            ["owner@example.invalid"],
            DateTimeOffset.Parse("2026-06-08T12:00:00Z"),
            "Follow-up message confirms the action item was assigned.");
    }

    private static BusinessAnalysisEvidenceItem CreateCompleteBusinessItem()
    {
        return new BusinessAnalysisEvidenceItem(
            BusinessAnalysisEvidenceItemKind.Deliverable,
            "analysis-1",
            "Customer churn analysis",
            "Requirement: explain churn risk. Deliverable text cites supplied evidence.",
            DateTimeOffset.Parse("2026-06-08T13:00:00Z"));
    }

    private static BusinessAnalysisEvidenceItem CreateSupportingBusinessEvidenceItem()
    {
        return new BusinessAnalysisEvidenceItem(
            BusinessAnalysisEvidenceItemKind.SupportingEvidence,
            "evidence-1",
            "Interview summary",
            "Evidence: supplied customer feedback supports the churn analysis.",
            DateTimeOffset.Parse("2026-06-08T13:05:00Z"));
    }

    private static void AssertNoForbiddenRuntimeSurface(string source)
    {
        var forbiddenTokens = new[]
        {
            "IProcessDriver",
            "ProcessDriverRegistry",
            "ProcessDriverPack",
            "ProcessDriverRuntimeSelector",
            "ProcessDriverManagerCommand",
            "ProcessDriverRuntime",
            "ProcessDriverProvider",
            "ProcessDriverHost",
            "IServiceProvider",
            "GetRequiredService",
            "Assembly.GetTypes",
            "Activator.CreateInstance",
            "dynamic",
            "IServiceCollection",
            "ServiceCollection",
            "Dictionary<ProcessDriverVerificationGatewayLane",
            "Func<ProcessDriverVerificationGatewayLane",
            "AddProcessDriver",
            "MapProcessDriver",
            "System.Diagnostics.Process",
            "Process.Start",
            "HttpClient",
            "File.",
            "Directory.",
            "DbContext"
        };

        foreach (var forbiddenToken in forbiddenTokens)
        {
            Assert.DoesNotContain(forbiddenToken, source, StringComparison.Ordinal);
        }
    }

    private static void AssertReadOnlyList<T>(IReadOnlyList<T> values, T sample)
    {
        Assert.False(values.GetType().IsArray);

        var collection = Assert.IsAssignableFrom<ICollection<T>>(values);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Add(sample));
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

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
