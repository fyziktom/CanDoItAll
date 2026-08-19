using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tools.Abstractions;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkspaceExternalProcessRunner(
    IWorkspaceProcessHost processHost,
    IWorkspacePathResolutionService pathResolver) : IExternalProcessRunner
{
    private readonly WorkspaceCommandEnvironmentPolicy environmentPolicy = new();
    private readonly WorkspaceExecutableLocator executableLocator = new();

    public async Task<ExternalProcessRunResult> RunAsync(
        ExternalProcessRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var timeoutSeconds = Math.Clamp(
            (int)Math.Ceiling(request.Timeout.TotalSeconds),
            1,
            3600);
        var outputLimit = Math.Clamp(request.MaxOutputBytes, 0, 1024 * 1024);
        string workingDirectory;
        try
        {
            workingDirectory = pathResolver.ResolveDirectoryPath(
                request.WorkingDirectory,
                allowMissing: false).FullPath;
        }
        catch (Exception exception) when (
            exception is WorkspacePathResolutionException or
                DirectoryNotFoundException or
                UnauthorizedAccessException)
        {
            throw new ExternalProcessAccessPolicyException(
                "The external-process working directory is unavailable or outside workspace authority.");
        }

        var executablePath = executableLocator.ResolveExecutablePath(
            [request.ExecutablePath],
            workingDirectory);
        if (request.AllowedExecutableNames is not { Count: > 0 } allowedExecutableNames ||
            !new WorkspaceExecutableAuthorizationPolicy().IsAllowedResolvedPath(
                executablePath,
                allowedExecutableNames))
        {
            throw new ExternalProcessCommandPolicyException(
                "The resolved external-process executable is outside the capability-owned command policy.");
        }

        var result = await processHost.ExecuteAsync(
            new WorkspaceProcessExecutionRequest(
                ToolName: "external_process",
                RecipeId: request.CorrelationId,
                ExecutablePath: executablePath,
                Arguments: request.Arguments,
                WorkingDirectory: workingDirectory,
                EnvironmentVariables: environmentPolicy.MergeEnvironmentVariables(
                    environmentVariables: null,
                    toolName: "external_process"),
                TimeoutSeconds: timeoutSeconds,
                StdoutLimitCharacters: Math.Max(outputLimit, 256),
                StderrLimitCharacters: Math.Max(outputLimit, 256),
                StandardInput: request.StandardInput),
            cancellationToken).ConfigureAwait(false);

        if (result.ResidualProcessPossible)
        {
            throw new ExternalProcessResidualProcessException(
                "External process termination could not be confirmed; a residual process may remain.");
        }

        if (result.TerminationReason == WorkspaceProcessTerminationReason.TimedOut)
        {
            throw new TimeoutException($"Process exceeded the configured timeout of {request.Timeout}.");
        }

        if (result.TerminationReason == WorkspaceProcessTerminationReason.CallerCanceled)
        {
            throw new OperationCanceledException(
                "External process invocation was canceled by its caller.",
                cancellationToken);
        }

        return new ExternalProcessRunResult(
            result.Started,
            result.ExitCode,
            LimitUtf8(result.Stdout, outputLimit),
            LimitUtf8(result.Stderr, outputLimit),
            result.CompletedAtUtc - result.StartedAtUtc);
    }

    private static string LimitUtf8(string value, int maxBytes)
    {
        if (maxBytes <= 0 || string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (Encoding.UTF8.GetByteCount(value) <= maxBytes)
        {
            return value;
        }

        var buffer = new byte[maxBytes];
        Encoding.UTF8.GetEncoder().Convert(
            value.AsSpan(),
            buffer.AsSpan(),
            flush: true,
            out _,
            out var bytesUsed,
            out _);
        return Encoding.UTF8.GetString(buffer, 0, bytesUsed);
    }
}
