using System.Runtime.CompilerServices;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRuntimeHostCodeFirstGuardTests
{
    private const string CodexSegment = "codex";
    private const string BundlesSegment = "bundles";
    private const int RequiredSourceTestDominanceMultiplier = 5;

    private static readonly string[] ConcreteBundlePathFragments =
    [
        $"{CodexSegment}/{BundlesSegment}",
        $@"{CodexSegment}\{BundlesSegment}"
    ];

    private static readonly string[] ConcretePathApiTokens =
    [
        "Path.Combine",
        "Path.Join",
        "Directory.EnumerateFiles",
        "Directory.EnumerateDirectories",
        "Directory.GetFiles",
        "Directory.GetDirectories",
        "Directory.Exists",
        "File.ReadAllText",
        "File.ReadAllLines",
        "File.Exists",
        "File.OpenRead"
    ];

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_001_source_inventory_is_not_bundle_path_coupled()
    {
        var root = FindRepositoryRoot();
        var sourceFiles = new[]
        {
            Path.Combine(root, "src", "CanDoItAll.Processes.Contracts", "Runtime", "ProcessRuntimeHostContractModels.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessVerificationHostCapabilityCatalog.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessVerificationRuntimeHost.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessVerificationAuditStore.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessManagerReadOnlyVerificationCommandService.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessReadOnlyVerificationJobRunner.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessDryRunExecutionHost.cs")
        };
        var missingFiles = sourceFiles
            .Where(path => !File.Exists(path))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();
        var bundleCoupledFiles = sourceFiles
            .Where(path => File.Exists(path))
            .Where(path => ContainsConcreteBundlePath(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(missingFiles);
        Assert.Empty(bundleCoupledFiles);
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_002_long_lived_runtime_host_tests_do_not_read_concrete_bundle_files()
    {
        var root = FindRepositoryRoot();
        var coupledFiles = EnumerateRuntimeHostGuardedFiles(root)
            .Select(path => new
            {
                Path = path,
                Couplings = FindConcreteBundleFileReadCouplings(File.ReadAllText(path))
            })
            .Where(result => result.Couplings.Count > 0)
            .Select(result => $"{Path.GetRelativePath(root, result.Path)}: {string.Join("; ", result.Couplings)}")
            .ToArray();

        Assert.Empty(coupledFiles);
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_003_numstat_summary_groups_source_tests_docs_and_bundle_lines()
    {
        var summary = ProcessRuntimeHostCodeFirstDiffSummary.FromNumstatLines(
        [
            "12\t3\tsrc/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionHost.cs",
            "7\t1\ttests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs",
            "2\t2\tdocs/process-runtime-restoration-ledger.md",
            "1\t1\tcodex/bundles/process-driver-runtime-host-real-implementation-phase-v1/reviews/01-execution-report.md",
            "-\t-\toutput/binary-artifact.zip"
        ]);

        Assert.Equal(15, summary.SourceChangedLines);
        Assert.Equal(8, summary.TestChangedLines);
        Assert.Equal(4, summary.DocumentationChangedLines);
        Assert.Equal(2, summary.BundleChangedLines);
        Assert.Equal(23, summary.SourceAndTestChangedLines);
        Assert.Equal(0, summary.OtherChangedLines);
        Assert.True(summary.SatisfiesSourceTestDominance(RequiredSourceTestDominanceMultiplier));
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_004_numstat_summary_blocks_bundle_heavy_closure()
    {
        var summary = ProcessRuntimeHostCodeFirstDiffSummary.FromNumstatLines(
        [
            "3\t0\tsrc/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionHost.cs",
            "1\t1\ttests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs",
            "5\t1\tcodex/bundles/process-driver-runtime-host-real-implementation-phase-v1/reviews/01-execution-report.md"
        ]);

        Assert.Equal(5, summary.SourceAndTestChangedLines);
        Assert.Equal(6, summary.BundleChangedLines);
        Assert.False(summary.SatisfiesSourceTestDominance(RequiredSourceTestDominanceMultiplier));
    }

    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_005_numstat_summary_accepts_exact_five_to_one_source_test_dominance()
    {
        var summary = ProcessRuntimeHostCodeFirstDiffSummary.FromNumstatLines(
        [
            "6\t4\tsrc/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionHost.cs",
            "3\t2\ttests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs",
            "2\t1\tcodex/bundles/process-template-automation-e2e-multiteam-host-readiness-v1/reviews/01-execution-report.md"
        ]);

        Assert.Equal(15, summary.SourceAndTestChangedLines);
        Assert.Equal(3, summary.BundleChangedLines);
        Assert.True(summary.SatisfiesSourceTestDominance(RequiredSourceTestDominanceMultiplier));
    }

    private static bool ContainsConcreteBundlePath(string text)
    {
        return ConcreteBundlePathFragments.Any(fragment => text.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> FindConcreteBundleFileReadCouplings(string text)
    {
        return text
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Where(IsConcreteBundleFileReadCoupling)
            .Select(line => line.Trim())
            .ToArray();
    }

    private static bool IsConcreteBundleFileReadCoupling(string line)
    {
        return line.Contains(CodexSegment, StringComparison.OrdinalIgnoreCase) &&
            line.Contains(BundlesSegment, StringComparison.OrdinalIgnoreCase) &&
            ConcretePathApiTokens.Any(token => line.Contains(token, StringComparison.Ordinal));
    }

    private static IEnumerable<string> EnumerateRuntimeHostGuardedFiles(string root)
    {
        var guardedDirectories = new[]
        {
            Path.Combine(root, "src", "CanDoItAll.Processes.Contracts", "Runtime"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch"),
            Path.Combine(root, "tests", "CanDoItAll.Tests.Integration")
        };

        return guardedDirectories
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly))
            .Where(path =>
                path.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).StartsWith("Process", StringComparison.Ordinal));
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

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record ProcessRuntimeHostCodeFirstDiffSummary(
        int SourceChangedLines,
        int TestChangedLines,
        int DocumentationChangedLines,
        int BundleChangedLines,
        int OtherChangedLines)
    {
        public int SourceAndTestChangedLines => SourceChangedLines + TestChangedLines;

        public bool SatisfiesSourceTestDominance(int requiredMultiplier)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requiredMultiplier);

            return BundleChangedLines == 0 ||
                SourceAndTestChangedLines >= requiredMultiplier * BundleChangedLines;
        }

        public static ProcessRuntimeHostCodeFirstDiffSummary FromNumstatLines(IEnumerable<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            var sourceChangedLines = 0;
            var testChangedLines = 0;
            var documentationChangedLines = 0;
            var bundleChangedLines = 0;
            var otherChangedLines = 0;

            foreach (var line in lines)
            {
                if (!TryParseNumstatLine(line, out var changedLines, out var path))
                {
                    continue;
                }

                var normalizedPath = path.Replace('\\', '/');
                if (normalizedPath.StartsWith("src/", StringComparison.OrdinalIgnoreCase))
                {
                    sourceChangedLines += changedLines;
                }
                else if (normalizedPath.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
                {
                    testChangedLines += changedLines;
                }
                else if (normalizedPath.StartsWith("docs/", StringComparison.OrdinalIgnoreCase))
                {
                    documentationChangedLines += changedLines;
                }
                else if (normalizedPath.StartsWith($"{CodexSegment}/{BundlesSegment}/", StringComparison.OrdinalIgnoreCase))
                {
                    bundleChangedLines += changedLines;
                }
                else
                {
                    otherChangedLines += changedLines;
                }
            }

            return new ProcessRuntimeHostCodeFirstDiffSummary(
                sourceChangedLines,
                testChangedLines,
                documentationChangedLines,
                bundleChangedLines,
                otherChangedLines);
        }

        private static bool TryParseNumstatLine(string line, out int changedLines, out string path)
        {
            changedLines = 0;
            path = string.Empty;

            var fields = line.Split('\t', 3);
            if (fields.Length != 3 ||
                !int.TryParse(fields[0], out var addedLines) ||
                !int.TryParse(fields[1], out var deletedLines))
            {
                return false;
            }

            changedLines = addedLines + deletedLines;
            path = fields[2].Trim();
            return path.Length > 0;
        }
    }
}
