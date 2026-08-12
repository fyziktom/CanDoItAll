using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public enum WorkspaceExecutableResolutionFailure
{
    Missing,
    NotExecutable,
    ForeignPathSyntax,
    InvalidCandidate
}

public sealed class WorkspaceExecutableResolutionException(
    WorkspaceExecutableResolutionFailure failure,
    string message) : InvalidOperationException(message)
{
    public WorkspaceExecutableResolutionFailure Failure { get; } = failure;
}

public sealed class WorkspaceExecutableLocator
{
    private const string DefaultWindowsPathExtensions = ".COM;.EXE;.BAT;.CMD";

    private readonly LocalHostPlatform platform;
    private readonly Func<string, string?> environmentVariableReader;

    public WorkspaceExecutableLocator()
        : this(LocalHostPlatformExtensions.CaptureCurrent(), Environment.GetEnvironmentVariable)
    {
    }

    internal WorkspaceExecutableLocator(
        LocalHostPlatform platform,
        Func<string, string?> environmentVariableReader)
    {
        this.platform = platform;
        this.environmentVariableReader = environmentVariableReader ?? throw new ArgumentNullException(nameof(environmentVariableReader));
    }

    public string ResolveExecutablePath(
        IReadOnlyList<string> candidateNames,
        string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(candidateNames);
        var normalizedCandidates = candidateNames
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => candidate.Trim())
            .ToArray();
        if (normalizedCandidates.Length == 0)
        {
            throw new WorkspaceExecutableResolutionException(
                WorkspaceExecutableResolutionFailure.InvalidCandidate,
                "At least one executable candidate is required.");
        }

        var sawNonExecutable = false;
        foreach (var candidateName in normalizedCandidates)
        {
            ValidateCandidateSyntax(candidateName);
            if (TryResolveExecutablePath(candidateName, workingDirectory, out var resolvedPath, out var nonExecutable))
            {
                return resolvedPath;
            }

            sawNonExecutable |= nonExecutable;
        }

        throw new WorkspaceExecutableResolutionException(
            sawNonExecutable
                ? WorkspaceExecutableResolutionFailure.NotExecutable
                : WorkspaceExecutableResolutionFailure.Missing,
            sawNonExecutable
                ? $"None of the requested executable candidates is executable: {string.Join(", ", normalizedCandidates)}."
                : $"Unable to resolve any requested executable candidate: {string.Join(", ", normalizedCandidates)}.");
    }

    internal static IReadOnlyList<string> GetCandidateFileNames(
        string executableName,
        LocalHostPlatform platform,
        string? pathExtensions)
    {
        if (platform != LocalHostPlatform.Windows || Path.HasExtension(executableName))
        {
            return [executableName];
        }

        var extensions = (string.IsNullOrWhiteSpace(pathExtensions)
                ? DefaultWindowsPathExtensions
                : pathExtensions)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(extension => extension[0] == '.' ? extension : "." + extension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return extensions
            .Select(extension => executableName + extension)
            .ToArray();
    }

    private bool TryResolveExecutablePath(
        string executableName,
        string? workingDirectory,
        out string resolvedPath,
        out bool nonExecutable)
    {
        nonExecutable = false;
        var pathExtensions = environmentVariableReader("PATHEXT");
        var candidateFileNames = GetCandidateFileNames(executableName, platform, pathExtensions);
        if (IsExplicitPath(executableName))
        {
            var baseDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Environment.CurrentDirectory
                : Path.GetFullPath(workingDirectory);
            foreach (var candidateFileName in candidateFileNames)
            {
                var candidatePath = Path.IsPathRooted(candidateFileName)
                    ? candidateFileName
                    : Path.Combine(baseDirectory, candidateFileName);
                if (TryAcceptCandidate(candidatePath, out resolvedPath, out var candidateNonExecutable))
                {
                    return true;
                }

                nonExecutable |= candidateNonExecutable;
            }

            resolvedPath = string.Empty;
            return false;
        }

        var pathDirectories = (environmentVariableReader("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pathDirectory in pathDirectories)
        {
            foreach (var candidateFileName in candidateFileNames)
            {
                if (TryAcceptCandidate(
                        Path.Combine(pathDirectory, candidateFileName),
                        out resolvedPath,
                        out var candidateNonExecutable))
                {
                    return true;
                }

                nonExecutable |= candidateNonExecutable;
            }
        }

        resolvedPath = string.Empty;
        return false;
    }

    private bool TryAcceptCandidate(
        string candidatePath,
        out string resolvedPath,
        out bool nonExecutable)
    {
        nonExecutable = false;
        if (!File.Exists(candidatePath))
        {
            resolvedPath = string.Empty;
            return false;
        }

        var fullPath = Path.GetFullPath(candidatePath);
        var finalTarget = File.ResolveLinkTarget(fullPath, returnFinalTarget: true);
        var identityPath = finalTarget?.FullName ?? fullPath;
        if (platform != LocalHostPlatform.Windows && !HasUnixExecutePermission(identityPath))
        {
            nonExecutable = true;
            resolvedPath = string.Empty;
            return false;
        }

        resolvedPath = Path.GetFullPath(identityPath);
        return true;
    }

    private void ValidateCandidateSyntax(string candidate)
    {
        if (candidate.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new WorkspaceExecutableResolutionException(
                WorkspaceExecutableResolutionFailure.InvalidCandidate,
                "Executable candidates cannot contain control characters.");
        }

        var syntax = PhysicalPathSyntaxClassifier.Classify(candidate);
        var foreign = platform == LocalHostPlatform.Windows
            ? syntax == PhysicalPathSyntax.UnixAbsolute
            : syntax is PhysicalPathSyntax.WindowsDriveAbsolute or
                PhysicalPathSyntax.WindowsDriveRelative or
                PhysicalPathSyntax.WindowsUnc or
                PhysicalPathSyntax.WindowsDevice;
        if (foreign || syntax == PhysicalPathSyntax.Uri)
        {
            throw new WorkspaceExecutableResolutionException(
                WorkspaceExecutableResolutionFailure.ForeignPathSyntax,
                $"Executable candidate '{candidate}' uses syntax that is not valid for this host.");
        }
    }

    private bool IsExplicitPath(string candidate)
        => Path.IsPathRooted(candidate) ||
           candidate.Contains('/', StringComparison.Ordinal) ||
           platform == LocalHostPlatform.Windows && candidate.Contains('\\', StringComparison.Ordinal);

    private static bool HasUnixExecutePermission(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return false;
        }

        var mode = File.GetUnixFileMode(path);
        const UnixFileMode executePermissions =
            UnixFileMode.UserExecute |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherExecute;
        return (mode & executePermissions) != 0;
    }
}
