using System.ComponentModel;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkspaceProcessStartFailureClassifierTests
{
    private const string ShortFolder = @"C:\work\app";

    [Theory]
    [InlineData(2, WorkspaceProcessStartFailureKind.ExecutableNotFound)]
    [InlineData(3, WorkspaceProcessStartFailureKind.ExecutableNotFound)]
    [InlineData(5, WorkspaceProcessStartFailureKind.AccessDenied)]
    [InlineData(740, WorkspaceProcessStartFailureKind.AccessDenied)]
    [InlineData(193, WorkspaceProcessStartFailureKind.ExecutableNotRunnable)]
    [InlineData(216, WorkspaceProcessStartFailureKind.ExecutableNotRunnable)]
    [InlineData(206, WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong)]
    [InlineData(267, WorkspaceProcessStartFailureKind.WorkingDirectoryMissing)]
    [InlineData(1450, WorkspaceProcessStartFailureKind.Unknown)]
    public void Windows_error_codes_map_to_start_failure_kinds(int nativeCode, WorkspaceProcessStartFailureKind expected)
    {
        var kind = Classify(new Win32Exception(nativeCode), ShortFolder, LocalHostPlatform.Windows, directoryExists: true);

        Assert.Equal(expected, kind);
    }

    [Theory]
    [InlineData("Linux", 2, WorkspaceProcessStartFailureKind.ExecutableNotFound)]
    [InlineData("Linux", 1, WorkspaceProcessStartFailureKind.AccessDenied)]
    [InlineData("Linux", 13, WorkspaceProcessStartFailureKind.AccessDenied)]
    [InlineData("Linux", 8, WorkspaceProcessStartFailureKind.ExecutableNotRunnable)]
    [InlineData("Linux", 20, WorkspaceProcessStartFailureKind.WorkingDirectoryMissing)]
    [InlineData("Linux", 36, WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong)]
    [InlineData("Linux", 63, WorkspaceProcessStartFailureKind.Unknown)]
    [InlineData("MacOS", 63, WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong)]
    [InlineData("MacOS", 36, WorkspaceProcessStartFailureKind.Unknown)]
    [InlineData("MacOS", 13, WorkspaceProcessStartFailureKind.AccessDenied)]
    public void Unix_errno_values_map_to_start_failure_kinds(
        string platform,
        int errno,
        WorkspaceProcessStartFailureKind expected)
    {
        var kind = Classify(new Win32Exception(errno), "/srv/app", Enum.Parse<LocalHostPlatform>(platform), directoryExists: true);

        Assert.Equal(expected, kind);
    }

    [Theory]
    [InlineData("Windows", 267)]
    [InlineData("Windows", 2)]
    [InlineData("Linux", 2)]
    [InlineData("MacOS", 2)]
    public void A_missing_working_folder_is_reported_whatever_the_native_code(string platform, int nativeCode)
    {
        var kind = Classify(new Win32Exception(nativeCode), "/missing/folder", Enum.Parse<LocalHostPlatform>(platform), directoryExists: false);

        Assert.Equal(WorkspaceProcessStartFailureKind.WorkingDirectoryMissing, kind);
    }

    [Theory]
    [InlineData(267)]
    [InlineData(206)]
    [InlineData(3)]
    public void A_long_existing_windows_working_folder_is_reported_as_too_long(int nativeCode)
    {
        var longFolder = @"C:\" + new string('w', WorkspaceLaunchExplanations.WindowsWorkingDirectoryLimit);

        var kind = Classify(new Win32Exception(nativeCode), longFolder, LocalHostPlatform.Windows, directoryExists: true);

        Assert.Equal(WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong, kind);
    }

    [Fact]
    public void A_missing_program_in_a_long_windows_folder_stays_a_missing_program()
    {
        var longFolder = @"C:\" + new string('w', WorkspaceLaunchExplanations.WindowsWorkingDirectoryLimit);

        var kind = Classify(new Win32Exception(2), longFolder, LocalHostPlatform.Windows, directoryExists: true);

        Assert.Equal(WorkspaceProcessStartFailureKind.ExecutableNotFound, kind);
    }

    [Fact]
    public void A_long_working_folder_is_not_a_limit_outside_windows()
    {
        var longFolder = "/" + new string('w', 400);

        var kind = Classify(new Win32Exception(2), longFolder, LocalHostPlatform.Linux, directoryExists: true);

        Assert.Equal(WorkspaceProcessStartFailureKind.ExecutableNotFound, kind);
    }

    [Theory]
    [InlineData(WorkspaceExecutableResolutionFailure.Missing, WorkspaceProcessStartFailureKind.ExecutableNotFound)]
    [InlineData(WorkspaceExecutableResolutionFailure.NotExecutable, WorkspaceProcessStartFailureKind.ExecutableNotRunnable)]
    [InlineData(WorkspaceExecutableResolutionFailure.ForeignPathSyntax, WorkspaceProcessStartFailureKind.ExecutableNotRunnable)]
    [InlineData(WorkspaceExecutableResolutionFailure.InvalidCandidate, WorkspaceProcessStartFailureKind.ExecutableNotRunnable)]
    public void Executable_resolution_failures_map_to_program_kinds(
        WorkspaceExecutableResolutionFailure failure,
        WorkspaceProcessStartFailureKind expected)
    {
        var kind = Classify(
            new WorkspaceExecutableResolutionException(failure, "resolution failed"),
            "/missing/folder",
            LocalHostPlatform.Linux,
            directoryExists: false);

        Assert.Equal(expected, kind);
    }

    [Fact]
    public void A_failure_after_the_process_started_is_an_ownership_failure()
    {
        var kind = WorkspaceProcessStartFailureClassifier.Classify(
            new Win32Exception(5),
            ShortFolder,
            processStarted: true,
            LocalHostPlatform.Windows,
            _ => true);

        Assert.Equal(WorkspaceProcessStartFailureKind.ProcessOwnershipFailed, kind);
    }

    [Fact]
    public void Wrapped_causes_are_classified_by_their_root()
    {
        Assert.Equal(
            WorkspaceProcessStartFailureKind.ExecutableNotFound,
            Classify(
                new WorkspaceProcessStartException("start failed", new Win32Exception(2)),
                ShortFolder,
                LocalHostPlatform.Windows,
                directoryExists: true));
        Assert.Equal(
            WorkspaceProcessStartFailureKind.AccessDenied,
            Classify(
                new AggregateException(new Win32Exception(5), new InvalidOperationException("cleanup")),
                ShortFolder,
                LocalHostPlatform.Windows,
                directoryExists: true));
    }

    [Theory]
    [InlineData(typeof(FileNotFoundException), WorkspaceProcessStartFailureKind.ExecutableNotFound)]
    [InlineData(typeof(DirectoryNotFoundException), WorkspaceProcessStartFailureKind.WorkingDirectoryMissing)]
    [InlineData(typeof(PathTooLongException), WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong)]
    [InlineData(typeof(UnauthorizedAccessException), WorkspaceProcessStartFailureKind.AccessDenied)]
    [InlineData(typeof(InvalidOperationException), WorkspaceProcessStartFailureKind.Unknown)]
    public void Managed_exceptions_without_a_native_code_are_classified_by_type(
        Type exceptionType,
        WorkspaceProcessStartFailureKind expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "failed")!;

        var kind = Classify(exception, ShortFolder, LocalHostPlatform.Windows, directoryExists: true);

        Assert.Equal(expected, kind);
    }

    [Fact]
    public void Operator_cause_names_the_exception_type_and_native_code()
    {
        var cause = WorkspaceProcessStartFailureClassifier.DescribeCause(
            new WorkspaceProcessStartException("x", new Win32Exception(267, "The directory name is invalid.")));

        Assert.Equal("Win32Exception 267: The directory name is invalid.", cause);
    }

    private static WorkspaceProcessStartFailureKind Classify(
        Exception exception,
        string workingDirectory,
        LocalHostPlatform platform,
        bool directoryExists)
        => WorkspaceProcessStartFailureClassifier.Classify(
            exception,
            workingDirectory,
            processStarted: false,
            platform,
            _ => directoryExists);
}

