using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CanDoItAll.Tests.Unit;

public sealed class RepositoryTransientArtifactHygieneTests
{
    [Fact]
    public void RepositoryTransientArtifactHygiene_rejects_tracked_codex_work_package_outputs()
    {
        var root = FindRepositoryRoot();
        var repositoryPaths = TryGetTrackedRepositoryPaths(root, out var trackedPaths)
            ? trackedPaths
            : EnumerateRepositoryPaths(root);

        var forbiddenPaths = repositoryPaths
            .Select(NormalizePath)
            .Where(IsForbiddenTransientArtifactPath)
            .Take(20)
            .ToList();

        Assert.True(
            forbiddenPaths.Count == 0,
            "Tracked transient Codex work-package artifacts must not be committed: " + string.Join(", ", forbiddenPaths));
    }

    private static bool TryGetTrackedRepositoryPaths(string root, out IReadOnlyList<string> paths)
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
            paths = [];
            return false;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            paths = [];
            return false;
        }

        paths = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))))
            .ToList();
        return true;
    }

    private static IReadOnlyList<string> EnumerateRepositoryPaths(string root)
    {
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !ShouldSkipPhysicalPath(root, path))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();
    }

    private static bool ShouldSkipPhysicalPath(string root, string filePath)
    {
        var relativePath = NormalizePath(Path.GetRelativePath(root, filePath));
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length >= 2 &&
            string.Equals(segments[0], "codex", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[1], "bundles", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (segments.Length >= 2 &&
            string.Equals(segments[0], "codex", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[1], "bundle-exports", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (segments.Length >= 2 &&
            segments[0].StartsWith(".codex", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return segments.Any(segment =>
            string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".artifacts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsForbiddenTransientArtifactPath(string path)
    {
        if (string.Equals(path, "01-execution-report.md", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("codex/bundles/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("codex/bundle-exports/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("codex/", StringComparison.OrdinalIgnoreCase) &&
            path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWith(".codex/runlogs/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(".codex/tmp/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(".codex/temp/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(".codex-artifacts/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(".codex-tmp/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(".codex-temp/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
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
}
