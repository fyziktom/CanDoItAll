using System.Diagnostics;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspacePathAliasSession : IDisposable
{
    private const int WindowsPathBudget = 240;
    private const int RelativePathSafetyAllowance = 120;
    private static readonly object DriveLetterReservationGate = new();
    private static readonly HashSet<char> ReservedDriveLetters = [];

    private readonly string aliasRootPath;
    private readonly char driveLetter;
    private readonly string workspaceRootPath;
    private int disposeState;

    private WorkspacePathAliasSession(
        string workspaceRootPath,
        string aliasRootPath,
        char driveLetter)
    {
        this.workspaceRootPath = Path.GetFullPath(workspaceRootPath);
        this.aliasRootPath = aliasRootPath;
        this.driveLetter = driveLetter;
    }

    public static WorkspacePathAliasSession? TryCreate(
        string workspaceRootPath,
        string workingDirectoryPath,
        IReadOnlyList<string> arguments)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRootPath);
        if (!NeedsAlias(normalizedWorkspaceRoot, workingDirectoryPath, arguments))
        {
            return null;
        }

        var driveLetter = ReserveAvailableDriveLetter();
        if (!TryRunSubst($"{driveLetter}: {normalizedWorkspaceRoot}", out var failureMessage))
        {
            ReleaseDriveLetter(driveLetter);
            throw new InvalidOperationException(
                $"Failed to create temporary workspace drive alias for '{normalizedWorkspaceRoot}'. {failureMessage}");
        }

        return new WorkspacePathAliasSession(
            normalizedWorkspaceRoot,
            $"{driveLetter}:\\",
            driveLetter);
    }

    public string RewritePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var normalizedPath = Path.GetFullPath(path);
        if (!WorkspacePathPolicy.IsPathWithinRoot(normalizedPath, workspaceRootPath))
        {
            return path;
        }

        var relativePath = Path.GetRelativePath(workspaceRootPath, normalizedPath);
        return string.Equals(relativePath, ".", StringComparison.Ordinal)
            ? aliasRootPath
            : Path.Combine(aliasRootPath, relativePath);
    }

    public IReadOnlyList<string> RewriteArguments(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return arguments;
        }

        return arguments
            .Select(argument => RewriteArgument(argument))
            .ToArray();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposeState, 1) != 0)
        {
            return;
        }

        try
        {
            TryRunSubst($"{driveLetter}: /d", out _);
        }
        finally
        {
            ReleaseDriveLetter(driveLetter);
        }
    }

    private string RewriteArgument(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument) || !Path.IsPathRooted(argument))
        {
            return argument;
        }

        return RewritePath(argument);
    }

    private static bool NeedsAlias(string workspaceRootPath, string workingDirectoryPath, IReadOnlyList<string> arguments)
    {
        if (workspaceRootPath.Length >= WindowsPathBudget - RelativePathSafetyAllowance)
        {
            return true;
        }

        if (workingDirectoryPath.Length >= WindowsPathBudget)
        {
            return true;
        }

        foreach (var argument in arguments)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                continue;
            }

            if (Path.IsPathRooted(argument))
            {
                if (WorkspacePathPolicy.IsPathWithinRoot(argument, workspaceRootPath) &&
                    argument.Length >= WindowsPathBudget)
                {
                    return true;
                }

                continue;
            }

            if (!LooksLikeWorkspacePath(argument))
            {
                continue;
            }

            var candidatePath = Path.GetFullPath(Path.Combine(workingDirectoryPath, argument));
            if (WorkspacePathPolicy.IsPathWithinRoot(candidatePath, workspaceRootPath) &&
                candidatePath.Length >= WindowsPathBudget)
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeWorkspacePath(string argument)
    {
        if (argument.StartsWith("-", StringComparison.Ordinal))
        {
            return false;
        }

        if (argument.Contains(Path.DirectorySeparatorChar) || argument.Contains(Path.AltDirectorySeparatorChar))
        {
            return true;
        }

        var extension = Path.GetExtension(argument);
        return extension.Length > 0;
    }

    private static char ReserveAvailableDriveLetter()
    {
        lock (DriveLetterReservationGate)
        {
            var usedLetters = DriveInfo.GetDrives()
                .Select(drive => char.ToUpperInvariant(drive.Name[0]))
                .Concat(ReservedDriveLetters)
                .ToHashSet();

            for (var candidate = 'Z'; candidate >= 'P'; candidate--)
            {
                if (usedLetters.Contains(candidate))
                {
                    continue;
                }

                ReservedDriveLetters.Add(candidate);
                return candidate;
            }
        }

        throw new InvalidOperationException("No free drive letter is available for temporary workspace path aliasing.");
    }

    private static void ReleaseDriveLetter(char driveLetter)
    {
        lock (DriveLetterReservationGate)
        {
            ReservedDriveLetters.Remove(driveLetter);
        }
    }

    private static bool TryRunSubst(string arguments, out string failureMessage)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "subst",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        if (!process.Start())
        {
            failureMessage = $"Unable to start subst with arguments '{arguments}'.";
            return false;
        }

        process.WaitForExit();
        if (process.ExitCode == 0)
        {
            failureMessage = string.Empty;
            return true;
        }

        var stdout = process.StandardOutput.ReadToEnd().Trim();
        var stderr = process.StandardError.ReadToEnd().Trim();
        failureMessage = string.Join(
            " ",
            new[]
            {
                stdout,
                stderr,
                $"Exit code {process.ExitCode}."
            }.Where(item => !string.IsNullOrWhiteSpace(item)));
        return false;
    }
}
