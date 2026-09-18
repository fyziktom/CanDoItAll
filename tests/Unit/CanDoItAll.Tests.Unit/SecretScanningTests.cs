using System.Text.RegularExpressions;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class SecretScanningTests
{
    // Source scan scope. It walks the working tree, so git-ignored files inside the checkout are included; generated
    // build and browser outputs are excluded by the skip policy below. Proof artifacts have their own scanner with a
    // different selection (tools/Validation/Portability/scan_artifacts_for_secrets.py), which also reports unreadable
    // files as missing coverage instead of treating them as clean.
    internal const string ScanScope =
        "working-tree text files under the repository root, tracked and git-ignored, excluding .git, .artifacts, " +
        "generated browser outputs, transient codex bundles, bin, obj and node_modules";

    private static readonly SecretPattern[] SecretPatterns =
    [
        new(
            "OpenAI API key",
            new Regex("(?<![A-Za-z0-9_-])s" + "k-" + "[A-Za-z0-9_-]{20,}", RegexOptions.Compiled | RegexOptions.CultureInvariant),
            "s" + "k-proj-" + new string('A', 40)),
        new(
            "GitHub token",
            new Regex("gh[pousr]_[A-Za-z0-9_]{30,}", RegexOptions.Compiled | RegexOptions.CultureInvariant),
            "ghp_" + new string('A', 36)),
        new(
            "GitHub fine-grained token",
            new Regex("github_pat_[A-Za-z0-9_]{20,}", RegexOptions.Compiled | RegexOptions.CultureInvariant),
            "github_pat_" + new string('A', 80)),
        new(
            "Azure storage account key",
            new Regex("AccountKey=[A-Za-z0-9+/]{60,}={0,2}", RegexOptions.Compiled | RegexOptions.CultureInvariant),
            "AccountKey=" + new string('A', 88))
    ];

    [Fact]
    public void Repository_contains_no_realistic_provider_keys()
    {
        var result = Scan(TestRepositoryRoot.Find(), PhysicalScanFileSystem.Instance);
        var problems = new List<string>();
        if (result.ScannedFileCount == 0)
        {
            problems.Add("The source secret scan read no files, so its scope was not exercised.");
        }

        if (result.Omissions.Count > 0)
        {
            problems.Add(
                $"Secret scan coverage is incomplete for {ScanScope}; unreadable in-scope entries (path and error type only): " +
                string.Join(", ", result.Omissions.Take(20)));
        }

        if (result.Findings.Count > 0)
        {
            problems.Add(
                $"Realistic provider key pattern found in {ScanScope}: " + string.Join(", ", result.Findings.Take(10)));
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Unreadable_entries_are_reported_as_missing_coverage_without_echoing_content()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "candoitall-secret-scan-fake"));
        var fileSystem = new FakeScanFileSystem(root)
            .WithFile("src/Clean.cs", "public sealed class Clean { }")
            .WithFile("src/Leaky.cs", "var value = \"" + SecretPatterns[1].Sample + "\";")
            .WithUnreadableFile("src/Locked.cs", new UnauthorizedAccessException("denied " + SecretPatterns[0].Sample))
            .WithUnreadableDirectory("docs/private", new IOException("device not ready"))
            .WithFile("bin/Generated.cs", "var value = \"" + SecretPatterns[2].Sample + "\";");

        var result = Scan(root, fileSystem);

        Assert.Equal(2, result.ScannedFileCount);
        Assert.Equal([$"{Path.Combine("src", "Leaky.cs")} (GitHub token)"], result.Findings);
        Assert.Equal(
            [
                $"{Path.Combine("docs", "private")} (directory, IOException)",
                $"{Path.Combine("src", "Locked.cs")} (file, UnauthorizedAccessException)"
            ],
            result.Omissions.Select(omission => omission.ToString()).Order(StringComparer.Ordinal));
        string rendered = string.Join(" ", result.Findings.Concat(result.Omissions.Select(omission => omission.ToString())));
        Assert.All(SecretPatterns, pattern => Assert.DoesNotContain(pattern.Sample, rendered, StringComparison.Ordinal));
        Assert.DoesNotContain("denied", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("device not ready", rendered, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(RealisticSecretSamples))]
    public void Secret_scanner_rejects_realistic_provider_key_patterns(string provider, string simulatedSecret)
    {
        Assert.True(
            SecretPatterns.Any(pattern => pattern.Pattern.IsMatch(simulatedSecret)),
            $"Scanner did not reject the {provider} sample.");
    }

    public static TheoryData<string, string> RealisticSecretSamples()
    {
        var data = new TheoryData<string, string>();
        foreach (var pattern in SecretPatterns)
        {
            data.Add(pattern.Provider, pattern.Sample);
        }

        return data;
    }

    [Fact]
    public void Secret_scanner_does_not_treat_embedded_css_identifiers_as_provider_keys()
    {
        const string cssClassName = ".ps-task-sk-process-http-boundary";

        Assert.DoesNotContain(
            SecretPatterns,
            pattern => pattern.Pattern.IsMatch(cssClassName));
    }

    [Theory]
    [InlineData("artifacts/gpu-profile/Default/Cache/data_1", true)]
    [InlineData("artifacts/gpu-profile/Default/Local Storage/state.json", true)]
    [InlineData("artifacts/gpu-profile-evidence/report.txt", false)]
    [InlineData("artifacts/acceptance/provider-output.json", false)]
    [InlineData(".playwright-cli/page.yml", true)]
    [InlineData(".playwright-mcp/page.yml", true)]
    [InlineData("src/Modules/Feature.cs", false)]
    public void Secret_scanner_skips_only_approved_generated_browser_subtrees(
        string relativePath,
        bool expectedToSkip)
    {
        var root = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "candoitall-secret-scan-policy"));
        var candidate = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal(expectedToSkip, ShouldSkipPath(root, candidate));
    }

    private static ScanResult Scan(string root, IScanFileSystem fileSystem)
    {
        var findings = new List<string>();
        var omissions = new List<ScanOmission>();
        var scannedFiles = 0;
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(root);
        while (pendingDirectories.TryPop(out string? directory))
        {
            string[] files;
            string[] childDirectories;
            try
            {
                files = fileSystem.EnumerateFiles(directory).ToArray();
                childDirectories = fileSystem.EnumerateDirectories(directory).ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                omissions.Add(new(Path.GetRelativePath(root, directory), "directory", exception.GetType().Name));
                continue;
            }

            foreach (string filePath in files)
            {
                if (ShouldSkipPath(root, filePath) || !IsTextFile(filePath))
                {
                    continue;
                }

                string content;
                try
                {
                    content = fileSystem.ReadAllText(filePath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    omissions.Add(new(Path.GetRelativePath(root, filePath), "file", exception.GetType().Name));
                    continue;
                }

                scannedFiles++;
                var matchingPattern = SecretPatterns.FirstOrDefault(pattern => pattern.Pattern.IsMatch(content));
                if (matchingPattern is not null)
                {
                    findings.Add($"{Path.GetRelativePath(root, filePath)} ({matchingPattern.Provider})");
                }
            }

            foreach (string childDirectory in childDirectories)
            {
                if (ShouldSkipPath(root, childDirectory))
                {
                    continue;
                }

                bool isReparsePoint;
                try
                {
                    isReparsePoint = fileSystem.IsReparsePoint(childDirectory);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    omissions.Add(new(Path.GetRelativePath(root, childDirectory), "directory", exception.GetType().Name));
                    continue;
                }

                if (!isReparsePoint)
                {
                    pendingDirectories.Push(childDirectory);
                }
            }
        }

        return new(findings, omissions, scannedFiles);
    }

    private static bool ShouldSkipPath(string root, string filePath)
    {
        var relativePath = Path.GetRelativePath(root, filePath);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (IsUnderTransientBundlePath(segments) ||
            IsUnderGeneratedGpuBrowserProfilePath(segments))
        {
            return true;
        }

        return segments.Any(segment =>
            string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".artifacts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".playwright-cli", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "node_modules", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUnderTransientBundlePath(IReadOnlyList<string> pathSegments)
    {
        for (var index = 0; index < pathSegments.Count; index++)
        {
            if (string.Equals(pathSegments[index], "codex-bundles", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (index == pathSegments.Count - 1)
            {
                continue;
            }

            if (string.Equals(pathSegments[index], "codex", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(pathSegments[index + 1], "bundles", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUnderGeneratedGpuBrowserProfilePath(IReadOnlyList<string> pathSegments)
    {
        return pathSegments.Count >= 2 &&
               string.Equals(pathSegments[0], "artifacts", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(pathSegments[1], "gpu-profile", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextFile(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            "" or ".cs" or ".csproj" or ".css" or ".editorconfig" or ".gitignore" or ".html" or ".json" or ".md" or ".ps1" or ".props" or ".razor" or ".sln" or ".slnx" or ".targets" or ".txt" or ".xml" or ".yaml" or ".yml" => true,
            _ => false
        };
    }

    private sealed record SecretPattern(
        string Provider,
        Regex Pattern,
        string Sample);

    private sealed record ScanResult(
        IReadOnlyList<string> Findings,
        IReadOnlyList<ScanOmission> Omissions,
        int ScannedFileCount);

    private sealed record ScanOmission(string RelativePath, string Kind, string ErrorType)
    {
        public override string ToString() => $"{RelativePath} ({Kind}, {ErrorType})";
    }

    private interface IScanFileSystem
    {
        IEnumerable<string> EnumerateFiles(string directory);

        IEnumerable<string> EnumerateDirectories(string directory);

        string ReadAllText(string filePath);

        bool IsReparsePoint(string directory);
    }

    private sealed class PhysicalScanFileSystem : IScanFileSystem
    {
        public static PhysicalScanFileSystem Instance { get; } = new();

        public IEnumerable<string> EnumerateFiles(string directory) => Directory.EnumerateFiles(directory);

        public IEnumerable<string> EnumerateDirectories(string directory) => Directory.EnumerateDirectories(directory);

        public string ReadAllText(string filePath) => File.ReadAllText(filePath);

        public bool IsReparsePoint(string directory)
            => new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    private sealed class FakeScanFileSystem(string root) : IScanFileSystem
    {
        private readonly Dictionary<string, Func<string>> files = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Exception> unreadableDirectories = new(StringComparer.OrdinalIgnoreCase);

        public FakeScanFileSystem WithFile(string relativePath, string content)
        {
            files[Full(relativePath)] = () => content;
            return this;
        }

        public FakeScanFileSystem WithUnreadableFile(string relativePath, Exception error)
        {
            files[Full(relativePath)] = () => throw error;
            return this;
        }

        public FakeScanFileSystem WithUnreadableDirectory(string relativePath, Exception error)
        {
            unreadableDirectories[Full(relativePath)] = error;
            return this;
        }

        public IEnumerable<string> EnumerateFiles(string directory)
        {
            ThrowIfUnreadable(directory);
            return files.Keys.Where(path => string.Equals(Path.GetDirectoryName(path), directory, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<string> EnumerateDirectories(string directory)
        {
            ThrowIfUnreadable(directory);
            return files.Keys
                .Concat(unreadableDirectories.Keys.Select(path => Path.Combine(path, "placeholder")))
                .Select(path => Path.GetRelativePath(directory, path))
                .Where(relative => !relative.StartsWith("..", StringComparison.Ordinal) && relative.Contains(Path.DirectorySeparatorChar))
                .Select(relative => Path.Combine(directory, relative[..relative.IndexOf(Path.DirectorySeparatorChar)]))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public string ReadAllText(string filePath) => files[filePath]();

        public bool IsReparsePoint(string directory) => false;

        private void ThrowIfUnreadable(string directory)
        {
            if (unreadableDirectories.TryGetValue(directory, out var error))
            {
                throw error;
            }
        }

        private string Full(string relativePath)
            => Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
