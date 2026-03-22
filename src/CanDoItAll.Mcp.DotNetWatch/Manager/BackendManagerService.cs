using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Mcp.DotNetWatch.Backend;
using CanDoItAll.Mcp.DotNetWatch.Runtime;

namespace CanDoItAll.Mcp.DotNetWatch.Manager;

internal sealed class BackendManagerService(
    BackendIdentityProvider identityProvider,
    SessionCoordinator coordinator,
    GlobalBackendCatalogStore catalogStore,
    IHttpClientFactory httpClientFactory,
    ILogger<BackendManagerService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public BackendRuntimeStatusResponse CreateLocalRuntimeStatus(BackendRegistrationRecord? registrationRecord)
    {
        var workspaceInfo = coordinator.GetWorkspaceInfo(includeHistory: true, includeConfigSnapshot: false);
        return new BackendRuntimeStatusResponse(
            identityProvider.Current,
            registrationRecord?.BackendId ?? "pending",
            registrationRecord?.ProcessId ?? Environment.ProcessId,
            registrationRecord?.ProcessStartedUtc ?? Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            registrationRecord?.RegisteredUtc ?? DateTimeOffset.UtcNow,
            registrationRecord?.BaseUrl ?? string.Empty,
            registrationRecord?.ManagerUrl ?? string.Empty,
            workspaceInfo.ActiveAppSessions,
            workspaceInfo.ActiveOperations,
            workspaceInfo.History?.RecentOperations ?? [],
            DateTimeOffset.UtcNow);
    }

    public async Task<BackendManagerStatusResponse> CreateAggregateStatusAsync(BackendRegistrationRecord? registrationRecord, CancellationToken cancellationToken)
    {
        var current = CreateLocalRuntimeStatus(registrationRecord);
        var records = await catalogStore.ReadAllAsync(cancellationToken);
        var staleBackendIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var backends = new Dictionary<string, ManagedBackendStatusData>(StringComparer.OrdinalIgnoreCase);

        backends[current.BackendId] = CreateManagedBackendStatus(current, isCurrentBackend: true, isReachable: true, unavailableReason: null);

        foreach (var record in records)
        {
            if (!catalogStore.IsLiveProcess(record))
            {
                staleBackendIds.Add(record.BackendId);
                continue;
            }

            if (string.Equals(record.BackendId, current.BackendId, StringComparison.OrdinalIgnoreCase))
            {
                backends[record.BackendId] = CreateManagedBackendStatus(current, isCurrentBackend: true, isReachable: true, unavailableReason: null);
                continue;
            }

            var remoteStatus = await TryGetRemoteStatusAsync(record, cancellationToken);
            if (remoteStatus is null)
            {
                backends[record.BackendId] = new ManagedBackendStatusData(
                    record.Identity,
                    record.BackendId,
                    record.ProcessId,
                    record.ProcessStartedUtc,
                    record.RegisteredUtc,
                    record.BaseUrl,
                    record.ManagerUrl,
                    IsCurrentBackend: false,
                    IsReachable: false,
                    UnavailableReason: "Backend process is running, but the HTTP manager endpoint did not respond.",
                    [],
                    [],
                    [],
                    DateTimeOffset.UtcNow);
                continue;
            }

            backends[record.BackendId] = CreateManagedBackendStatus(remoteStatus, isCurrentBackend: false, isReachable: true, unavailableReason: null);
        }

        if (staleBackendIds.Count > 0)
        {
            await catalogStore.DeleteManyAsync(staleBackendIds, cancellationToken);
        }

        var orderedBackends = backends.Values
            .OrderByDescending(static backend => backend.IsCurrentBackend)
            .ThenBy(static backend => backend.Identity.WorkspaceRoot, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static backend => backend.RegisteredUtc)
            .ToArray();

        return new BackendManagerStatusResponse(
            current.Identity,
            current.BackendId,
            current.ProcessId,
            current.StartedUtc,
            current.BaseUrl,
            current.ManagerUrl,
            current.ActiveSessions,
            current.ActiveOperations,
            current.RecentOperations,
            orderedBackends,
            orderedBackends.Count(static backend => backend.IsReachable),
            orderedBackends.Sum(static backend => backend.ActiveSessions.Count),
            orderedBackends.Sum(static backend => backend.ActiveOperations.Count),
            DateTimeOffset.UtcNow);
    }

    public async Task<BackendManagerActionResponse> ExecuteManagerActionAsync(
        BackendManagerActionRequest request,
        BackendRegistrationRecord? currentRegistration,
        CancellationToken cancellationToken)
    {
        if (currentRegistration is not null &&
            string.Equals(request.BackendId, currentRegistration.BackendId, StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteLocalActionAsync(request, proxied: false, cancellationToken);
        }

        var target = (await catalogStore.ReadAllAsync(cancellationToken))
            .FirstOrDefault(record => string.Equals(record.BackendId, request.BackendId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ToolInvocationException("BackendNotFound", "The requested backend was not found in the machine catalog.", new { request.BackendId });

        if (!catalogStore.IsLiveProcess(target))
        {
            await catalogStore.DeleteAsync(target.BackendId, cancellationToken);
            throw new ToolInvocationException("BackendUnavailable", "The requested backend is no longer running.", new { target.BackendId });
        }

        return await ProxyActionAsync(target, request, cancellationToken);
    }

    public async Task<BackendManagerActionResponse> ExecuteLocalActionAsync(
        BackendManagerActionRequest request,
        bool proxied,
        CancellationToken cancellationToken)
    {
        switch (request.Action)
        {
            case BackendManagerActionKind.StartDefaultApp:
            {
                var started = await coordinator.StartAppAsync(
                    projectPath: null,
                    mode: null,
                    configurationName: null,
                    framework: null,
                    launchProfile: null,
                    workingDirectory: null,
                    arguments: [],
                    environmentOverlay: null,
                    urls: [],
                    reuseIfCompatible: true,
                    conflictPolicy: AppStartConflictPolicy.Fail,
                    waitFor: AppWaitCondition.None,
                    cancellationToken);

                return Success(
                    request,
                    $"Default app session '{started.SessionId}' is {started.State}.",
                    started.SessionId,
                    operationId: null,
                    proxied);
            }

            case BackendManagerActionKind.StopSession:
            {
                var stop = await coordinator.StopAppAsync(RequireSessionId(request), "Manager stop requested.", force: false, cancellationToken);
                return Success(request, $"Session '{stop.SessionId}' stopped.", stop.SessionId, operationId: null, proxied);
            }

            case BackendManagerActionKind.ForceStopSession:
            {
                var stop = await coordinator.StopAppAsync(RequireSessionId(request), "Manager force stop requested.", force: true, cancellationToken);
                return Success(request, $"Session '{stop.SessionId}' was force-stopped.", stop.SessionId, operationId: null, proxied);
            }

            case BackendManagerActionKind.RebuildSession:
            {
                var rebuild = await coordinator.RebuildAppAsync(RequireSessionId(request), cancellationToken);
                return Success(
                    request,
                    $"Session '{rebuild.SessionId}' rebuild requested via {rebuild.Strategy}.",
                    rebuild.SessionId,
                    operationId: null,
                    proxied);
            }

            case BackendManagerActionKind.ForceRebuildSession:
            {
                var rebuild = await coordinator.ForceRebuildAppAsync(RequireSessionId(request), cancellationToken);
                return Success(
                    request,
                    $"Session '{rebuild.SessionId}' force rebuild completed via {rebuild.Strategy}.",
                    rebuild.SessionId,
                    operationId: null,
                    proxied);
            }

            case BackendManagerActionKind.BuildWorkspace:
            {
                var build = await coordinator.StartBuildAsync(
                    targetPath: null,
                    configurationName: null,
                    framework: null,
                    arguments: [],
                    environmentOverlay: null,
                    whenAppRunning: null,
                    timeout: null,
                    waitForCompletion: request.WaitForCompletion,
                    cancellationToken);

                return Success(
                    request,
                    $"Build operation '{build.OperationId}' started.",
                    sessionId: null,
                    build.OperationId,
                    proxied);
            }

            default:
                throw new ToolInvocationException("UnsupportedAction", $"Manager action '{request.Action}' is not supported.");
        }
    }

    private async Task<BackendRuntimeStatusResponse?> TryGetRemoteStatusAsync(BackendRegistrationRecord registration, CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateClient(registration);
            return await client.GetFromJsonAsync<BackendRuntimeStatusResponse>("/api/backend/status", JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to retrieve backend runtime status from {BaseUrl}", registration.BaseUrl);
            return null;
        }
    }

    private async Task<BackendManagerActionResponse> ProxyActionAsync(
        BackendRegistrationRecord target,
        BackendManagerActionRequest request,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(target);
        using var response = await client.PostAsJsonAsync("/api/backend/manager-action", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var remote = await response.Content.ReadFromJsonAsync<BackendManagerActionResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Remote backend manager action returned an empty response.");
        return remote with { Proxied = true };
    }

    private HttpClient CreateClient(BackendRegistrationRecord registration)
    {
        var client = httpClientFactory.CreateClient(nameof(BackendManagerService));
        client.BaseAddress = new Uri(registration.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Remove(BackendAuth.HeaderName);
        client.DefaultRequestHeaders.Add(BackendAuth.HeaderName, registration.AuthToken);
        return client;
    }

    private static ManagedBackendStatusData CreateManagedBackendStatus(
        BackendRuntimeStatusResponse status,
        bool isCurrentBackend,
        bool isReachable,
        string? unavailableReason)
    {
        return new ManagedBackendStatusData(
            status.Identity,
            status.BackendId,
            status.ProcessId,
            status.StartedUtc,
            status.RegisteredUtc,
            status.BaseUrl,
            status.ManagerUrl,
            isCurrentBackend,
            isReachable,
            unavailableReason,
            status.ActiveSessions,
            status.ActiveOperations,
            status.RecentOperations,
            status.TimestampUtc);
    }

    private static string RequireSessionId(BackendManagerActionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            return request.SessionId;
        }

        throw new ToolInvocationException("ValidationError", "This manager action requires a sessionId.", new { request.Action, request.BackendId });
    }

    private static BackendManagerActionResponse Success(
        BackendManagerActionRequest request,
        string message,
        string? sessionId,
        string? operationId,
        bool proxied)
    {
        return new BackendManagerActionResponse(
            Success: true,
            BackendId: request.BackendId,
            Action: request.Action,
            Message: message,
            SessionId: sessionId,
            OperationId: operationId,
            Proxied: proxied,
            TimestampUtc: DateTimeOffset.UtcNow);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
