using System.Text.RegularExpressions;

namespace CanDoItAll.Tests.Unit;

public sealed class SecretScanningTests
{
    private static readonly SecretPattern[] SecretPatterns =
    [
        new(
            "OpenAI API key",
            new Regex("s" + "k-" + "[A-Za-z0-9_-]{20,}", RegexOptions.Compiled | RegexOptions.CultureInvariant),
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
        var findings = ScanRepositoryFiles().ToList();

        Assert.True(
            findings.Count == 0,
            "Realistic provider key pattern found in tracked text files: " + string.Join(", ", findings.Take(10)));
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

    private static IEnumerable<string> ScanRepositoryFiles()
    {
        var root = FindRepositoryRoot();
        foreach (var filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (ShouldSkipPath(root, filePath) || !IsTextFile(filePath))
            {
                continue;
            }

            string content;
            try
            {
                content = File.ReadAllText(filePath);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            var matchingPattern = SecretPatterns.FirstOrDefault(pattern => pattern.Pattern.IsMatch(content));
            if (matchingPattern is not null)
            {
                yield return $"{Path.GetRelativePath(root, filePath)} ({matchingPattern.Provider})";
            }
        }
    }

    private static bool ShouldSkipPath(string root, string filePath)
    {
        var relativePath = Path.GetRelativePath(root, filePath);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment =>
            string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "node_modules", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTextFile(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            "" or ".cs" or ".csproj" or ".css" or ".editorconfig" or ".gitignore" or ".html" or ".json" or ".md" or ".ps1" or ".props" or ".razor" or ".sln" or ".slnx" or ".targets" or ".txt" or ".xml" or ".yaml" or ".yml" => true,
            _ => false
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed record SecretPattern(
        string Provider,
        Regex Pattern,
        string Sample);
}
