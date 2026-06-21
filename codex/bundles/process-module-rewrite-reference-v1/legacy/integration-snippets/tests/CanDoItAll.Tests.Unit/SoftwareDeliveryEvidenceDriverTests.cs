using CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence;

namespace CanDoItAll.Tests.Unit;

public sealed class SoftwareDeliveryEvidenceDriverTests
{
    [Fact]
    public void SoftwareDelivery_evidence_package_is_solution_bound_and_runtime_free()
    {
        var root = FindRepositoryRoot();
        var solution = ReadRepositoryFile("CanDoItAll.slnx");
        var project = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence",
            "CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence.csproj");
        var source = ReadProjectSource(root);

        Assert.Contains(
            "src/CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence/CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence.csproj",
            solution,
            StringComparison.Ordinal);
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
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRuntimeSelector", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverManagerCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverHost", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SoftwareDelivery_evidence_policy_preserves_caller_supplied_carried_proof()
    {
        var carriedProof = new SoftwareDeliveryCarriedProofSnapshot(
            HasCarriedConcreteImplementationProof: true,
            HasCarriedRunnableApplicationProof: true,
            HasCarriedConcreteProductMutation: false,
            SourceRunId: "previous-run",
            Summary: "Prior execution supplied concrete implementation and runnable proof.");
        var request = CreatePolicyRequest(carriedProof, requiresConcreteImplementationProof: false);

        var result = SoftwareDeliveryEvidencePolicy.Evaluate(request);

        Assert.False(result.HasMissingProof);
        Assert.Equal(SoftwareDeliveryImplementationStack.NonSoftware, result.Stack);
        Assert.Equal(carriedProof, result.CarriedProof);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void SoftwareDelivery_guidance_policy_emits_migrated_prompt_fragments()
    {
        var contract = CreateContract() with
        {
            ContractText = "Implement a .NET Blazor browser app with tests.",
            RequiresConcreteBrowserProof = true
        };
        var executionRequest = new SoftwareDeliveryExecutionGuidanceRequest(
            contract,
            HasProjectStructureExecutionContext: true,
            HasGroundedExternalTarget: false,
            GroundedExternalAbsolutePath: string.Empty,
            GroundedExternalMappedAlias: string.Empty,
            UsesGroundedExternalArtifactDestination: false,
            AllowsExternalTargetMutation: false,
            HasGroundedExternalScaffoldTarget: false,
            GroundedExternalParentAlias: string.Empty,
            GroundedExternalLeafName: string.Empty,
            HasBrowserSurfaceSignalWithoutProof: false,
            CurrentRunManagedArtifactRoot: "artifacts/process-runs/current",
            CurrentRunManagedOutputRoot: "output/process-runs/current");
        var recoveryRequest = new SoftwareDeliveryRecoveryGuidanceRequest(
            contract,
            HasProjectStructureGrounding: true,
            HasMissingRunnableApplicationProof: true,
            CurrentRunManagedArtifactRoot: "artifacts/process-runs/current");

        var executionResult = SoftwareDeliveryGuidancePolicy.CreateExecutionGuidance(executionRequest);
        var recoveryResult = SoftwareDeliveryGuidancePolicy.CreateRecoveryGuidance(recoveryRequest);
        var executionLines = string.Join(Environment.NewLine, executionResult.MandatoryBrowserProofPlanLines
            .Concat(executionResult.ImplementationProofLines)
            .Concat(executionResult.BrowserProofLines));
        var recoveryLines = string.Join(Environment.NewLine, recoveryResult.ImplementationGuidanceLines
            .Concat(recoveryResult.BrowserGuidanceLines));

        Assert.Contains("workspace_dotnet_run", executionLines, StringComparison.Ordinal);
        Assert.Contains("Blazor", executionLines, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_run", recoveryLines, StringComparison.Ordinal);
    }

    [Fact]
    public void SoftwareDelivery_evidence_policy_accepts_realistic_dotnet_receipts()
    {
        var request = CreatePolicyRequest(
            SoftwareDeliveryEvidencePolicy.EmptyCarriedProof,
            CreateRealisticDotNetReceiptTimeline(),
            contractText: "Implement a .NET Blazor browser app in ProductApp.csproj.");

        var result = SoftwareDeliveryEvidencePolicy.Evaluate(request);

        Assert.False(result.HasMissingProof);
        Assert.Equal(SoftwareDeliveryImplementationStack.DotNet, result.Stack);
        Assert.True(result.HasSuccessfulConcreteProductMutation);
        Assert.True(result.HasConcreteImplementationProofEvidence);
        Assert.True(result.HasRunnableApplicationProofEvidence);
        Assert.NotNull(result.LatestConcreteProductReadReceipt);
        Assert.NotNull(result.LatestConcreteProductMutationReceipt);
        Assert.NotNull(result.LatestImplementationValidationReceipt);
    }

    [Fact]
    public void SoftwareDelivery_evidence_policy_rejects_metadata_only_proof_snapshots()
    {
        var request = CreatePolicyRequest(
            SoftwareDeliveryEvidencePolicy.EmptyCarriedProof,
            receiptSnapshots: [],
            contractText: "Implement a .NET Blazor browser app in ProductApp.csproj.")
            with
            {
                PathFacts = new SoftwareDeliveryPathFacts(
                    WorkspacePaths: ["external-target/C/app/Program.cs"],
                    OutputFiles: ["external-target/C/app/bin/Debug/app.dll"],
                    ExpectedArtifactPaths: ["artifacts/process-runs/current/browser-proof.json"],
                    ManagedArtifactRoots: ["artifacts/process-runs/current"],
                    ManagedOutputRoots: ["output/process-runs/current"]),
                ExpectedArtifacts =
                [
                    new SoftwareDeliveryArtifactExpectationSnapshot(
                        Id: "browser-proof",
                        Title: "Browser proof",
                        IsRequired: true,
                        ValidationRequirementSummary: "Write browser proof to artifacts/process-runs/current/browser-proof.json.",
                        ExpectedPath: "artifacts/process-runs/current/browser-proof.json",
                        ArtifactKind: "Evidence")
                ],
                ArtifactRecords =
                [
                    new SoftwareDeliveryArtifactRecordSnapshot(
                        Id: "artifact",
                        DisplayName: "Browser proof",
                        RelativePath: "artifacts/process-runs/current/browser-proof.json",
                        ContentType: "application/json",
                        ProducedBy: "agent",
                        Summary: "Claimed proof without receipts.",
                        CreatedAtUtc: new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero))
                ],
                BrowserEvidence = new SoftwareDeliveryBrowserEvidenceSnapshot(
                    BrowserProofRequired: true,
                    HasCurrentRunBrowserEvidence: true,
                    HasConsoleErrorEvidence: false,
                    Routes: ["http://localhost:5000"],
                    ArtifactPaths: ["artifacts/process-runs/current/browser-proof.json"],
                    Summary: "Browser output exists.")
            };

        var result = SoftwareDeliveryEvidencePolicy.Evaluate(request);

        Assert.True(result.HasMissingProof);
        Assert.Equal(
            "the current attempt did not read any concrete product source or project file",
            result.MissingConcreteImplementationProofSummary);
        Assert.False(result.HasSuccessfulConcreteProductMutation);
        Assert.False(result.HasConcreteImplementationProofEvidence);
        Assert.False(result.HasRunnableApplicationProofEvidence);
    }

    private static SoftwareDeliveryProofPolicyRequest CreatePolicyRequest(
        SoftwareDeliveryCarriedProofSnapshot carriedProof,
        IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot>? receiptSnapshots = null,
        bool requiresConcreteImplementationProof = true,
        string contractText = "Implement the requested product behavior.")
    {
        var toolReceipts = receiptSnapshots ?? [];
        return new SoftwareDeliveryProofPolicyRequest(
            CreateContract(requiresConcreteImplementationProof, contractText),
            new SoftwareDeliveryPathFacts(
                WorkspacePaths: toolReceipts
                    .SelectMany(receipt => receipt.WorkspacePaths)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                OutputFiles: toolReceipts
                    .SelectMany(receipt => receipt.OutputFiles)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ExpectedArtifactPaths: [],
                ManagedArtifactRoots: ["artifacts/process-runs/current"],
                ManagedOutputRoots: ["output/process-runs/current"]),
            new SoftwareDeliveryExternalTargetSnapshot(
                AllowedAliases: [],
                GroundedMappedAlias: string.Empty,
                GroundedAbsolutePath: string.Empty,
                HasGroundedTarget: false,
                HasScaffoldTarget: false,
                CurrentRunManagedArtifactRoot: "artifacts/process-runs/current",
                CurrentRunManagedOutputRoot: "output/process-runs/current"),
            toolReceipts,
            ExpectedArtifacts: [],
            ArtifactRecords: [],
            new SoftwareDeliveryBrowserEvidenceSnapshot(
                BrowserProofRequired: false,
                HasCurrentRunBrowserEvidence: false,
                HasConsoleErrorEvidence: false,
                Routes: [],
                ArtifactPaths: [],
                Summary: string.Empty),
            new SoftwareDeliveryRunnableHostSnapshot(
                RunnableProjectPaths: [],
                InvalidHostSummary: string.Empty),
            carriedProof,
            RequiredToolNames: toolReceipts
                .Select(receipt => SoftwareDeliveryEvidencePolicy.NormalizeToolToken(receipt.ToolName))
                .Where(SoftwareDeliveryReceiptTimeline.IsImplementationValidationToolName)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            HasConcreteImplementationMockProof: false,
            new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero));
    }

