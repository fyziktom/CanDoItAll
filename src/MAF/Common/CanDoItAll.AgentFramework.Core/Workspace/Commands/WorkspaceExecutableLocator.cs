using CanDoItAll.SharedKernel;
using System.Runtime.InteropServices;

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
    private const int MaximumCandidateCount = 16;
    private const int MaximumCandidateLength = 1024;
    private const int MaximumPathExtensionCount = 32;
    private const int MaximumPathExtensionLength = 16;
    private const int MaximumPathExtensionsLength = 512;

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
        if (candidateNames.Count is 0 or > MaximumCandidateCount)
        {
            throw new WorkspaceExecutableResolutionException(
                WorkspaceExecutableResolutionFailure.InvalidCandidate,
                $"Executable resolution requires from 1 through {MaximumCandidateCount} candidates.");
        }

        var sawNonExecutable = false;
        foreach (var candidateName in candidateNames)
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
                ? $"None of the requested executable candidates is executable: {string.Join(", ", candidateNames)}."
                : $"Unable to resolve any requested executable candidate: {string.Join(", ", candidateNames)}.");
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

        var extensions = ParseWindowsPathExtensions(pathExtensions);

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
        var canonicalPath = platform == LocalHostPlatform.Windows
            ? Path.GetFullPath(identityPath)
            : ResolveUnixRealPath(identityPath);
        if (platform != LocalHostPlatform.Windows && !HasCurrentIdentityUnixExecuteAccess(canonicalPath))
        {
            nonExecutable = true;
            resolvedPath = string.Empty;
            return false;
        }

        resolvedPath = canonicalPath;
        return true;
    }

    private void ValidateCandidateSyntax(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) ||
            candidate.Length > MaximumCandidateLength ||
            candidate.Any(char.IsControl))
        {
            throw new WorkspaceExecutableResolutionException(
                WorkspaceExecutableResolutionFailure.InvalidCandidate,
                $"Executable candidates must contain from 1 through {MaximumCandidateLength} characters and no control characters.");
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

    private static string[] ParseWindowsPathExtensions(string? pathExtensions)
    {
        string configuredExtensions = string.IsNullOrEmpty(pathExtensions)
            ? DefaultWindowsPathExtensions
            : pathExtensions;
        if (configuredExtensions.Length > MaximumPathExtensionsLength)
        {
            throw InvalidPathExtensions(
                $"PATHEXT exceeds the {MaximumPathExtensionsLength}-character limit.");
        }

        string[] entries = configuredExtensions.Split(';', StringSplitOptions.None);
        if (entries.Length is 0 or > MaximumPathExtensionCount)
        {
            throw InvalidPathExtensions(
                $"PATHEXT requires from 1 through {MaximumPathExtensionCount} entries.");
        }

        var normalizedExtensions = new string[entries.Length];
        var seenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < entries.Length; index++)
        {
            string entry = entries[index];
            string extension = entry.StartsWith('.')
                ? entry
                : "." + entry;
            if (entry.Length == 0 ||
                extension.Length > MaximumPathExtensionLength ||
                extension.Length == 1 ||
                extension.Skip(1).Any(character => !char.IsAsciiLetterOrDigit(character)))
            {
                throw InvalidPathExtensions(
                    "PATHEXT entries must be bounded simple alphanumeric file extensions.");
            }

            if (!seenExtensions.Add(extension))
            {
                throw InvalidPathExtensions("PATHEXT cannot contain duplicate extensions.");
            }

            normalizedExtensions[index] = extension;
        }

        return normalizedExtensions;
    }

    private static WorkspaceExecutableResolutionException InvalidPathExtensions(string message)
        => new(WorkspaceExecutableResolutionFailure.InvalidCandidate, message);

    private static bool HasCurrentIdentityUnixExecuteAccess(string path)
        => (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) &&
           UnixExecutableNativeMethods.Access(path, UnixExecutableNativeMethods.ExecuteAccess) == 0;

    private static string ResolveUnixRealPath(string path)
    {
        var pointer = UnixExecutableNativeMethods.RealPath(path, 0);
        if (pointer == 0)
        {
            throw new WorkspaceExecutableResolutionException(
                WorkspaceExecutableResolutionFailure.InvalidCandidate,
                "The executable candidate's canonical path could not be resolved.");
        }

        try
        {
            return Marshal.PtrToStringUTF8(pointer)
                ?? throw new WorkspaceExecutableResolutionException(
                    WorkspaceExecutableResolutionFailure.InvalidCandidate,
                    "The executable candidate's canonical path was empty.");
        }
        finally
        {
            UnixExecutableNativeMethods.Free(pointer);
        }
    }
}

internal static partial class UnixExecutableNativeMethods
{
    internal const int ExecuteAccess = 1;

    [LibraryImport("libc", EntryPoint = "access", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    internal static partial int Access(string path, int mode);

    [LibraryImport("libc", EntryPoint = "realpath", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    internal static partial nint RealPath(string path, nint resolvedPath);

    [LibraryImport("libc", EntryPoint = "free")]
    internal static partial void Free(nint pointer);
}
