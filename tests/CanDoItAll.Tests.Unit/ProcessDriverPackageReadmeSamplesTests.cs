using System.Runtime.CompilerServices;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.ArtifactEvidence;
using CanDoItAll.Processes.Drivers.BusinessAnalysis;
using CanDoItAll.Processes.Drivers.ObservationAggregation;
using CanDoItAll.Processes.Drivers.OfficeEvidence;
using CanDoItAll.Processes.Drivers.RuntimeEvidence;
using CanDoItAll.Processes.Drivers.TranscriptVerification;
using CanDoItAll.Processes.Drivers.VerificationGateway;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverPackageReadmeSamplesTests
{
    [Fact]
    public void Process_driver_package_readmes_SB049_INV_001_all_alpha_samples_use_supplied_inmemory_payloads_only()
    {
        var expectations = new[]
        {
            new PackageReadmeExpectation(
                "CanDoItAll.Processes.Drivers.TranscriptVerification",
                typeof(TranscriptVerificationAlphaVerifier),
                typeof(TranscriptVerificationAlphaRequest),
                nameof(ProcessDriverSuppliedEvidenceContentRules.CreateTranscriptText)),
            new PackageReadmeExpectation(
                "CanDoItAll.Processes.Drivers.RuntimeEvidence",
                typeof(RuntimeEvidenceConsistencyAlphaVerifier),
                typeof(RuntimeEvidenceConsistencyVerificationRequest),
                nameof(ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload)),
            new PackageReadmeExpectation(
                "CanDoItAll.Processes.Drivers.OfficeEvidence",
                typeof(OfficeEvidenceAlphaVerifier),
                typeof(OfficeEvidenceVerificationRequest),
                nameof(ProcessDriverSuppliedEvidenceContentRules.CreateOfficeEvidencePayload)),
            new PackageReadmeExpectation(
                "CanDoItAll.Processes.Drivers.BusinessAnalysis",
                typeof(BusinessAnalysisAlphaVerifier),
                typeof(BusinessAnalysisVerificationRequest),
                nameof(ProcessDriverSuppliedEvidenceContentRules.CreateBusinessAnalysisPayload)),
            new PackageReadmeExpectation(
                "CanDoItAll.Processes.Drivers.ArtifactEvidence",
                typeof(ArtifactEvidenceAlphaVerifier),
                typeof(ArtifactEvidenceVerificationRequest),
                nameof(ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload))
        };

        foreach (var expectation in expectations)
        {
            var readme = ReadPackageReadme(expectation.PackageName);

            Assert.Contains("In-Memory Sample", readme, StringComparison.Ordinal);
            Assert.True(
                readme.Contains("in-memory", StringComparison.OrdinalIgnoreCase) ||
                readme.Contains("in memory", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(expectation.VerifierType.Name, readme, StringComparison.Ordinal);
            Assert.Contains(expectation.RequestType.Name, readme, StringComparison.Ordinal);
            Assert.Contains($"ProcessDriverSuppliedEvidenceContentRules.{expectation.SuppliedContentFactory}", readme, StringComparison.Ordinal);
            Assert.Contains("Verify(request)", readme, StringComparison.Ordinal);
            Assert.NotNull(expectation.VerifierType.GetMethod("Verify", [expectation.RequestType]));
            AssertNoRuntimeOrExternalAccessSamples(readme);
        }
    }

    [Fact]
    public void Process_driver_package_readmes_SB049_INV_002_observation_aggregation_sample_uses_existing_responses_only()
    {
        var readme = ReadPackageReadme("CanDoItAll.Processes.Drivers.ObservationAggregation");

        Assert.Contains("Observation Aggregation Alpha", readme, StringComparison.Ordinal);
        Assert.Contains("already-produced verification responses", readme, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverObservationAggregationRequest", readme, StringComparison.Ordinal);
        Assert.Contains("new ProcessDriverObservationAggregator().Aggregate(request)", readme, StringComparison.Ordinal);
        Assert.Contains("never runs drivers", readme, StringComparison.Ordinal);
        Assert.NotNull(typeof(ProcessDriverObservationAggregator).GetMethod(
            nameof(ProcessDriverObservationAggregator.Aggregate),
            [typeof(ProcessDriverObservationAggregationRequest)]));
        AssertNoRuntimeOrExternalAccessSamples(readme);
    }

    [Fact]
    public void Process_driver_package_readmes_SB042_INV_001_gateway_documents_typed_v1_migration_without_runtime_approval()
    {
        var readme = ReadPackageReadme("CanDoItAll.Processes.Drivers.VerificationGateway");

        Assert.Contains("explicit v1.x entry point", readme, StringComparison.Ordinal);
        Assert.Contains("VerifyArtifactEvidence", readme, StringComparison.Ordinal);
        Assert.Contains("VerifyOfficeEvidence", readme, StringComparison.Ordinal);
        Assert.Contains("VerifyBusinessAnalysis", readme, StringComparison.Ordinal);
        Assert.Contains("AggregateObservations", readme, StringComparison.Ordinal);
        Assert.Contains("Readiness Matrix", readme, StringComparison.Ordinal);
        Assert.Contains("Do not introduce lane-name lookup", readme, StringComparison.Ordinal);
        AssertNoRuntimeOrExternalAccessSamples(readme);
    }

    [Fact]
    public void Process_driver_package_readmes_SB043_SB044_INV_001_gateway_and_process_migration_docs_match_current_batch_orchestration_source()
    {
        var gatewayReadme = ReadPackageReadme("CanDoItAll.Processes.Drivers.VerificationGateway");
        var processesReadme = ReadRepositoryFile("src", "CanDoItAll.Modules.Processes", "README.md");
        var orchestratorSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessReadOnlyVerificationBatchOrchestrator.cs");
        var payloadBuilderSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessReadOnlyVerificationPayloadBuilder.cs");

        Assert.Contains("Source-Backed Batch Sample", gatewayReadme, StringComparison.Ordinal);
        Assert.Contains("new ProcessDriverVerificationBatchRequest(", gatewayReadme, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverVerificationBatchAggregationRequest", gatewayReadme, StringComparison.Ordinal);
        Assert.Contains("gateway.VerifyBatch(request)", gatewayReadme, StringComparison.Ordinal);
        Assert.NotNull(typeof(ProcessDriverVerificationGateway).GetMethod(
            nameof(ProcessDriverVerificationGateway.VerifyBatch),
            [typeof(ProcessDriverVerificationBatchRequest)]));

        Assert.Contains("Process Driver Read-Only Verification Migration", processesReadme, StringComparison.Ordinal);
        Assert.Contains("ProcessReadOnlyVerificationBatchOrchestrator.Verify(ProcessReadOnlyVerificationBatchPayload)", processesReadme, StringComparison.Ordinal);
        Assert.Contains("public ProcessReadOnlyVerificationBatchObservation Verify(ProcessReadOnlyVerificationBatchPayload payload)", orchestratorSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessReadOnlyVerificationBatchPayload", orchestratorSource, StringComparison.Ordinal);

        foreach (var payloadBuilderMethod in new[]
        {
            "CreateTranscriptPayload",
            "CreateRuntimeEvidencePayload",
            "CreateArtifactEvidencePayload",
            "CreateOfficeEvidencePayload",
            "CreateBusinessAnalysisPayload"
        })
        {
            Assert.Contains(payloadBuilderMethod, processesReadme, StringComparison.Ordinal);
            Assert.Contains($"public static ", payloadBuilderSource, StringComparison.Ordinal);
            Assert.Contains(payloadBuilderMethod, payloadBuilderSource, StringComparison.Ordinal);
        }

        foreach (var adapterTypeName in new[]
        {
            "ProcessTranscriptVerificationReadOnlyAdapter",
            "ProcessRuntimeEvidenceVerificationReadOnlyAdapter",
            "ProcessArtifactEvidenceReadOnlyAdapter",
            "ProcessOfficeEvidenceReadOnlyAdapter",
            "ProcessBusinessAnalysisReadOnlyAdapter"
        })
        {
            Assert.Contains(adapterTypeName, processesReadme, StringComparison.Ordinal);
            Assert.Contains(adapterTypeName, orchestratorSource, StringComparison.Ordinal);
        }

        AssertNoRuntimeOrExternalAccessSamples(gatewayReadme);
        AssertNoProcessDriverRuntimeApprovalDocs(processesReadme);
    }

    private static void AssertNoRuntimeOrExternalAccessSamples(
        string readme)
    {
        foreach (var forbiddenToken in new[]
        {
            "File.ReadAllText",
            "Directory.",
            "HttpClient",
            "Process.Start",
            "IServiceCollection",
            "AddScoped",
            "AddSingleton",
            "ProcessDriverRegistry",
            "ProcessDriverRuntimeSelector",
            "ProcessDriverManagerCommand",
            "ProcessDriverHost",
            "MapProcessDriver"
        })
        {
            Assert.DoesNotContain(forbiddenToken, readme, StringComparison.Ordinal);
        }
    }

    private static string ReadPackageReadme(
        string packageName)
    {
        return File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            packageName,
            "README.md"));
    }

    private static string ReadRepositoryFile(
        params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. pathParts]));
    }

    private static void AssertNoProcessDriverRuntimeApprovalDocs(
        string readme)
    {
        foreach (var forbiddenClaim in new[]
        {
            "runtime host approval: granted",
            "runtime host is approved",
            "driver registry is approved",
            "runtime selector is approved",
            "dependency-injection registration is approved",
            "manager command is approved",
            "scheduler hook is approved",
            "workflow hook is approved",
            "workspace write allowed",
            "storage write allowed",
            "process mutation allowed"
        })
        {
            Assert.DoesNotContain(forbiddenClaim, readme, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRepositoryRoot(
        [CallerFilePath] string sourceFilePath = "")
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

    private sealed record PackageReadmeExpectation(
        string PackageName,
        Type VerifierType,
        Type RequestType,
        string SuppliedContentFactory);
}
