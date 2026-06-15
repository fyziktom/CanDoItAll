using System.Diagnostics;

namespace CanDoItAll.Git;

public sealed class DefaultGitCommandExecutor : IGitCommandExecutor
{
    public async Task<GitCommandResult> ExecuteAsync(
        GitCommandSpec spec,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        using var process = new Process();
        process.StartInfo.FileName = spec.Executable;
        process.StartInfo.WorkingDirectory = spec.RepositoryPath.Value;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        foreach (var argument in spec.Arguments)
        {
            process.StartInfo.ArgumentList.Add(argument.Value);
        }

        process.Start();
        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new GitCommandResult(
            process.ExitCode == 0,
            process.ExitCode,
            standardOutput,
            standardError,
            spec.SanitizedCommand);
    }
}
