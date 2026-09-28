using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Plugins.Presentation;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Plugins.UI;
using CanDoItAll.SharedKernel.Configuration;
using System.Collections.Concurrent;

namespace CanDoItAll.Plugins.UiSandbox;

public enum PluginScenario {
    Normal, Empty, Large, MissingReferences, PartialReads, InvalidFields, HeldSave, HeldReadback,
    SaveWarning, UnknownSave, RefusedSave, Denied, HeldGrant, HeldOAuth, PackageWarning, HeldUpload,
    UnavailableReads, StaleReads, HeldReads, OAuthConnected, OAuthReconnect, OAuthError, MissingDescriptor
}
public enum PluginScenarioWait { Save, Settings, Grant, OAuth, Upload, Lifecycle, Package }
public enum PluginCatalogScenario { All, MissingFirst, Empty }

public sealed class PluginScenarioStore : IPluginWorkspaceOwner {
    private readonly Dictionary<PluginScenarioWait, TaskCompletionSource> waits = [];
    private readonly Dictionary<PluginScenarioWait, TaskCompletionSource> entered = [];
    private readonly List<PluginCatalogItem> catalog = [];
    private readonly ConcurrentDictionary<PluginConnectionId, PluginConnectionItem> connections = new();
    private readonly ConcurrentDictionary<PluginGrantTarget, PluginCapabilityGrantItem> grants = new();
    private readonly ConcurrentDictionary<PluginConnectionId, PluginOAuthConnectionStatusItem> oauth = new();
    private readonly List<PluginLogItem> logEntries = [];
    private PluginRuntimeRestartStatus restart = new(false, false, string.Empty, null, string.Empty, null, 0);
    private bool installed;
    private int settingsReads;
    private int saveWrites;
    private int grantWrites;
    private readonly object lifecycleGate = new();

