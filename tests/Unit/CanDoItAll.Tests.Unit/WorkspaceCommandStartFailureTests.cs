using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkspaceCommandStartFailureTests
{
    [Fact]
    public async Task A_start_failure_explains_the_reason_with_a_typed_code_and_no_diagnostics_hint()
    {
        using var workspace = new StartFailureWorkspace();
        var host = new StartFailingProcessHost(WorkspaceProcessStartFailureKind.ExecutableNotFound);
        var runner = workspace.CreateRunner(host);
        using var effectScope = AgentToolInvocationEffectScope.Begin();

        var result = await runner.ExecuteAsync(workspace.CreatePlan(["pwsh", "powershell"]));

        Assert.False(result.Succeeded);
        Assert.Equal("ProcessStartFailed.ExecutableNotFound", result.FailureCode);
        Assert.Equal(
            "Recipe 'start-failure-test' could not start: The program this command needs was not found on the host (pwsh or powershell). [ProcessStartFailed.ExecutableNotFound]",
            result.Message);
        Assert.DoesNotContain("Inspect captured diagnostics", result.Message, StringComparison.Ordinal);
        Assert.True(effectScope.RejectedBeforeEffect);
        var receiptJson = await File.ReadAllTextAsync(workspace.Resolve(result.Receipt.ReceiptRelativePath));
        using var receipt = System.Text.Json.JsonDocument.Parse(receiptJson);
        Assert.Equal(
            "ProcessStartFailed.ExecutableNotFound",
            receipt.RootElement.GetProperty("startFailureCode").GetString());
        Assert.DoesNotContain(StartFailingProcessHost.OperatorDetail, receiptJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_ownership_failure_is_explained_but_not_recorded_as_a_no_effect_rejection()
    {
        using var workspace = new StartFailureWorkspace();
        var runner = workspace.CreateRunner(new StartFailingProcessHost(WorkspaceProcessStartFailureKind.ProcessOwnershipFailed));
        using var effectScope = AgentToolInvocationEffectScope.Begin();

        var result = await runner.ExecuteAsync(workspace.CreatePlan(["pwsh"]));

        Assert.False(result.Succeeded);
        Assert.Equal("ProcessStartFailed.ProcessOwnershipFailed", result.FailureCode);
        Assert.False(effectScope.RejectedBeforeEffect);
    }

    [Fact]
    public async Task A_program_missing_from_path_returns_a_typed_result_instead_of_throwing()
    {
        using var workspace = new StartFailureWorkspace();
        var host = new StartFailingProcessHost(WorkspaceProcessStartFailureKind.Unknown);
        var runner = workspace.CreateRunner(
            host,
            new WorkspaceExecutableLocator(LocalHostPlatformExtensions.CaptureCurrent(), _ => null));
        using var effectScope = AgentToolInvocationEffectScope.Begin();

        var result = await runner.ExecuteAsync(workspace.CreatePlan(["cdia-missing-tool"]));

        Assert.False(result.Succeeded);
        Assert.Equal("ProcessStartFailed.ExecutableNotFound", result.FailureCode);
        Assert.Contains("(cdia-missing-tool)", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, host.ExecuteCount);
        Assert.True(effectScope.RejectedBeforeEffect);
    }

    [Fact]
    public async Task A_process_that_started_and_failed_keeps_the_diagnostics_hint_and_has_no_start_code()
    {
        using var workspace = new StartFailureWorkspace();
        var runner = workspace.CreateRunner(new StartFailingProcessHost(WorkspaceProcessStartFailureKind.None, started: true));

        var result = await runner.ExecuteAsync(workspace.CreatePlan(["pwsh"]));

        Assert.False(result.Succeeded);
        Assert.Null(result.FailureCode);
        Assert.Contains("failed with exit code 1", result.Message, StringComparison.Ordinal);
        Assert.Contains("Inspect captured diagnostics before editing or retrying", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_powershell_script_whose_program_cannot_start_returns_a_no_effect_result_through_the_service()
    {
        using var workspace = new StartFailureWorkspace();
        await File.WriteAllTextAsync(Path.Combine(workspace.Root, "script.ps1"), "Write-Output 'ok'");
        var service = TestWorkspaceServices.CreateCommandExecutionService(
            workspace.Root,
            new StartFailingProcessHost(WorkspaceProcessStartFailureKind.ExecutableNotFound));
        using var effectScope = AgentToolInvocationEffectScope.Begin();

        var result = await service.PowerShellRunScript("script.ps1");

        Assert.False(result.Succeeded);
        Assert.Equal("ProcessStartFailed.ExecutableNotFound", result.FailureCode);
        Assert.True(effectScope.RejectedBeforeEffect);
    }

    [Fact]
    public async Task A_long_workspace_path_whose_drive_alias_cannot_be_created_is_a_typed_no_effect_failure()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new StartFailureWorkspace(new string('w', 90));
        Assert.True(workspace.Root.Length >= 120, workspace.Root);
        var host = new StartFailingProcessHost(WorkspaceProcessStartFailureKind.Unknown);
        var runner = workspace.CreateRunner(host);
        using var effectScope = AgentToolInvocationEffectScope.Begin();

        var result = await runner.ExecuteAsync(workspace.CreatePlan(["pwsh"]));

        Assert.False(result.Succeeded);
        Assert.Equal("ProcessStartFailed.WorkingDirectoryTooLong", result.FailureCode);
        Assert.True(effectScope.RejectedBeforeEffect);
        Assert.Equal(1, host.ExecuteCount);
    }

    private sealed class StartFailureWorkspace : IDisposable
    {
        public StartFailureWorkspace(string? longSuffix = null)
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                $"CanDoItAll.WorkspaceCommandStartFailureTests.{Guid.NewGuid():N}{longSuffix}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public WorkspaceCommandProcessRunner CreateRunner(
            IWorkspaceProcessHost host,
            WorkspaceExecutableLocator? locator = null)
            => new(
                host,
                new WorkspaceCommandEnvironmentPolicy(),
                locator ?? new WorkspaceExecutableLocator(),
                new WorkspaceCommandReceiptWriter(Root),
                TestWorkspaceServices.CreatePathPolicy(
                    Root,
                    externalTargetRegistry: TestExternalTargetPathRegistry.Create()));

        public WorkspaceCommandPlan CreatePlan(IReadOnlyList<string> executableCandidates)
            => new(
                Decision: new ToolExecutionDecision(
                    ToolName: "workspace_start_failure_test",
                    RecipeId: "start-failure-test",
                    RiskClass: "LocalExecution",
                    Allowed: true,
                    ApprovalRequired: false,
                    NetworkAllowed: false,
                    ExternalRootsAllowed: false,
                    Reason: "Unit test start failure."),
                MutatesWorkspace: false,
                TargetPaths: [],
                WorkspaceRootPath: Root,
                WorkingDirectory: ".",
                WorkingDirectoryPath: Root,
                ExecutableCandidates: executableCandidates,
                Arguments: ["-NoProfile"],
                TimeoutSeconds: 30,
                StdoutLimitCharacters: 4096,
                StderrLimitCharacters: 4096,
                EnvironmentVariables: new Dictionary<string, string?>());

        public string Resolve(string relativePath)
            => Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class StartFailingProcessHost(
        WorkspaceProcessStartFailureKind kind,
        bool started = false) : IWorkspaceProcessHost
    {
        public const string OperatorDetail = @"'C:\secret-host-folder\pwsh.exe' was not found on this host.";

        public int ExecuteCount { get; private set; }

        public ExecutionBoundaryDescriptor DescribeBoundary() => ExecutionBoundaryDescriptor.Unknown;

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(started
                ? new WorkspaceProcessExecutionResult(
                    Started: true,
                    ExitCode: 1,
                    Stdout: string.Empty,
                    Stderr: "boom",
                    StdoutTruncated: false,
                    StderrTruncated: false,
                    StartedAtUtc: now,
                    CompletedAtUtc: now,
                    TimedOut: false,
                    Boundary: DescribeBoundary(),
                    FailureMessage: string.Empty)
                : WorkspaceLaunchExplanations.CreateStartFailedResult(kind, DescribeBoundary(), now, OperatorDetail));
        }
    }
}
