namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspacePathAliasSession : IAsyncDisposable
{
    private const int WindowsPathBudget = 240;
    private const int RelativePathSafetyAllowance = 120;
    private static readonly object DriveLetterReservationGate = new();
    private static readonly HashSet<char> ReservedDriveLetters = [];

    private readonly string aliasRootPath;
    private readonly char driveLetter;
    private readonly string workspaceRootPath;
    private readonly WorkspacePathPolicy pathPolicy;
    private readonly IWorkspaceProcessHost processHost;
    private readonly string substExecutablePath;
    private readonly IReadOnlyDictionary<string, string?> environmentVariables;
    private int disposeState;

    private WorkspacePathAliasSession(
        string workspaceRootPath,
        string aliasRootPath,
        char driveLetter,
        WorkspacePathPolicy pathPolicy,
        IWorkspaceProcessHost processHost,
        string substExecutablePath,
        IReadOnlyDictionary<string, string?> environmentVariables)
    {
        this.workspaceRootPath = Path.GetFullPath(workspaceRootPath);
        this.aliasRootPath = aliasRootPath;
        this.driveLetter = driveLetter;
        this.pathPolicy = pathPolicy;
        this.processHost = processHost;
        this.substExecutablePath = substExecutablePath;
        this.environmentVariables = environmentVariables;
    }

    public static async Task<WorkspacePathAliasSession?> TryCreateAsync(
        string workspaceRootPath,
        string workingDirectoryPath,
        IReadOnlyList<string> arguments,
        WorkspacePathPolicy pathPolicy,
        IWorkspaceProcessHost processHost,
        IReadOnlyDictionary<string, string?> environmentVariables,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRootPath);
        if (!NeedsAlias(normalizedWorkspaceRoot, workingDirectoryPath, arguments, pathPolicy))
        {
            return null;
        }

        var driveLetter = ReserveAvailableDriveLetter();
        var substExecutablePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "subst.exe");
        WorkspaceProcessExecutionResult createResult;
        try
        {
            createResult = await RunSubstAsync(
                processHost,
                substExecutablePath,
                [$"{driveLetter}:", normalizedWorkspaceRoot],
                environmentVariables,
                recipeId: "workspace_path_alias_create",
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ReleaseDriveLetter(driveLetter);
            throw;
        }

        if (!Succeeded(createResult))
        {
            ReleaseDriveLetter(driveLetter);
            throw new InvalidOperationException(
                $"Failed to create a temporary workspace drive alias. {BuildFailureMessage(createResult)}");
        }

        return new WorkspacePathAliasSession(
            normalizedWorkspaceRoot,
            $"{driveLetter}:\\",
            driveLetter,
            pathPolicy,
            processHost,
            substExecutablePath,
            environmentVariables);
    }

    public string RewritePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var normalizedPath = Path.GetFullPath(path);
        if (!pathPolicy.IsPathWithinRoot(normalizedPath, workspaceRootPath))
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

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposeState, 1) != 0)
        {
            return;
        }

        try
        {
            var deleteResult = await RunSubstAsync(
                processHost,
                substExecutablePath,
                [$"{driveLetter}:", "/d"],
                environmentVariables,
                recipeId: "workspace_path_alias_delete",
                CancellationToken.None).ConfigureAwait(false);
            if (!Succeeded(deleteResult))
            {
                throw new InvalidOperationException(
                    $"Failed to remove a temporary workspace drive alias. {BuildFailureMessage(deleteResult)}");
            }
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

    private static bool NeedsAlias(
        string workspaceRootPath,
        string workingDirectoryPath,
        IReadOnlyList<string> arguments,
        WorkspacePathPolicy pathPolicy)
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
                if (pathPolicy.IsPathWithinRoot(argument, workspaceRootPath) &&
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
            if (pathPolicy.IsPathWithinRoot(candidatePath, workspaceRootPath) &&
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

    private static Task<WorkspaceProcessExecutionResult> RunSubstAsync(
        IWorkspaceProcessHost processHost,
        string substExecutablePath,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environmentVariables,
        string recipeId,
        CancellationToken cancellationToken)
        => processHost.ExecuteAsync(
            new WorkspaceProcessExecutionRequest(
                ToolName: "workspace_path_alias",
                RecipeId: recipeId,
                ExecutablePath: substExecutablePath,
                Arguments: arguments,
                WorkingDirectory: Environment.GetFolderPath(Environment.SpecialFolder.System),
                EnvironmentVariables: environmentVariables,
                TimeoutSeconds: 15,
                StdoutLimitCharacters: 4096,
                StderrLimitCharacters: 4096),
            cancellationToken);

    private static bool Succeeded(WorkspaceProcessExecutionResult result)
        => result.Started &&
           result.ExitCode == 0 &&
           result.TerminationReason == WorkspaceProcessTerminationReason.Completed &&
           !result.ResidualProcessPossible;

    private static string BuildFailureMessage(WorkspaceProcessExecutionResult result)
    {
        if (!result.Started)
        {
            return "The operating-system alias command did not start.";
        }

        if (result.ResidualProcessPossible)
        {
            return "Termination could not be confirmed and a residual process may remain.";
        }

        return result.TerminationReason switch
        {
            WorkspaceProcessTerminationReason.TimedOut => "The operating-system alias command timed out.",
            WorkspaceProcessTerminationReason.CallerCanceled => "The operating-system alias command was canceled.",
            _ => $"The operating-system alias command exited with code {result.ExitCode}."
        };
    }
}