    public PluginScenarioStore(PluginScenario scenario = PluginScenario.Normal) {
        Scenario = scenario;
        foreach (var wait in Enum.GetValues<PluginScenarioWait>()) {
            entered[wait] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        if (scenario == PluginScenario.Empty) {
            return;
        }
        var count = scenario == PluginScenario.Large ? 100 : 3;
        for (var index = 0; index < count; index++) {
            var id = new PluginId(index == 0 ? "office365.mail" : $"sandbox.plugin-{index}");
            var fields = index == 0 ? new ConfigurationFieldDescriptor[] {
                new(PluginOAuthConnectionSettingKeys.ClientId, "Client ID", ConfigurationFieldType.Guid, true, "A harmless client identifier."),
                new(PluginOAuthConnectionSettingKeys.RedirectUri, "Redirect URI", ConfigurationFieldType.Url, false, "Uses the host callback by default.")
            } : Enum.GetValues<ConfigurationFieldType>().Select(type => new ConfigurationFieldDescriptor(
                type.ToString(), type.ToString(), type, false, $"Sandbox {type} value.") {
                    Options = type == ConfigurationFieldType.Select ? [new("one", "One"), new("two", "Two")] : []
                }).ToArray();
            var descriptor = new PluginConnectionDescriptor(new(index == 0 ? "office365" : "example"),
                index == 0 ? "Office365 account" : "Example connection", "Local scenario connection", PluginConnectionAuthKind.OAuth2, new("1.0", fields));
            var manifest = new PluginDescriptor(id, index == 0 ? "Office365 Mail" : $"Sandbox plugin {index}",
                "Controlled plugin presentation; no provider or runtime is loaded.", "1.0.0", "Sandbox", PluginSourceKind.LocalPackage,
                PluginTrustLevel.LocalPackage, "1.0.0", PluginCapabilityKind.OAuth2 | PluginCapabilityKind.HttpClient | (index == 1 ? PluginCapabilityKind.HostCommand : PluginCapabilityKind.None),
                index == 0 ? Enumerable.Range(1, 3).Select(number => new PluginWorkflowExecutorDescriptor(
                    new($"sandbox.executor-{number}"), $"Mailbox action {number}", "Harmless descriptor; never executed.",
                    WorkflowExecutorCategoryKind.Http, new("sandbox.schema"), ConfigurationSchema.Empty(),
                    WorkflowValueShape.Text, WorkflowValueShape.Text, WorkflowExecutorExecutionPolicy.Default)).ToArray() : [],
                PluginSettingsDescriptor.Empty, [descriptor], new(new("sandbox.package"), "1.0.0", "1.0.0", "", ""),
                new(descriptor.Key, new("https://provider.invalid/authorize"), new("https://provider.invalid/token"), ["profile"]),
                index == 1 ? new(UiIconKind.PackageAsset, "icons/plugin.svg", "sandbox.package")
                    : new(UiIconKind.StaticAsset, "/icons/plugin.svg")) { Tags = ["email", "sandbox"] };
            catalog.Add(new(id, manifest.DisplayName, manifest.Description, manifest.Version, manifest.Vendor,
                manifest.SourceKind, manifest.TrustLevel, manifest.Capabilities, manifest.Package!.PackageId,
                PluginInstallationStateKind.InstalledEnabled, PluginCatalogAvailabilityKind.Available, "", null, null, manifest.Icon) { Descriptor = manifest });
            foreach (var capability in new[] { PluginCapabilityKind.OAuth2, PluginCapabilityKind.HttpClient }) {
                var grant = new PluginCapabilityGrantItem(id, capability, null, PluginGrantScopeKind.Plugin, "",
                    scenario == PluginScenario.Denied ? PluginGrantState.Denied : PluginGrantState.Granted,
                    PluginGrantRiskKind.Medium, "Scenario decision", "sandbox", null, null, null);
                grants[PluginGrantTarget.From(grant)] = grant;
            }
            foreach (var stream in Enum.GetValues<PluginLogStreamKind>()) {
                logEntries.Add(new(Guid.NewGuid(), stream, stream == PluginLogStreamKind.Installation
                    ? PluginLogOperationKind.PluginInstall : PluginLogOperationKind.ExecutorCompleted, PluginLogSeverity.Information,
                    "Completed", $"{manifest.DisplayName}: harmless {stream} fixture record.", "{}", id, manifest.Package.PackageId,
                    null, "sandbox-fixture", DateTimeOffset.UnixEpoch.AddSeconds(index)));
            }
            if (index == 1) {
                foreach (var recipe in new string?[] { null, "sandbox.inspect", "sandbox.report" }) {
                    var grant = new PluginCapabilityGrantItem(id, PluginCapabilityKind.HostCommand, recipe is null ? null : new(recipe),
                        PluginGrantScopeKind.Plugin, "", PluginGrantState.Requested, PluginGrantRiskKind.Medium, "Harmless recipe descriptor",
                        "sandbox", null, null, null);
                    grants[PluginGrantTarget.From(grant)] = grant;
                }
            }
        }
        foreach (var scope in new[] { "connection-a", "connection-b" }) {
            var grant = new PluginCapabilityGrantItem(catalog[0].PluginId, PluginCapabilityKind.HttpClient, null,
                PluginGrantScopeKind.Connection, scope, PluginGrantState.Requested, PluginGrantRiskKind.Medium,
                "Stored scope; runtime evaluation remains plugin-scoped.", "sandbox", null, null, null);
            grants[PluginGrantTarget.From(grant)] = grant;
        }
        if (scenario is PluginScenario.OAuthConnected or PluginScenario.OAuthReconnect or PluginScenario.OAuthError) {
            var descriptor = catalog[0].Descriptor.Connections[0];
            var connectionId = new PluginConnectionId(Guid.NewGuid());
            connections[connectionId] = new(connectionId, catalog[0].PluginId, descriptor.Key, "Saved sandbox account",
                "{\"clientId\":\"d0b7c6c2-e343-45f9-831f-fbb53a91ac43\"}", true, "Not checked", "sandbox",
                DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, Guid.NewGuid());
            var status = scenario switch {
                PluginScenario.OAuthConnected => PluginOAuthConnectionStatusKind.Connected,
                PluginScenario.OAuthReconnect => PluginOAuthConnectionStatusKind.ReconnectRequired,
                _ => PluginOAuthConnectionStatusKind.Error
            };
            oauth[connectionId] = new(connectionId, catalog[0].PluginId, descriptor.Key, status, "Harmless fixture account",
                ["profile"], null, null, status == PluginOAuthConnectionStatusKind.Error ? "fixture-error" : "",
                status == PluginOAuthConnectionStatusKind.Error ? "Controlled provider failure" : "", DateTimeOffset.UnixEpoch);
        }
        if (scenario == PluginScenario.HeldReads) {
            Hold(PluginScenarioWait.Settings);
        } else if (scenario == PluginScenario.HeldSave) {
            Hold(PluginScenarioWait.Save);
        } else if (scenario == PluginScenario.HeldGrant) {
            Hold(PluginScenarioWait.Grant);
        } else if (scenario == PluginScenario.HeldOAuth) {
            Hold(PluginScenarioWait.OAuth);
        } else if (scenario == PluginScenario.HeldUpload) {
            Hold(PluginScenarioWait.Upload);
        }
    }

    public PluginScenario Scenario { get; set; }
    public long MaxPackageBytes => 1024 * 1024;
    public int SaveWrites => saveWrites;
    public int SaveDispatches { get; private set; }
    public int GrantWrites => grantWrites;
    public int LifecycleWrites { get; private set; }
    public int OAuthStarts { get; private set; }
    public int BrowserEffects { get; private set; }
    public int PackageWrites { get; private set; }
    public int RestartRequests { get; private set; }
    public int CatalogReads { get; private set; }
    public int SettingsReads => settingsReads;
    public List<PluginLogQuery> LogQueries { get; } = [];
    public List<string> Diagnostics { get; } = [];
    public IReadOnlyCollection<PluginConnectionItem> Connections => connections.Values.ToArray();
    public bool FailSettings { get; set; }
    public bool FailBrowser { get; set; }
    public bool FailCatalog { get; set; }
    public bool FailOAuth { get; set; }
    public bool FailPackages { get; set; }
    public bool FailRestart { get; set; }
    public PluginCatalogScenario CatalogScenario { get; set; }
    public Func<CancellationToken, Task<IReadOnlyList<PluginCatalogItem>>>? ReadCatalogOverride { get; set; }
    public Func<PluginId, CancellationToken, Task<PluginSettingsDetail?>>? ReadSettingsOverride { get; set; }
    public Func<PluginLogQuery, CancellationToken, Task<IReadOnlyList<PluginLogItem>>>? ReadLogsOverride { get; set; }

    public void Hold(PluginScenarioWait kind) {
        waits[kind] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        entered[kind] = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    public Task EnteredAsync(PluginScenarioWait kind) => entered[kind].Task;
    public void Release(PluginScenarioWait kind) {
        if (waits.Remove(kind, out var gate)) {
            gate.TrySetResult();
        }
    }
    public void ReleaseAll() {
        foreach (var kind in waits.Keys.ToArray()) {
            Release(kind);
        }
    }
    private async Task WaitAsync(PluginScenarioWait kind) {
        entered[kind].TrySetResult();
        if (waits.TryGetValue(kind, out var gate)) {
            await gate.Task;
        }
    }

    public Task<IReadOnlyList<PluginCatalogItem>> CatalogAsync(CancellationToken token) {
        CatalogReads++;
        if (FailCatalog || Scenario == PluginScenario.UnavailableReads || (Scenario == PluginScenario.StaleReads && CatalogReads > 1)) {
            throw new InvalidOperationException("Controlled catalog read unavailable.");
        }
        return ReadCatalogOverride?.Invoke(token) ?? Task.FromResult<IReadOnlyList<PluginCatalogItem>>(CatalogScenario switch {
            PluginCatalogScenario.MissingFirst => catalog.Skip(1).ToArray(),
            PluginCatalogScenario.Empty => [],
            _ => catalog.ToArray()
        });
    }
    public async Task<PluginSettingsDetail?> SettingsAsync(PluginId id, CancellationToken token) {
        settingsReads++;
        if (ReadSettingsOverride is { } read) {
            return await read(id, token);
        }
        if (Scenario == PluginScenario.HeldReads || (SaveWrites > 0 && Scenario == PluginScenario.HeldReadback)) {
            await WaitAsync(PluginScenarioWait.Settings);
        }
        if (FailSettings || Scenario == PluginScenario.PartialReads || (SaveWrites > 0 && Scenario == PluginScenario.SaveWarning)) {
            throw new InvalidOperationException("Controlled settings read unavailable.");
        }
        return Snapshot(id);
    }
    public PluginSettingsDetail? Snapshot(PluginId id) {
        var item = catalog.SingleOrDefault(item => item.PluginId == id);
        return item is null || Scenario == PluginScenario.MissingReferences ? null : new(item,
            grants.Values.Where(grant => grant.PluginId == id).ToArray(), connections.Values.Where(connection => connection.PluginId == id).ToArray(),
            item.Capabilities.HasFlag(PluginCapabilityKind.HostCommand) ? [new(new("sandbox.inspect"), "Inspect", "Harmless descriptor", PluginGrantRiskKind.Low, false),
                new(new("sandbox.report"), "Report", "Harmless descriptor", PluginGrantRiskKind.Low, false)] : [],
            Scenario == PluginScenario.MissingDescriptor ? [] : item.Descriptor.Connections, item.Descriptor.OAuth2);
    }
    public Task<IReadOnlyList<PluginOAuthConnectionStatusItem>> OAuthAsync(PluginId id, CancellationToken token) {
        if (FailOAuth || Scenario == PluginScenario.UnavailableReads) {
            throw new InvalidOperationException("Controlled OAuth status read unavailable.");
        }
        return Task.FromResult<IReadOnlyList<PluginOAuthConnectionStatusItem>>(oauth.Values.Where(item => item.PluginId == id).ToArray());
    }
    public Task<IReadOnlyList<PluginLogItem>> LogsAsync(PluginLogQuery query, CancellationToken token) {
        LogQueries.Add(query);
        return ReadLogsOverride?.Invoke(query, token) ?? Task.FromResult<IReadOnlyList<PluginLogItem>>(logEntries
            .Where(item => (query.PluginId is null || item.PluginId == query.PluginId) && (query.StreamKind is null || item.StreamKind == query.StreamKind))
            .OrderByDescending(item => item.CreatedAtUtc).Take(query.Take).ToArray());
    }
    public async Task<IReadOnlyList<PluginPackageCatalogItem>> PackagesAsync(CancellationToken token) {
        await WaitAsync(PluginScenarioWait.Package);
        if (FailPackages || Scenario == PluginScenario.UnavailableReads) {
            throw new InvalidOperationException("Controlled package read unavailable.");
        }
        return [new(new("sandbox.package"), new("sandbox.plugin-1"), "Harmless package",
            "Local fake storage only", "1.0.0", "Sandbox", PluginPackageCatalogSourceKind.Catalogue, PluginSourceKind.LocalPackage,
            PluginTrustLevel.LocalPackage, PluginCapabilityKind.None, installed, true, false, "icons/plugin.svg", "fixture.zip")];
    }
    public Task<PluginRuntimeRestartStatus> RestartStatusAsync(CancellationToken token) {
        if (FailRestart || Scenario == PluginScenario.UnavailableReads) {
            throw new InvalidOperationException("Controlled restart status read unavailable.");
        }
        return Task.FromResult(restart);
    }

    public async Task<PluginWriteReceipt<PluginConnectionItem>> SaveAsync(PluginId id, PluginConnectionSaveRequest request) {
        SaveDispatches++;
        await WaitAsync(PluginScenarioWait.Save);
        if (Scenario == PluginScenario.RefusedSave) {
            return PluginWriteReceipt<PluginConnectionItem>.Refused();
        }
        Interlocked.Increment(ref saveWrites);
        var connectionId = request.Id ?? new PluginConnectionId(Guid.NewGuid());
        var item = new PluginConnectionItem(connectionId, id, request.ConnectionKey, request.DisplayName, request.SettingsJson,
            request.IsEnabled, "Not checked", "sandbox", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddSeconds(SaveWrites), Guid.NewGuid());
        connections[connectionId] = item;
        if (Scenario == PluginScenario.HeldReadback) {
            Hold(PluginScenarioWait.Settings);
        }
        return Scenario == PluginScenario.UnknownSave ? PluginWriteReceipt<PluginConnectionItem>.Unknown() : PluginWriteReceipt<PluginConnectionItem>.Saved(item);
    }
    public async Task<PluginWriteReceipt<PluginCapabilityGrantItem>> GrantAsync(PluginId id, PluginGrantUpdateRequest request) {
        await WaitAsync(PluginScenarioWait.Grant);
        Interlocked.Increment(ref grantWrites);
        var item = new PluginCapabilityGrantItem(id, request.Capability, request.RecipeId is null ? null : new(request.RecipeId),
            request.ScopeKind, request.ScopeKey, request.State, request.RiskKind, request.Reason, "sandbox", DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(GrantWrites), Guid.NewGuid());
        grants[PluginGrantTarget.From(item)] = item;
        return PluginWriteReceipt<PluginCapabilityGrantItem>.Saved(item);
    }
    public async Task<PluginWriteReceipt<PluginCatalogItem>> LifecycleAsync(PluginId id, PluginLifecycleAction action) {
        await WaitAsync(PluginScenarioWait.Lifecycle);
        lock (lifecycleGate) {
            LifecycleWrites++;
            var index = catalog.FindIndex(item => item.PluginId == id);
            var item = catalog[index] with { InstallationState = action == PluginLifecycleAction.Disable
                ? PluginInstallationStateKind.InstalledDisabled : PluginInstallationStateKind.InstalledEnabled };
            catalog[index] = item;
            return PluginWriteReceipt<PluginCatalogItem>.Saved(item);
        }
    }
    public async Task<PluginWriteReceipt<PluginOAuthStartResponse>> StartOAuthAsync(PluginId id, PluginOAuthStartRequest request) {
        await WaitAsync(PluginScenarioWait.OAuth);
        OAuthStarts++;
        return PluginWriteReceipt<PluginOAuthStartResponse>.Saved(new(request.ConnectionId!.Value,
            "https://provider.invalid/authorize", "/api/plugins/oauth/callback", ["profile"]));
    }
    public Task OpenOAuthAsync(string authorizationUrl) {
        if (FailBrowser) {
            throw new InvalidOperationException("Controlled browser effect failure.");
        }
        BrowserEffects++;
        return Task.CompletedTask;
    }
    public Task<PluginWriteReceipt<PluginOAuthDisconnectResponse>> DisconnectOAuthAsync(PluginId id, PluginConnectionId connectionId) {
        oauth.TryRemove(connectionId, out _);
        return Task.FromResult(PluginWriteReceipt<PluginOAuthDisconnectResponse>.Saved(new(connectionId, PluginOAuthConnectionStatusKind.NotConnected)));
    }
    public Task<PluginWriteReceipt<PluginPackageInstallResult>> InstallAsync(PluginPackageId id) {
        PackageWrites++;
        installed = true;
        restart = new(true, false, "Sandbox package was installed.", DateTimeOffset.UnixEpoch, "", null, 0);
        var result = new PluginPackageInstallResult(id, new("sandbox.plugin-1"), "Harmless package", "1.0.0",
            PluginPackageInstallSourceKind.Catalogue, true, restart);
        return Task.FromResult(Scenario == PluginScenario.PackageWarning
            ? new PluginWriteReceipt<PluginPackageInstallResult>(PluginMutationStatus.SavedWithWarning) {
                PackageProgress = new(id, result.PluginId, PluginPackageStage.RestartRecorded, true, restart)
            }
            : PluginWriteReceipt<PluginPackageInstallResult>.Saved(result));
    }
    public async Task<PluginWriteReceipt<PluginPackageInstallResult>> UploadAsync(Stream stream, string fileName, CancellationToken token) {
        await WaitAsync(PluginScenarioWait.Upload);
        var buffer = new byte[8192];
        long count = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, token)) != 0) {
            count += read;
            if (count > MaxPackageBytes) {
                return PluginWriteReceipt<PluginPackageInstallResult>.Refused();
            }
        }
        if (count == 0) {
            return PluginWriteReceipt<PluginPackageInstallResult>.Refused();
        }
        return await InstallAsync(new("sandbox.package"));
    }
    public Task<PluginWriteReceipt<PluginRuntimeRestartStatus>> RestartAsync() {
        RestartRequests++;
        restart = restart with { IsRestartRequested = true, RequestedBy = "sandbox", RequestedAtUtc = DateTimeOffset.UnixEpoch };
        return Task.FromResult(PluginWriteReceipt<PluginRuntimeRestartStatus>.Saved(restart));
    }
    public void ReportFailure(string operation, Exception exception) => Diagnostics.Add(operation + ": " + exception.GetType().Name);
}