public sealed class WorkspaceLaunchExplanationsTests
{
    private static readonly Regex PhysicalPathPattern = new(
        @"[A-Za-z]:[\\/]|\\\\|/home/|/Users/|/tmp/|/var/|/private/",
        RegexOptions.CultureInvariant);

    [Fact]
    public void Every_start_failure_kind_has_a_complete_explanation()
    {
        var kinds = Enum.GetValues<WorkspaceProcessStartFailureKind>()
            .Where(kind => kind != WorkspaceProcessStartFailureKind.None)
            .ToArray();

        Assert.Equal(kinds.Length, WorkspaceLaunchExplanations.All.Count);
        foreach (var kind in kinds)
        {
            var explanation = WorkspaceLaunchExplanations.For(kind);
            Assert.Equal(kind, explanation.Kind);
            Assert.Equal($"{WorkspaceLaunchExplanations.CodePrefix}{kind}", explanation.Code);
            Assert.False(string.IsNullOrWhiteSpace(explanation.AgentText));
            Assert.False(string.IsNullOrWhiteSpace(explanation.OperatorRemediation));
            Assert.EndsWith(".", explanation.AgentText, StringComparison.Ordinal);
        }

        Assert.Equal(
            WorkspaceLaunchExplanations.All.Count,
            WorkspaceLaunchExplanations.All.Select(explanation => explanation.Code).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Agent_texts_never_name_a_physical_path()
    {
        foreach (var explanation in WorkspaceLaunchExplanations.All)
        {
            Assert.DoesNotMatch(PhysicalPathPattern, explanation.AgentText);
        }
    }

    [Fact]
    public void Only_a_missing_working_folder_can_be_fixed_by_correcting_the_input()
    {
        var retryable = WorkspaceLaunchExplanations.All
            .Where(explanation => explanation.CanRetryWithCorrectedInput)
            .Select(explanation => explanation.Kind)
            .ToArray();

        Assert.Equal([WorkspaceProcessStartFailureKind.WorkingDirectoryMissing], retryable);
    }

    [Fact]
    public void Codes_parse_back_to_their_explanation()
    {
        foreach (var explanation in WorkspaceLaunchExplanations.All)
        {
            Assert.True(WorkspaceLaunchExplanations.TryParseCode(explanation.Code, out var parsed));
            Assert.Same(explanation, parsed);
        }

        Assert.False(WorkspaceLaunchExplanations.TryParseCode("ToolPolicyDenied", out _));
        Assert.False(WorkspaceLaunchExplanations.TryParseCode("ProcessStartFailed.NotAKind", out _));
        Assert.False(WorkspaceLaunchExplanations.TryParseCode(null, out _));
    }

    [Fact]
    public void Program_names_are_added_without_their_folders()
    {
        var text = WorkspaceLaunchExplanations.WithProgramNames(
            "The program this command needs was not found on the host.",
            ["pwsh", @"C:\Program Files\PowerShell\7\powershell.exe", "PWSH", " "]);

        Assert.Equal(
            "The program this command needs was not found on the host (pwsh or powershell.exe).",
            text);
        Assert.Equal(
            "Unchanged.",
            WorkspaceLaunchExplanations.WithProgramNames("Unchanged.", []));
    }

    [Fact]
    public void Operator_description_of_a_long_folder_names_its_length_path_and_the_limit()
    {
        var folder = @"C:\" + new string('w', 267);

        var description = WorkspaceLaunchExplanations.DescribeForOperator(
            WorkspaceProcessStartFailureKind.WorkingDirectoryTooLong,
            "dotnet.exe",
            folder);

        Assert.Contains("270 characters long", description, StringComparison.Ordinal);
        Assert.Contains(folder, description, StringComparison.Ordinal);
        Assert.Contains(
            $"longer than {WorkspaceLaunchExplanations.WindowsWorkingDirectoryLimit} characters",
            description,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_start_failed_result_carries_the_kind_and_the_agent_safe_text()
    {
        var result = WorkspaceLaunchExplanations.CreateStartFailedResult(
            WorkspaceProcessStartFailureKind.ExecutableNotFound,
            ExecutionBoundaryDescriptor.Unknown,
            DateTimeOffset.UtcNow,
            @"'C:\tools\pwsh.exe' was not found on this host.");

        Assert.False(result.Started);
        Assert.Equal(-1, result.ExitCode);
        Assert.Equal(WorkspaceProcessTerminationReason.StartFailed, result.TerminationReason);
        Assert.Equal(WorkspaceProcessStartFailureKind.ExecutableNotFound, result.StartFailureKind);
        Assert.Equal(WorkspaceLaunchExplanations.For(WorkspaceProcessStartFailureKind.ExecutableNotFound).AgentText, result.FailureMessage);
        Assert.Contains(@"C:\tools", result.StartFailureDetail, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\tools", result.FailureMessage, StringComparison.Ordinal);
    }
}
