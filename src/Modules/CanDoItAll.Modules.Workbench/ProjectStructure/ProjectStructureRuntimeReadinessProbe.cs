using System.Security.Authentication;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectStructureRuntimeReadinessStatus
{
    Serving,
    Exited,
    Stopped,
    TimedOut
}

public sealed record ProjectStructureRuntimeReadiness(
    ProjectStructureRuntimeReadinessStatus Status,
    string Message)
{
    public bool IsServing => Status == ProjectStructureRuntimeReadinessStatus.Serving;
}

// Waits until a started runtime node answers on its loopback URL, so a preview is not opened for an
// application that is still building or has already exited.
public sealed class ProjectStructureRuntimeReadinessProbe(IHttpClientFactory httpClientFactory)
{
    public const string HttpClientName = "workbench-runtime-readiness";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);

    public async Task<ProjectStructureRuntimeReadiness> WaitUntilServingAsync(
        IProjectStructureRuntimeLauncher runtimeLauncher,
        string nodeId,
        Uri url,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtimeLauncher);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri || !url.IsLoopback)
        {
            throw new ArgumentException("Only loopback runtime URLs can be probed.", nameof(url));
        }

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            while (true)
            {
                if (await IsServingAsync(client, url, deadline.Token).ConfigureAwait(false))
                {
                    return new(
                        ProjectStructureRuntimeReadinessStatus.Serving,
                        $"The runtime is serving {url.AbsoluteUri}.");
                }

                var exit = await runtimeLauncher.WaitForExitAsync(nodeId, PollInterval, deadline.Token).ConfigureAwait(false);
                if (exit is not null)
                {
                    return new(
                        ProjectStructureRuntimeReadinessStatus.Exited,
                        $"The runtime stopped before {url.AbsoluteUri} responded. {exit.Describe()}");
                }

                if (!runtimeLauncher.IsRunning(nodeId))
                {
                    return new(
                        ProjectStructureRuntimeReadinessStatus.Stopped,
                        $"The runtime is no longer running, so {url.AbsoluteUri} was not opened.");
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(
                ProjectStructureRuntimeReadinessStatus.TimedOut,
                $"The runtime is still running but {url.AbsoluteUri} did not respond within {timeout.TotalSeconds:N0} seconds. Open the preview when the application reports that it is listening.");
        }
    }

    private static async Task<bool> IsServingAsync(HttpClient client, Uri url, CancellationToken cancellationToken)
    {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(RequestTimeout);
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, attempt.Token)
                .ConfigureAwait(false);
            return true;
        }
        catch (HttpRequestException exception) when (exception.InnerException is AuthenticationException)
        {
            // A completed TLS handshake failure still proves that the application is listening.
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