    private static IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot> CreateRealisticDotNetReceiptTimeline()
    {
        var start = new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero);
        return
        [
            CreateReceipt(
                "workspace_write_file",
                start.AddMinutes(1),
                ["external-target/C/app/Program.cs"]),
            CreateReceipt(
                "workspace_read_file",
                start.AddMinutes(2),
                ["external-target/C/app/Program.cs"]),
            CreateReceipt(
                "workspace_dotnet_build",
                start.AddMinutes(3),
                ["external-target/C/app/ProductApp.csproj"]),
            CreateReceipt(
                "workspace_dotnet_run",
                start.AddMinutes(4),
                ["external-target/C/app/ProductApp.csproj"])
        ];
    }

    private static SoftwareDeliveryToolReceiptSnapshot CreateReceipt(
        string toolName,
        DateTimeOffset startedAtUtc,
        IReadOnlyList<string> workspacePaths)
    {
        return new SoftwareDeliveryToolReceiptSnapshot(
            toolName,
            startedAtUtc,
            startedAtUtc.AddSeconds(15),
            Succeeded: true,
            RequestSummary: string.Join(' ', workspacePaths),
            WorkingDirectory: string.Empty,
            ExitSummary: "Succeeded",
            workspacePaths,
            OutputFiles: []);
    }

    private static SoftwareDeliveryImplementationContractSnapshot CreateContract(
        bool requiresConcreteImplementationProof = true,
        string contractText = "Implement the requested product behavior.")
    {
        return new SoftwareDeliveryImplementationContractSnapshot(
            ContractText: contractText,
            TriggerText: "Process step execution",
            AdditionalGroundingText: string.Empty,
            RequiresConcreteImplementationProof: requiresConcreteImplementationProof,
            RequiresConcreteImplementationReview: false,
            RequiresConcreteBrowserProof: false,
            UsesScaffoldContractDrivenSetup: false,
            IsDotNetSolutionSetupScaffoldMutationStep: false);
    }

    private static string ReadProjectSource(string root)
    {
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(
                    Path.Combine(root, "src", "CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText));
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        return File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. segments]));
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "")
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "CanDoItAll.slnx")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
