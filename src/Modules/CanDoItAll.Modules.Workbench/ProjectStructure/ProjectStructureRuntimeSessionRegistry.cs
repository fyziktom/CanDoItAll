using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectStructureRuntimeSessionStartResult(
    WorkspaceOwnedProcessIdentity? Identity,
    string Message)
{
    public bool IsSuccess => Identity is not null;
}

internal interface IProjectStructureRuntimeSessionRegistry
{
    bool IsRunning(string nodeId);

    Task<ProjectStructureRuntimeSessionStartResult> StartSessionAsync(
        string nodeId,
        WorkspaceProcessSessionRequest request,
        CancellationToken cancellationToken);

    Task<ProjectStructureRuntimeLaunchResult> StopSessionAsync(
        string nodeId,
        CancellationToken cancellationToken);
}

internal sealed class ProjectStructureRuntimeSessionRegistry :
    IProjectStructureRuntimeSessionRegistry,
    IAsyncDisposable
{
    private readonly IServiceScopeFactory? scopeFactory;
    private readonly ILogger<ProjectStructureRuntimeSessionRegistry> logger;
    private readonly ConcurrentDictionary<string, IWorkspaceProcessSession> sessions =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object stopSync = new();
    private IServiceScope? processHostScope;
    private IWorkspaceLongRunningProcessHost? processHost;
    private Task? stopTask;
    private int stopping;

    public ProjectStructureRuntimeSessionRegistry(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectStructureRuntimeSessionRegistry> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    internal ProjectStructureRuntimeSessionRegistry(
        IWorkspaceLongRunningProcessHost processHost,
        ILogger<ProjectStructureRuntimeSessionRegistry> logger)
    {
        this.processHost = processHost;
        this.logger = logger;
    }

    public bool IsRunning(string nodeId)
        => sessions.TryGetValue(nodeId, out var session) && !session.HasExited;

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public async Task<ProjectStructureRuntimeSessionStartResult> StartSessionAsync(
        string nodeId,
        WorkspaceProcessSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref stopping) != 0)
        {
            return new(null, "Workbench runtime sessions are stopping; no new process was launched.");
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref stopping) != 0)
            {
                return new(null, "Workbench runtime sessions are stopping; no new process was launched.");
            }

            if (sessions.TryGetValue(nodeId, out var existing) && !existing.HasExited)
            {
                return new(
                    null,
                    $"Runtime node {nodeId} already has a Workbench-owned process. Stop it before launching another instance.");
            }

            if (existing is not null)
            {
                sessions.TryRemove(nodeId, out _);
                await existing.DisposeAsync().ConfigureAwait(false);
            }

            var session = await ResolveProcessHost().StartSessionAsync(request, cancellationToken).ConfigureAwait(false);
            sessions[nodeId] = session;
            _ = ObserveCompletionAsync(nodeId, session);
            return new(session.Identity, "Workbench runtime session started.");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ProjectStructureRuntimeLaunchResult> StopSessionAsync(
        string nodeId,
        CancellationToken cancellationToken)
    {
        IWorkspaceProcessSession? session;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!sessions.TryGetValue(nodeId, out session))
            {
                return new(false, "This runtime node has no Workbench-owned process to stop.");
            }
        }
        finally
        {
            gate.Release();
        }

        var result = await session.TerminateAsync(
            WorkspaceProcessTerminationReason.CallerCanceled,
            "The Workbench operator stopped the runtime node.",
            cancellationToken).ConfigureAwait(false);
        await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (sessions.TryGetValue(nodeId, out var current) && ReferenceEquals(current, session))
            {
                sessions.TryRemove(nodeId, out _);
            }
        }
        finally
        {
            gate.Release();
        }

        await session.DisposeAsync().ConfigureAwait(false);
        return result.ResidualProcessPossible
            ? new(false, "The runtime process could not be confirmed stopped; a residual process may remain.")
            : new(true, "The Workbench-owned runtime process was stopped.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (stopSync)
        {
            return stopTask ??= StopCoreAsync(cancellationToken);
        }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref stopping, 1);
        if (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Workbench runtime shutdown cancellation was requested before cleanup; owned sessions will still receive bounded termination attempts.");
        }

        KeyValuePair<string, IWorkspaceProcessSession>[] ownedSessions;
        await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            ownedSessions = sessions.ToArray();
            sessions.Clear();
        }
        finally
        {
            gate.Release();
        }

        foreach (var (nodeId, session) in ownedSessions)
        {
            try
            {
                var result = await session.TerminateAsync(
                    WorkspaceProcessTerminationReason.CallerCanceled,
                    "The application host is stopping its Workbench runtime sessions.",
                    CancellationToken.None).ConfigureAwait(false);
                if (result.ResidualProcessPossible)
                {
                    logger.LogError(
                        "Workbench runtime node {NodeId} could not be confirmed stopped. ProcessId={ProcessId}.",
                        nodeId,
                        session.Identity.ProcessId);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Workbench runtime node {NodeId} failed during host-shutdown cleanup. ProcessId={ProcessId}.",
                    nodeId,
                    session.Identity.ProcessId);
            }
            finally
            {
                try
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Workbench runtime node {NodeId} failed while disposing its host-shutdown session. ProcessId={ProcessId}.",
                        nodeId,
                        session.Identity.ProcessId);
                }
            }
        }

        processHost = null;
        try
        {
            processHostScope?.Dispose();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The Workbench runtime process-host scope failed to dispose during shutdown.");
        }

        processHostScope = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task ObserveCompletionAsync(
        string nodeId,
        IWorkspaceProcessSession session)
    {
        try
        {
            await session.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Workbench runtime node {NodeId} completion could not be observed. ProcessId={ProcessId}.",
                nodeId,
                session.Identity.ProcessId);
        }
        finally
        {
            await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (sessions.TryGetValue(nodeId, out var current) && ReferenceEquals(current, session))
                {
                    sessions.TryRemove(nodeId, out _);
                }
            }
            finally
            {
                gate.Release();
            }

            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private IWorkspaceLongRunningProcessHost ResolveProcessHost()
    {
        if (processHost is not null)
        {
            return processHost;
        }

        processHostScope = scopeFactory?.CreateScope()
            ?? throw new InvalidOperationException("The Workbench runtime process-host scope factory is unavailable.");
        processHost = processHostScope.ServiceProvider.GetRequiredService<IWorkspaceLongRunningProcessHost>();
        return processHost;
    }
}

internal sealed class ProjectStructureRuntimeSessionRegistryHostedService(
    ProjectStructureRuntimeSessionRegistry registry) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => registry.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken)
        => registry.StopAsync(cancellationToken);
}
