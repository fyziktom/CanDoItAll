using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverContractPrerequisitesVerificationTests
{
    private const string BundleName = "process-driver-contract-prerequisites-verification-alpha-v1";

    [Fact]
    public void Process_driver_prerequisites_SB003_INV_001_preserve_baseline_branch_and_no_runtime_guardrails()
    {
        var root = FindRepositoryRoot();
        var branchName = RunGit(root, "branch", "--show-current").Single();
        var priorExecutionReport = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-core-evidence-descriptors-driver-contract-roadmap-v1",
            "reviews",
            "01-execution-report.md");
        var productionSource = ReadProductionSourceText(root);
        var changedFilesOutsideBundle = ReadGitChangedFilesOutsideCurrentBundle(root);

        Assert.Equal("maf-processes-refactor", branchName);
        Assert.Contains("SB042", priorExecutionReport, StringComparison.Ordinal);
        Assert.DoesNotContain("Final closure gate: `Not started`", priorExecutionReport, StringComparison.Ordinal);
        AssertNoProductionDriverRuntimeTokens(productionSource);
        Assert.All(changedFilesOutsideBundle, path =>
            Assert.False(IsUiOrMediaPath(path), $"Unexpected UI or media drift outside bundle: {path}"));
    }

    [Fact]
    public void Process_driver_prerequisites_SB006_INV_001_keep_core_public_api_governed_and_dependency_clean()
    {
        var root = FindRepositoryRoot();
        var coreProject = ReadRepositoryFile("src", "CanDoItAll.Processes.Core", "CanDoItAll.Processes.Core.csproj");
        var coreSource = ReadProcessCoreSource(root);
        var governanceDoc = ReadBundleFile("architecture", "05-core-api-governance.md");
        var publicApiSurface = ReadProcessCorePublicApiSurface();

        Assert.NotEmpty(publicApiSurface);
        Assert.All(publicApiSurface, line =>
            Assert.Contains("CanDoItAll.Processes.Core", line, StringComparison.Ordinal));
        Assert.Contains("owner classification", governanceDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("public Core change", governanceDoc, StringComparison.Ordinal);
        Assert.Contains("forbidden dependency scans", governanceDoc, StringComparison.Ordinal);
        Assert.Contains(@"<ProjectReference Include=""..\CanDoItAll.Processes.Contracts\CanDoItAll.Processes.Contracts.csproj"" />", coreProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", coreProject, StringComparison.OrdinalIgnoreCase);

        foreach (var forbiddenCoreToken in CreateForbiddenCoreDependencyTokens())
        {
            Assert.DoesNotContain(forbiddenCoreToken, coreSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Process_driver_prerequisites_SB009_INV_001_enforce_permission_modes_and_capability_denials()
    {
        var existingEvidenceId = Guid.NewGuid();
        var allOperations = Enum.GetValues<DriverOperation>();

        Assert.All(allOperations, operation =>
        {
            var decision = EvaluatePermission(
                DriverPermissionMode.Missing,
                DriverLane.DotnetRustTranscriptVerifier,
                operation,
                [existingEvidenceId]);

            AssertDenied(decision, DriverDenialReason.MissingPermissionMode);
        });

        AssertAccepted(EvaluatePermission(
            DriverPermissionMode.VerificationOnly,
            DriverLane.DotnetRustTranscriptVerifier,
            DriverOperation.InspectExistingEvidence,
            [existingEvidenceId]));
        AssertAccepted(EvaluatePermission(
            DriverPermissionMode.VerificationOnly,
            DriverLane.DotnetRustTranscriptVerifier,
            DriverOperation.ReturnDiagnostics,
            [existingEvidenceId]));
        AssertAccepted(EvaluatePermission(
            DriverPermissionMode.ManagerReadonly,
            DriverLane.RuntimeVerification,
            DriverOperation.ReadProcessFacts,
            [existingEvidenceId]));

        foreach (var operation in CreateSideEffectOperations())
        {
            AssertDenied(EvaluatePermission(
                DriverPermissionMode.VerificationOnly,
                DriverLane.DotnetRustTranscriptVerifier,
                operation,
                [existingEvidenceId]));
            AssertDenied(EvaluatePermission(
                DriverPermissionMode.ManagerReadonly,
                DriverLane.RuntimeVerification,
                operation,
                [existingEvidenceId]));
            AssertDenied(EvaluatePermission(
                DriverPermissionMode.ExecutionCapableFuture,
                DriverLane.RuntimeVerification,
                operation,
                [existingEvidenceId]));
        }

        AssertDenied(
            EvaluatePermission(
                DriverPermissionMode.VerificationOnly,
                DriverLane.DotnetRustTranscriptVerifier,
                DriverOperation.InspectExistingEvidence,
                []),
            DriverDenialReason.MissingEvidence);
    }

    [Fact]
    public void Process_driver_prerequisites_SB012_INV_001_capture_audit_facts_and_redact_sensitive_values()
    {
        var fact = BuildAuditFact(
            callerId: "manager:lucy",
            mode: DriverPermissionMode.VerificationOnly,
            lane: DriverLane.DotnetRustTranscriptVerifier,
            operation: DriverOperation.InspectExistingEvidence,
            evidenceIds: [Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")],
            denialReason: DriverDenialReason.None,
            diagnosticText: "Build warning was found in compiler transcript.",
            sensitiveDiagnosticText: "token=sk-live-secret user lucy@example.com");

        Assert.Equal("manager:lucy", fact.CallerId);
        Assert.Equal(DriverPermissionMode.VerificationOnly, fact.Mode);
        Assert.Equal(DriverLane.DotnetRustTranscriptVerifier, fact.Lane);
        Assert.Equal(DriverOperation.InspectExistingEvidence, fact.RequestedOperation);
        Assert.Equal(DriverRedactionStatus.Redacted, fact.RedactionStatus);
        Assert.Equal("Build warning was found in compiler transcript.", fact.DiagnosticSummary);
        Assert.DoesNotContain("sk-live-secret", fact.RedactedDiagnosticText, StringComparison.Ordinal);
        Assert.DoesNotContain("lucy@example.com", fact.RedactedDiagnosticText, StringComparison.Ordinal);
        Assert.Contains("[redacted-secret]", fact.RedactedDiagnosticText, StringComparison.Ordinal);
        Assert.Contains("[redacted-email]", fact.RedactedDiagnosticText, StringComparison.Ordinal);
        Assert.Matches("^[A-F0-9]{64}$", fact.OutputHash);
    }

    [Fact]
    public void Process_driver_prerequisites_SB015_INV_001_keep_command_and_sandbox_policy_denial_only()
    {
        var policy = CreateCurrentSandboxPolicy();
        var futurePolicy = CreateFutureSandboxPrerequisites();
        var evidenceId = Guid.NewGuid();
        var deniedOperations = new[]
        {
            DriverOperation.ExecuteCommand,
            DriverOperation.RestorePackage,
            DriverOperation.CallOfficeGraph,
            DriverOperation.WriteArtifact,
            DriverOperation.WriteWorkspaceStorage,
            DriverOperation.ApplyTransition,
            DriverOperation.ApplyFinalizer
        };

        Assert.False(policy.CommandExecutionAllowed);
        Assert.False(policy.GraphOrOfficeCallsAllowed);
        Assert.False(policy.WorkspaceWritesAllowed);
        Assert.False(policy.StorageWritesAllowed);
        Assert.False(policy.ProcessMutationAllowed);
        Assert.All(deniedOperations, operation =>
            AssertDenied(EvaluatePermission(
                DriverPermissionMode.VerificationOnly,
                DriverLane.DotnetRustTranscriptVerifier,
                operation,
                [evidenceId])));
        Assert.Contains(SandboxRequirement.CommandAllowlist, futurePolicy);
        Assert.Contains(SandboxRequirement.WorkingDirectoryPolicy, futurePolicy);
        Assert.Contains(SandboxRequirement.Timeout, futurePolicy);
        Assert.Contains(SandboxRequirement.OutputCaptureHash, futurePolicy);
        Assert.Contains(SandboxRequirement.NetworkPolicy, futurePolicy);
        Assert.Contains(SandboxRequirement.FileSystemPolicy, futurePolicy);
        Assert.Contains(SandboxRequirement.SecretMasking, futurePolicy);
        Assert.Contains(SandboxRequirement.FailureSemantics, futurePolicy);
    }

    [Fact]
    public void Process_driver_prerequisites_SB018_INV_001_rehearse_verification_contract_without_production_runtime_api()
    {
        var root = FindRepositoryRoot();
        var request = new VerificationEvidenceRequest(
            DriverPermissionMode.VerificationOnly,
            DriverLane.DotnetRustTranscriptVerifier,
            "process-run-1",
            "step-run-1",
            [Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")],
            DriverOperation.ReturnDiagnostics,
            "manager:lucy");
        var response = EvaluateVerificationRequest(request);
        var productionSource = ReadProductionSourceText(root);
        var rehearsalDoc = ReadBundleFile("architecture", "03-verification-only-driver-contract-rehearsal.md");

        Assert.True(response.Accepted);
        Assert.Equal(DriverDenialReason.None, response.DenialReason);
        Assert.True(response.NoMutationPerformed);
        Assert.Contains("No mutation performed flag", rehearsalDoc, StringComparison.Ordinal);
        Assert.DoesNotContain("public interface IProcessDriver", rehearsalDoc, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", rehearsalDoc, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", rehearsalDoc, StringComparison.Ordinal);
        AssertNoProductionDriverRuntimeTokens(productionSource);
    }

    [Fact]
    public void Process_driver_prerequisites_SB021_INV_001_make_dotnet_rust_transcript_lane_readonly()
    {
        var evidenceId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var diagnostics = InspectTranscript(new TranscriptInspectionRequest(
            DriverLane.DotnetRustTranscriptVerifier,
            "CSC : warning CS8618: Non-nullable property must contain a non-null value.\nTest Failed: Expected true.\nTargetFramework net6.0 is unsupported.\nMissing artifact proof/SB003/manifest.md.",
            DriverOperation.InspectExistingEvidence,
            [evidenceId]));

        Assert.Contains(diagnostics, diagnostic => diagnostic.Kind == TranscriptDiagnosticKind.BuildWarning);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Kind == TranscriptDiagnosticKind.TestFailure);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Kind == TranscriptDiagnosticKind.UnsupportedTargetFramework);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Kind == TranscriptDiagnosticKind.MissingArtifact);
        AssertDenied(EvaluatePermission(
            DriverPermissionMode.VerificationOnly,
            DriverLane.DotnetRustTranscriptVerifier,
            DriverOperation.ExecuteCommand,
            [evidenceId]),
            DriverDenialReason.UnsafeCommand);
        AssertDenied(EvaluatePermission(
            DriverPermissionMode.VerificationOnly,
            DriverLane.DotnetRustTranscriptVerifier,
            DriverOperation.WriteWorkspaceStorage,
            [evidenceId]),
            DriverDenialReason.MutationDenied);
    }

    [Fact]
    public void Process_driver_prerequisites_SB024_INV_001_keep_core_descriptor_consumers_allowlisted()
    {
        var root = FindRepositoryRoot();
        var dispatchRoot = Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch");
        var allowedCoreConsumers = CreateAllowedDispatchProcessCoreConsumerFiles();
        var unapprovedCoreConsumers = Directory
            .EnumerateFiles(dispatchRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => File.ReadAllText(path).Contains("CanDoItAll.Processes.Core", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Where(name => name is not null && !allowedCoreConsumers.Contains(name))
            .Select(name => name!)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(unapprovedCoreConsumers);
        Assert.Contains("ProcessExecutionEvidenceDescriptorAdapter.cs", allowedCoreConsumers);
        Assert.Contains("ProcessFinalizerEvidenceDescriptorAdapter.cs", allowedCoreConsumers);
        Assert.Contains("ProcessArtifactProjectionEvidenceDescriptorAdapter.cs", allowedCoreConsumers);
        Assert.Contains("ProcessArtifactValidationDescriptorAdapter.cs", allowedCoreConsumers);
    }

    [Fact]
    public void Process_driver_prerequisites_SB027_INV_001_keep_office_and_business_lanes_readonly()
    {
        var evidenceId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var officeDeniedOperations = new[]
        {
            DriverOperation.CallOfficeGraph,
            DriverOperation.MutateEmailCategory,
            DriverOperation.CreateTask,
            DriverOperation.WriteArtifact
        };
        var businessDeniedOperations = new[]
        {
            DriverOperation.MutateBusinessRecord,
            DriverOperation.CreateTask,
            DriverOperation.ApplyTransition,
            DriverOperation.WriteWorkspaceStorage
        };

        AssertAccepted(EvaluatePermission(
            DriverPermissionMode.VerificationOnly,
            DriverLane.OfficeEvidence,
            DriverOperation.InspectExistingEvidence,
            [evidenceId]));
        AssertAccepted(EvaluatePermission(
            DriverPermissionMode.VerificationOnly,
            DriverLane.BusinessAnalysis,
            DriverOperation.ReturnDiagnostics,
            [evidenceId]));
        Assert.All(officeDeniedOperations, operation =>
            AssertDenied(EvaluatePermission(
                DriverPermissionMode.VerificationOnly,
                DriverLane.OfficeEvidence,
                operation,
                [evidenceId])));
        Assert.All(businessDeniedOperations, operation =>
            AssertDenied(EvaluatePermission(
                DriverPermissionMode.VerificationOnly,
                DriverLane.BusinessAnalysis,
                operation,
                [evidenceId])));
    }

    [Fact]
    public void Process_driver_prerequisites_SB030_INV_001_defer_production_driver_contract_until_all_prerequisites_are_green()
    {
        var root = FindRepositoryRoot();
        var decisionDoc = ReadBundleFile("architecture", "06-production-driver-contract-decision-template.md");
        var productionSource = ReadProductionSourceText(root);
        var prerequisiteResults = new[]
        {
            PrerequisiteResult.Pass("permission modes have executable negative tests"),
            PrerequisiteResult.Pass("audit facts and redaction are tested"),
            PrerequisiteResult.Pass("sandbox and command denial policy is tested"),
            PrerequisiteResult.Pass("verification-only cannot mutate process state"),
            PrerequisiteResult.Fail("production contract owner has not approved a follow-up bundle")
        };

        Assert.Equal(ProductionDriverContractDecision.Defer, DecideProductionDriverContract(prerequisiteResults));
        Assert.Contains("Default", decisionDoc, StringComparison.Ordinal);
        Assert.Contains("Defer", decisionDoc, StringComparison.Ordinal);
        AssertNoProductionDriverRuntimeTokens(productionSource);
    }

    [Fact]
    public void Process_driver_prerequisites_SB033_INV_001_document_core_package_rules_without_broad_runtime_ownership()
    {
        var targetSolution = ReadBundleFile("architecture", "01-target-solution.md");
        var governanceDoc = ReadBundleFile("architecture", "05-core-api-governance.md");
        var roadmap = ReadBundleFile("analysis", "03-roadmap-to-stable-core-and-drivers.md");

        Assert.Contains("deterministic descriptors", targetSolution, StringComparison.Ordinal);
        Assert.Contains("forbidden dependency", governanceDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Every new public Core type/member requires owner classification", governanceDoc, StringComparison.Ordinal);
        Assert.Contains("Remaining: API versioning, compatibility docs, descriptor governance", roadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("Core owns process mutation", targetSolution + governanceDoc + roadmap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Core owns finalizer application", targetSolution + governanceDoc + roadmap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Core owns runtime dispatch", targetSolution + governanceDoc + roadmap, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Process_driver_prerequisites_SB036_INV_001_keep_domain_driver_roadmap_consistent_with_deferred_runtime()
    {
        var laneRoadmap = ReadBundleFile("architecture", "04-domain-driver-lane-roadmap.md");
        var longRangeRoadmap = ReadBundleFile("analysis", "03-roadmap-to-stable-core-and-drivers.md");
        var decisionTemplate = ReadBundleFile("architecture", "06-production-driver-contract-decision-template.md");
        var combined = string.Join(Environment.NewLine, laneRoadmap, longRangeRoadmap, decisionTemplate);

        Assert.Contains(".NET/Rust Transcript Verifier", laneRoadmap, StringComparison.Ordinal);
        Assert.Contains("existing build/test/proof transcripts", laneRoadmap, StringComparison.Ordinal);
        Assert.Contains("Milestone 3: Production Driver Contracts", longRangeRoadmap, StringComparison.Ordinal);
        Assert.Contains("Future bundle only after this bundle passes", longRangeRoadmap, StringComparison.Ordinal);
        Assert.Contains("Default", decisionTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("Implement production driver runtime in this bundle", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Run shell commands", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Workspace write allowed", combined, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_driver_prerequisites_SB039_INV_001_keep_final_report_rows_separate_and_source_free_of_stubs()
    {
        var root = FindRepositoryRoot();
        var executionReport = ReadBundleFile("reviews", "01-execution-report.md");
        var gateSection = ExtractMarkdownSection(executionReport, "## Subbundle Gate Results");
        var productionSource = ReadProductionSourceText(root);
        var subbundleRows = Enumerable
            .Range(1, 39)
            .Select(number => $"| SB{number:000} |")
            .ToArray();

        Assert.All(subbundleRows, rowPrefix =>
            Assert.Contains(rowPrefix, gateSection, StringComparison.Ordinal));
        Assert.DoesNotContain("SB001-SB039 |", gateSection, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"(?im)^\s*//\s*TODO\b|TODO\s*:", productionSource);
        Assert.DoesNotContain("NotImplementedException", productionSource, StringComparison.Ordinal);
        AssertNoProductionDriverRuntimeTokens(productionSource);
    }

    private static PermissionDecision EvaluatePermission(
        DriverPermissionMode mode,
        DriverLane lane,
        DriverOperation operation,
        IReadOnlyCollection<Guid> evidenceIds)
    {
        if (mode == DriverPermissionMode.Missing)
        {
            return PermissionDecision.Denied(DriverDenialReason.MissingPermissionMode);
        }

        if (mode == DriverPermissionMode.ExecutionCapableFuture)
        {
            return PermissionDecision.Denied(DriverDenialReason.UnsupportedMode);
        }

        if (CreateSideEffectOperations().Contains(operation))
        {
            return PermissionDecision.Denied(ResolveSideEffectDenial(operation));
        }

        if ((operation is DriverOperation.InspectExistingEvidence or DriverOperation.ReturnDiagnostics) && evidenceIds.Count == 0)
        {
            return PermissionDecision.Denied(DriverDenialReason.MissingEvidence);
        }

        if (mode == DriverPermissionMode.VerificationOnly &&
            operation is DriverOperation.InspectExistingEvidence or DriverOperation.ReturnDiagnostics)
        {
            return PermissionDecision.Allow();
        }

        if (mode == DriverPermissionMode.ManagerReadonly &&
            operation is DriverOperation.ReadProcessFacts or DriverOperation.ExplainDenial or DriverOperation.InspectExistingEvidence)
        {
            return PermissionDecision.Allow();
        }

        return PermissionDecision.Denied(DriverDenialReason.UnsupportedOperation);
    }

    private static VerificationEvidenceResponse EvaluateVerificationRequest(VerificationEvidenceRequest request)
    {
        var decision = EvaluatePermission(
            request.Mode,
            request.Lane,
            request.RequestedOperation,
            request.EvidenceIds.ToHashSet());

        return new VerificationEvidenceResponse(
            decision.Accepted,
            decision.DenialReason,
            decision.Accepted ? "Diagnostics returned from existing evidence." : "Request denied before any side effect.",
            request.EvidenceIds,
            DriverRedactionStatus.None,
            NoMutationPerformed: true);
    }

    private static IReadOnlyList<TranscriptDiagnostic> InspectTranscript(TranscriptInspectionRequest request)
    {
        var decision = EvaluatePermission(
            DriverPermissionMode.VerificationOnly,
            request.Lane,
            request.AttemptedOperation,
            request.EvidenceIds.ToHashSet());
        if (!decision.Accepted)
        {
            return [new TranscriptDiagnostic(TranscriptDiagnosticKind.RuntimeProofGap, "Inspection request was denied.")];
        }

        var diagnostics = new List<TranscriptDiagnostic>();
        AddDiagnosticWhen(
            request.TranscriptText.Contains("warning", StringComparison.OrdinalIgnoreCase),
            TranscriptDiagnosticKind.BuildWarning,
            "Build warning found.",
            diagnostics);
        AddDiagnosticWhen(
            request.TranscriptText.Contains("error", StringComparison.OrdinalIgnoreCase),
            TranscriptDiagnosticKind.BuildError,
            "Build error found.",
            diagnostics);
        AddDiagnosticWhen(
            request.TranscriptText.Contains("test failed", StringComparison.OrdinalIgnoreCase),
            TranscriptDiagnosticKind.TestFailure,
            "Test failure found.",
            diagnostics);
        AddDiagnosticWhen(
            request.TranscriptText.Contains("missing artifact", StringComparison.OrdinalIgnoreCase),
            TranscriptDiagnosticKind.MissingArtifact,
            "Referenced artifact is missing.",
            diagnostics);
        AddDiagnosticWhen(
            request.TranscriptText.Contains("unsupported", StringComparison.OrdinalIgnoreCase),
            TranscriptDiagnosticKind.UnsupportedTargetFramework,
            "Unsupported target framework found.",
            diagnostics);

        return diagnostics;
    }

    private static void AddDiagnosticWhen(
        bool condition,
        TranscriptDiagnosticKind kind,
        string message,
        ICollection<TranscriptDiagnostic> diagnostics)
    {
        if (condition)
        {
            diagnostics.Add(new TranscriptDiagnostic(kind, message));
        }
    }

    private static DriverAuditFact BuildAuditFact(
        string callerId,
        DriverPermissionMode mode,
        DriverLane lane,
        DriverOperation operation,
        IReadOnlyList<Guid> evidenceIds,
        DriverDenialReason denialReason,
        string diagnosticText,
        string sensitiveDiagnosticText)
    {
        var redactedDiagnosticText = RedactSensitiveText(sensitiveDiagnosticText);
        var redactionStatus = string.Equals(redactedDiagnosticText, sensitiveDiagnosticText, StringComparison.Ordinal)
            ? DriverRedactionStatus.None
            : DriverRedactionStatus.Redacted;

        return new DriverAuditFact(
            callerId,
            mode,
            lane,
            "process-run-1",
            "step-run-1",
            operation,
            evidenceIds,
            denialReason,
            diagnosticText,
            redactedDiagnosticText,
            ComputeSha256(diagnosticText),
            redactionStatus);
    }

    private static string RedactSensitiveText(string value)
    {
        var redacted = Regex.Replace(
            value,
            @"(?i)\b(token|password|secret|connectionstring|connection string)\s*[:=]\s*[^;\s]+",
            "[redacted-secret]");

        return Regex.Replace(
            redacted,
            @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
            "[redacted-email]",
            RegexOptions.IgnoreCase);
    }

    private static string ComputeSha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static IReadOnlySet<DriverOperation> CreateSideEffectOperations()
    {
        return new HashSet<DriverOperation>
        {
            DriverOperation.MutateProcessState,
            DriverOperation.ExecuteCommand,
            DriverOperation.RestorePackage,
            DriverOperation.WriteArtifact,
            DriverOperation.WriteWorkspaceStorage,
            DriverOperation.CallOfficeGraph,
            DriverOperation.MutateEmailCategory,
            DriverOperation.CreateTask,
            DriverOperation.MutateBusinessRecord,
            DriverOperation.ApplyTransition,
            DriverOperation.ClaimDispatch,
            DriverOperation.ApplyFinalizer,
            DriverOperation.ScheduleRetry
        };
    }

    private static DriverDenialReason ResolveSideEffectDenial(DriverOperation operation)
    {
        return operation switch
        {
            DriverOperation.ExecuteCommand or DriverOperation.RestorePackage => DriverDenialReason.UnsafeCommand,
            DriverOperation.CallOfficeGraph => DriverDenialReason.ExternalCallDenied,
            _ => DriverDenialReason.MutationDenied
        };
    }

    private static CurrentSandboxPolicy CreateCurrentSandboxPolicy()
    {
        return new CurrentSandboxPolicy(
            CommandExecutionAllowed: false,
            GraphOrOfficeCallsAllowed: false,
            WorkspaceWritesAllowed: false,
            StorageWritesAllowed: false,
            ProcessMutationAllowed: false);
    }

    private static IReadOnlySet<SandboxRequirement> CreateFutureSandboxPrerequisites()
    {
        return Enum.GetValues<SandboxRequirement>().ToHashSet();
    }

    private static ProductionDriverContractDecision DecideProductionDriverContract(
        IReadOnlyList<PrerequisiteResult> prerequisiteResults)
    {
        return prerequisiteResults.All(result => result.Passed)
            ? ProductionDriverContractDecision.ApproveContractOnlyFollowUpBundle
            : ProductionDriverContractDecision.Defer;
    }

    private static void AssertAccepted(PermissionDecision decision)
    {
        Assert.True(decision.Accepted, $"Expected accepted decision but got {decision.DenialReason}.");
        Assert.Equal(DriverDenialReason.None, decision.DenialReason);
    }

    private static void AssertDenied(
        PermissionDecision decision,
        DriverDenialReason? expectedReason = null)
    {
        Assert.False(decision.Accepted);
        if (expectedReason is not null)
        {
            Assert.Equal(expectedReason, decision.DenialReason);
        }
    }

    private static IReadOnlyList<string> ReadProcessCorePublicApiSurface()
    {
        var assembly = typeof(CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePipeline).Assembly;
        var lines = new List<string>();
        foreach (var type in assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            lines.Add($"Type:{type.FullName}");
            if (type.IsEnum)
            {
                foreach (var name in Enum.GetNames(type))
                {
                    lines.Add($"Enum:{type.FullName}.{name}");
                }

                continue;
            }

            const BindingFlags constructorFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            const BindingFlags memberFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var constructor in type.GetConstructors(constructorFlags))
            {
                lines.Add($"Constructor:{type.FullName}({FormatApiParameters(constructor)})");
            }

            foreach (var property in type.GetProperties(memberFlags).OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                lines.Add($"Property:{type.FullName}.{property.Name}:{FormatApiType(property.PropertyType)}");
            }

            foreach (var method in type
                .GetMethods(memberFlags)
                .Where(method => !method.IsSpecialName)
                .Where(method => method.Name is not "ToString" and not "GetHashCode" and not "Equals" and not "Deconstruct" and not "<Clone>$")
                .OrderBy(method => method.Name, StringComparer.Ordinal)
                .ThenBy(FormatApiParameters, StringComparer.Ordinal))
            {
                lines.Add($"Method:{type.FullName}.{method.Name}({FormatApiParameters(method)}):{FormatApiType(method.ReturnType)}");
            }
        }

        return lines;
    }

    private static string FormatApiParameters(MethodBase method)
    {
        return string.Join(",", method.GetParameters().Select(parameter => FormatApiType(parameter.ParameterType)));
    }

    private static string FormatApiType(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var name = type.GetGenericTypeDefinition().FullName ?? type.GetGenericTypeDefinition().Name;
        var tickIndex = name.IndexOf('`', StringComparison.Ordinal);
        if (tickIndex >= 0)
        {
            name = name[..tickIndex];
        }

        return $"{name}<{string.Join(",", type.GetGenericArguments().Select(FormatApiType))}>";
    }

    private static string ReadProcessCoreSource(string repositoryRoot)
    {
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.Combine(repositoryRoot, "src", "CanDoItAll.Processes.Core"), "*.cs", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadProductionSourceText(string repositoryRoot)
    {
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.*", SearchOption.AllDirectories)
                .Where(static path =>
                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string[] CreateForbiddenCoreDependencyTokens()
    {
        return
        [
            "CanDoItAll.Modules.",
            "CanDoItAll.Infrastructure",
            "CanDoItAll.AgentFramework",
            "Microsoft.EntityFrameworkCore",
            "DbContext",
            "IDbContextFactory",
            "IWorkspace",
            "WorkspacePathResolver",
            "IStorage",
            "StoragePlacement",
            "File.",
            "Directory.",
            "AgentFramework",
            "Maf",
            "ProcessRunAutomationDispatchService",
            "IProcessDriver",
            "DriverPack",
            "DriverRegistry",
            "IServiceProvider",
            "IServiceScopeFactory",
            "ILogger<"
        ];
    }

    private static void AssertNoProductionDriverRuntimeTokens(string productionSource)
    {
        foreach (var forbiddenToken in CreateForbiddenProductionDriverRuntimeTokens())
        {
            Assert.DoesNotContain(forbiddenToken, productionSource, StringComparison.Ordinal);
        }
    }

    private static string[] CreateForbiddenProductionDriverRuntimeTokens()
    {
        return
        [
            "IProcessDriverPack",
            "IProcessDriverRegistry",
            "ProcessDriverRegistry",
            "ProcessDriverPack",
            "ProcessDriverRuntimeSelector",
            "ProcessDriverManagerCommand",
            "ProcessDriverRuntime",
            "ProcessDriverProvider",
            "AddProcessDriver",
            "MapProcessDriver"
        ];
    }

    private static HashSet<string> CreateAllowedDispatchProcessCoreConsumerFiles()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ProcessArtifactExpectationMatcher.cs",
            "ProcessArtifactExpectationSatisfactionAdapter.cs",
            "ProcessArtifactProjectionEvidenceDescriptorAdapter.cs",
            "ProcessArtifactRecordedSatisfactionRules.cs",
            "ProcessArtifactValidationDescriptorAdapter.cs",
            "ProcessCoreArtifactModelAdapters.cs",
            "ProcessDispatchCandidateHeaderSelector.cs",
            "ProcessDispatchCandidateHydrationLoader.cs",
            "ProcessDispatchRouteExecutionModels.cs",
            "ProcessDispatchRouteFacets.cs",
            "ProcessDispatchRouteHandlerPipeline.cs",
            "ProcessDispatchRouteHandlers.cs",
            "ProcessDispatchRouteModelAdapters.cs",
            "ProcessDispatchRunClosureGuardService.cs",
            "ProcessDispatchStartTransitionPlanner.cs",
            "ProcessExecutionEvidenceDescriptorAdapter.cs",
            "ProcessFinalizerEvidenceDescriptorAdapter.cs",
            "ProcessRetryDiagnosticDescriptorAdapter.cs",
            "ProcessRunAutomationDispatchService.Concurrency.cs",
            "ProcessSubprocessArtifactSourceResolver.cs",
            "ProcessSubprocessLifecycleRules.cs",
            "ProcessTransitionIntentAdapters.cs"
        };
    }

    private static IReadOnlyList<string> ReadGitChangedFilesOutsideCurrentBundle(string repositoryRoot)
    {
        var tracked = RunGit(
            repositoryRoot,
            "diff",
            "--name-only",
            "--",
            ".",
            $":(exclude)codex/bundles/{BundleName}");
        var untracked = RunGit(
            repositoryRoot,
            "ls-files",
            "--others",
            "--exclude-standard",
            "--",
            ".",
            $":(exclude)codex/bundles/{BundleName}");

        return tracked
            .Concat(untracked)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> RunGit(string repositoryRoot, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), $"Failed to start git {string.Join(" ", arguments)}.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        process.WaitForExit();
        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();

        Assert.True(
            process.ExitCode == 0,
            $"git {string.Join(" ", arguments)} failed with exit code {process.ExitCode}: {error}");

        return output
            .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static bool IsUiOrMediaPath(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        var extension = Path.GetExtension(normalizedPath);
        var forbiddenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".razor",
            ".css",
            ".scss",
            ".js",
            ".ts",
            ".tsx",
            ".jsx",
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".webp",
            ".svg"
        };
        var forbiddenTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet"
        };

        return forbiddenExtensions.Contains(extension) ||
            forbiddenTokens.Any(token => normalizedPath.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadBundleFile(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([FindRepositoryRoot(), "codex", "bundles", BundleName, .. pathParts]));
    }

    private static string ExtractMarkdownSection(string content, string heading)
    {
        var startIndex = content.IndexOf(heading, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            throw new InvalidOperationException($"Could not find markdown heading '{heading}'.");
        }

        var sectionStart = startIndex + heading.Length;
        var remainingContent = content[sectionStart..];
        var nextHeading = Regex.Match(remainingContent, @"\r?\n## ");

        return nextHeading.Success
            ? remainingContent[..nextHeading.Index]
            : remainingContent;
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

    private enum DriverPermissionMode
    {
        Missing,
        VerificationOnly,
        ManagerReadonly,
        ExecutionCapableFuture
    }

    private enum DriverLane
    {
        DotnetRustTranscriptVerifier,
        RuntimeVerification,
        OfficeEvidence,
        BusinessAnalysis
    }

    private enum DriverOperation
    {
        InspectExistingEvidence,
        ReturnDiagnostics,
        ReadProcessFacts,
        ExplainDenial,
        MutateProcessState,
        ExecuteCommand,
        RestorePackage,
        WriteArtifact,
        WriteWorkspaceStorage,
        CallOfficeGraph,
        MutateEmailCategory,
        CreateTask,
        MutateBusinessRecord,
        ApplyTransition,
        ClaimDispatch,
        ApplyFinalizer,
        ScheduleRetry
    }

    private enum DriverDenialReason
    {
        None,
        MissingPermissionMode,
        UnsupportedMode,
        UnsupportedOperation,
        UnsafeCommand,
        ExternalCallDenied,
        MutationDenied,
        MissingEvidence
    }

    private enum DriverRedactionStatus
    {
        None,
        Redacted
    }

    private enum SandboxRequirement
    {
        CommandAllowlist,
        WorkingDirectoryPolicy,
        Timeout,
        OutputCaptureHash,
        NetworkPolicy,
        FileSystemPolicy,
        SecretMasking,
        FailureSemantics
    }

    private enum TranscriptDiagnosticKind
    {
        BuildWarning,
        BuildError,
        TestFailure,
        MissingArtifact,
        UnsupportedTargetFramework,
        RuntimeProofGap
    }

    private enum ProductionDriverContractDecision
    {
        Defer,
        ApproveContractOnlyFollowUpBundle
    }

    private sealed record PermissionDecision(
        bool Accepted,
        DriverDenialReason DenialReason)
    {
        public static PermissionDecision Allow()
        {
            return new PermissionDecision(true, DriverDenialReason.None);
        }

        public static PermissionDecision Denied(DriverDenialReason denialReason)
        {
            return new PermissionDecision(false, denialReason);
        }
    }

    private sealed record DriverAuditFact(
        string CallerId,
        DriverPermissionMode Mode,
        DriverLane Lane,
        string ProcessRunId,
        string StepRunId,
        DriverOperation RequestedOperation,
        IReadOnlyList<Guid> InspectedEvidenceIds,
        DriverDenialReason DenialReason,
        string DiagnosticSummary,
        string RedactedDiagnosticText,
        string OutputHash,
        DriverRedactionStatus RedactionStatus);

    private sealed record CurrentSandboxPolicy(
        bool CommandExecutionAllowed,
        bool GraphOrOfficeCallsAllowed,
        bool WorkspaceWritesAllowed,
        bool StorageWritesAllowed,
        bool ProcessMutationAllowed);

    private sealed record VerificationEvidenceRequest(
        DriverPermissionMode Mode,
        DriverLane Lane,
        string ProcessRunId,
        string StepRunId,
        IReadOnlyList<Guid> EvidenceIds,
        DriverOperation RequestedOperation,
        string CallerContext);

    private sealed record VerificationEvidenceResponse(
        bool Accepted,
        DriverDenialReason DenialReason,
        string DiagnosticSummary,
        IReadOnlyList<Guid> EvidenceReferences,
        DriverRedactionStatus RedactionStatus,
        bool NoMutationPerformed);

    private sealed record TranscriptInspectionRequest(
        DriverLane Lane,
        string TranscriptText,
        DriverOperation AttemptedOperation,
        IReadOnlyList<Guid> EvidenceIds);

    private sealed record TranscriptDiagnostic(
        TranscriptDiagnosticKind Kind,
        string Message);

    private sealed record PrerequisiteResult(
        string Name,
        bool Passed)
    {
        public static PrerequisiteResult Pass(string name)
        {
            return new PrerequisiteResult(name, true);
        }

        public static PrerequisiteResult Fail(string name)
        {
            return new PrerequisiteResult(name, false);
        }
    }
}
