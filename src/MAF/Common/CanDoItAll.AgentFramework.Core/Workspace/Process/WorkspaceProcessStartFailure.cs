using System.ComponentModel;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Why the operating system refused to start a workspace process. The kind selects the
/// operator remediation and the agent-facing failure code; it never carries a path.
/// </summary>
public enum WorkspaceProcessStartFailureKind
{
    None = 0,
    ExecutableNotFound = 1,
    ExecutableNotRunnable = 2,
    WorkingDirectoryMissing = 3,
    WorkingDirectoryTooLong = 4,
    AccessDenied = 5,
    ProcessOwnershipFailed = 6,
    Unknown = 7
}

/// <summary>
/// The explanation of one start-failure kind. <see cref="AgentText"/> is safe for the model:
/// it names no physical path and quotes no operating-system message.
/// </summary>
public sealed record WorkspaceLaunchExplanation(
    WorkspaceProcessStartFailureKind Kind,
    string Code,
    string AgentText,
    string OperatorRemediation,
    bool CanRetryWithCorrectedInput);

public static class WorkspaceLaunchExplanations
{
    public const string CodePrefix = "ProcessStartFailed.";

    /// <summary>
    /// The longest working directory Windows can start a process in while long paths are
    /// disabled (measured: 250 characters start, 270 fail).
    /// </summary>
    public const int WindowsWorkingDirectoryLimit = 258;

    private static readonly IReadOnlyDictionary<WorkspaceProcessStartFailureKind, WorkspaceLaunchExplanation> Explanations =
        new[]
        {
            new WorkspaceLaunchExplanation(
                WorkspaceProcessStartFailureKind.ExecutableNotFound,
                CodePrefix + nameof(WorkspaceProcessStartFailureKind.ExecutableNotFound),
                "The program this command needs was not found on the host.",
                "Install it or add its folder to the PATH of the account that runs CanDoItAll, then restart CanDoItAll.",
                CanRetryWithCorrectedInput: false),
            new WorkspaceLaunchExplanation(
                WorkspaceProcessStartFailureKind.ExecutableNotRunnable,
                CodePrefix + nameof(WorkspaceProcessStartFailureKind.ExecutableNotRunnable),
                "The program this command needs exists but cannot be run on this host.",
                "Check that the executable is built for this operating system and that the account that runs CanDoItAll may execute it.",
                CanRetryWithCorrectedInput: false),
            new WorkspaceLaunchExplanation(
                WorkspaceProcessStartFailureKind.WorkingDirectoryMissing,
                CodePrefix + nameof(WorkspaceProcessStartFailureKind.WorkingDirectoryMissing),
                "The working folder for this command does not exist.",
                "Create the folder or correct the working folder path, then retry.",
                CanRetryWithCorrectedInput: true),
            new WorkspaceLaunchExplanation(
                WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong,
                CodePrefix + nameof(WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong),
                "The working folder path is too long for the operating system to start a process in it.",
                $"Windows cannot start a process in a folder whose path is longer than {WindowsWorkingDirectoryLimit} characters while long paths are disabled. Move the project to a shorter folder or shorten the workspace root.",
                CanRetryWithCorrectedInput: false),
            new WorkspaceLaunchExplanation(
                WorkspaceProcessStartFailureKind.AccessDenied,
                CodePrefix + nameof(WorkspaceProcessStartFailureKind.AccessDenied),
                "The host denied starting the program for this command.",
                "Grant the account that runs CanDoItAll permission to run the program and to open its working folder.",
                CanRetryWithCorrectedInput: false),
            new WorkspaceLaunchExplanation(
                WorkspaceProcessStartFailureKind.ProcessOwnershipFailed,
                CodePrefix + nameof(WorkspaceProcessStartFailureKind.ProcessOwnershipFailed),
                "The program started, but CanDoItAll could not take ownership of it, so it was stopped.",
                "Check the CanDoItAll log for the process-ownership error (Windows job object or Unix process group) and retry.",
                CanRetryWithCorrectedInput: false),
            new WorkspaceLaunchExplanation(
                WorkspaceProcessStartFailureKind.Unknown,
                CodePrefix + nameof(WorkspaceProcessStartFailureKind.Unknown),
                "The program for this command could not be started on the host.",
                "See the CanDoItAll log for the operating-system error.",
                CanRetryWithCorrectedInput: false)
        }.ToDictionary(explanation => explanation.Kind);

    public static IReadOnlyCollection<WorkspaceLaunchExplanation> All => Explanations.Values.ToArray();

    public static WorkspaceLaunchExplanation For(WorkspaceProcessStartFailureKind kind)
        => Explanations.TryGetValue(kind, out var explanation)
            ? explanation
            : Explanations[WorkspaceProcessStartFailureKind.Unknown];

