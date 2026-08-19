using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tools.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Git;
using CanDoItAll.Infrastructure;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "UnixRuntimePortability")]
public sealed class ExecutionFoundationPortabilityIntegrationTests
{
    [Fact]
    [Trait("Category", "ProcessPortability")]
    public async Task External_process_uses_canonical_host_and_native_executable_resolution()
    {
        var runner = new WorkspaceExternalProcessRunner(
            new LocalWorkspaceProcessHost(),
            new WorkspacePathResolutionService(
                Path.GetTempPath(),
                new PhysicalFileSystemPathPolicyFactory()));
        var request = new ExternalProcessRunRequest(
            ExecutablePath: "dotnet",
            Arguments: ["--version"],
            WorkingDirectory: Path.GetTempPath(),
            Timeout: TimeSpan.FromSeconds(15),
            StandardInput: string.Empty,
            MaxOutputBytes: 4096,
            CorrelationId: "process-portability-integration",
            AllowedExecutableNames: new HashSet<string> { "dotnet" });

        var result = await runner.RunAsync(request, CancellationToken.None);

        Assert.True(result.Started);
        Assert.Equal(0, result.ExitCode);
        Assert.Matches("^[0-9]+\\.[0-9]+\\.[0-9]+", result.Stdout);
        Assert.True(result.Elapsed < TimeSpan.FromSeconds(15));
    }

    [Fact]
    [Trait("Category", "ProcessPortability")]
    public async Task Git_foundation_uses_canonical_host_and_native_executable_resolution()
    {
        var executor = new WorkspaceGitCommandExecutor(new LocalWorkspaceProcessHost());
        var spec = new GitCommandSpec(
            new GitRepositoryPath(FindRepositoryRoot()),
            [new GitCommandArgument("--version")]);

        var result = await executor.ExecuteAsync(spec);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("git version", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
