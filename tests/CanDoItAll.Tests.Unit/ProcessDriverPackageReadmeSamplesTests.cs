using System.Runtime.CompilerServices;

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
                "TranscriptVerificationAlphaVerifier",
                "ProcessDriverSuppliedEvidenceContentRules.CreateTranscriptText"),
            new PackageReadmeExpectation(
                "CanDoItAll.Processes.Drivers.RuntimeEvidence",
                "RuntimeEvidenceConsistencyAlphaVerifier",
                "ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload"),
            new PackageReadmeExpectation(
                "CanDoItAll.Processes.Drivers.OfficeEvidence",
                "OfficeEvidenceAlphaVerifier",
                "ProcessDriverSuppliedEvidenceContentRules.CreateOfficeEvidencePayload"),
            new PackageReadmeExpectation(
                "CanDoItAll.Processes.Drivers.BusinessAnalysis",
                "BusinessAnalysisAlphaVerifier",
                "ProcessDriverSuppliedEvidenceContentRules.CreateBusinessAnalysisPayload"),
            new PackageReadmeExpectation(
                "CanDoItAll.Processes.Drivers.ArtifactEvidence",
                "ArtifactEvidenceAlphaVerifier",
                "ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload")
        };

        foreach (var expectation in expectations)
        {
            var readme = ReadPackageReadme(expectation.PackageName);

            Assert.Contains("In-Memory Sample", readme, StringComparison.Ordinal);
            Assert.True(
                readme.Contains("in-memory", StringComparison.OrdinalIgnoreCase) ||
                readme.Contains("in memory", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(expectation.VerifierTypeName, readme, StringComparison.Ordinal);
            Assert.Contains(expectation.SuppliedContentFactory, readme, StringComparison.Ordinal);
            Assert.Contains("Verify(request)", readme, StringComparison.Ordinal);
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
        Assert.Contains("ProcessDriverObservationAggregator.Aggregate(request)", readme, StringComparison.Ordinal);
        Assert.Contains("never runs drivers", readme, StringComparison.Ordinal);
        AssertNoRuntimeOrExternalAccessSamples(readme);
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
        string VerifierTypeName,
        string SuppliedContentFactory);
}
