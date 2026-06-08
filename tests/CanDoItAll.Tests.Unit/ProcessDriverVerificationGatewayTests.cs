using System.Runtime.CompilerServices;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.RuntimeEvidence;
using CanDoItAll.Processes.Drivers.TranscriptVerification;
using CanDoItAll.Processes.Drivers.VerificationGateway;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverVerificationGatewayTests
{
    [Fact]
    public void Process_driver_verification_gateway_SB020_INV_001_explicitly_runs_transcript_and_runtime_lanes_only()
    {
        var gateway = ProcessDriverVerificationGateway.CreateDefault();
        var implementedLanes = gateway.ImplementedLanes.Select(descriptor => descriptor.Lane).ToArray();

        Assert.Equal(
            [
                ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification,
                ProcessDriverVerificationGatewayLane.RuntimeEvidenceConsistency
            ],
            implementedLanes);

        var transcriptResult = gateway.VerifyTranscript(CreateTranscriptRequest("Build succeeded."));
        var runtimeResult = gateway.VerifyRuntimeEvidence(CreateRuntimeEvidenceRequest());

        Assert.True(transcriptResult.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, transcriptResult.DenialReason);
        Assert.True(transcriptResult.NoMutationPerformed);
        Assert.Equal(ProcessDriverContractVersion.Current, transcriptResult.ContractVersion);
        Assert.Contains(
            transcriptResult.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        Assert.True(runtimeResult.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, runtimeResult.DenialReason);
        Assert.True(runtimeResult.NoMutationPerformed);
        Assert.Equal(ProcessDriverContractVersion.Current, runtimeResult.ContractVersion);
        Assert.Contains(
            runtimeResult.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
    }

    [Fact]
    public void Process_driver_verification_gateway_SB020_INV_002_source_has_no_dynamic_registry_selector_di_or_manager_surface()
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
        Assert.DoesNotContain("<PackageReference", gatewayProject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VerifyTranscript", source, StringComparison.Ordinal);
        Assert.Contains("VerifyRuntimeEvidence", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Verify(ProcessDriverVerificationGatewayLane", source, StringComparison.Ordinal);
        Assert.DoesNotContain("object ", source, StringComparison.Ordinal);
        AssertNoForbiddenRuntimeSurface(source + gatewayProject);
    }

    [Fact]
    public void Process_driver_verification_gateway_SB021_INV_001_rejects_side_effects_and_keeps_unimplemented_lanes_absent()
    {
        var gateway = ProcessDriverVerificationGateway.CreateDefault();
        var implementedLanes = gateway.ImplementedLanes.Select(descriptor => descriptor.Lane).ToArray();

        Assert.DoesNotContain(ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency, implementedLanes);
        Assert.DoesNotContain(ProcessDriverVerificationGatewayLane.OfficeEvidenceRead, implementedLanes);
        Assert.DoesNotContain(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead, implementedLanes);

        foreach (var operation in ProcessDriverVerificationTestHarness.SideEffectOperations)
        {
            var transcriptResult = gateway.VerifyTranscript(CreateTranscriptRequest(
                "Build succeeded.",
                requestedOperations: [operation]));
            var runtimeResult = gateway.VerifyRuntimeEvidence(CreateRuntimeEvidenceRequest([operation]));

            ProcessDriverVerificationTestHarness.AssertSideEffectDenied(transcriptResult, operation);
            ProcessDriverVerificationTestHarness.AssertSideEffectDenied(runtimeResult, operation);
        }
    }

    [Fact]
    public void Process_driver_verification_gateway_SB027_INV_001_audit_redaction_and_no_mutation_cover_accepted_and_denied_responses()
    {
        var gateway = ProcessDriverVerificationGateway.CreateDefault();
        var acceptedTranscript = gateway.VerifyTranscript(CreateTranscriptRequest("Build succeeded."));
        var deniedTranscript = gateway.VerifyTranscript(CreateTranscriptRequest(
            "Build succeeded. token=fixture-secret reviewer@example.invalid",
            requestedOperations: [ProcessDriverOperation.ExecuteCommand]));
        var acceptedRuntime = gateway.VerifyRuntimeEvidence(CreateRuntimeEvidenceRequest());
        var deniedRuntime = gateway.VerifyRuntimeEvidence(CreateRuntimeEvidenceRequest(
            [ProcessDriverOperation.ExecuteCommand]));

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
        ProcessDriverVerificationTestHarness.AssertRedaction(
            deniedTranscript,
            ProcessDriverRedactionStatus.Redacted,
            ProcessDriverRedactionKind.Secret,
            ProcessDriverRedactionKind.EmailAddress);
        ProcessDriverVerificationTestHarness.AssertDiagnosticsAndAuditDoNotContain(
            deniedTranscript,
            "fixture-secret",
            "reviewer@example.invalid");
    }

    private static void AssertGatewayResponseEnvelope(
        ProcessDriverVerificationResponse response,
        bool accepted,
        ProcessDriverDenialReason denialReason,
        string callerContext,
        ProcessDriverCapabilityScopeKind lane,
        IReadOnlyList<ProcessDriverOperation> operations)
    {
        Assert.Equal(accepted, response.Accepted);
        Assert.Equal(denialReason, response.DenialReason);
        Assert.NotEmpty(response.Diagnostics);
        ProcessDriverVerificationTestHarness.AssertNoMutation(response);
        ProcessDriverVerificationTestHarness.AssertReadonlyAuditFacts(
            response,
            lane == ProcessDriverCapabilityScopeKind.RuntimeFactsRead
                ? ProcessDriverPermissionMode.ManagerReadonly
                : ProcessDriverPermissionMode.VerificationOnly,
            lane);
        ProcessDriverVerificationTestHarness.AssertNormalizedAuditFacts(
            response,
            callerContext,
            lane,
            operations,
            denialReason);
    }

    private static TranscriptVerificationAlphaRequest CreateTranscriptRequest(
        string transcriptText,
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null)
    {
        var transcriptUri = "bundle://proof/SB020/transcripts/dotnet-transcript.txt";
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
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null)
    {
        var evidenceReference = ProcessDriverVerificationTestHarness.CreateEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            "bundle://proof/SB020/transcripts/runtime-evidence.json",
            "runtime evidence gateway",
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
        var suppliedContent = ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
            evidenceReference,
            "runtime evidence gateway");
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
            "IServiceCollection",
            "ServiceCollection",
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