    /// <summary>
    /// Appends the program file names (never their folders) inside the sentence, for example
    /// "… was not found on the host (pwsh or powershell)."
    /// </summary>
    public static string WithProgramNames(string text, IEnumerable<string> executableCandidates)
    {
        var programs = executableCandidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(ProgramFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return programs.Length == 0
            ? text
            : $"{text.TrimEnd('.')} ({string.Join(" or ", programs)}).";
    }

    /// <summary>
    /// The file name of a program path written for any host. A persisted Windows path evaluated on
    /// Linux or macOS would otherwise come back whole from <see cref="Path.GetFileName(string)"/> and
    /// put a physical folder into agent-facing text.
    /// </summary>
    internal static string ProgramFileName(string path)
    {
        var trimmed = path.Trim();
        var separator = trimmed.LastIndexOfAny(['/', '\\']);
        return separator < 0 ? trimmed : trimmed[(separator + 1)..];
    }

    public static bool TryParseCode(string? code, out WorkspaceLaunchExplanation explanation)
    {
        explanation = Explanations[WorkspaceProcessStartFailureKind.Unknown];
        if (string.IsNullOrWhiteSpace(code) ||
            !code.StartsWith(CodePrefix, StringComparison.Ordinal) ||
            !Enum.TryParse<WorkspaceProcessStartFailureKind>(code[CodePrefix.Length..], out var kind) ||
            !Explanations.TryGetValue(kind, out var found))
        {
            return false;
        }

        explanation = found;
        return true;
    }

    /// <summary>
    /// Operator-facing sentence with the executable and folder named. Use it only on operator
    /// surfaces (logs, runtime-node feedback); never return it to an agent.
    /// </summary>
    public static string DescribeForOperator(
        WorkspaceProcessStartFailureKind kind,
        string? executable,
        string? workingDirectory)
    {
        var explanation = For(kind);
        var program = string.IsNullOrWhiteSpace(executable) ? "The program" : $"'{executable}'";
        var folder = string.IsNullOrWhiteSpace(workingDirectory) ? "its working folder" : $"'{workingDirectory}'";
        var cause = kind switch
        {
            WorkspaceProcessStartFailureKind.ExecutableNotFound => $"{program} was not found on this host.",
            WorkspaceProcessStartFailureKind.ExecutableNotRunnable => $"{program} cannot be run on this host.",
            WorkspaceProcessStartFailureKind.WorkingDirectoryMissing => $"The working folder {folder} does not exist.",
            WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong =>
                $"The working folder path is {workingDirectory?.Length ?? 0} characters long ({folder}).",
            WorkspaceProcessStartFailureKind.AccessDenied => $"The host denied starting {program} in {folder}.",
            WorkspaceProcessStartFailureKind.ProcessOwnershipFailed =>
                $"{program} started, but CanDoItAll could not take ownership of it, so it was stopped.",
            _ => $"{program} could not be started in {folder}."
        };

        return $"{cause} {explanation.OperatorRemediation}";
    }

    /// <summary>A not-started process result for the given kind (agent-safe message).</summary>
    public static WorkspaceProcessExecutionResult CreateStartFailedResult(
        WorkspaceProcessStartFailureKind kind,
        ExecutionBoundaryDescriptor boundary,
        DateTimeOffset startedAtUtc,
        string operatorDetail)
        => new(
            Started: false,
            ExitCode: -1,
            Stdout: string.Empty,
            Stderr: string.Empty,
            StdoutTruncated: false,
            StderrTruncated: false,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            TimedOut: false,
            Boundary: boundary,
            FailureMessage: For(kind).AgentText,
            TerminationReason: WorkspaceProcessTerminationReason.StartFailed)
        {
            StartFailureKind = kind,
            StartFailureDetail = operatorDetail ?? string.Empty
        };
}

/// <summary>
/// Maps the exception a failed start raised to a <see cref="WorkspaceProcessStartFailureKind"/>.
/// Windows reports Win32 error codes; .NET on Linux and macOS reports errno values through
/// <see cref="Win32Exception.NativeErrorCode"/>.
/// </summary>
public static class WorkspaceProcessStartFailureClassifier
{
    public static WorkspaceProcessStartFailureKind Classify(
        Exception exception,
        string? workingDirectory,
        bool processStarted)
        => Classify(
            exception,
            workingDirectory,
            processStarted,
            LocalHostPlatformExtensions.CaptureCurrent(),
            Directory.Exists);

