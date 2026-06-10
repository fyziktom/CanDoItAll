using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core.Execution;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.VerificationGateway;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverContractApiVerificationBoundaryTests
{
    [Fact]
    public void Process_core_public_api_SB007_INV_001_snapshot_matches_owner_classification_and_descriptor_surface()
    {
        var snapshot = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "06-core-public-api-owner-classification.md");

        AssertDocumentedPublicSurface(
            snapshot,
            typeof(ProcessExecutionEvidenceDescriptor).Assembly,
            "CanDoItAll.Processes.Core.",
            expectedCount: 64,
            expectedHash: "99e2a6a6033d749f388a440360e4ef6db5b92c1d1fb2949a9f22d321ccd606d1");
        Assert.Contains("Allowed project reference: `CanDoItAll.Processes.Contracts`", snapshot, StringComparison.Ordinal);
        Assert.Contains("Runtime capability: none", snapshot, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Core.Artifacts", snapshot, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Core.Execution", snapshot, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Core.Finalization", snapshot, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Core.Routing", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_driver_contract_api_SB008_INV_001_versioning_snapshot_matches_runtime_free_surface()
    {
        var snapshot = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "07-driver-abstraction-api-versioning-snapshot.md");
        var publicTypes = typeof(ProcessDriverPermissionMode).Assembly.GetExportedTypes();

        AssertDocumentedPublicSurface(
            snapshot,
            typeof(ProcessDriverPermissionMode).Assembly,
            "CanDoItAll.Processes.Drivers.Abstractions.",
            expectedCount: 34,
            expectedHash: "f92df2a77fbc8800345444c17edca2929f97328f9266dccb54d37bd4dd4781c5");
        Assert.Equal(new ProcessDriverContractVersion(1, 10, 0), ProcessDriverContractVersion.Current);
        Assert.Contains("Contract version: `1.10.0`", snapshot, StringComparison.Ordinal);
        Assert.Contains("Runtime surfaces denied", snapshot, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(publicTypes, type => type.IsInterface);
        Assert.DoesNotContain(publicTypes, type => type.Name.Contains("Host", StringComparison.Ordinal));
        Assert.DoesNotContain(publicTypes, type => type.Name.Contains("Provider", StringComparison.Ordinal));
        Assert.DoesNotContain(publicTypes, type => type.Name.Contains("Selector", StringComparison.Ordinal));
        Assert.DoesNotContain(publicTypes, type => type.Name.Contains("Registry", StringComparison.Ordinal));
    }

    [Fact]
    public void Process_driver_contract_api_SB006_INV_001_contract_project_is_solution_bound_dependency_clean_and_runtime_free()
    {
        var root = FindRepositoryRoot();
        var solution = ReadRepositoryFile("CanDoItAll.slnx");
        var project = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.Abstractions",
            "CanDoItAll.Processes.Drivers.Abstractions.csproj");
        var contractSource = ReadContractSource(root);
        var publicTypeNames = ReadContractPublicTypeNames();

        Assert.Contains(
            "src/CanDoItAll.Processes.Drivers.Abstractions/CanDoItAll.Processes.Drivers.Abstractions.csproj",
            solution,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<ProjectReference", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanDoItAll.Modules.", contractSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Infrastructure", contractSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework", contractSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", contractSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", contractSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IStorage", contractSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IWorkspace", contractSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkspacePathResolver", contractSource, StringComparison.Ordinal);
        AssertNoForbiddenProductionDriverRuntimeTokens(contractSource);

        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Permissions.ProcessDriverPermissionMode", publicTypeNames);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Permissions.ProcessDriverCapabilityScope", publicTypeNames);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Permissions.ProcessDriverCapabilityScopeRules", publicTypeNames);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Permissions.ProcessDriverOperationRules", publicTypeNames);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Gateway.ProcessDriverVerificationGatewayLane", publicTypeNames);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Gateway.ProcessDriverVerificationGatewayLaneDescriptor", publicTypeNames);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Gateway.ProcessDriverVerificationGatewayLaneRules", publicTypeNames);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Evidence.ProcessDriverSuppliedEvidenceContent", publicTypeNames);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Evidence.ProcessDriverSuppliedEvidenceContentKind", publicTypeNames);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Evidence.ProcessDriverSuppliedEvidenceContentRules", publicTypeNames);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Evidence.ProcessDriverEvidenceReference", publicTypeNames);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Audit.ProcessDriverAuditFact", publicTypeNames);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Verification.ProcessDriverVerificationRequest", publicTypeNames);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.Verification.ProcessDriverVerificationResponse", publicTypeNames);
    }

    [Fact]
    public void Process_driver_contract_api_SB009_INV_001_permission_modes_scopes_and_denials_represent_readonly_semantics()
    {
        var readonlyScopes = CreateReadonlyScopes();
        var sideEffectOperations = CreateSideEffectOperations();

        Assert.Equal(0, (int)ProcessDriverPermissionMode.Unspecified);
        Assert.All(readonlyScopes, scope =>
        {
            Assert.False(scope.AllowsProcessMutation);
            Assert.False(scope.AllowsExternalCalls);
            Assert.False(scope.AllowsWorkspaceWrites);
            Assert.False(scope.AllowsStorageWrites);
        });
        Assert.Contains(ProcessDriverOperation.ExecuteCommand, sideEffectOperations);
        Assert.Contains(ProcessDriverOperation.WriteWorkspaceStorage, sideEffectOperations);
        Assert.Contains(ProcessDriverOperation.CallOfficeGraph, sideEffectOperations);
        Assert.Contains(ProcessDriverOperation.MutateBusinessRecord, sideEffectOperations);
        Assert.Contains(ProcessDriverDiagnosticCategory.BusinessRequirementMissing, Enum.GetValues<ProcessDriverDiagnosticCategory>());
        Assert.Contains(ProcessDriverDiagnosticCategory.BusinessUnsupportedAssumption, Enum.GetValues<ProcessDriverDiagnosticCategory>());
        Assert.Contains(ProcessDriverDiagnosticCategory.BusinessContradictionMarker, Enum.GetValues<ProcessDriverDiagnosticCategory>());
        Assert.Contains(ProcessDriverDiagnosticCategory.BusinessEvidenceGap, Enum.GetValues<ProcessDriverDiagnosticCategory>());
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactLineageMissing, Enum.GetValues<ProcessDriverDiagnosticCategory>());
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactTrustSensitivityMismatch, Enum.GetValues<ProcessDriverDiagnosticCategory>());
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactSatisfactionInconsistent, Enum.GetValues<ProcessDriverDiagnosticCategory>());

        var denied = sideEffectOperations
            .Select(operation => new ProcessDriverDeniedOperation(
                operation,
                ResolveExpectedDenial(operation),
                ProcessDriverPermissionMode.VerificationOnly,
                readonlyScopes[0]))
            .ToArray();

        Assert.All(denied, denial =>
        {
            Assert.NotEqual(ProcessDriverDenialReason.None, denial.Reason);
            Assert.Equal(ProcessDriverPermissionMode.VerificationOnly, denial.RequestedMode);
            Assert.False(denial.Scope.AllowsProcessMutation);
        });
    }

    [Fact]
    public void Process_driver_contract_api_SB012_INV_001_audit_facts_redaction_and_evidence_references_are_immutable_contracts()
    {
        var redaction = new ProcessDriverRedactionDescriptor(
            ProcessDriverRedactionStatus.Redacted,
            [ProcessDriverRedactionKind.Secret, ProcessDriverRedactionKind.EmailAddress],
            "BEE3701B1528648B7D54A6B29311D8D822F32F87F20D5D4C5A26C8417E109B0F");
        var scope = CreateReadonlyScopes()[0];
        var evidenceReference = new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CommandTranscript,
            "bundle://proof/SB006/transcripts/passing-focused-tests.txt",
            "D6FCF6DB6C7C547B70C972A70902DA6203B08F4EF34690CD8E34C41858F3F7D5",
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
        var fact = new ProcessDriverAuditFact(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DateTimeOffset.Parse("2026-06-07T20:30:00Z"),
            ProcessDriverAuditFactKind.OperationDenied,
            "manager:readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            scope,
            scope.Kind,
            ProcessDriverOperation.ExecuteCommand,
            [evidenceReference],
            ProcessDriverDenialReason.UnsafeCommand,
            redaction,
            "Command execution denied for verification-only contract.",
            "BEE3701B1528648B7D54A6B29311D8D822F32F87F20D5D4C5A26C8417E109B0F");

        Assert.Equal(ProcessDriverAuditFactKind.OperationDenied, fact.Kind);
        Assert.Equal(ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification, fact.Lane);
        Assert.Equal(ProcessDriverOperation.ExecuteCommand, fact.RequestedOperation);
        Assert.Equal(ProcessDriverRedactionStatus.Redacted, fact.Redaction.Status);
        Assert.Contains(ProcessDriverRedactionKind.Secret, fact.Redaction.AppliedKinds);
        Assert.Contains(ProcessDriverRedactionKind.EmailAddress, fact.Redaction.AppliedKinds);
        Assert.Equal(ProcessDriverDenialReason.UnsafeCommand, fact.DenialReason);
        Assert.Contains(evidenceReference, fact.EvidenceReferences);
        Assert.Equal(ProcessDriverEvidenceReferenceKind.CommandTranscript, evidenceReference.Kind);
        Assert.Equal(ProcessDriverCoreDescriptorFamily.ExecutionEvidence, evidenceReference.CoreDescriptorFamily);
    }

    [Fact]
    public void Process_driver_contract_api_SB015_INV_001_verification_request_response_cannot_claim_state_mutation()
    {
        var evidenceReference = new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.BundleProofArtifact,
            "bundle://proof/SB015/transcripts/passing-focused-tests.txt",
            "E5E7EE6B9E72E8D63B5B66156D58D02DBF7F1496B80B73946D135D7FCB2D5C24",
            null);
        var request = new ProcessDriverVerificationRequest(
            ProcessDriverPermissionMode.VerificationOnly,
            CreateReadonlyScopes()[0],
            [evidenceReference],
            [ProcessDriverOperation.InspectExistingEvidence, ProcessDriverOperation.ReturnDiagnostics],
            "manager:readonly",
            ProcessDriverContractVersion.Current);
        var response = new ProcessDriverVerificationResponse(
            Accepted: true,
            DenialReason: ProcessDriverDenialReason.None,
            Diagnostics: [],
            EvidenceReferences: [evidenceReference],
            Redaction: new ProcessDriverRedactionDescriptor(ProcessDriverRedactionStatus.None, [], evidenceReference.ContentHash),
            NoMutationPerformed: true,
            AuditFacts: [],
            ContractVersion: ProcessDriverContractVersion.Current);

        Assert.All(request.RequestedOperations, operation =>
            Assert.DoesNotContain(operation, CreateSideEffectOperations()));
        Assert.False(request.Scope.AllowsProcessMutation);
        Assert.False(request.Scope.AllowsExternalCalls);
        Assert.True(response.NoMutationPerformed);
        Assert.Equal(ProcessDriverDenialReason.None, response.DenialReason);
    }

    [Fact]
    public void Process_driver_contract_api_SB021_INV_001_dotnet_rust_transcript_rehearsal_is_reference_only()
    {
        var root = FindRepositoryRoot();
        var dotnetFixture = ReadRepositoryFile(
            "tests",
            "CanDoItAll.Tests.Unit",
            "TestData",
            "ProcessDriverContractTranscripts",
            "dotnet-warning-transcript.txt");
        var rustFixture = ReadRepositoryFile(
            "tests",
            "CanDoItAll.Tests.Unit",
            "TestData",
            "ProcessDriverContractTranscripts",
            "rust-test-failure-transcript.txt");
        var dotnetTranscript = new ProcessDriverTranscriptReference(
            "repo://tests/CanDoItAll.Tests.Unit/TestData/ProcessDriverContractTranscripts/dotnet-warning-transcript.txt",
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            ProcessDriverTranscriptLanguage.DotNet,
            "dotnet",
            "net10.0");
        var rustTranscript = new ProcessDriverTranscriptReference(
            "repo://tests/CanDoItAll.Tests.Unit/TestData/ProcessDriverContractTranscripts/rust-test-failure-transcript.txt",
            "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
            ProcessDriverTranscriptLanguage.Rust,
            "cargo",
            "rust-stable");
        var categories = ClassifyTranscript(dotnetFixture)
            .Concat(ClassifyTranscript(rustFixture))
            .ToHashSet();

        Assert.True(File.Exists(Path.Combine(
            root,
            "tests",
            "CanDoItAll.Tests.Unit",
            "TestData",
            "ProcessDriverContractTranscripts",
            "dotnet-warning-transcript.txt")));
        Assert.Equal(ProcessDriverTranscriptLanguage.DotNet, dotnetTranscript.Language);
        Assert.Equal(ProcessDriverTranscriptLanguage.Rust, rustTranscript.Language);
        Assert.Contains(ProcessDriverDiagnosticCategory.BuildWarning, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.TestFailure, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.MissingArtifact, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.UnsupportedTargetFramework, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.RuntimeProofGap, categories);
    }

    [Fact]
    public void Process_driver_contract_api_SB024_INV_001_driver_evidence_vocabulary_maps_core_descriptor_families_without_reverse_dependency()
    {
        var root = FindRepositoryRoot();
        var coreProject = ReadRepositoryFile("src", "CanDoItAll.Processes.Core", "CanDoItAll.Processes.Core.csproj");
        var contractProject = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.Abstractions",
            "CanDoItAll.Processes.Drivers.Abstractions.csproj");
        var families = Enum.GetValues<ProcessDriverCoreDescriptorFamily>();

        Assert.Contains(ProcessDriverCoreDescriptorFamily.ExecutionEvidence, families);
        Assert.Contains(ProcessDriverCoreDescriptorFamily.FinalizerEvidence, families);
        Assert.Contains(ProcessDriverCoreDescriptorFamily.RetryDiagnostics, families);
        Assert.Contains(ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence, families);
        Assert.Contains(ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation, families);
        Assert.DoesNotContain("CanDoItAll.Processes.Drivers.Abstractions", coreProject, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", contractProject, StringComparison.Ordinal);
        Assert.Contains("ProcessExecutionEvidenceDescriptors.cs", ReadFileNames(root, "src", "CanDoItAll.Processes.Core", "Execution"));
        Assert.Contains("ProcessFinalizerEvidenceDescriptors.cs", ReadFileNames(root, "src", "CanDoItAll.Processes.Core", "Finalization"));
        Assert.Contains("ProcessRetryDiagnosticDescriptors.cs", ReadFileNames(root, "src", "CanDoItAll.Processes.Core", "Diagnostics"));
        Assert.Contains("ProcessArtifactProjectionEvidenceDescriptors.cs", ReadFileNames(root, "src", "CanDoItAll.Processes.Core", "Artifacts"));
    }

    [Fact]
    public void Process_driver_contract_api_SB040_INV_001_core_descriptor_family_ordinals_are_backward_compatible_and_gateway_allow_list_is_explicit()
    {
        var expectedFamilies = new Dictionary<ProcessDriverCoreDescriptorFamily, int>
        {
            [ProcessDriverCoreDescriptorFamily.ExecutionEvidence] = 1,
            [ProcessDriverCoreDescriptorFamily.FinalizerEvidence] = 2,
            [ProcessDriverCoreDescriptorFamily.RetryDiagnostics] = 3,
            [ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence] = 4,
            [ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation] = 5
        };
        var descriptors = ProcessDriverVerificationGatewayLaneRules.AllowedLanes.ToDictionary(
            descriptor => descriptor.Lane);
        var gatewayCoreFamilies = descriptors.Values
            .Select(descriptor => descriptor.CoreDescriptorFamily)
            .OfType<ProcessDriverCoreDescriptorFamily>()
            .ToArray();

        Assert.Equal(expectedFamilies.Count, Enum.GetValues<ProcessDriverCoreDescriptorFamily>().Length);
        foreach (var expectedFamily in expectedFamilies)
        {
            Assert.Equal(expectedFamily.Value, (int)expectedFamily.Key);
        }

        Assert.Equal(ProcessDriverCoreDescriptorFamily.ExecutionEvidence, descriptors[ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification].CoreDescriptorFamily);
        Assert.Equal(ProcessDriverCoreDescriptorFamily.ExecutionEvidence, descriptors[ProcessDriverVerificationGatewayLane.RuntimeEvidenceConsistency].CoreDescriptorFamily);
        Assert.Equal(ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence, descriptors[ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency].CoreDescriptorFamily);
        Assert.Null(descriptors[ProcessDriverVerificationGatewayLane.OfficeEvidenceRead].CoreDescriptorFamily);
        Assert.Null(descriptors[ProcessDriverVerificationGatewayLane.BusinessAnalysisRead].CoreDescriptorFamily);
        Assert.DoesNotContain(ProcessDriverCoreDescriptorFamily.FinalizerEvidence, gatewayCoreFamilies);
        Assert.DoesNotContain(ProcessDriverCoreDescriptorFamily.RetryDiagnostics, gatewayCoreFamilies);
        Assert.All(descriptors.Values, descriptor =>
            Assert.All(descriptor.AllowedOperations, operation =>
                Assert.True(ProcessDriverOperationRules.IsReadonlyVerificationOperation(operation))));
    }

    [Fact]
    public void Process_driver_contract_api_SB011_INV_001_runtime_host_contract_snapshot_stays_out_of_process_core()
    {
        var coreProject = ReadRepositoryFile("src", "CanDoItAll.Processes.Core", "CanDoItAll.Processes.Core.csproj");
        var coreSource = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(
                    Path.Combine(FindRepositoryRoot(), "src", "CanDoItAll.Processes.Core"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        var publicRuntimeContract = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Contracts",
            "Runtime",
            "ProcessRuntimeHostContractModels.cs");

        Assert.DoesNotContain("CanDoItAll.Processes.Drivers", coreProject, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRuntimeHostContract", coreSource, StringComparison.Ordinal);
        Assert.Contains("VerificationHost = 1", publicRuntimeContract, StringComparison.Ordinal);
        Assert.Contains("DryRunExecution = 2", publicRuntimeContract, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", publicRuntimeContract, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Drivers", publicRuntimeContract, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_driver_contract_api_SB002_INV_002_runtime_host_contract_reports_readonly_safety_violations()
    {
        var safe = ProcessRuntimeHostContractSnapshot.Create(ProcessRuntimeHostContractSurface.DryRunExecution);

        Assert.True(safe.IsReadOnlySafe);
        Assert.Empty(safe.ValidateReadOnlySafety());
        Assert.Equal(ProcessRuntimeHostContractVersion.Current, safe.Version);
        Assert.Equal(new ProcessRuntimeHostContractVersion(1, 2, 0), safe.Version);

        var unsafeSnapshot = safe with
        {
            DryRunOnly = false,
            NoMutationPerformed = false,
            AllowsProcessMutation = true,
            AllowsTransitionMutation = true,
            AllowsFinalizerMutation = true,
            SandboxDecision = new ProcessRuntimeHostSandboxDecision(
                ProcessRuntimeHostSandboxDecisionKind.FutureExecutionPrerequisitesSatisfied,
                executionAllowed: true,
                dryRunOnly: false,
                [ProcessRuntimeHostEffectSurface.LocalCommand],
                [],
                [])
        };
        var violations = unsafeSnapshot
            .ValidateReadOnlySafety()
            .Select(item => item.Kind)
            .ToHashSet();

        Assert.False(unsafeSnapshot.IsReadOnlySafe);
        Assert.Contains(ProcessRuntimeHostContractViolationKind.ProductionExecutionAllowed, violations);
        Assert.Contains(ProcessRuntimeHostContractViolationKind.MutationNotRecordedAsDenied, violations);
        Assert.Contains(ProcessRuntimeHostContractViolationKind.ProcessMutationAllowed, violations);
        Assert.Contains(ProcessRuntimeHostContractViolationKind.TransitionMutationAllowed, violations);
        Assert.Contains(ProcessRuntimeHostContractViolationKind.FinalizerMutationAllowed, violations);
        Assert.Contains(ProcessRuntimeHostContractViolationKind.SandboxExecutionAllowed, violations);
    }

    [Fact]
    public void Process_driver_contract_api_SB002_INV_003_runtime_host_contract_carries_generic_identity_decision_audit_and_capability_refs()
    {
        var requestedAt = new DateTimeOffset(2026, 6, 10, 18, 20, 0, TimeSpan.Zero);
        var identity = new ProcessRuntimeHostRequestIdentity(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            " operator@example.test ",
            requestedAt);
        var denial = new ProcessRuntimeHostDenial(
            ProcessRuntimeHostDenialCategory.SideEffect,
            "side-effect-denied",
            "Execution-capable side effects are denied by the current runtime-host gate.",
            [
                ProcessRuntimeHostEffectSurface.LocalCommand,
                ProcessRuntimeHostEffectSurface.WorkspaceStorage,
                ProcessRuntimeHostEffectSurface.LocalCommand
            ]);
        var decision = new ProcessRuntimeHostSandboxDecision(
            ProcessRuntimeHostSandboxDecisionKind.Denied,
            executionAllowed: false,
            dryRunOnly: true,
            [
                ProcessRuntimeHostEffectSurface.LocalCommand,
                ProcessRuntimeHostEffectSurface.WorkspaceStorage
            ],
            [
                ProcessRuntimeHostEffectSurface.LocalCommand,
                ProcessRuntimeHostEffectSurface.WorkspaceStorage
            ],
            [denial]);
        var audit = new ProcessRuntimeHostAuditReference(
            "runtime-host-audit-001",
            "bc6ac35cf00bd75cdcf5485b89caa95e88ac0e9d30e4eac017e2fa9b369a9397",
            requestedAt);
        var capability = new ProcessRuntimeHostCapabilityDescriptorReference(
            "dry-run:execution-capable-future-gate",
            ProcessRuntimeHostContractSurface.DryRunExecution,
            ProcessRuntimeHostOperationCategory.DryRunPlanning);

        var snapshot = ProcessRuntimeHostContractSnapshot.Create(ProcessRuntimeHostContractSurface.DryRunExecution) with
        {
            RequestIdentity = identity,
            SandboxDecision = decision,
            AuditReference = audit,
            CapabilityDescriptor = capability
        };

        Assert.True(snapshot.IsReadOnlySafe);
        Assert.Empty(snapshot.ValidateReadOnlySafety());
        Assert.Equal("operator@example.test", snapshot.RequestIdentity?.RequestedBy);
        Assert.Equal(2, denial.Surfaces.Count);
        Assert.Equal(ProcessRuntimeHostSandboxDecisionKind.Denied, snapshot.SandboxDecision?.Kind);
        Assert.False(snapshot.SandboxDecision?.ExecutionAllowed);
        Assert.True(snapshot.SandboxDecision?.DryRunOnly);
        Assert.Equal("runtime-host-audit-001", snapshot.AuditReference?.AuditId);
        Assert.Equal(ProcessRuntimeHostOperationCategory.DryRunPlanning, snapshot.CapabilityDescriptor?.OperationCategory);
    }

    [Fact]
    public void Process_driver_contract_api_SB002_INV_004_runtime_host_contract_rejects_invalid_identity_capability_and_domain_leakage()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Contracts",
            "Runtime",
            "ProcessRuntimeHostContractModels.cs");

        Assert.Throws<ArgumentException>(() => new ProcessRuntimeHostRequestIdentity(
            Guid.Empty,
            Guid.NewGuid(),
            null,
            "operator",
            DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new ProcessRuntimeHostCapabilityDescriptorReference(
            " ",
            ProcessRuntimeHostContractSurface.DryRunExecution,
            ProcessRuntimeHostOperationCategory.DryRunPlanning));
        Assert.DoesNotContain("Office", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenAI", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DotNet", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Process_driver_contract_api_SB007_INV_002_verification_gateway_static_descriptors_are_readonly_and_complete()
    {
        var descriptors = ProcessDriverVerificationGatewayLaneRules.AllowedLanes;

        Assert.Equal(5, descriptors.Count);
        Assert.All(descriptors, descriptor =>
        {
            Assert.All(descriptor.AllowedOperations, operation =>
                Assert.False(ProcessDriverOperationRules.IsSideEffectOperation(operation)));
            Assert.Equal(descriptor, ProcessDriverVerificationGatewayLaneRules.Describe(descriptor.Lane));
        });

        Assert.All(ProcessDriverOperationRules.SideEffectOperations, operation =>
            Assert.False(ProcessDriverOperationRules.IsReadonlyVerificationOperation(operation)));
    }

    [Fact]
    public void Process_driver_contract_api_SB040_INV_002_contract_version_history_documents_every_public_descriptor_family_change()
    {
        var snapshot = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "07-driver-abstraction-api-versioning-snapshot.md");
        var version = ProcessDriverContractVersion.Current;
        var expectedVersionHistory = new[]
        {
            ("SB025", "1.5.0"),
            ("SB028", "1.6.0"),
            ("SB031", "1.7.0"),
            ("SB032", "1.8.0"),
            ("SB034", "1.9.0"),
            ("SB035", "1.10.0")
        };

        Assert.Equal(new ProcessDriverContractVersion(1, 10, 0), version);
        Assert.Equal(1, version.Major);
        Assert.Equal(10, version.Minor);
        Assert.Equal(0, version.Patch);
        Assert.Contains("Contract version: `1.10.0`", snapshot, StringComparison.Ordinal);
        Assert.Contains("Version source: `ProcessDriverContractVersion.Current => 1.10.0`", snapshot, StringComparison.Ordinal);
        foreach (var (subbundle, expectedVersion) in expectedVersionHistory)
        {
            Assert.Contains($"## {subbundle}", snapshot, StringComparison.Ordinal);
            Assert.Contains($"ProcessDriverContractVersion.Current` is `{expectedVersion}`", snapshot, StringComparison.Ordinal);
        }

        Assert.Contains("## SB040 API Compatibility Contract Note", snapshot, StringComparison.Ordinal);
        Assert.Contains("ExecutionEvidence = 1", snapshot, StringComparison.Ordinal);
        Assert.Contains("FinalizerEvidence = 2", snapshot, StringComparison.Ordinal);
        Assert.Contains("RetryDiagnostics = 3", snapshot, StringComparison.Ordinal);
        Assert.Contains("ArtifactProjectionEvidence = 4", snapshot, StringComparison.Ordinal);
        Assert.Contains("ArtifactProjectionValidation = 5", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverContractVersion.Current => 1.11.0", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_driver_contract_api_SB041_INV_001_v1_migration_docs_match_current_contract_and_alpha_verifier_behavior()
    {
        var migrationDoc = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "09-v1-contract-migration-compatibility.md");
        var version = ProcessDriverContractVersion.Current;

        Assert.Equal(new ProcessDriverContractVersion(1, 10, 0), version);
        Assert.Contains("Current driver contract version: `1.10.0`", migrationDoc, StringComparison.Ordinal);
        Assert.Contains("Compatibility line: `v1.x verification-only alpha`", migrationDoc, StringComparison.Ordinal);
        Assert.Contains("Major compatibility rule: v1 consumers must reject any contract with `Major != 1`", migrationDoc, StringComparison.Ordinal);
        Assert.Contains("ExecutionCapableFuture` remains a denied future marker", migrationDoc, StringComparison.Ordinal);
        Assert.Contains("Core descriptor family ordinals are compatibility-significant", migrationDoc, StringComparison.Ordinal);

        foreach (var requiredText in new[]
        {
            "CanDoItAll.Processes.Drivers.TranscriptVerification",
            "CanDoItAll.Processes.Drivers.RuntimeEvidence",
            "CanDoItAll.Processes.Drivers.OfficeEvidence",
            "CanDoItAll.Processes.Drivers.BusinessAnalysis",
            "CanDoItAll.Processes.Drivers.ArtifactEvidence",
            "CanDoItAll.Processes.Drivers.ObservationAggregation",
            "`TranscriptText` bound to `CommandTranscript`",
            "`CoreDescriptorPayload` bound to `CoreDescriptor`",
            "`OfficeEvidencePayload` bound to `OfficeReadonlyArtifact`",
            "`BusinessAnalysisPayload` bound to `BusinessReadonlyArtifact`",
            "`ProcessDriverVerificationResponse` envelopes only",
            "Does not invoke verifiers, discover drivers, register DI services, persist observations, schedule work, trigger commands, or mutate state."
        })
        {
            Assert.Contains(requiredText, migrationDoc, StringComparison.Ordinal);
        }

        foreach (var deniedRuntimeClaim in new[]
        {
            "runtime host approval: granted",
            "runtime host is approved",
            "DI registration is approved",
            "scheduler is approved",
            "manager command is approved",
            "workspace write allowed",
            "storage write allowed"
        })
        {
            Assert.DoesNotContain(deniedRuntimeClaim, migrationDoc, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Process_driver_contract_api_SB046_INV_001_runtime_host_approval_matrix_keeps_runtime_surfaces_unapproved()
    {
        var matrix = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "10-runtime-host-approval-matrix.md");
        var migrationDoc = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "09-v1-contract-migration-compatibility.md");
        var contractSource = ReadContractSource(FindRepositoryRoot());

        Assert.Contains("Current decision: all runtime-host surfaces are `Not approved`", matrix, StringComparison.Ordinal);
        Assert.Contains("Contract line: `v1.x verification-only alpha`", matrix, StringComparison.Ordinal);
        Assert.Contains("architecture/10-runtime-host-approval-matrix.md", migrationDoc, StringComparison.Ordinal);
        Assert.Contains("ExecutionCapableFuture` remains a denied marker", matrix, StringComparison.Ordinal);
        Assert.Contains("Future Approval Gates", matrix, StringComparison.Ordinal);
        Assert.Contains("lifecycle ownership", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Audit persistence", matrix, StringComparison.Ordinal);
        Assert.Contains("Sandbox and allow-list policy", matrix, StringComparison.Ordinal);
        Assert.Contains("Approval and authorization", matrix, StringComparison.Ordinal);
        Assert.Contains("Compatibility review", matrix, StringComparison.Ordinal);
        Assert.Contains("Red-team proof", matrix, StringComparison.Ordinal);

        foreach (var surface in new[]
        {
            "Runtime host",
            "Driver registry",
            "Runtime selector",
            "DI registration",
            "Manager command",
            "Scheduler hook",
            "Workflow hook",
            "Execution-capable drivers"
        })
        {
            Assert.Contains($"| {surface} | `Not approved` |", matrix, StringComparison.Ordinal);
        }

        foreach (var deniedApprovalClaim in new[]
        {
            "Current decision: approved",
            "runtime host is approved",
            "registry is approved",
            "selector is approved",
            "DI registration is approved",
            "manager command is approved",
            "scheduler hook is approved",
            "workflow hook is approved",
            "execution-capable drivers are approved"
        })
        {
            Assert.DoesNotContain(deniedApprovalClaim, matrix, StringComparison.OrdinalIgnoreCase);
        }

        AssertNoForbiddenProductionDriverRuntimeTokens(contractSource);
    }

    [Fact]
    public void Process_driver_contract_api_SB047_INV_001_future_runtime_prerequisites_are_exact_and_unsatisfied()
    {
        var prerequisites = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "11-future-production-runtime-prerequisites.md");
        var matrix = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "10-runtime-host-approval-matrix.md");

        Assert.Contains("Runtime host status: `Not approved`", prerequisites, StringComparison.Ordinal);
        Assert.Contains("Prerequisite status: every prerequisite in this document is `Not satisfied`", prerequisites, StringComparison.Ordinal);
        Assert.Contains("architecture/11-future-production-runtime-prerequisites.md", matrix, StringComparison.Ordinal);

        foreach (var prerequisite in new[]
        {
            "Audit persistence",
            "Sandbox boundary",
            "Command and external-call allow-list",
            "Lifecycle ownership",
            "Approval and authorization",
            "Compatibility governance"
        })
        {
            Assert.Contains($"| {prerequisite} | `Not satisfied` |", prerequisites, StringComparison.Ordinal);
        }

        foreach (var requiredEvidence in new[]
        {
            "Request id, caller context, lane, permission mode, capability scope, requested operation, denial reason, and timestamp.",
            "Process isolation model and resource limits.",
            "Tests proving unknown commands, unknown connectors, unknown paths, unknown lanes, and unknown operations fail predictably.",
            "Owning module and package boundary.",
            "How approval is recorded, revoked, expired, and audited.",
            "`ProcessDriverContractVersion.Current`.",
            "Driver abstraction public API snapshot and surface hash.",
            "Red-team tests rejecting report-only approval"
        })
        {
            Assert.Contains(requiredEvidence, prerequisites, StringComparison.Ordinal);
        }

        foreach (var forbiddenApprovalClaim in new[]
        {
            "Prerequisite status: satisfied",
            "Runtime host status: approved",
            "runtime host may run now",
            "execution-capable driver is approved",
            "workspace write is approved",
            "storage write is approved"
        })
        {
            Assert.DoesNotContain(forbiddenApprovalClaim, prerequisites, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Process_driver_contract_api_SB057_INV_001_roadmaps_deny_runtime_host_and_list_approval_gates()
    {
        var coreRoadmap = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "12-stable-process-core-roadmap.md");
        var domainRoadmap = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "13-domain-driver-roadmap.md");

        Assert.Contains("Runtime host status: `Not approved`", coreRoadmap, StringComparison.Ordinal);
        Assert.Contains("Runtime host status: `Not approved`", domainRoadmap, StringComparison.Ordinal);
        Assert.Contains("Execution-capable driver status: `Not approved`", domainRoadmap, StringComparison.Ordinal);
        Assert.Contains("remaining runtime side effects stay outside Process Core", coreRoadmap, StringComparison.Ordinal);
        Assert.Contains("continue read-only domain drivers and adapters", domainRoadmap, StringComparison.Ordinal);
        Assert.Contains("prerequisites in `architecture/11-future-production-runtime-prerequisites.md` remain `Not satisfied`", domainRoadmap, StringComparison.Ordinal);

        foreach (var approvalGate in new[]
        {
            "Runtime lifecycle ownership",
            "Audit persistence",
            "Sandbox boundary",
            "allow-list",
            "Approval and authorization",
            "Compatibility governance",
            "Red-team proof"
        })
        {
            Assert.Contains(approvalGate, domainRoadmap, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var surface in new[]
        {
            "Verification host registration",
            "Driver registry/selector",
            "DI registration/startup hook",
            "Manager command",
            "Scheduler/workflow hook",
            "Workspace/storage writes",
            "File/network/connector calls",
            "Finalizer/transition/claim mutation",
            "Provider repair/retry execution"
        })
        {
            Assert.Contains(surface, coreRoadmap, StringComparison.Ordinal);
        }

        var combinedRoadmaps = string.Concat(coreRoadmap, Environment.NewLine, domainRoadmap);
        foreach (var forbiddenApprovalClaim in new[]
        {
            "Runtime host status: `Approved`",
            "Execution-capable driver status: `Approved`",
            "Default next bundle: production verification host registration",
            "ExecutionCapableFuture is permission",
            "runtime host may run now",
            "driver registry is approved",
            "DI registration is approved",
            "manager command is approved",
            "scheduler hook is approved",
            "workflow hook is approved"
        })
        {
            Assert.DoesNotContain(forbiddenApprovalClaim, combinedRoadmaps, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Process_driver_contract_api_SB058_INV_001_next_bundle_keeps_production_host_registration_not_ready()
    {
        var decision = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "14-next-bundle-runtime-host-decision.md");
        var approvalMatrix = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "10-runtime-host-approval-matrix.md");
        var prerequisites = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "11-future-production-runtime-prerequisites.md");

        Assert.Contains("Production verification host registration decision: `Not ready`", decision, StringComparison.Ordinal);
        Assert.Contains("Next bundle path: `Continue read-only adapters and projection planning`", decision, StringComparison.Ordinal);
        Assert.Contains("Runtime host status: `Not approved`", decision, StringComparison.Ordinal);
        Assert.Contains("Prerequisite status: `Not satisfied`", decision, StringComparison.Ordinal);
        Assert.Contains("Current decision: all runtime-host surfaces are `Not approved`.", approvalMatrix, StringComparison.Ordinal);
        Assert.Contains("Prerequisite status: every prerequisite in this document is `Not satisfied`.", prerequisites, StringComparison.Ordinal);

        foreach (var prerequisite in new[]
        {
            "Runtime lifecycle ownership",
            "Audit persistence",
            "Sandbox boundary",
            "Command and external-call allow-list",
            "Approval and authorization",
            "Compatibility governance",
            "Red-team proof"
        })
        {
            Assert.Contains(prerequisite, decision, StringComparison.Ordinal);
        }

        foreach (var deniedSurface in new[]
        {
            "Production verification host registration",
            "Generic runtime host",
            "Driver registry or runtime selector",
            "DI registration or startup hook",
            "Manager command, scheduler hook, or workflow hook that invokes drivers",
            "Workspace writes, storage writes, file/network/connector calls",
            "Execution-capable driver contract line"
        })
        {
            Assert.Contains(deniedSurface, decision, StringComparison.Ordinal);
        }

        foreach (var forbiddenDecision in new[]
        {
            "Production verification host registration decision: `Ready`",
            "Next bundle path: `Production verification host registration`",
            "Runtime host status: `Approved`",
            "Prerequisite status: `Satisfied`",
            "register drivers in DI for the next bundle",
            "manager command may invoke drivers",
            "scheduler hook may invoke drivers",
            "ExecutionCapableFuture is permission"
        })
        {
            Assert.DoesNotContain(forbiddenDecision, decision, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Process_driver_contract_api_SB059_INV_001_backlog_candidates_keep_runtime_host_and_execution_blocked()
    {
        var backlog = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "15-next-backlog-candidates-and-reopen-triggers.md");
        var decision = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "14-next-bundle-runtime-host-decision.md");

        Assert.Contains("Backlog decision: `Continue read-only path`", backlog, StringComparison.Ordinal);
        Assert.Contains("Runtime host registration candidate: `Blocked`", backlog, StringComparison.Ordinal);
        Assert.Contains("Execution-capable driver candidate: `Blocked`", backlog, StringComparison.Ordinal);
        Assert.Contains("Production verification host registration decision: `Not ready`", decision, StringComparison.Ordinal);

        foreach (var readyCandidate in new[]
        {
            "Manager-visible read-only verification projection planning",
            "Read-only adapter hardening",
            "Compatibility and descriptor guard hardening"
        })
        {
            Assert.Contains(readyCandidate, backlog, StringComparison.Ordinal);
        }

        foreach (var blockedCandidate in new[]
        {
            "Runtime-host approval pre-bundle",
            "Production verification host registration",
            "Execution-capable driver contract line"
        })
        {
            Assert.Contains(blockedCandidate, backlog, StringComparison.Ordinal);
        }

        foreach (var reopenTrigger in new[]
        {
            "invoking drivers",
            "registering services",
            "persisting runtime-host state",
            "writing workspace/storage",
            "mutating processes",
            "public API snapshots",
            "supplied-content hash binding",
            "UI/media files change"
        })
        {
            Assert.Contains(reopenTrigger, backlog, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var forbiddenBacklogClaim in new[]
        {
            "Runtime host registration candidate: `Ready`",
            "Execution-capable driver candidate: `Ready`",
            "production verification host registration is ready",
            "execution-capable drivers are ready",
            "manager-visible projection may invoke drivers",
            "read-only adapter hardening may write workspace",
            "runtime-host approval pre-bundle may skip audit persistence"
        })
        {
            Assert.DoesNotContain(forbiddenBacklogClaim, backlog, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Process_driver_contract_api_SB058_SB059_INV_002_process_docs_describe_operator_readback_without_runtime_approval()
    {
        var moduleReadme = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "README.md");
        var operatorRunbook = ReadRepositoryFile(
            "docs",
            "process-agent-operator-runbook.md");
        var restorationLedger = ReadRepositoryFile(
            "docs",
            "process-runtime-restoration-ledger.md");
        var combinedDocs = string.Join(Environment.NewLine, moduleReadme, operatorRunbook, restorationLedger);

        foreach (var requiredReadbackTerm in new[]
        {
            "IProcessManagerReadOnlyVerificationFacade.VerifyForReadbackAsync",
            "ProcessManagerReadOnlyVerificationReadbackDto",
            "ProcessVerificationHostFailureCategory",
            "ProcessVerificationHostDenialCode",
            "auditRecords",
            "observationHash",
            "denialCategory",
            "denialCode",
            "denialMessage",
            "noMutationPerformed = true",
            "allowsProcessMutation = false",
            "allowsTransitionMutation = false",
            "allowsFinalizerMutation = false"
        })
        {
            Assert.Contains(requiredReadbackTerm, combinedDocs, StringComparison.Ordinal);
        }

        Assert.Contains("Current status: `Not approved`", moduleReadme, StringComparison.Ordinal);
        Assert.Contains("The generic process-driver runtime host remains not approved", operatorRunbook, StringComparison.Ordinal);
        Assert.Contains("Generic process-driver runtime host is not approved", restorationLedger, StringComparison.Ordinal);

        foreach (var forbiddenApprovalClaim in new[]
        {
            "Production verification host registration decision: `Ready`",
            "Runtime host status: `Approved`",
            "runtime host is approved",
            "driver registry is approved",
            "DI registration is approved",
            "manager command may invoke drivers",
            "scheduler hook may invoke drivers",
            "workflow hook may invoke drivers",
            "ExecutionCapableFuture is permission"
        })
        {
            Assert.DoesNotContain(forbiddenApprovalClaim, combinedDocs, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Process_driver_contract_api_SB027_INV_001_office_business_analysis_and_artifact_lanes_stay_readonly()
    {
        var scopes = CreateReadonlyScopes();
        var officeScope = Assert.Single(scopes, scope => scope.Kind == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        var businessScope = Assert.Single(scopes, scope => scope.Kind == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);
        var artifactScope = Assert.Single(scopes, scope => scope.Kind == ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead);

        Assert.Equal(ProcessDriverPermissionMode.VerificationOnly, officeScope.RequiredPermissionMode);
        Assert.Equal(ProcessDriverPermissionMode.VerificationOnly, businessScope.RequiredPermissionMode);
        Assert.Equal(ProcessDriverPermissionMode.VerificationOnly, artifactScope.RequiredPermissionMode);
        Assert.False(officeScope.AllowsExternalCalls);
        Assert.False(officeScope.AllowsStorageWrites);
        Assert.False(businessScope.AllowsProcessMutation);
        Assert.False(businessScope.AllowsWorkspaceWrites);
        Assert.False(artifactScope.AllowsExternalCalls);
        Assert.False(artifactScope.AllowsProcessMutation);
        Assert.True(ProcessDriverCapabilityScopeRules.IsOfficeEvidenceReadScope(
            officeScope,
            ProcessDriverPermissionMode.VerificationOnly));
        Assert.True(ProcessDriverCapabilityScopeRules.IsBusinessAnalysisReadScope(
            businessScope,
            ProcessDriverPermissionMode.VerificationOnly));
        Assert.True(ProcessDriverCapabilityScopeRules.IsArtifactEvidenceReadScope(
            artifactScope,
            ProcessDriverPermissionMode.VerificationOnly));
    }

    [Fact]
    public void Process_driver_contract_api_SB019_INV_001_gateway_allow_list_is_explicit_typed_and_runtime_free()
    {
        var root = FindRepositoryRoot();
        var design = ReadProcessDriverMultiDomainFixtureFile(
            "architecture",
            "08-explicit-verification-gateway-design.md");
        var contractSource = ReadContractSource(root);
        var descriptors = ProcessDriverVerificationGatewayLaneRules.AllowedLanes;
        var descriptorByLane = descriptors.ToDictionary(descriptor => descriptor.Lane);

        Assert.Equal(5, descriptors.Count);
        Assert.Equal(
            Enum.GetValues<ProcessDriverVerificationGatewayLane>().OrderBy(lane => lane).ToArray(),
            descriptors.Select(descriptor => descriptor.Lane).OrderBy(lane => lane).ToArray());
        AssertGatewayDescriptor(
            descriptorByLane[ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification],
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverEvidenceReferenceKind.CommandTranscript,
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence,
            [ProcessDriverOperation.InspectExistingEvidence, ProcessDriverOperation.ReturnDiagnostics, ProcessDriverOperation.ExplainDenial]);
        AssertGatewayDescriptor(
            descriptorByLane[ProcessDriverVerificationGatewayLane.RuntimeEvidenceConsistency],
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
            ProcessDriverPermissionMode.ManagerReadonly,
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence,
            [ProcessDriverOperation.ReadProcessFacts, ProcessDriverOperation.ReturnDiagnostics, ProcessDriverOperation.ExplainDenial]);
        AssertGatewayDescriptor(
            descriptorByLane[ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency],
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence,
            [ProcessDriverOperation.InspectExistingEvidence, ProcessDriverOperation.ReturnDiagnostics, ProcessDriverOperation.ExplainDenial]);
        AssertGatewayDescriptor(
            descriptorByLane[ProcessDriverVerificationGatewayLane.OfficeEvidenceRead],
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact,
            null,
            [ProcessDriverOperation.InspectExistingEvidence, ProcessDriverOperation.ReturnDiagnostics, ProcessDriverOperation.ExplainDenial]);
        AssertGatewayDescriptor(
            descriptorByLane[ProcessDriverVerificationGatewayLane.BusinessAnalysisRead],
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
            ProcessDriverPermissionMode.VerificationOnly,
            ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact,
            null,
            [ProcessDriverOperation.InspectExistingEvidence, ProcessDriverOperation.ReturnDiagnostics, ProcessDriverOperation.ExplainDenial]);
        Assert.All(descriptors, descriptor =>
            Assert.All(descriptor.AllowedOperations, operation =>
                Assert.True(ProcessDriverOperationRules.IsReadonlyVerificationOperation(operation))));
        Assert.Contains("ProcessDriverVerificationGatewayLaneRules", design, StringComparison.Ordinal);
        Assert.Contains("No dynamic lane discovery.", design, StringComparison.Ordinal);
        Assert.Contains("Contract version is `1.10.0`", design, StringComparison.Ordinal);
        AssertNoForbiddenProductionDriverRuntimeTokens(contractSource);
    }

    [Fact]
    public void Process_driver_contract_api_SB031_INV_001_gateway_v1_public_api_snapshot_freezes_typed_batch_surface()
    {
        var publicTypeNames = ReadPublicTypeNames(typeof(ProcessDriverVerificationGateway).Assembly)
            .Where(typeName => typeName.StartsWith(
                "CanDoItAll.Processes.Drivers.VerificationGateway.",
                StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(new ProcessDriverContractVersion(1, 10, 0), ProcessDriverContractVersion.Current);
        Assert.Equal(4, publicTypeNames.Length);
        Assert.Equal(
            "69fd070de1004e6b01f71ae2251d1d3f63b7b2f306d4b165cf3329822f6ad62c",
            ComputePublicApiSurfaceHash(publicTypeNames));
        Assert.Equal(
            [
                "CanDoItAll.Processes.Drivers.VerificationGateway.ProcessDriverVerificationBatchAggregationRequest",
                "CanDoItAll.Processes.Drivers.VerificationGateway.ProcessDriverVerificationBatchRequest",
                "CanDoItAll.Processes.Drivers.VerificationGateway.ProcessDriverVerificationBatchResponse",
                "CanDoItAll.Processes.Drivers.VerificationGateway.ProcessDriverVerificationGateway"
            ],
            publicTypeNames);
        Assert.Equal(
            [
                "AggregateObservations",
                "CreateDefault",
                "VerifyArtifactEvidence",
                "VerifyBatch",
                "VerifyBusinessAnalysis",
                "VerifyOfficeEvidence",
                "VerifyRuntimeEvidence",
                "VerifyTranscript"
            ],
            ReadDeclaredPublicMethodNames(typeof(ProcessDriverVerificationGateway)));
        Assert.Equal(
            ["ImplementedLanes"],
            ReadDeclaredPublicPropertyNames(typeof(ProcessDriverVerificationGateway)));
        Assert.Equal(
            ["CallerContext", "RequestedAt"],
            ReadDeclaredPublicPropertyNames(typeof(ProcessDriverVerificationBatchAggregationRequest)));
        Assert.Equal(
            [
                "Aggregation",
                "ArtifactEvidenceRequests",
                "BusinessAnalysisRequests",
                "OfficeEvidenceRequests",
                "RuntimeEvidenceRequests",
                "TranscriptRequests"
            ],
            ReadDeclaredPublicPropertyNames(typeof(ProcessDriverVerificationBatchRequest)));
        Assert.Equal(
            [
                "Aggregate",
                "AllResponses",
                "ArtifactEvidenceResponses",
                "BusinessAnalysisResponses",
                "OfficeEvidenceResponses",
                "RuntimeEvidenceResponses",
                "TranscriptResponses"
            ],
            ReadDeclaredPublicPropertyNames(typeof(ProcessDriverVerificationBatchResponse)));
    }

    [Fact]
    public void Process_driver_contract_api_SB032_INV_001_gateway_batch_migration_guard_is_documented_and_runtime_free()
    {
        var readme = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.VerificationGateway",
            "README.md");

        Assert.Contains("Contract line: `v1.x verification-only alpha`", readme, StringComparison.Ordinal);
        Assert.Contains("`ProcessDriverContractVersion.Current` remains `1.10.0`", readme, StringComparison.Ordinal);
        Assert.Contains("Public type count: `4`", readme, StringComparison.Ordinal);
        Assert.Contains(
            "Surface hash: `69fd070de1004e6b01f71ae2251d1d3f63b7b2f306d4b165cf3329822f6ad62c`",
            readme,
            StringComparison.Ordinal);
        Assert.Contains("`VerifyBatch` is an additive v1.x convenience over the typed lane methods.", readme, StringComparison.Ordinal);
        Assert.Contains("It accepts only `ProcessDriverVerificationBatchRequest`", readme, StringComparison.Ordinal);
        Assert.Contains("It does not replace the lane-specific methods", readme, StringComparison.Ordinal);
        Assert.Contains("does not introduce `Verify(object)`", readme, StringComparison.Ordinal);
        Assert.Contains("driver discovery", readme, StringComparison.Ordinal);
        Assert.Contains("`ProcessDriverVerificationBatchResponse.AllResponses` is a read-only concatenation", readme, StringComparison.Ordinal);
        Assert.Contains("treat batch aggregation as diagnostic evidence only", readme, StringComparison.Ordinal);

        foreach (var deniedRuntimeClaim in new[]
        {
            "runtime host approval: granted",
            "runtime host is approved",
            "DI registration is approved",
            "scheduler is approved",
            "manager command is approved",
            "workspace write allowed",
            "storage write allowed"
        })
        {
            Assert.DoesNotContain(deniedRuntimeClaim, readme, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Process_driver_contract_api_SB022_INV_001_supplied_evidence_content_envelope_is_typed_hashable_and_payload_only()
    {
        const string transcriptText = "Build succeeded.";
        const string descriptorPayload = """{"executionRunId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}""";
        const string artifactPayload = """{"projection":[{"source":"file-write"}],"validation":[{"kind":"deliverable"}]}""";
        const string officePayload = """{"items":[{"kind":"email","id":"message-1"}]}""";
        const string businessPayload = """{"items":[{"kind":"deliverable","id":"analysis-1"}]}""";
        var transcriptReference = new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CommandTranscript,
            "bundle://proof/SB022/transcripts/dotnet-transcript.txt",
            ProcessDriverEvidencePolicy.ComputeSha256(transcriptText),
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
        var descriptorReference = new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            "bundle://proof/SB022/runtime-evidence.json",
            ProcessDriverEvidencePolicy.ComputeSha256(descriptorPayload),
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
        var artifactReference = new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            "bundle://proof/SB034/artifact-projection-evidence.json",
            ProcessDriverEvidencePolicy.ComputeSha256(artifactPayload),
            ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence);
        var officeReference = new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact,
            "bundle://proof/SB028/office-evidence.json",
            ProcessDriverEvidencePolicy.ComputeSha256(officePayload),
            null);
        var businessReference = new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact,
            "bundle://proof/SB031/business-analysis.json",
            ProcessDriverEvidencePolicy.ComputeSha256(businessPayload),
            null);

        var transcriptContent = ProcessDriverSuppliedEvidenceContentRules.CreateTranscriptText(
            transcriptReference,
            transcriptText);
        var descriptorContent = ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
            descriptorReference,
            descriptorPayload);
        var artifactContent = ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
            artifactReference,
            artifactPayload);
        var officeContent = ProcessDriverSuppliedEvidenceContentRules.CreateOfficeEvidencePayload(
            officeReference,
            officePayload);
        var businessContent = ProcessDriverSuppliedEvidenceContentRules.CreateBusinessAnalysisPayload(
            businessReference,
            businessPayload);

        Assert.Equal(ProcessDriverSuppliedEvidenceContentKind.TranscriptText, transcriptContent.Kind);
        Assert.Equal(ProcessDriverSuppliedEvidenceContentRules.PlainTextContentType, transcriptContent.ContentType);
        Assert.Equal(transcriptReference, transcriptContent.EvidenceReference);
        Assert.Equal(ProcessDriverEvidencePolicy.ComputeSha256(transcriptText), transcriptContent.ContentHash);
        Assert.True(transcriptContent.SizeBytes > 0);
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasExpectedEnvelope(
            transcriptContent,
            ProcessDriverSuppliedEvidenceContentKind.TranscriptText,
            ProcessDriverSuppliedEvidenceContentRules.PlainTextContentType));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasAllowedSize(transcriptContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasValidContentHash(transcriptContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasEvidenceReferenceHashBinding(transcriptContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HashMatchesSuppliedPayload(
            transcriptContent,
            transcriptText));
        Assert.Equal(ProcessDriverSuppliedEvidenceContentKind.CoreDescriptorPayload, descriptorContent.Kind);
        Assert.Equal(ProcessDriverSuppliedEvidenceContentRules.JsonContentType, descriptorContent.ContentType);
        Assert.Equal(descriptorReference, descriptorContent.EvidenceReference);
        Assert.Equal(ProcessDriverEvidencePolicy.ComputeSha256(descriptorPayload), descriptorContent.ContentHash);
        Assert.True(descriptorContent.SizeBytes > 0);
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasExpectedEnvelope(
            descriptorContent,
            ProcessDriverSuppliedEvidenceContentKind.CoreDescriptorPayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasAllowedSize(descriptorContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasValidContentHash(descriptorContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasEvidenceReferenceHashBinding(descriptorContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HashMatchesSuppliedPayload(
            descriptorContent,
            descriptorPayload));
        Assert.Equal(ProcessDriverSuppliedEvidenceContentKind.CoreDescriptorPayload, artifactContent.Kind);
        Assert.Equal(ProcessDriverSuppliedEvidenceContentRules.JsonContentType, artifactContent.ContentType);
        Assert.Equal(artifactReference, artifactContent.EvidenceReference);
        Assert.Equal(ProcessDriverEvidencePolicy.ComputeSha256(artifactPayload), artifactContent.ContentHash);
        Assert.True(artifactContent.SizeBytes > 0);
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasExpectedEnvelope(
            artifactContent,
            ProcessDriverSuppliedEvidenceContentKind.CoreDescriptorPayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasAllowedSize(artifactContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasValidContentHash(artifactContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasEvidenceReferenceHashBinding(artifactContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HashMatchesSuppliedPayload(
            artifactContent,
            artifactPayload));
        Assert.Equal(ProcessDriverSuppliedEvidenceContentKind.OfficeEvidencePayload, officeContent.Kind);
        Assert.Equal(ProcessDriverSuppliedEvidenceContentRules.JsonContentType, officeContent.ContentType);
        Assert.Equal(officeReference, officeContent.EvidenceReference);
        Assert.Equal(ProcessDriverEvidencePolicy.ComputeSha256(officePayload), officeContent.ContentHash);
        Assert.True(officeContent.SizeBytes > 0);
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasExpectedEnvelope(
            officeContent,
            ProcessDriverSuppliedEvidenceContentKind.OfficeEvidencePayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasAllowedSize(officeContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasValidContentHash(officeContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasEvidenceReferenceHashBinding(officeContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HashMatchesSuppliedPayload(
            officeContent,
            officePayload));
        Assert.Equal(ProcessDriverSuppliedEvidenceContentKind.BusinessAnalysisPayload, businessContent.Kind);
        Assert.Equal(ProcessDriverSuppliedEvidenceContentRules.JsonContentType, businessContent.ContentType);
        Assert.Equal(businessReference, businessContent.EvidenceReference);
        Assert.Equal(ProcessDriverEvidencePolicy.ComputeSha256(businessPayload), businessContent.ContentHash);
        Assert.True(businessContent.SizeBytes > 0);
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasExpectedEnvelope(
            businessContent,
            ProcessDriverSuppliedEvidenceContentKind.BusinessAnalysisPayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasAllowedSize(businessContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasValidContentHash(businessContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasEvidenceReferenceHashBinding(businessContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HashMatchesSuppliedPayload(
            businessContent,
            businessPayload));
    }

    [Fact]
    public void Process_driver_contract_api_SB033_INV_001_runtime_alpha_remains_deferred_to_followup_decision()
    {
        var root = FindRepositoryRoot();
        var contractSource = ReadContractSource(root);
        var publicTypes = typeof(ProcessDriverPermissionMode).Assembly.GetExportedTypes();
        var version = ProcessDriverContractVersion.Current;

        Assert.Equal(new ProcessDriverContractVersion(1, 10, 0), version);
        Assert.Contains(ProcessDriverPermissionMode.ExecutionCapableFuture, Enum.GetValues<ProcessDriverPermissionMode>());
        AssertNoForbiddenProductionDriverRuntimeTokens(contractSource);
        Assert.DoesNotContain(publicTypes, type => type.IsInterface);
        Assert.DoesNotContain(publicTypes, type => type.Name.Contains("Host", StringComparison.Ordinal));
        Assert.DoesNotContain(publicTypes, type => type.Name.Contains("Provider", StringComparison.Ordinal));
        Assert.DoesNotContain(publicTypes, type => type.Name.Contains("Selector", StringComparison.Ordinal));
        Assert.DoesNotContain(publicTypes, type => type.Name.Contains("Registry", StringComparison.Ordinal));
    }

    [Fact]
    public void Process_driver_contract_api_SB040_SB042_INV_001_current_bundle_runtime_host_matrix_keeps_runtime_surfaces_unapproved()
    {
        var decision = ReadProcessDriverReadonlyReleaseCandidateFixtureFile(
            "architecture",
            "04-runtime-host-decision.md");

        Assert.Contains("Current decision: all runtime-host surfaces are `Not approved`.", decision, StringComparison.Ordinal);
        Assert.Contains("Contract line: `v1.x verification-only alpha`.", decision, StringComparison.Ordinal);
        Assert.Contains("ExecutionCapableFuture` remains a denied marker", decision, StringComparison.Ordinal);
        Assert.Contains("Future Approval Prerequisites", decision, StringComparison.Ordinal);
        Assert.Contains("Every prerequisite in this section is `Not satisfied`.", decision, StringComparison.Ordinal);

        foreach (var surface in new[]
        {
            "Runtime host",
            "Driver registry",
            "Runtime selector",
            "Dependency injection registration",
            "Manager command",
            "Scheduler hook",
            "Workflow hook",
            "Execution-capable drivers",
            "File/network/storage/workspace mutation"
        })
        {
            Assert.Contains($"| {surface} | `Not approved` |", decision, StringComparison.Ordinal);
        }

        foreach (var prerequisite in new[]
        {
            "Audit persistence",
            "Runtime lifecycle ownership",
            "Authorization and approval",
            "Sandbox and allow-list policy",
            "Failure semantics",
            "Compatibility governance",
            "Red-team negative proof"
        })
        {
            Assert.Contains($"| {prerequisite} | `Not satisfied` |", decision, StringComparison.Ordinal);
        }

        foreach (var forbiddenApprovalClaim in new[]
        {
            "Current decision: approved",
            "runtime host is approved",
            "registry is approved",
            "selector is approved",
            "DI registration is approved",
            "manager command is approved",
            "scheduler hook is approved",
            "workflow hook is approved",
            "execution-capable drivers are approved",
            "ExecutionCapableFuture is permission"
        })
        {
            Assert.DoesNotContain(forbiddenApprovalClaim, decision, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Process_driver_contract_api_SB041_SB042_INV_001_current_readonly_pipeline_source_rejects_runtime_host_hooks()
    {
        var root = FindRepositoryRoot();
        var sourceText = ReadReadonlyDriverPipelineSource(root);
        var gatewayReadme = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.VerificationGateway",
            "README.md");

        Assert.Contains("ExecutionCapableFuture = 3", sourceText, StringComparison.Ordinal);
        Assert.Contains("No runtime host, dynamic registry, selector, dependency-injection registration", gatewayReadme, StringComparison.Ordinal);
        Assert.Contains("manager command, scheduler hook, workflow hook", gatewayReadme, StringComparison.Ordinal);
        AssertNoForbiddenRuntimeHostHookTokens(sourceText);

        foreach (var forbiddenReadmeApprovalClaim in new[]
        {
            "runtime host approval: granted",
            "runtime host is approved",
            "dynamic registry is approved",
            "dependency-injection registration is approved",
            "manager command is approved",
            "scheduler hook is approved",
            "workflow hook is approved",
            "ExecutionCapableFuture is permission"
        })
        {
            Assert.DoesNotContain(forbiddenReadmeApprovalClaim, gatewayReadme, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Process_driver_contract_api_SB043_SB044_INV_001_future_execution_guards_remain_unsatisfied_and_source_rejects_premature_surfaces()
    {
        var root = FindRepositoryRoot();
        var ledger = ReadRepositoryFile("docs", "process-runtime-restoration-ledger.md");
        var operatorRunbook = ReadRepositoryFile("docs", "process-agent-operator-runbook.md");
        var processesReadme = ReadRepositoryFile("src", "CanDoItAll.Modules.Processes", "README.md");
        var sourceText = ReadReadonlyDriverPipelineSource(root);

        Assert.Contains("## Execution-Capable Future Gate Guards", ledger, StringComparison.Ordinal);
        Assert.Contains("Guard status: every execution-capable driver prerequisite is `Not satisfied`.", ledger, StringComparison.Ordinal);
        Assert.Contains("The generic process-driver runtime host remains not approved", operatorRunbook, StringComparison.Ordinal);
        Assert.Contains("Future approval gate:", processesReadme, StringComparison.Ordinal);
        Assert.Contains("Denied until that future gate passes:", processesReadme, StringComparison.Ordinal);

        foreach (var prerequisite in new[]
        {
            "Runtime lifecycle ownership",
            "Audit persistence",
            "Sandbox and allow-list policy",
            "Authorization and approval",
            "Command, network, and storage policy",
            "Compatibility governance",
            "Red-team negative proof"
        })
        {
            Assert.Contains($"| {prerequisite} | `Not satisfied` |", ledger, StringComparison.Ordinal);
        }

        foreach (var blockedSurface in new[]
        {
            "Runtime host",
            "Driver registry",
            "Runtime selector",
            "Dependency-injection registration",
            "Manager command",
            "Scheduler hook",
            "Workflow hook",
            "Endpoint mapping",
            "Workspace or storage write",
            "External command, network, Office/Graph, or CRM call",
            "Transition, claim, finalizer, retry, or process mutation",
            "Execution-capable drivers"
        })
        {
            Assert.Contains($"| {blockedSurface} | `Blocked` |", ledger, StringComparison.Ordinal);
        }

        foreach (var forbiddenApprovalClaim in new[]
        {
            "Guard status: satisfied",
            "Runtime host status: approved",
            "runtime host is approved",
            "driver registry is approved",
            "runtime selector is approved",
            "dependency-injection registration is approved",
            "manager command is approved",
            "scheduler hook is approved",
            "workflow hook is approved",
            "execution-capable drivers are approved",
            "process mutation allowed"
        })
        {
            Assert.DoesNotContain(forbiddenApprovalClaim, ledger, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbiddenApprovalClaim, operatorRunbook, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbiddenApprovalClaim, processesReadme, StringComparison.OrdinalIgnoreCase);
        }

        AssertNoForbiddenRuntimeHostHookTokens(sourceText);
    }

    [Fact]
    public void Process_driver_contract_api_SB052_SB053_INV_001_current_bundle_roadmap_keeps_runtime_integration_blocked()
    {
        var root = FindRepositoryRoot();
        var decision = ReadProcessDriverReadonlyReleaseCandidateFixtureFile(
            "architecture",
            "06-next-roadmap-decision.md");
        var roadmap = ReadProcessDriverReadonlyReleaseCandidateFixtureFile(
            "architecture",
            "07-stable-core-domain-driver-roadmap-and-reopen-triggers.md");
        var runtimeDecision = ReadProcessDriverReadonlyReleaseCandidateFixtureFile(
            "architecture",
            "04-runtime-host-decision.md");
        var sourceText = ReadReadonlyDriverPipelineSource(root);

        Assert.Contains("Next candidate decision: `Continue read-only domain-driver expansion and manager-visible projection planning`", decision, StringComparison.Ordinal);
        Assert.Contains("Controlled read-only runtime integration: `Blocked`", decision, StringComparison.Ordinal);
        Assert.Contains("Runtime host status: `Not approved`", decision, StringComparison.Ordinal);
        Assert.Contains("Prerequisite status: `Not satisfied`", decision, StringComparison.Ordinal);
        Assert.Contains("Current decision: all runtime-host surfaces are `Not approved`.", runtimeDecision, StringComparison.Ordinal);
        Assert.Contains("Keep `CanDoItAll.Processes.Core` deterministic and driver-free.", roadmap, StringComparison.Ordinal);
        Assert.Contains("Any `CanDoItAll.Processes.Core` reference to `CanDoItAll.Processes.Drivers`.", roadmap, StringComparison.Ordinal);
        Assert.Contains("Any `Verify(object)`", roadmap, StringComparison.Ordinal);
        Assert.Contains("Any completed validator failure", roadmap, StringComparison.Ordinal);

        foreach (var forbiddenRoadmapClaim in new[]
        {
            "Controlled read-only runtime integration: `Ready`",
            "Runtime host status: `Approved`",
            "Prerequisite status: `Satisfied`",
            "generic runtime host is next",
            "driver registry is approved",
            "service registration is approved",
            "scheduler hook is approved",
            "workflow hook is approved",
            "ExecutionCapableFuture is permission"
        })
        {
            Assert.DoesNotContain(forbiddenRoadmapClaim, decision, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbiddenRoadmapClaim, roadmap, StringComparison.OrdinalIgnoreCase);
        }

        AssertNoForbiddenRuntimeHostHookTokens(sourceText);
    }

    private static ProcessDriverCapabilityScope[] CreateReadonlyScopes()
    {
        return
        [
            new ProcessDriverCapabilityScope(
                ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
                ProcessDriverPermissionMode.VerificationOnly,
                AllowsProcessMutation: false,
                AllowsExternalCalls: false,
                AllowsWorkspaceWrites: false,
                AllowsStorageWrites: false),
            new ProcessDriverCapabilityScope(
                ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
                ProcessDriverPermissionMode.ManagerReadonly,
                AllowsProcessMutation: false,
                AllowsExternalCalls: false,
                AllowsWorkspaceWrites: false,
                AllowsStorageWrites: false),
            new ProcessDriverCapabilityScope(
                ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
                ProcessDriverPermissionMode.VerificationOnly,
                AllowsProcessMutation: false,
                AllowsExternalCalls: false,
                AllowsWorkspaceWrites: false,
                AllowsStorageWrites: false),
            new ProcessDriverCapabilityScope(
                ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
                ProcessDriverPermissionMode.VerificationOnly,
                AllowsProcessMutation: false,
                AllowsExternalCalls: false,
                AllowsWorkspaceWrites: false,
                AllowsStorageWrites: false),
            new ProcessDriverCapabilityScope(
                ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
                ProcessDriverPermissionMode.VerificationOnly,
                AllowsProcessMutation: false,
                AllowsExternalCalls: false,
                AllowsWorkspaceWrites: false,
                AllowsStorageWrites: false)
        ];
    }

    private static void AssertGatewayDescriptor(
        ProcessDriverVerificationGatewayLaneDescriptor descriptor,
        ProcessDriverCapabilityScopeKind expectedScopeKind,
        ProcessDriverPermissionMode expectedPermissionMode,
        ProcessDriverEvidenceReferenceKind expectedEvidenceKind,
        ProcessDriverCoreDescriptorFamily? expectedCoreDescriptorFamily,
        IReadOnlyList<ProcessDriverOperation> expectedAllowedOperations)
    {
        Assert.Equal(expectedScopeKind, descriptor.RequiredScopeKind);
        Assert.Equal(expectedPermissionMode, descriptor.RequiredPermissionMode);
        Assert.Equal(expectedEvidenceKind, descriptor.PrimaryEvidenceKind);
        Assert.Equal(expectedCoreDescriptorFamily, descriptor.CoreDescriptorFamily);
        Assert.Equal(expectedAllowedOperations, descriptor.AllowedOperations);
    }

    private static ProcessDriverOperation[] CreateSideEffectOperations()
    {
        return
        [
            ProcessDriverOperation.MutateProcessState,
            ProcessDriverOperation.ExecuteCommand,
            ProcessDriverOperation.RestorePackage,
            ProcessDriverOperation.WriteArtifact,
            ProcessDriverOperation.WriteWorkspaceStorage,
            ProcessDriverOperation.CallOfficeGraph,
            ProcessDriverOperation.MutateEmailCategory,
            ProcessDriverOperation.CreateTask,
            ProcessDriverOperation.MutateBusinessRecord,
            ProcessDriverOperation.ApplyTransition,
            ProcessDriverOperation.ClaimDispatch,
            ProcessDriverOperation.ApplyFinalizer,
            ProcessDriverOperation.ScheduleRetry
        ];
    }

    private static ProcessDriverDenialReason ResolveExpectedDenial(ProcessDriverOperation operation)
    {
        return ProcessDriverOperationRules.ResolveReadonlyDenialReason(operation);
    }

    private static IReadOnlyList<ProcessDriverDiagnosticCategory> ClassifyTranscript(string transcriptText)
    {
        var categories = new List<ProcessDriverDiagnosticCategory>();
        if (transcriptText.Contains("warning", StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(ProcessDriverDiagnosticCategory.BuildWarning);
        }

        if (transcriptText.Contains("FAILED", StringComparison.OrdinalIgnoreCase) ||
            transcriptText.Contains("Test Failed", StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(ProcessDriverDiagnosticCategory.TestFailure);
        }

        if (transcriptText.Contains("missing artifact", StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(ProcessDriverDiagnosticCategory.MissingArtifact);
        }

        if (transcriptText.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(ProcessDriverDiagnosticCategory.UnsupportedTargetFramework);
        }

        if (transcriptText.Contains("runtime proof gap", StringComparison.OrdinalIgnoreCase))
        {
            categories.Add(ProcessDriverDiagnosticCategory.RuntimeProofGap);
        }

        return categories;
    }

    private static IReadOnlyList<string> ReadContractPublicTypeNames()
        => ReadPublicTypeNames(typeof(ProcessDriverPermissionMode).Assembly);

    private static IReadOnlyList<string> ReadPublicTypeNames(Assembly assembly)
    {
        return assembly
            .GetExportedTypes()
            .Select(type => type.FullName ?? type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadDeclaredPublicMethodNames(Type type)
    {
        return type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadDeclaredPublicPropertyNames(Type type)
    {
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertDocumentedPublicSurface(
        string snapshot,
        Assembly assembly,
        string namespacePrefix,
        int expectedCount,
        string expectedHash)
    {
        var publicTypeNames = ReadPublicTypeNames(assembly)
            .Where(typeName => typeName.StartsWith(namespacePrefix, StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(expectedCount, publicTypeNames.Length);
        Assert.Equal(expectedHash, ComputePublicApiSurfaceHash(publicTypeNames));
        Assert.Contains($"Public type count: `{expectedCount}`", snapshot, StringComparison.Ordinal);
        Assert.Contains($"Surface hash: `{expectedHash}`", snapshot, StringComparison.Ordinal);
        Assert.All(publicTypeNames, publicTypeName =>
            Assert.Contains($"`{publicTypeName}`", snapshot, StringComparison.Ordinal));
    }

    private static string ComputePublicApiSurfaceHash(IEnumerable<string> publicTypeNames)
    {
        var payload = string.Join('\n', publicTypeNames);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ReadContractSource(string repositoryRoot)
    {
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(
                    Path.Combine(repositoryRoot, "src", "CanDoItAll.Processes.Drivers.Abstractions"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadReadonlyDriverPipelineSource(string repositoryRoot)
    {
        var sourceFiles = EnumerateReadonlyDriverPipelineSourceTargets()
            .Select(pathParts => Path.Combine([repositoryRoot, .. pathParts]))
            .SelectMany(ReadSourceFiles)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return string.Join(Environment.NewLine, sourceFiles.Select(File.ReadAllText));
    }

    private static IEnumerable<string> ReadSourceFiles(string path)
    {
        if (Directory.Exists(path))
        {
            return Directory
                .EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(path, "*.csproj", SearchOption.TopDirectoryOnly));
        }

        if (File.Exists(path))
        {
            return [path];
        }

        throw new FileNotFoundException("Expected runtime-host denial source target was not found.", path);
    }

    private static IReadOnlyList<string[]> EnumerateReadonlyDriverPipelineSourceTargets()
    {
        return
        [
            ["src", "CanDoItAll.Processes.Drivers.Abstractions"],
            ["src", "CanDoItAll.Processes.Drivers.ArtifactEvidence"],
            ["src", "CanDoItAll.Processes.Drivers.BusinessAnalysis"],
            ["src", "CanDoItAll.Processes.Drivers.ObservationAggregation"],
            ["src", "CanDoItAll.Processes.Drivers.OfficeEvidence"],
            ["src", "CanDoItAll.Processes.Drivers.RuntimeEvidence"],
            ["src", "CanDoItAll.Processes.Drivers.TranscriptVerification"],
            ["src", "CanDoItAll.Processes.Drivers.VerificationGateway"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessArtifactEvidenceReadOnlyAdapter.cs"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessBusinessAnalysisReadOnlyAdapter.cs"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessDriverObservationAggregationReadOnlyAdapter.cs"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessOfficeEvidenceReadOnlyAdapter.cs"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessReadOnlyVerificationAggregateObservation.cs"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessReadOnlyVerificationBatchOrchestrator.cs"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessReadOnlyVerificationOperationPolicy.cs"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessReadOnlyVerificationPayloadBuilder.cs"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessReadOnlyVerificationRequestFactory.cs"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessRuntimeEvidenceVerificationObservationMapper.cs"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessRuntimeEvidenceVerificationReadOnlyAdapter.cs"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessTranscriptVerificationObservationMapper.cs"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessTranscriptVerificationPreflightPolicy.cs"],
            ["src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessTranscriptVerificationReadOnlyAdapter.cs"]
        ];
    }

    private static string ReadFileNames(string repositoryRoot, params string[] pathParts)
    {
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.Combine([repositoryRoot, .. pathParts]), "*.cs")
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal));
    }

    private static void AssertNoForbiddenProductionDriverRuntimeTokens(string sourceText)
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
            "ProcessDriverServiceCollectionExtensions",
            "AddProcessDriver",
            "MapProcessDriver",
            "IServiceCollection",
            "ServiceCollection"
        };

        foreach (var forbiddenToken in forbiddenTokens)
        {
            Assert.DoesNotContain(forbiddenToken, sourceText, StringComparison.Ordinal);
        }
    }

    private static void AssertNoForbiddenRuntimeHostHookTokens(string sourceText)
    {
        var forbiddenTokens = new[]
        {
            "IProcessDriver",
            "IProcessDriverRegistry",
            "ProcessDriverRegistry",
            "IProcessDriverSelector",
            "ProcessDriverRuntimeSelector",
            "IProcessDriverRuntime",
            "ProcessDriverRuntime",
            "IProcessDriverHost",
            "ProcessDriverHost",
            "IProcessDriverPack",
            "ProcessDriverPack",
            "ProcessDriverProvider",
            "ProcessDriverManagerCommand",
            "ProcessDriverServiceCollectionExtensions",
            "AddProcessDriver",
            "MapProcessDriver",
            "IServiceCollection",
            "ServiceCollection",
            "AddScoped",
            "AddSingleton",
            "GetRequiredService",
            "IHostedService",
            "BackgroundService",
            "IScheduler",
            "IWorkflow",
            "SchedulerHook",
            "WorkflowHook"
        };

        foreach (var forbiddenToken in forbiddenTokens)
        {
            Assert.DoesNotContain(forbiddenToken, sourceText, StringComparison.Ordinal);
        }
    }

    private static string ReadProcessDriverMultiDomainFixtureFile(params string[] pathParts)
    {
        return ReadStableArchitectureFixtureFile("ProcessDriverMultiDomainVerificationGateway", pathParts);
    }

    private static string ReadProcessDriverReadonlyReleaseCandidateFixtureFile(params string[] pathParts)
    {
        return ReadStableArchitectureFixtureFile("ProcessDriverReadonlyReleaseCandidateStabilization", pathParts);
    }

    private static string ReadStableArchitectureFixtureFile(
        string fixtureDirectory,
        params string[] pathParts)
    {
        var stablePathParts = new List<string>
        {
            "tests",
            "CanDoItAll.Tests.Unit",
            "TestData",
            "Architecture",
            fixtureDirectory
        };
        stablePathParts.AddRange(pathParts);

        return ReadRepositoryFile(stablePathParts.ToArray());
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


