using CanDoItAll.Git;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkspaceGitCommandExecutor(
    IWorkspaceProcessHost processHost) : IGitCommandExecutor
{
    private readonly WorkspaceCommandEnvironmentPolicy environmentPolicy = new();
    private readonly WorkspaceExecutableLocator executableLocator = new();

    public async Task<GitCommandResult> ExecuteAsync(
        GitCommandSpec spec,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var executablePath = executableLocator.ResolveExecutablePath(
            [spec.Executable],
            spec.RepositoryPath.Value);
        var processResult = await processHost.ExecuteAsync(
            new WorkspaceProcessExecutionRequest(
                ToolName: "workspace_git",
                RecipeId: "git_command",
                ExecutablePath: executablePath,
                Arguments: spec.Arguments.Select(argument => argument.Value).ToArray(),
                WorkingDirectory: spec.RepositoryPath.Value,
                EnvironmentVariables: environmentPolicy.MergeEnvironmentVariables(
                    environmentVariables: null,
                    toolName: "workspace_git"),
                TimeoutSeconds: Math.Clamp((int)Math.Ceiling(spec.Timeout.TotalSeconds), 1, 3600),
                StdoutLimitCharacters: 1024 * 1024,
                StderrLimitCharacters: 1024 * 1024),
            cancellationToken).ConfigureAwait(false);

        if (processResult.ResidualProcessPossible)
        {
            throw new InvalidOperationException(
                "Git process termination could not be confirmed; a residual process may remain.");
        }

        if (processResult.TerminationReason == WorkspaceProcessTerminationReason.TimedOut)
        {
            throw new TimeoutException($"Git command exceeded the configured timeout of {spec.Timeout}.");
        }

        if (processResult.TerminationReason == WorkspaceProcessTerminationReason.CallerCanceled)
        {
            throw new OperationCanceledException(
                "Git command execution was canceled by its caller.",
                cancellationToken);
        }

        return new GitCommandResult(
            processResult.Started && processResult.ExitCode == 0,
            processResult.ExitCode,
            processResult.Stdout,
            processResult.Stderr,
            spec.SanitizedCommand);
    }
}
