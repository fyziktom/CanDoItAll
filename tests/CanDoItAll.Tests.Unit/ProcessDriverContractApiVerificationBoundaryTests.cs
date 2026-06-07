using System.Reflection;
using System.Runtime.CompilerServices;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverContractApiVerificationBoundaryTests
{
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
        var fact = new ProcessDriverAuditFact(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DateTimeOffset.Parse("2026-06-07T20:30:00Z"),
            ProcessDriverAuditFactKind.OperationDenied,
            "manager:readonly",
            ProcessDriverPermissionMode.VerificationOnly,
            scope,
            ProcessDriverOperation.ExecuteCommand,
            ProcessDriverDenialReason.UnsafeCommand,
            redaction,
            "Command execution denied for verification-only contract.",
            "BEE3701B1528648B7D54A6B29311D8D822F32F87F20D5D4C5A26C8417E109B0F");
        var evidenceReference = new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CommandTranscript,
            "bundle://proof/SB006/transcripts/passing-focused-tests.txt",
            "D6FCF6DB6C7C547B70C972A70902DA6203B08F4EF34690CD8E34C41858F3F7D5",
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);

        Assert.Equal(ProcessDriverAuditFactKind.OperationDenied, fact.Kind);
        Assert.Equal(ProcessDriverRedactionStatus.Redacted, fact.Redaction.Status);
        Assert.Contains(ProcessDriverRedactionKind.Secret, fact.Redaction.AppliedKinds);
        Assert.Contains(ProcessDriverRedactionKind.EmailAddress, fact.Redaction.AppliedKinds);
        Assert.Equal(ProcessDriverDenialReason.UnsafeCommand, fact.DenialReason);
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
    public void Process_driver_contract_api_SB027_INV_001_office_and_business_analysis_lanes_stay_readonly()
    {
        var scopes = CreateReadonlyScopes();
        var officeScope = Assert.Single(scopes, scope => scope.Kind == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        var businessScope = Assert.Single(scopes, scope => scope.Kind == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);

        Assert.Equal(ProcessDriverPermissionMode.VerificationOnly, officeScope.RequiredPermissionMode);
        Assert.Equal(ProcessDriverPermissionMode.VerificationOnly, businessScope.RequiredPermissionMode);
        Assert.False(officeScope.AllowsExternalCalls);
        Assert.False(officeScope.AllowsStorageWrites);
        Assert.False(businessScope.AllowsProcessMutation);
        Assert.False(businessScope.AllowsWorkspaceWrites);
    }

    [Fact]
    public void Process_driver_contract_api_SB033_INV_001_runtime_alpha_remains_deferred_to_followup_decision()
    {
        var root = FindRepositoryRoot();
        var contractSource = ReadContractSource(root);
        var publicTypes = typeof(ProcessDriverPermissionMode).Assembly.GetExportedTypes();
        var version = ProcessDriverContractVersion.Current;

        Assert.Equal(new ProcessDriverContractVersion(1, 0, 0), version);
        Assert.Contains(ProcessDriverPermissionMode.ExecutionCapableFuture, Enum.GetValues<ProcessDriverPermissionMode>());
        AssertNoForbiddenProductionDriverRuntimeTokens(contractSource);
        Assert.DoesNotContain(publicTypes, type => type.IsInterface);
        Assert.DoesNotContain(publicTypes, type => type.Name.Contains("Host", StringComparison.Ordinal));
        Assert.DoesNotContain(publicTypes, type => type.Name.Contains("Provider", StringComparison.Ordinal));
        Assert.DoesNotContain(publicTypes, type => type.Name.Contains("Selector", StringComparison.Ordinal));
        Assert.DoesNotContain(publicTypes, type => type.Name.Contains("Registry", StringComparison.Ordinal));
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
                AllowsStorageWrites: false)
        ];
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
        return operation switch
        {
            ProcessDriverOperation.ExecuteCommand or ProcessDriverOperation.RestorePackage => ProcessDriverDenialReason.UnsafeCommand,
            ProcessDriverOperation.CallOfficeGraph => ProcessDriverDenialReason.ExternalCallDenied,
            _ => ProcessDriverDenialReason.MutationDenied
        };
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
    {
        return typeof(ProcessDriverPermissionMode)
            .Assembly
            .GetExportedTypes()
            .Select(type => type.FullName ?? type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
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
