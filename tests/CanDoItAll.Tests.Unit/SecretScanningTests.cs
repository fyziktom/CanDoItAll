using System.Text.RegularExpressions;

namespace CanDoItAll.Tests.Unit;

public sealed class SecretScanningTests
{
    private static readonly Regex OpenAiKeyPattern = new(
        "s" + "k-" + "[A-Za-z0-9_-]{20,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void Repository_contains_no_realistic_openai_api_keys()
    {
        var findings = ScanRepositoryFiles().ToList();

        Assert.True(
            findings.Count == 0,
            "Realistic OpenAI API key pattern found in tracked text files: " + string.Join(", ", findings.Take(10)));
    }

    [Fact]
    public void Secret_scanner_rejects_realistic_openai_key_pattern()
    {
        var simulatedSecret = "s" + "k-proj-" + new string('A', 40);

        Assert.Matches(OpenAiKeyPattern, simulatedSecret);
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

            if (OpenAiKeyPattern.IsMatch(content))
            {
                yield return Path.GetRelativePath(root, filePath);
            }
        }
    }

    private static bool ShouldSkipPath(string root, string filePath)
    {
        var relativePath = Path.GetRelativePath(root, filePath);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment =>
            string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
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
}
