using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CanDoItAll.Tests.Unit;

public sealed class RepositoryNamingHygieneTests
{
    private static readonly Regex TestMethodPattern = new(
        @"^\s*public\s+(?:async\s+)?(?:Task|void)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DisplayNamePattern = new(
        "DisplayName\\s*=\\s*\"(?<name>[^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ForbiddenWorkPackageNamePattern = new(
        "(SB[0-9]{2,3}|INV_[0-9]{3}|subbundle|bundle|maf[-_]processes|process[-_]runtime[-_]live[-_]openai)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    [Fact]
    public void RepositoryNamingHygiene_active_tests_do_not_contain_work_package_identifiers()
    {
        var root = FindRepositoryRoot();
        var findings = EnumerateTestSourceFiles(root)
            .SelectMany(path => FindForbiddenNames(root, path))
            .Take(20)
            .ToList();

        Assert.True(
            findings.Count == 0,
            "Active test method or display names must use behavior language instead of work-package identifiers: " + string.Join(", ", findings));
    }

    private static IEnumerable<string> EnumerateTestSourceFiles(string root)
    {
        var trackedPaths = TryGetTrackedRepositoryPaths(root);
        if (trackedPaths is not null)
        {
            return trackedPaths
                .Where(path => path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))
                .Where(File.Exists)
                .ToList();
        }

        return Directory.EnumerateFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !ShouldSkipPhysicalPath(path))
            .ToList();
    }

    private static IReadOnlyList<string>? TryGetTrackedRepositoryPaths(string root)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "ls-files",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0
            ? output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : null;
    }

    private static IEnumerable<string> FindForbiddenNames(string root, string path)
    {
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            foreach (var name in ExtractNames(line))
            {
                var match = ForbiddenWorkPackageNamePattern.Match(name);
                if (!match.Success)
                {
                    continue;
                }

                yield return $"{Path.GetRelativePath(root, path)}:{lineNumber} `{name}` matched `{match.Value}`";
            }
        }
    }

    private static IEnumerable<string> ExtractNames(string line)
    {
        var methodMatch = TestMethodPattern.Match(line);
        if (methodMatch.Success)
        {
            yield return methodMatch.Groups["name"].Value;
        }

        foreach (Match displayNameMatch in DisplayNamePattern.Matches(line))
        {
            yield return displayNameMatch.Groups["name"].Value;
        }
    }

    private static bool ShouldSkipPhysicalPath(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
            path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
            path.Contains($"{Path.DirectorySeparatorChar}.artifacts{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
            path.Contains($"{Path.DirectorySeparatorChar}.playwright-mcp{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
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
