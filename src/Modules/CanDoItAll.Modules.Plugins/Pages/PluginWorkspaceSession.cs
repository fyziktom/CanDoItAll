using CanDoItAll.Modules.Plugins.Presentation;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Plugins.UI;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Plugins.Pages;

public sealed class PluginWorkspaceSession(
    PluginCatalogService catalog, PluginSettingsService settings, PluginOAuthService oauth,
    PluginPackageService packages, PluginLogStore logs, PluginRuntimeRestartService restart,
    PluginPackageOptions options, ILogger<PluginWorkspaceSession> logger) : IPluginWorkspaceOwner {
    private const string Actor = "ui";
    private Uri? baseUri;
    private Func<string, Task>? open;

    public PluginsWorkspace CreateWorkspace(Uri requestBaseUri, Func<string, Task> openBrowser) {
        if (baseUri is not null) {
            throw new InvalidOperationException("A Plugins session belongs to one page lifetime.");
        }
        baseUri = requestBaseUri;
        open = openBrowser;
        return new(this, new Uri(requestBaseUri, "/api/plugins/oauth/callback").ToString());
    }

    public long MaxPackageBytes => options.MaxPackageBytes;
    public Task<IReadOnlyList<PluginCatalogItem>> CatalogAsync(CancellationToken token) => catalog.ListCatalogAsync(token);
    public Task<PluginSettingsDetail?> SettingsAsync(PluginId id, CancellationToken token) => settings.GetSettingsAsync(id, token);
    public Task<IReadOnlyList<PluginOAuthConnectionStatusItem>> OAuthAsync(PluginId id, CancellationToken token) => oauth.ListStatusesAsync(id, token);
    public Task<IReadOnlyList<PluginLogItem>> LogsAsync(PluginLogQuery query, CancellationToken token) => logs.ListAsync(query, token);
    public Task<IReadOnlyList<PluginPackageCatalogItem>> PackagesAsync(CancellationToken token) => packages.ListPackagesAsync(token);
    public Task<PluginRuntimeRestartStatus> RestartStatusAsync(CancellationToken token) => restart.GetStatusAsync(token);
    public Task<PluginWriteReceipt<PluginConnectionItem>> SaveAsync(PluginId id, PluginConnectionSaveRequest request)
        => ExecuteAsync(() => settings.SaveConnectionAsync(id, request, Actor));
    public Task<PluginWriteReceipt<PluginCapabilityGrantItem>> GrantAsync(PluginId id, PluginGrantUpdateRequest request)
        => ExecuteAsync(() => settings.UpdateGrantAsync(id, request, Actor));
    public Task<PluginWriteReceipt<PluginCatalogItem>> LifecycleAsync(PluginId id, PluginLifecycleAction action)
        => ExecuteAsync(() => action switch {
            PluginLifecycleAction.Install => catalog.InstallAsync(id, new(Enable: true, Actor)),
            PluginLifecycleAction.Enable => catalog.SetEnabledAsync(id, true, new(Actor)),
            PluginLifecycleAction.Disable => catalog.SetEnabledAsync(id, false, new(Actor)),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        });
    public async Task<PluginWriteReceipt<PluginOAuthStartResponse>> StartOAuthAsync(PluginId id, PluginOAuthStartRequest request) {
        PluginOAuthProgress? progress = null;
        try {
            var result = await oauth.StartObservedAsync(id, request, baseUri!, Actor, value => progress = value);
            return (result.IsSuccess ? PluginWriteReceipt<PluginOAuthStartResponse>.Saved(result.Value!)
                : PluginWriteReceipt<PluginOAuthStartResponse>.Refused()) with { OAuthProgress = progress };
        } catch (Exception exception) {
            ReportFailure("OAuth start", exception);
            return new(progress?.Stage == PluginOAuthStage.SessionCreated
                ? PluginMutationStatus.SavedWithWarning : PluginMutationStatus.Unknown) { OAuthProgress = progress };
        }
    }
    public Task OpenOAuthAsync(string authorizationUrl) => open!(authorizationUrl);
    public Task<PluginWriteReceipt<PluginOAuthDisconnectResponse>> DisconnectOAuthAsync(PluginId id, PluginConnectionId connectionId)
        => ExecuteAsync(() => oauth.DisconnectAsync(id, connectionId));
    public Task<PluginWriteReceipt<PluginPackageInstallResult>> InstallAsync(PluginPackageId id)
        => ExecuteAsync(() => packages.InstallFromCatalogAsync(id, new(Enable: true, Actor)));
    public Task<PluginWriteReceipt<PluginPackageInstallResult>> UploadAsync(Stream stream, string fileName, CancellationToken token)
        => ExecuteAsync(() => packages.InstallUploadedPackageAsync(stream, fileName, new(Enable: true, Actor), token));
    public Task<PluginWriteReceipt<PluginRuntimeRestartStatus>> RestartAsync()
        => ExecuteAsync(() => restart.RequestRestartAsync(new(Actor)));

    public void ReportFailure(string operation, Exception exception)
        => logger.LogWarning("Plugins {Operation} failed. ExceptionType={ExceptionType}.", operation, exception.GetType().Name);

    private async Task<PluginWriteReceipt<T>> ExecuteAsync<T>(Func<Task<Result<T>>> action) {
        try {
            var result = await action();
            return result.IsSuccess ? PluginWriteReceipt<T>.Saved(result.Value!) : PluginWriteReceipt<T>.Refused();
        } catch (PluginPackageStageException exception) {
            logger.LogWarning("Plugin package {PackageId} failed after {Stage}. PrimaryExceptionType={PrimaryExceptionType}; CleanupExceptionType={CleanupExceptionType}.",
                exception.Progress.PackageId, exception.Progress.Stage, exception.InnerException?.GetType().Name,
                exception.CleanupException?.GetType().Name);
            return new(exception.Progress.Stage >= PluginPackageStage.Installed
                ? PluginMutationStatus.SavedWithWarning : PluginMutationStatus.Unknown) { PackageProgress = exception.Progress };
        } catch (PluginCommittedException<T> exception) {
            ReportFailure("post-commit step", exception);
            return new(PluginMutationStatus.SavedWithWarning, exception.Value);
        } catch (Exception exception) {
            ReportFailure("owner mutation", exception);
            return PluginWriteReceipt<T>.Unknown();
        }
    }
}