    internal static WorkspaceProcessStartFailureKind Classify(
        Exception exception,
        string? workingDirectory,
        bool processStarted,
        LocalHostPlatform platform,
        Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(directoryExists);
        if (processStarted)
        {
            return WorkspaceProcessStartFailureKind.ProcessOwnershipFailed;
        }

        var root = Unwrap(exception);
        if (root is WorkspaceExecutableResolutionException resolution)
        {
            return resolution.Failure == WorkspaceExecutableResolutionFailure.Missing
                ? WorkspaceProcessStartFailureKind.ExecutableNotFound
                : WorkspaceProcessStartFailureKind.ExecutableNotRunnable;
        }

        var nativeCode = root is Win32Exception win32 ? win32.NativeErrorCode : (int?)null;
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            if (!directoryExists(workingDirectory))
            {
                return WorkspaceProcessStartFailureKind.WorkingDirectoryMissing;
            }

            if (platform == LocalHostPlatform.Windows &&
                workingDirectory.TrimEnd('\\', '/').Length > WorkspaceLaunchExplanations.WindowsWorkingDirectoryLimit &&
                nativeCode is null or WindowsPathNotFound or WindowsFilenameExceedsRange or WindowsDirectoryInvalid)
            {
                return WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong;
            }
        }

        if (nativeCode is { } code)
        {
            return platform == LocalHostPlatform.Windows
                ? ClassifyWindowsError(code)
                : ClassifyUnixErrno(code, platform);
        }

        return root switch
        {
            FileNotFoundException => WorkspaceProcessStartFailureKind.ExecutableNotFound,
            DirectoryNotFoundException => WorkspaceProcessStartFailureKind.WorkingDirectoryMissing,
            PathTooLongException => WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong,
            UnauthorizedAccessException => WorkspaceProcessStartFailureKind.AccessDenied,
            _ => WorkspaceProcessStartFailureKind.Unknown
        };
    }

    /// <summary>
    /// Operator detail for logs: the exception type, the native code and the operating-system
    /// message. It can contain paths, so it must not reach an agent.
    /// </summary>
    public static string DescribeCause(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var root = Unwrap(exception);
        var nativeCode = root is Win32Exception win32 ? $" {win32.NativeErrorCode}" : string.Empty;
        return $"{root.GetType().Name}{nativeCode}: {root.Message}";
    }

    private const int WindowsFileNotFound = 2;
    private const int WindowsPathNotFound = 3;
    private const int WindowsAccessDenied = 5;
    private const int WindowsBadExeFormat = 193;
    private const int WindowsFilenameExceedsRange = 206;
    private const int WindowsExeMachineTypeMismatch = 216;
    private const int WindowsDirectoryInvalid = 267;
    private const int WindowsElevationRequired = 740;

    private const int UnixPermissionDenied = 1;
    private const int UnixNoSuchFile = 2;
    private const int UnixExecFormatError = 8;
    private const int UnixAccessDenied = 13;
    private const int UnixNotADirectory = 20;
    private const int LinuxNameTooLong = 36;
    private const int MacOSNameTooLong = 63;

    private static WorkspaceProcessStartFailureKind ClassifyWindowsError(int code)
        => code switch
        {
            WindowsFileNotFound or WindowsPathNotFound => WorkspaceProcessStartFailureKind.ExecutableNotFound,
            WindowsAccessDenied or WindowsElevationRequired => WorkspaceProcessStartFailureKind.AccessDenied,
            WindowsBadExeFormat or WindowsExeMachineTypeMismatch => WorkspaceProcessStartFailureKind.ExecutableNotRunnable,
            WindowsFilenameExceedsRange => WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong,
            WindowsDirectoryInvalid => WorkspaceProcessStartFailureKind.WorkingDirectoryMissing,
            _ => WorkspaceProcessStartFailureKind.Unknown
        };

    private static WorkspaceProcessStartFailureKind ClassifyUnixErrno(int code, LocalHostPlatform platform)
        => code switch
        {
            UnixNoSuchFile => WorkspaceProcessStartFailureKind.ExecutableNotFound,
            UnixPermissionDenied or UnixAccessDenied => WorkspaceProcessStartFailureKind.AccessDenied,
            UnixExecFormatError => WorkspaceProcessStartFailureKind.ExecutableNotRunnable,
            UnixNotADirectory => WorkspaceProcessStartFailureKind.WorkingDirectoryMissing,
            LinuxNameTooLong when platform == LocalHostPlatform.Linux => WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong,
            MacOSNameTooLong when platform == LocalHostPlatform.MacOS => WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong,
            _ => WorkspaceProcessStartFailureKind.Unknown
        };

    private static Exception Unwrap(Exception exception)
    {
        var current = exception;
        while (true)
        {
            switch (current)
            {
                case WorkspaceProcessStartException { InnerException: { } inner }:
                    current = inner;
                    continue;
                case AggregateException { InnerExceptions.Count: > 0 } aggregate:
                    current = aggregate.InnerExceptions[0];
                    continue;
                default:
                    return current;
            }
        }
    }
}
