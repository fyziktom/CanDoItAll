using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverFakeProofResistanceTests
{
    private const string StableProofFixturePath = "tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverMultiDomainVerificationGateway";
    private const string CorpusPath = "tests/CanDoItAll.Tests.Unit/TestData/ProcessDriverMultiDomainCorpus";
    private const string SecretPattern = @"sk-[A-Za-z0-9_-]{20,}|gh[pousr]_[A-Za-z0-9_]{30,}|github_pat_[A-Za-z0-9_]{20,}|AccountKey=[A-Za-z0-9+/]{60,}={0,2}";

    [Fact]
    public void Process_driver_fake_proof_rejects_status_only_and_non_empty_diagnostic_claims()
    {
        var statusOnly = new FakeProofEvidence(
            ReportRowSaysPassed: true,
            FocusedTranscriptShowsExpectedPassCount: false,
            SourceScanPassed: false,
            ManifestCompleted: false,
            HasTypedVerifierCoverage: false,
            EveryFixtureIsReferencedByTests: false,
            HasPositiveNoIssueProof: false,
            HasNegativeCategoryProof: false,
            HasNoMutationProof: false,
            HasSecretBearingFixtures: false,
            HasSecretNonLeakAssertions: false,
            DiagnosticText: string.Empty);
        var nonEmptyDiagnosticOnly = statusOnly with
        {
            ReportRowSaysPassed = false,
            DiagnosticText = "RuntimeEvidenceInconsistent"
        };

        var statusOnlyIssues = Evaluate(statusOnly);
        var nonEmptyDiagnosticIssues = Evaluate(nonEmptyDiagnosticOnly);

        Assert.Contains(FakeProofIssue.StatusOnlyReport, statusOnlyIssues);
        Assert.Contains(FakeProofIssue.MissingFocusedTestTranscript, statusOnlyIssues);
        Assert.Contains(FakeProofIssue.MissingSourceScanTranscript, statusOnlyIssues);
        Assert.Contains(FakeProofIssue.MissingManifest, statusOnlyIssues);
        Assert.Contains(FakeProofIssue.NonEmptyDiagnosticsOnly, nonEmptyDiagnosticIssues);
        Assert.Contains(FakeProofIssue.MissingSemanticAssertions, nonEmptyDiagnosticIssues);
    }

    [Fact]
    public void Process_driver_fake_proof_rejects_unredacted_secrets_and_fixture_only_parsing()
    {
        var unredactedSecret = new FakeProofEvidence(
            ReportRowSaysPassed: false,
            FocusedTranscriptShowsExpectedPassCount: true,
            SourceScanPassed: true,
            ManifestCompleted: true,
            HasTypedVerifierCoverage: true,
            EveryFixtureIsReferencedByTests: true,
            HasPositiveNoIssueProof: true,
            HasNegativeCategoryProof: true,
            HasNoMutationProof: true,
            HasSecretBearingFixtures: true,
            HasSecretNonLeakAssertions: false,
            DiagnosticText: "diagnostic leaked fixture-password reviewer@example.invalid");
        var fixtureOnly = unredactedSecret with
        {
            HasTypedVerifierCoverage = false,
            EveryFixtureIsReferencedByTests = false,
            HasSecretNonLeakAssertions = true,
            DiagnosticText = string.Empty
        };

        var unredactedIssues = Evaluate(unredactedSecret);
        var fixtureOnlyIssues = Evaluate(fixtureOnly);

        Assert.Contains(FakeProofIssue.MissingSecretRedactionProof, unredactedIssues);
        Assert.Contains(FakeProofIssue.UnredactedSecretLeak, unredactedIssues);
        Assert.Contains(FakeProofIssue.FixtureOnlyParsing, fixtureOnlyIssues);
        Assert.Contains(FakeProofIssue.MissingTypedVerifierCoverage, fixtureOnlyIssues);
    }

    [Fact]
    public void Process_driver_fake_proof_accepts_only_source_backed_multi_domain_corpus_proof()
    {
        var evidence = LoadActualSb043Evidence();
        var issues = Evaluate(evidence);

        Assert.Empty(issues);
    }

    [Fact]
    public void Process_driver_fake_proof_stable_architecture_fixtures_do_not_embed_transient_package_paths()
    {
        var repositoryRoot = FindRepositoryRoot();
        var fixtureRoots = new[]
        {
            StableProofFixturePath,
            "tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverRuntimeEvidenceVerifierIntegrationHardening"
        };
        var forbiddenPattern = CreateTransientBundlePathPattern();
        var matches = fixtureRoots
            .Select(root => Path.Combine(repositoryRoot, root))
            .SelectMany(root => Directory
                .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .SelectMany(file => FindTransientBundlePathMatches(root, file, forbiddenPattern)))
            .ToArray();

        Assert.Empty(matches);
    }

    private static FakeProofEvidence LoadActualSb043Evidence()
    {
        var report = ReadRepositoryFile(StableProofFixturePath, "reviews", "01-execution-report.md");
        var manifest = ReadRepositoryFile(StableProofFixturePath, "proof", "SB043", "manifest.md");
        var focusedTranscript = ReadRepositoryFile(
            StableProofFixturePath,
            "proof",
            "SB043",
            "transcripts",
            "focused-multi-domain-corpus-tests.txt");
        var sourceScan = ReadRepositoryFile(
            StableProofFixturePath,
            "proof",
            "SB043",
            "transcripts",
            "multi-domain-corpus-source-scan-and-anti-stub-audit.txt");
        var testSource = ReadRepositoryFile("tests", "CanDoItAll.Tests.Unit", "ProcessDriverMultiDomainCorpusTests.cs");
        var fixtureContents = ExpectedFixturePaths()
            .Select(path => ReadRepositoryFile(CorpusPath, path))
            .ToArray();
        var allProofText = string.Join(Environment.NewLine, [report, manifest, focusedTranscript, sourceScan, testSource, .. fixtureContents]);

        Assert.DoesNotMatch(SecretPattern, allProofText);

        return new FakeProofEvidence(
            ReportRowSaysPassed: report.Contains("| SB043 | Passed | Passed | Checked | Passed |", StringComparison.Ordinal),
            FocusedTranscriptShowsExpectedPassCount: Regex.IsMatch(focusedTranscript, @"Celkem:\s+6", RegexOptions.CultureInvariant),
            SourceScanPassed: sourceScan.Contains("PASS: SB043 source scan and anti-stub audit passed.", StringComparison.Ordinal),
            ManifestCompleted: manifest.Contains("Status: `Completed`", StringComparison.Ordinal) &&
                manifest.Contains("Focused corpus tests passed: 6 passed", StringComparison.Ordinal),
            HasTypedVerifierCoverage: ContainsAll(
                testSource,
                "TranscriptVerificationAlphaVerifier",
                "RuntimeEvidenceConsistencyAlphaVerifier",
                "OfficeEvidenceAlphaVerifier",
                "BusinessAnalysisAlphaVerifier",
                "ArtifactEvidenceAlphaVerifier",
                "CreateTranscriptRequest",
                "CreateRuntimeRequest",
                "CreateOfficeRequest",
                "CreateBusinessAnalysisRequest",
                "CreateArtifactRequest"),
            EveryFixtureIsReferencedByTests: ExpectedFixturePaths()
                .Select(Path.GetFileName)
                .All(fileName => fileName is not null && testSource.Contains(fileName, StringComparison.Ordinal)),
            HasPositiveNoIssueProof: testSource.Contains("AssertAcceptedNoIssue", StringComparison.Ordinal) &&
                testSource.Contains("ProcessDriverDiagnosticCategory.NoIssueDetected", StringComparison.Ordinal),
            HasNegativeCategoryProof: testSource.Contains("AssertAcceptedWithCategories", StringComparison.Ordinal) &&
                ContainsAll(
                    testSource,
                    "ProcessDriverDiagnosticCategory.RuntimeEvidenceInconsistent",
                    "ProcessDriverDiagnosticCategory.BusinessUnsupportedAssumption",
                    "ProcessDriverDiagnosticCategory.ArtifactTrustSensitivityMismatch",
                    "ProcessDriverDiagnosticCategory.InsufficientProof"),
            HasNoMutationProof: testSource.Contains("AssertNoMutation", StringComparison.Ordinal) &&
                testSource.Contains("AssertReadonlyAuditFacts", StringComparison.Ordinal),
            HasSecretBearingFixtures: fixtureContents.Any(content => content.Contains("fixture-password", StringComparison.Ordinal)),
            HasSecretNonLeakAssertions: testSource.Contains("AssertDiagnosticsAndAuditDoNotContain", StringComparison.Ordinal) &&
                testSource.Contains("fixture-password", StringComparison.Ordinal),
            DiagnosticText: string.Empty);
    }

    private static IReadOnlyList<FakeProofIssue> Evaluate(
        FakeProofEvidence evidence)
    {
        var issues = new List<FakeProofIssue>();

        if (!evidence.FocusedTranscriptShowsExpectedPassCount)
        {
            issues.Add(FakeProofIssue.MissingFocusedTestTranscript);
        }

        if (!evidence.SourceScanPassed)
        {
            issues.Add(FakeProofIssue.MissingSourceScanTranscript);
        }

        if (!evidence.ManifestCompleted)
        {
            issues.Add(FakeProofIssue.MissingManifest);
        }

        if (!evidence.HasTypedVerifierCoverage)
        {
            issues.Add(FakeProofIssue.MissingTypedVerifierCoverage);
        }

        if (!evidence.EveryFixtureIsReferencedByTests)
        {
            issues.Add(FakeProofIssue.FixtureOnlyParsing);
        }

        if (!evidence.HasPositiveNoIssueProof || !evidence.HasNegativeCategoryProof || !evidence.HasNoMutationProof)
        {
            issues.Add(FakeProofIssue.MissingSemanticAssertions);
        }

        if (evidence.ReportRowSaysPassed && issues.Count > 0)
        {
            issues.Add(FakeProofIssue.StatusOnlyReport);
        }

        if (!string.IsNullOrWhiteSpace(evidence.DiagnosticText) &&
            (!evidence.HasPositiveNoIssueProof || !evidence.HasNegativeCategoryProof || !evidence.HasNoMutationProof))
        {
            issues.Add(FakeProofIssue.NonEmptyDiagnosticsOnly);
        }

        if (evidence.HasSecretBearingFixtures && !evidence.HasSecretNonLeakAssertions)
        {
            issues.Add(FakeProofIssue.MissingSecretRedactionProof);
        }

        if (ContainsFixtureSecret(evidence.DiagnosticText))
        {
            issues.Add(FakeProofIssue.UnredactedSecretLeak);
        }

        return issues.Distinct().ToArray();
    }

    private static bool ContainsFixtureSecret(
        string value)
    {
        return value.Contains("fixture-password", StringComparison.Ordinal) ||
            value.Contains("@example.invalid", StringComparison.Ordinal);
    }

    private static bool ContainsAll(
        string value,
        params string[] expectedFragments)
    {
        return expectedFragments.All(fragment => value.Contains(fragment, StringComparison.Ordinal));
    }

    private static IEnumerable<string> FindTransientBundlePathMatches(
        string fixtureRoot,
        string file,
        Regex forbiddenPattern)
    {
        var content = File.ReadAllText(file);

        return forbiddenPattern
            .Matches(content)
            .Select(match => $"{Path.GetRelativePath(fixtureRoot, file)}: {match.Value}");
    }

    private static Regex CreateTransientBundlePathPattern()
    {
        var transientBundlePath = string.Join("[/\\\\]", ["codex", "bundles"]);
        var currentBundleName = string.Join("-", ["process", "runtime", "live", "e2e", "openai", "hardening", "v1"]);
        var previousBundleName = string.Join("-", ["process", "runtime", "restoration", "ui", "e2e", "driver", "integration", "v1"]);

        return new Regex(
            $"{transientBundlePath}|{Regex.Escape(currentBundleName)}|{Regex.Escape(previousBundleName)}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    private static IReadOnlyList<string> ExpectedFixturePaths()
    {
        return
        [
            "transcript/dotnet-positive-clean-build.txt",
            "transcript/dotnet-negative-diagnostics-and-redaction.txt",
            "transcript/rust-positive-clean-test.txt",
            "transcript/rust-negative-diagnostics-and-redaction.txt",
            "runtime/runtime-positive-consistent-descriptors.json",
            "runtime/runtime-negative-contradictory-descriptors.json",
            "office/office-positive-escalation.json",
            "office/office-negative-missing-metadata.json",
            "business/business-positive-churn-analysis.md",
            "business/business-negative-unsupported-assumption.md",
            "artifact/artifact-positive-release-notes.json",
            "artifact/artifact-negative-projection-drift.json"
        ];
    }

    private static string ReadRepositoryFile(
        params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. pathParts]));
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

    private sealed record FakeProofEvidence(
        bool ReportRowSaysPassed,
        bool FocusedTranscriptShowsExpectedPassCount,
        bool SourceScanPassed,
        bool ManifestCompleted,
        bool HasTypedVerifierCoverage,
        bool EveryFixtureIsReferencedByTests,
        bool HasPositiveNoIssueProof,
        bool HasNegativeCategoryProof,
        bool HasNoMutationProof,
        bool HasSecretBearingFixtures,
        bool HasSecretNonLeakAssertions,
        string DiagnosticText);

    private enum FakeProofIssue
    {
        MissingFocusedTestTranscript,
        MissingSourceScanTranscript,
        MissingManifest,
        MissingTypedVerifierCoverage,
        FixtureOnlyParsing,
        MissingSemanticAssertions,
        StatusOnlyReport,
        NonEmptyDiagnosticsOnly,
        MissingSecretRedactionProof,
        UnredactedSecretLeak
    }
}
