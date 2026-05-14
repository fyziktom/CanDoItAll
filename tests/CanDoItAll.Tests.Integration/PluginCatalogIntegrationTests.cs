using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Text.Json;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class PluginCatalogIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PluginCatalog_lists_bundled_source_and_persists_installation_state()
    {
        var descriptor = CreatePluginDescriptor();
        await using var environment = CanDoItAllTestEnvironment.Create("plugin-catalog-tests");
        var profile = environment.CreateManagedSqliteProfile("plugins");
        await using var services = await BuildServiceProviderAsync(profile, [descriptor]);
        await using var scope = services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<PluginCatalogService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var initialCatalog = await catalog.ListCatalogAsync();
        var installResult = await catalog.InstallAsync(descriptor.Id, new PluginInstallRequest(Enable: true, Actor: "integration-test"));
        var installedCatalog = await catalog.ListCatalogAsync();
        var disableResult = await catalog.SetEnabledAsync(descriptor.Id, isEnabled: false, new PluginInstallationUpdateRequest("integration-test"));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var installation = await dbContext.Set<PluginInstallationRecord>().SingleAsync(item => item.PluginId == descriptor.Id.Value);

        var initialPlugin = Assert.Single(initialCatalog, item => item.PluginId == descriptor.Id);
        Assert.Equal(PluginInstallationStateKind.NotInstalled, initialPlugin.InstallationState);
        Assert.True(installResult.IsSuccess, FormatErrors(installResult.Errors));
        Assert.Equal(PluginInstallationStateKind.InstalledEnabled, installResult.Value!.InstallationState);
        Assert.Contains(installedCatalog, item => item.PluginId == descriptor.Id && item.InstallationState == PluginInstallationStateKind.InstalledEnabled);
        Assert.True(disableResult.IsSuccess, FormatErrors(disableResult.Errors));
        Assert.Equal(PluginInstallationStateKind.InstalledDisabled, disableResult.Value!.InstallationState);
        Assert.Equal(descriptor.Package!.PackageId.Value, installation.PackageId);
        Assert.Equal(descriptor.Version, installation.Version);
        Assert.Equal("integration-test", installation.InstalledBy);
        Assert.DoesNotContain(
            typeof(PluginInstallationRecord).GetProperties(),
            property => property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("ConnectionSettings", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PluginInstallation_returns_unavailable_when_bundled_source_disappears()
    {
        var descriptor = CreatePluginDescriptor();
        await using var environment = CanDoItAllTestEnvironment.Create("plugin-installation-tests");
        var profile = environment.CreateManagedSqliteProfile("plugins");

        await using (var services = await BuildServiceProviderAsync(profile, [descriptor]))
        await using (var scope = services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<PluginCatalogService>();
            var installResult = await catalog.InstallAsync(descriptor.Id, new PluginInstallRequest(Enable: true, Actor: "integration-test"));
            Assert.True(installResult.IsSuccess, FormatErrors(installResult.Errors));
        }

        await using var unavailableServices = await BuildServiceProviderAsync(profile, []);
        await using var unavailableScope = unavailableServices.CreateAsyncScope();
        var unavailableCatalog = unavailableScope.ServiceProvider.GetRequiredService<PluginCatalogService>();

        var items = await unavailableCatalog.ListCatalogAsync();
        var installed = Assert.Single(items, item => item.PluginId == descriptor.Id);

        Assert.Equal(descriptor.Id, installed.PluginId);
        Assert.Equal(PluginInstallationStateKind.InstalledEnabled, installed.InstallationState);
        Assert.Equal(PluginCatalogAvailabilityKind.Unavailable, installed.Availability);
        Assert.Contains("no bundled catalog source", installed.UnavailableReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PluginCatalog_api_returns_catalog_route()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);

        var response = await host.Client.GetAsync("/api/plugins/catalog");
        var body = await response.Content.ReadAsStringAsync();
        var catalog = JsonSerializer.Deserialize<IReadOnlyList<PluginCatalogItem>>(body, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(catalog);
        Assert.Contains(catalog, item => item.PluginId == DockerPluginConstants.PluginId);

        using var openApiPayload = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = openApiPayload.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/plugins/catalog", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/packages/catalog", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/packages/catalog/{packageId}/install", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/packages/upload", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/runtime/restart-status", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/runtime/restart", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/{pluginId}/install", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/{pluginId}/enable", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/{pluginId}/disable", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/{pluginId}/settings", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/{pluginId}/grants", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/{pluginId}/connections", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/{pluginId}/oauth/status", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/{pluginId}/oauth/start", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/{pluginId}/connections/{connectionId}/oauth/disconnect", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/oauth/callback", out _));
    }

    [Fact]
    public async Task Plugin_package_catalog_installs_package_and_exposes_descriptor_without_recompilation()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("plugin-package-catalog-tests");
        var profile = environment.CreateManagedSqliteProfile("plugins");
        var packagePaths = CreatePackagePathOverrides(environment.RootPath);
        Directory.CreateDirectory(packagePaths.CatalogRootPath);
        var manifest = CreatePackageManifest(
            pluginId: "integration.runtime.catalog",
            packageId: "integration.runtime.catalog.package",
            displayName: "Runtime catalog package",
            requiresRestart: false);
        await File.WriteAllBytesAsync(
            Path.Combine(packagePaths.CatalogRootPath, "runtime-catalog-package.zip"),
            CreatePackageArchive(manifest));

        await using var services = await BuildServiceProviderAsync(profile, [], packagePaths.ConfigurationOverrides);
        await using var scope = services.CreateAsyncScope();
        var packageService = scope.ServiceProvider.GetRequiredService<PluginPackageService>();
        var catalogService = scope.ServiceProvider.GetRequiredService<PluginCatalogService>();

        var packages = await packageService.ListPackagesAsync();
        var installResult = await packageService.InstallFromCatalogAsync(
            manifest.Plugin.Package!.PackageId,
            new PluginPackageInstallRequest(Enable: true, Actor: "integration-test"));
        var catalog = await catalogService.ListCatalogAsync();

        var package = Assert.Single(packages, item => item.PackageId == manifest.Plugin.Package!.PackageId);
        Assert.False(package.IsInstalled);
        Assert.Equal(PluginPackageCatalogSourceKind.Catalogue, package.CatalogSourceKind);
        Assert.True(installResult.IsSuccess, FormatErrors(installResult.Errors));
        Assert.False(installResult.Value!.RestartRequired);
        Assert.Contains(
            catalog,
            item => item.PluginId == manifest.Plugin.Id &&
                    item.InstallationState == PluginInstallationStateKind.InstalledEnabled &&
                    item.Availability == PluginCatalogAvailabilityKind.Available);
    }

    [Fact]
    public async Task Plugin_package_upload_installs_package_and_marks_restart_required()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("plugin-package-upload-tests");
        var profile = environment.CreateManagedSqliteProfile("plugins");
        var packagePaths = CreatePackagePathOverrides(environment.RootPath);
        var manifest = CreatePackageManifest(
            pluginId: "integration.runtime.upload",
            packageId: "integration.runtime.upload.package",
            displayName: "Runtime upload package",
            requiresRestart: true);

        await using var services = await BuildServiceProviderAsync(profile, [], packagePaths.ConfigurationOverrides);
        await using var scope = services.CreateAsyncScope();
        var packageService = scope.ServiceProvider.GetRequiredService<PluginPackageService>();
        var restartService = scope.ServiceProvider.GetRequiredService<PluginRuntimeRestartService>();
        await using var stream = new MemoryStream(CreatePackageArchive(manifest));

        var installResult = await packageService.InstallUploadedPackageAsync(
            stream,
            "runtime-upload-package.zip",
            new PluginPackageInstallRequest(Enable: true, Actor: "integration-test"));
        var restartStatus = await restartService.GetStatusAsync();

        Assert.True(installResult.IsSuccess, FormatErrors(installResult.Errors));
        Assert.True(installResult.Value!.RestartRequired);
        Assert.True(restartStatus.IsRestartRequired);
        Assert.Contains("Runtime upload package", restartStatus.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plugin_package_upload_rejects_path_traversal_entries()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("plugin-package-traversal-tests");
        var profile = environment.CreateManagedSqliteProfile("plugins");
        var packagePaths = CreatePackagePathOverrides(environment.RootPath);
        var manifest = CreatePackageManifest(
            pluginId: "integration.runtime.traversal",
            packageId: "integration.runtime.traversal.package",
            displayName: "Traversal package",
            requiresRestart: false);
        await using var services = await BuildServiceProviderAsync(profile, [], packagePaths.ConfigurationOverrides);
        await using var scope = services.CreateAsyncScope();
        var packageService = scope.ServiceProvider.GetRequiredService<PluginPackageService>();
        await using var stream = new MemoryStream(CreatePackageArchive(
            manifest,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["../escape.txt"] = Encoding.UTF8.GetBytes("escaped")
            }));

        var installResult = await packageService.InstallUploadedPackageAsync(
            stream,
            "traversal-package.zip",
            new PluginPackageInstallRequest(Enable: true, Actor: "integration-test"));

        Assert.True(installResult.IsFailure);
        Assert.Contains(installResult.Errors, error => error.Code == "plugins.package-invalid");
        Assert.False(File.Exists(Path.Combine(environment.RootPath, "escape.txt")));
    }

    [Fact]
    public async Task Plugin_runtime_restart_request_stops_host_lifetime()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("plugin-runtime-restart-tests");
        var profile = environment.CreateManagedSqliteProfile("plugins");
        await using var services = await BuildServiceProviderAsync(profile, []);
        var restartService = services.GetRequiredService<PluginRuntimeRestartService>();
        var lifetime = services.GetRequiredService<TestHostApplicationLifetime>();

        await restartService.MarkRestartRequiredAsync("Integration restart proof.", "integration-test");
        var restartResult = await restartService.RequestRestartAsync(new PluginRuntimeRestartRequest("integration-test"));
        await Task.Delay(TimeSpan.FromMilliseconds(1500));

        Assert.True(restartResult.IsSuccess, FormatErrors(restartResult.Errors));
        Assert.True(restartResult.Value!.IsRestartRequested);
        Assert.True(lifetime.ApplicationStopping.IsCancellationRequested);
    }

    [Fact]
    public async Task Gmail_oauth_start_creates_vault_backed_session_without_persisting_token_material()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var installResponse = await host.Client.PostAsJsonAsync(
            $"/api/plugins/{GmailPluginConstants.PluginId.Value}/install",
            new PluginInstallRequest(Enable: true, Actor: "integration-test"));
        var installBody = await installResponse.Content.ReadAsStringAsync();
        Assert.True(installResponse.IsSuccessStatusCode, installBody);
        await GrantAsync(host, GmailPluginConstants.PluginId, PluginCapabilityKind.OAuth2);

        var startResponse = await host.Client.PostAsJsonAsync(
            $"/api/plugins/{GmailPluginConstants.PluginId.Value}/oauth/start",
            new PluginOAuthStartRequest(GmailPluginConstants.ConnectionKey, ReturnPath: "/plugins"));
        var startBody = await startResponse.Content.ReadAsStringAsync();
        Assert.True(startResponse.IsSuccessStatusCode, startBody);
        var start = JsonSerializer.Deserialize<PluginOAuthStartResponse>(startBody, JsonOptions)!;

        await using var scope = host.App.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var connection = await dbContext.Set<PluginConnectionRecord>().SingleAsync(item => item.Id == start.ConnectionId.Value);
        var session = await dbContext.Set<PluginOAuthSessionRecord>().SingleAsync(item => item.ConnectionId == start.ConnectionId.Value);

        Assert.Contains("accounts.google.com", start.AuthorizationUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("code_challenge=", start.AuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains(GmailPluginConstants.ClientId, start.AuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains(GmailPluginConstants.GmailModifyScope, WebUtility.UrlDecode(start.AuthorizationUrl), StringComparison.Ordinal);
        Assert.Equal("{}", connection.SettingsJson);
        Assert.DoesNotContain("access_token", connection.SettingsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh_token", connection.SettingsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client_secret", connection.SettingsJson, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(session.StateHash);
        Assert.NotEmpty(session.CodeVerifierVaultKey);
    }

    [Fact]
    public async Task Office365_oauth_start_uses_connection_settings_client_id_and_redirect_uri()
    {
        const string clientId = "2f2a235f-7970-477b-93ba-656be29a8d03";
        const string redirectUri = "http://localhost:5107/api/plugins/oauth/callback";

        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var installResponse = await host.Client.PostAsJsonAsync(
            $"/api/plugins/{Office365PluginConstants.PluginId.Value}/install",
            new PluginInstallRequest(Enable: true, Actor: "integration-test"));
        var installBody = await installResponse.Content.ReadAsStringAsync();
        Assert.True(installResponse.IsSuccessStatusCode, installBody);
        await GrantAsync(host, Office365PluginConstants.PluginId, PluginCapabilityKind.OAuth2);

        var connectionSettings = new ConfigurationState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PluginOAuthConnectionSettingKeys.ClientId] = clientId,
            [PluginOAuthConnectionSettingKeys.RedirectUri] = redirectUri
        });
        var connectionResponse = await host.Client.PostAsJsonAsync(
            $"/api/plugins/{Office365PluginConstants.PluginId.Value}/connections",
            new PluginConnectionSaveRequest(
                Id: null,
                Office365PluginConstants.ConnectionKey,
                "CanDoItAll Local Connector",
                connectionSettings.ToJson(),
                IsEnabled: true));
        var connectionBody = await connectionResponse.Content.ReadAsStringAsync();
        Assert.True(connectionResponse.IsSuccessStatusCode, connectionBody);
        var connection = JsonSerializer.Deserialize<PluginConnectionItem>(connectionBody, JsonOptions)!;

        var startResponse = await host.Client.PostAsJsonAsync(
            $"/api/plugins/{Office365PluginConstants.PluginId.Value}/oauth/start",
            new PluginOAuthStartRequest(
                Office365PluginConstants.ConnectionKey,
                connection.Id,
                ReturnPath: "/plugins"));
        var startBody = await startResponse.Content.ReadAsStringAsync();
        Assert.True(startResponse.IsSuccessStatusCode, startBody);
        var start = JsonSerializer.Deserialize<PluginOAuthStartResponse>(startBody, JsonOptions)!;
        var decodedAuthorizationUrl = WebUtility.UrlDecode(start.AuthorizationUrl);

        Assert.Contains("login.microsoftonline.com/common/oauth2/v2.0/authorize", start.AuthorizationUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(clientId, decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains(redirectUri, decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains(Office365PluginConstants.OpenIdScope, decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains(Office365PluginConstants.MailReadScope, decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains(Office365PluginConstants.MailReadWriteScope, decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains(Office365PluginConstants.MailboxSettingsReadWriteScope, decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains(Office365PluginConstants.OfflineAccessScope, decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains("prompt=consent", decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", connection.SettingsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh_token", connection.SettingsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client_secret", connection.SettingsJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Office365_oauth_callback_preserves_openid_and_offline_access_from_token_artifacts()
    {
        const string clientId = "2f2a235f-7970-477b-93ba-656be29a8d03";
        const string redirectUri = "http://localhost:5107/api/plugins/oauth/callback";

        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services =>
            {
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(request =>
                {
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Contains("/oauth2/v2.0/token", request.RequestUri?.AbsoluteUri ?? string.Empty, StringComparison.OrdinalIgnoreCase);

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["access_token"] = "graph-access-token",
                            ["refresh_token"] = "graph-refresh-token",
                            ["expires_in"] = 3600,
                            ["scope"] = string.Join(
                                ' ',
                                Office365PluginConstants.MailReadScope,
                                Office365PluginConstants.MailReadWriteScope,
                                Office365PluginConstants.MailboxSettingsReadWriteScope),
                            ["token_type"] = "Bearer",
                            ["id_token"] = "openid-token"
                        })
                    };
                }));
            });
        var installResponse = await host.Client.PostAsJsonAsync(
            $"/api/plugins/{Office365PluginConstants.PluginId.Value}/install",
            new PluginInstallRequest(Enable: true, Actor: "integration-test"));
        var installBody = await installResponse.Content.ReadAsStringAsync();
        Assert.True(installResponse.IsSuccessStatusCode, installBody);
        await GrantAsync(host, Office365PluginConstants.PluginId, PluginCapabilityKind.OAuth2);

        var connectionSettings = new ConfigurationState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PluginOAuthConnectionSettingKeys.ClientId] = clientId,
            [PluginOAuthConnectionSettingKeys.RedirectUri] = redirectUri
        });
        var connectionResponse = await host.Client.PostAsJsonAsync(
            $"/api/plugins/{Office365PluginConstants.PluginId.Value}/connections",
            new PluginConnectionSaveRequest(
                Id: null,
                Office365PluginConstants.ConnectionKey,
                "CanDoItAll Local Connector",
                connectionSettings.ToJson(),
                IsEnabled: true));
        var connectionBody = await connectionResponse.Content.ReadAsStringAsync();
        Assert.True(connectionResponse.IsSuccessStatusCode, connectionBody);
        var connection = JsonSerializer.Deserialize<PluginConnectionItem>(connectionBody, JsonOptions)!;

        var startResponse = await host.Client.PostAsJsonAsync(
            $"/api/plugins/{Office365PluginConstants.PluginId.Value}/oauth/start",
            new PluginOAuthStartRequest(
                Office365PluginConstants.ConnectionKey,
                connection.Id,
                ReturnPath: "/plugins"));
        var startBody = await startResponse.Content.ReadAsStringAsync();
        Assert.True(startResponse.IsSuccessStatusCode, startBody);
        var start = JsonSerializer.Deserialize<PluginOAuthStartResponse>(startBody, JsonOptions)!;
        var state = ReadQueryParameter(start.AuthorizationUrl, "state");

        await using (var callbackScope = host.App.Services.CreateAsyncScope())
        {
            var oauthService = callbackScope.ServiceProvider.GetRequiredService<PluginOAuthService>();
            var returnUri = await oauthService.CompleteCallbackAsync(state, "provider-code", null, null);
            Assert.Contains("oauth=connected", returnUri.ToString(), StringComparison.Ordinal);
        }

        var statusResponse = await host.Client.GetAsync($"/api/plugins/{Office365PluginConstants.PluginId.Value}/oauth/status");
        var statusBody = await statusResponse.Content.ReadAsStringAsync();
        Assert.True(statusResponse.IsSuccessStatusCode, statusBody);
        var statuses = JsonSerializer.Deserialize<IReadOnlyList<PluginOAuthConnectionStatusItem>>(statusBody, JsonOptions)!;
        var status = Assert.Single(statuses);

        Assert.Equal(PluginOAuthConnectionStatusKind.Connected, status.Status);
        Assert.Contains(Office365PluginConstants.MailReadScope, status.GrantedScopes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Office365PluginConstants.MailReadWriteScope, status.GrantedScopes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Office365PluginConstants.MailboxSettingsReadWriteScope, status.GrantedScopes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Office365PluginConstants.OpenIdScope, status.GrantedScopes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Office365PluginConstants.OfflineAccessScope, status.GrantedScopes, StringComparer.OrdinalIgnoreCase);
        Assert.Empty(status.LastErrorCode);
    }

    [Fact]
    public async Task OAuth_workflow_connection_resolver_selects_connected_connection_when_setting_is_blank()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        await using var scope = host.App.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var oauthService = scope.ServiceProvider.GetRequiredService<PluginOAuthService>();
        var connectionId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<PluginConnectionRecord>().Add(new PluginConnectionRecord
            {
                Id = connectionId,
                PluginId = Office365PluginConstants.PluginId.Value,
                ConnectionKey = Office365PluginConstants.ConnectionKey.Value,
                DisplayName = "Office365 account",
                SettingsJson = "{}",
                IsEnabled = true,
                HealthStatus = "Connected",
                UpdatedBy = "integration-test",
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp,
                ConcurrencyToken = Guid.NewGuid()
            });
            dbContext.Set<PluginOAuthConnectionRecord>().Add(new PluginOAuthConnectionRecord
            {
                ConnectionId = connectionId,
                PluginId = Office365PluginConstants.PluginId.Value,
                ConnectionKey = Office365PluginConstants.ConnectionKey.Value,
                ProviderKey = $"{Office365PluginConstants.PluginId.Value}:{Office365PluginConstants.ConnectionKey.Value}",
                TokenVaultKey = "test-vault-key",
                Status = nameof(PluginOAuthConnectionStatusKind.Connected),
                AccountDisplay = "connected@example.test",
                GrantedScopesJson = JsonSerializer.Serialize(new[] { Office365PluginConstants.MailReadScope }, JsonOptions),
                AccessTokenExpiresAtUtc = timestamp.AddHours(1),
                RefreshTokenExpiresAtUtc = timestamp.AddDays(1),
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp,
                ConcurrencyToken = Guid.NewGuid()
            });
            await dbContext.SaveChangesAsync();
        }

        var resolved = await oauthService.ResolveWorkflowConnectionIdAsync(
            Office365PluginConstants.PluginId,
            Office365PluginConstants.ConnectionKey,
            configuredConnectionId: string.Empty,
            [Office365PluginConstants.MailReadScope]);

        Assert.Equal(connectionId, resolved.Value);
    }

    [Fact]
    public async Task OAuth_workflow_connection_resolver_rejects_invalid_explicit_connection_id()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        await using var scope = host.App.Services.CreateAsyncScope();
        var oauthService = scope.ServiceProvider.GetRequiredService<PluginOAuthService>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            oauthService.ResolveWorkflowConnectionIdAsync(
                Office365PluginConstants.PluginId,
                Office365PluginConstants.ConnectionKey,
                "not-a-guid",
                [Office365PluginConstants.MailReadScope]).AsTask());

        Assert.Contains("valid GUID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gmail_oauth_status_requires_reconnect_when_existing_grant_is_missing_current_scope()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var installResponse = await host.Client.PostAsJsonAsync(
            $"/api/plugins/{GmailPluginConstants.PluginId.Value}/install",
            new PluginInstallRequest(Enable: true, Actor: "integration-test"));
        var installBody = await installResponse.Content.ReadAsStringAsync();
        Assert.True(installResponse.IsSuccessStatusCode, installBody);
        await GrantAsync(host, GmailPluginConstants.PluginId, PluginCapabilityKind.OAuth2);

        var startResponse = await host.Client.PostAsJsonAsync(
            $"/api/plugins/{GmailPluginConstants.PluginId.Value}/oauth/start",
            new PluginOAuthStartRequest(GmailPluginConstants.ConnectionKey, ReturnPath: "/plugins"));
        var startBody = await startResponse.Content.ReadAsStringAsync();
        Assert.True(startResponse.IsSuccessStatusCode, startBody);
        var start = JsonSerializer.Deserialize<PluginOAuthStartResponse>(startBody, JsonOptions)!;

        await using var scope = host.App.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var timestamp = DateTimeOffset.UtcNow;
        dbContext.Set<PluginOAuthConnectionRecord>().Add(new PluginOAuthConnectionRecord
        {
            ConnectionId = start.ConnectionId.Value,
            PluginId = GmailPluginConstants.PluginId.Value,
            ConnectionKey = GmailPluginConstants.ConnectionKey.Value,
            ProviderKey = $"{GmailPluginConstants.PluginId.Value}:{GmailPluginConstants.ConnectionKey.Value}",
            TokenVaultKey = "plugins/oauth/test/token",
            Status = nameof(PluginOAuthConnectionStatusKind.Connected),
            GrantedScopesJson = JsonSerializer.Serialize(
                new[] { "https://www.googleapis.com/auth/gmail.readonly" },
                JsonOptions),
            AccessTokenExpiresAtUtc = timestamp.AddHours(1),
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp
        });
        await dbContext.SaveChangesAsync();

        var statusResponse = await host.Client.GetAsync($"/api/plugins/{GmailPluginConstants.PluginId.Value}/oauth/status");
        var statusBody = await statusResponse.Content.ReadAsStringAsync();
        Assert.True(statusResponse.IsSuccessStatusCode, statusBody);
        var statuses = JsonSerializer.Deserialize<IReadOnlyList<PluginOAuthConnectionStatusItem>>(statusBody, JsonOptions)!;
        var status = Assert.Single(statuses);

        Assert.Equal(PluginOAuthConnectionStatusKind.ReconnectRequired, status.Status);
        Assert.Equal("oauth-scope-missing", status.LastErrorCode);
        Assert.Contains(GmailPluginConstants.GmailModifyScope, status.LastErrorDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plugin_grant_evaluator_requires_install_enabled_capability_and_recipe_grants()
    {
        var descriptor = CreatePluginDescriptor() with
        {
            Capabilities = PluginCapabilityKind.WorkflowExecutor | PluginCapabilityKind.HostCommand
        };
        await using var environment = CanDoItAllTestEnvironment.Create("plugin-grant-tests");
        var profile = environment.CreateManagedSqliteProfile("plugins");
        await using var services = await BuildServiceProviderAsync(profile, [descriptor]);
        await using var scope = services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<PluginCatalogService>();
        var settings = scope.ServiceProvider.GetRequiredService<PluginSettingsService>();
        var evaluator = scope.ServiceProvider.GetRequiredService<PluginGrantEvaluator>();

        var installResult = await catalog.InstallAsync(descriptor.Id, new PluginInstallRequest(Enable: true, Actor: "integration-test"));
        var missingWorkflowGrant = evaluator.Evaluate(descriptor.Id, PluginCapabilityKind.WorkflowExecutor);
        var workflowGrant = await settings.UpdateGrantAsync(
            descriptor.Id,
            new PluginGrantUpdateRequest(PluginCapabilityKind.WorkflowExecutor, PluginGrantState.Granted),
            "integration-test");
        var allowedWorkflowGrant = evaluator.Evaluate(descriptor.Id, PluginCapabilityKind.WorkflowExecutor);
        var missingHostCommandGrant = evaluator.Evaluate(descriptor.Id, PluginCapabilityKind.HostCommand, PluginHostToolRecipeIds.DockerStartContainer);
        var hostCommandGrant = await settings.UpdateGrantAsync(
            descriptor.Id,
            new PluginGrantUpdateRequest(PluginCapabilityKind.HostCommand, PluginGrantState.Granted, RiskKind: PluginGrantRiskKind.High),
            "integration-test");
        var missingRecipeGrant = evaluator.Evaluate(descriptor.Id, PluginCapabilityKind.HostCommand, PluginHostToolRecipeIds.DockerStartContainer);
        var recipeGrant = await settings.UpdateGrantAsync(
            descriptor.Id,
            new PluginGrantUpdateRequest(
                PluginCapabilityKind.HostCommand,
                PluginGrantState.Granted,
                PluginHostToolRecipeIds.DockerStartContainer.Value,
                RiskKind: PluginGrantRiskKind.High),
            "integration-test");
        var allowedRecipeGrant = evaluator.Evaluate(descriptor.Id, PluginCapabilityKind.HostCommand, PluginHostToolRecipeIds.DockerStartContainer);

        Assert.True(installResult.IsSuccess, FormatErrors(installResult.Errors));
        Assert.False(missingWorkflowGrant.Allowed);
        Assert.Equal(PluginGrantDecisionKind.GrantMissing, missingWorkflowGrant.Kind);
        Assert.True(workflowGrant.IsSuccess, FormatErrors(workflowGrant.Errors));
        Assert.True(allowedWorkflowGrant.Allowed);
        Assert.False(missingHostCommandGrant.Allowed);
        Assert.Equal(PluginGrantDecisionKind.GrantMissing, missingHostCommandGrant.Kind);
        Assert.True(hostCommandGrant.IsSuccess, FormatErrors(hostCommandGrant.Errors));
        Assert.False(missingRecipeGrant.Allowed);
        Assert.Equal(PluginGrantDecisionKind.RecipeGrantMissing, missingRecipeGrant.Kind);
        Assert.True(recipeGrant.IsSuccess, FormatErrors(recipeGrant.Errors));
        Assert.True(allowedRecipeGrant.Allowed);
    }

    [Fact]
    public async Task Plugin_api_controls_docker_plugin_settings_and_workflow_executor_availability()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var settingsResponse = await host.Client.GetAsync($"/api/plugins/{DockerPluginConstants.PluginId.Value}/settings");
        var settingsBody = await settingsResponse.Content.ReadAsStringAsync();
        Assert.True(settingsResponse.IsSuccessStatusCode, settingsBody);
        var settings = JsonSerializer.Deserialize<PluginSettingsDetail>(settingsBody, JsonOptions)!;

        var initialCatalog = await ReadWorkflowExecutorCatalogAsync(host);
        var initialDockerStart = Assert.Single(initialCatalog, item => item.Id == DockerPluginConstants.StartContainerExecutorId);

        var installResponse = await host.Client.PostAsJsonAsync(
            $"/api/plugins/{DockerPluginConstants.PluginId.Value}/install",
            new PluginInstallRequest(Enable: true, Actor: "integration-test"));
        var installBody = await installResponse.Content.ReadAsStringAsync();
        Assert.True(installResponse.IsSuccessStatusCode, installBody);

        await GrantAsync(host, DockerPluginConstants.PluginId, PluginCapabilityKind.WorkflowExecutor);
        await GrantAsync(host, DockerPluginConstants.PluginId, PluginCapabilityKind.HostCommand);
        await GrantAsync(host, DockerPluginConstants.PluginId, PluginCapabilityKind.HostCommand, PluginHostToolRecipeIds.DockerStartContainer);

        var updatedCatalog = await ReadWorkflowExecutorCatalogAsync(host);
        var updatedDockerStart = Assert.Single(updatedCatalog, item => item.Id == DockerPluginConstants.StartContainerExecutorId);

        Assert.Contains(settings.Grants, item => item.Capability == PluginCapabilityKind.WorkflowExecutor);
        Assert.Contains(settings.Grants, item => item.RecipeId == PluginHostToolRecipeIds.DockerStartContainer);
        Assert.False(initialDockerStart.CanExecute);
        Assert.Equal(WorkflowExecutorSourceKind.BundledPlugin, updatedDockerStart.Source.Kind);
        Assert.Equal(DockerPluginConstants.PluginId.Value, updatedDockerStart.Source.PluginId);
        Assert.True(updatedDockerStart.CanExecute);
    }

    [Fact]
    public async Task Docker_qdrant_plugin_workflow_live_proof()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CANDOITALL_RUN_DOCKER_PROOF"), "1", StringComparison.Ordinal))
        {
            return;
        }

        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services =>
            {
                services.RemoveAll<IWorkflowLlmComponentInvoker>();
                services.AddScoped<IWorkflowLlmComponentInvoker, DockerLogSummaryLlmInvoker>();
            });
        await ConfigureDockerPluginForProofAsync(host);
        var component = await SaveDockerLogSummaryComponentAsync(host);
        var workflow = CreateDockerProofWorkflow(component.Id);

        var response = await host.Client.PostAsJsonAsync(
            "/api/workflows/test-runs",
            new WorkflowTestRunRequest(
                WorkflowId: null,
                VersionId: null,
                DraftDefinition: workflow,
                InputJson: "{}",
                RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
                ValidateOnly: false));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var result = JsonSerializer.Deserialize<WorkflowTestRunResult>(body, JsonOptions)!;

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(result.Run);
        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        var eventText = string.Join(Environment.NewLine, result.Events.Select(item => item.Message + item.PayloadJson));
        Assert.Contains("qdrant", eventText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Deterministic Docker log summary", eventText, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ServiceProvider> BuildServiceProviderAsync(
        TestDatabaseProfile profile,
        IReadOnlyList<PluginDescriptor> descriptors,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        return await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.PluginCatalog.Tests",
            TestSchemaBootstrapModules.Full,
            configurationOverrides,
            configureServices: services =>
            {
                services.AddScoped<IPluginCatalogSource>(_ => new StaticPluginCatalogSource(descriptors));
            });
    }

    private static PluginPackagePathOverrides CreatePackagePathOverrides(string rootPath)
    {
        var packageRootPath = Path.Combine(rootPath, "plugin-packages");
        var catalogRootPath = Path.Combine(packageRootPath, "catalogue");
        var installedRootPath = Path.Combine(packageRootPath, "installed");
        var runtimeStateRootPath = Path.Combine(packageRootPath, "state");
        return new PluginPackagePathOverrides(
            CatalogRootPath: catalogRootPath,
            InstalledRootPath: installedRootPath,
            RuntimeStateRootPath: runtimeStateRootPath,
            ConfigurationOverrides: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["PluginPackages:RootPath"] = packageRootPath,
                ["PluginPackages:CatalogRootPath"] = catalogRootPath,
                ["PluginPackages:InstalledRootPath"] = installedRootPath,
                ["PluginPackages:RuntimeStateRootPath"] = runtimeStateRootPath,
                ["PluginPackages:MaxPackageBytes"] = (20 * 1024 * 1024).ToString()
            });
    }

    private static PluginPackageManifest CreatePackageManifest(
        string pluginId,
        string packageId,
        string displayName,
        bool requiresRestart)
        => new()
        {
            Plugin = new PluginDescriptor(
                new PluginId(pluginId),
                displayName,
                "Runtime plugin package used by integration tests.",
                "1.0.0",
                "CanDoItAll",
                PluginSourceKind.LocalPackage,
                PluginTrustLevel.LocalPackage,
                "1.0.0",
                PluginCapabilityKind.None,
                [],
                PluginSettingsDescriptor.Empty,
                [],
                new PluginPackageDescriptor(
                    new PluginPackageId(packageId),
                    "1.0.0",
                    "1.0.0",
                    "sha256-test",
                    "signature-test")),
            IconPath = "icon.svg",
            RequiresRestart = requiresRestart
        };

    private static byte[] CreatePackageArchive(
        PluginPackageManifest manifest,
        IReadOnlyDictionary<string, byte[]>? extraEntries = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddArchiveEntry(
                archive,
                PluginPackageManifestStore.ManifestFileName,
                JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions));
            AddArchiveEntry(
                archive,
                manifest.IconPath,
                Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><rect width=\"16\" height=\"16\"/></svg>"));

            foreach (var entry in extraEntries ?? new Dictionary<string, byte[]>(StringComparer.Ordinal))
            {
                AddArchiveEntry(archive, entry.Key, entry.Value);
            }
        }

        return stream.ToArray();
    }

    private static void AddArchiveEntry(
        ZipArchive archive,
        string entryName,
        byte[] content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        entryStream.Write(content);
    }

    private static PluginDescriptor CreatePluginDescriptor()
        => new(
            new PluginId("integration.catalog"),
            "Integration catalog plugin",
            "Bundled plugin manifest used by integration tests.",
            "1.0.0",
            "CanDoItAll",
            PluginSourceKind.Bundled,
            PluginTrustLevel.Bundled,
            "1.0.0",
            PluginCapabilityKind.None,
            [],
            PluginSettingsDescriptor.Empty,
            [],
            new PluginPackageDescriptor(
                new PluginPackageId("integration.catalog.package"),
                "1.0.0",
                "1.0.0",
                "sha256",
                "signature"));

    private static string FormatErrors(IReadOnlyList<CanDoItAll.SharedKernel.Error> errors)
        => string.Join(" | ", errors.Select(error => error.Message));

    private static string ReadQueryParameter(
        string uri,
        string parameterName)
    {
        var query = new Uri(uri).Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2);
            var key = WebUtility.UrlDecode(parts[0]);
            if (string.Equals(key, parameterName, StringComparison.Ordinal))
            {
                return parts.Length == 2 ? WebUtility.UrlDecode(parts[1]) : string.Empty;
            }
        }

        throw new InvalidOperationException($"Query parameter '{parameterName}' was not found.");
    }

    private static async Task GrantAsync(
        ApiTestHost host,
        PluginId pluginId,
        PluginCapabilityKind capability,
        PluginHostToolRecipeId? recipeId = null)
    {
        var response = await host.Client.PutAsJsonAsync(
            $"/api/plugins/{pluginId.Value}/grants",
            new PluginGrantUpdateRequest(
                capability,
                PluginGrantState.Granted,
                recipeId?.Value,
                RiskKind: recipeId is null ? PluginGrantRiskKind.Low : PluginGrantRiskKind.High));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
    }

    private sealed class FakeHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(new FakeHttpMessageHandler(handler));
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }

    private static async Task ConfigureDockerPluginForProofAsync(ApiTestHost host)
    {
        var installResponse = await host.Client.PostAsJsonAsync(
            $"/api/plugins/{DockerPluginConstants.PluginId.Value}/install",
            new PluginInstallRequest(Enable: true, Actor: "docker-proof"));
        var installBody = await installResponse.Content.ReadAsStringAsync();
        Assert.True(installResponse.IsSuccessStatusCode, installBody);

        await GrantAsync(host, DockerPluginConstants.PluginId, PluginCapabilityKind.WorkflowExecutor);
        await GrantAsync(host, DockerPluginConstants.PluginId, PluginCapabilityKind.HostCommand);
        await GrantAsync(host, DockerPluginConstants.PluginId, PluginCapabilityKind.HostCommand, PluginHostToolRecipeIds.DockerPullImage);
        await GrantAsync(host, DockerPluginConstants.PluginId, PluginCapabilityKind.HostCommand, PluginHostToolRecipeIds.DockerStartContainer);
        await GrantAsync(host, DockerPluginConstants.PluginId, PluginCapabilityKind.HostCommand, PluginHostToolRecipeIds.DockerReadLogs);
    }

    private static async Task<LlmCallComponent> SaveDockerLogSummaryComponentAsync(ApiTestHost host)
    {
        var response = await host.Client.PostAsJsonAsync(
            "/api/workflows/components",
            new LlmCallComponentSaveRequest(
                Id: null,
                Name: "Summarize Docker logs",
                ProviderProfileId: null,
                Model: "deterministic-docker-proof",
                Modality: WorkflowModality.Text,
                ModelSettings: new WorkflowModelSettings(
                    Temperature: 0,
                    MaxOutputTokens: 400,
                    RequireJsonOutput: false,
                    ResponseFormatJsonSchema: string.Empty),
                Instructions: "Summarize the Docker logs and identify whether Qdrant started.",
                InputShape: JsonShape(),
                ResultShape: WorkflowValueShape.Text,
                Permissions: AgentPermissionsPolicy.Default));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<LlmCallComponent>(body, JsonOptions)!;
    }

    private static WorkflowDefinition CreateDockerProofWorkflow(WorkflowComponentId summaryComponentId)
    {
        var settings = new DockerWorkflowExecutorSettings
        {
            Image = "qdrant/qdrant:latest",
            ContainerName = "candoitall-qdrant-proof",
            PullIfMissing = true,
            Tail = 160,
            MaxOutputCharacters = 20000
        };
        var startedAt = DateTimeOffset.UtcNow;
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Docker plugin Qdrant proof",
            "Starts Qdrant through the Docker plugin, reads logs, and summarizes them through an LLM workflow node.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                new WorkflowNodeId("start"),
                [
                    CreateNode("start", WorkflowNodeKind.Start, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
                    CreateExecutorNode(
                        "pull",
                        DockerPluginConstants.PullImageExecutorId,
                        settings,
                        inputShape: WorkflowValueShape.Text,
                        resultShape: JsonShape(),
                        timeoutSeconds: 900),
                    CreateExecutorNode(
                        "start-qdrant",
                        DockerPluginConstants.StartContainerExecutorId,
                        settings,
                        inputShape: JsonShape(),
                        resultShape: JsonShape(),
                        timeoutSeconds: 180),
                    CreateExecutorNode(
                        "read-logs",
                        DockerPluginConstants.ReadLogsExecutorId,
                        settings,
                        inputShape: JsonShape(),
                        resultShape: JsonShape(),
                        timeoutSeconds: 45),
                    CreateNode("summarize", WorkflowNodeKind.LlmCall, summaryComponentId, inputShape: JsonShape(), resultShape: WorkflowValueShape.Text),
                    CreateNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text)
                ],
                [
                    CreateEdge("start-to-pull", "start", "pull"),
                    CreateEdge("pull-to-start-qdrant", "pull", "start-qdrant"),
                    CreateEdge("start-qdrant-to-read-logs", "start-qdrant", "read-logs"),
                    CreateEdge("read-logs-to-summarize", "read-logs", "summarize"),
                    CreateEdge("summarize-to-end", "summarize", "end")
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            startedAt,
            startedAt);
    }

    private static WorkflowNode CreateExecutorNode(
        string id,
        WorkflowExecutorId executorId,
        DockerWorkflowExecutorSettings settings,
        WorkflowValueShape inputShape,
        WorkflowValueShape resultShape,
        int timeoutSeconds)
        => new(
            new WorkflowNodeId(id),
            WorkflowNodeKind.Executor,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape,
                ResultShape: resultShape)
            {
                ExecutorId = executorId,
                ExecutorSettingsJson = JsonSerializer.Serialize(settings, JsonOptions),
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with
                {
                    TimeoutSeconds = timeoutSeconds,
                    CaptureOutputArtifact = true
                }
            });

    private static WorkflowNode CreateNode(
        string id,
        WorkflowNodeKind kind,
        WorkflowComponentId? componentId = null,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
        => new(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: resultShape ?? WorkflowValueShape.Text));

    private static WorkflowEdge CreateEdge(string id, string source, string target)
        => new(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);

    private static WorkflowValueShape JsonShape()
        => new(WorkflowValueShapeKind.Json, "{}", "JSON payload");

    private static async Task<IReadOnlyList<WorkflowExecutorDescriptor>> ReadWorkflowExecutorCatalogAsync(ApiTestHost host)
    {
        var response = await host.Client.GetAsync("/api/workflows/executor-catalog");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<IReadOnlyList<WorkflowExecutorDescriptor>>(body, JsonOptions)!;
    }

    private sealed class StaticPluginCatalogSource(IReadOnlyList<PluginDescriptor> descriptors) : IPluginCatalogSource
    {
        public ValueTask<IReadOnlyList<PluginDescriptor>> ListPluginsAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(descriptors);
    }

    private sealed record PluginPackagePathOverrides(
        string CatalogRootPath,
        string InstalledRootPath,
        string RuntimeStateRootPath,
        IReadOnlyDictionary<string, string?> ConfigurationOverrides);

    private sealed class DockerLogSummaryLlmInvoker : IWorkflowLlmComponentInvoker
    {
        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowDefinition definition,
            WorkflowNode node,
            LlmCallComponent component,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            var summary = new
            {
                summary = "Deterministic Docker log summary: Qdrant log payload was received by the LLM workflow step.",
                containsQdrant = input.PayloadJson.Contains("qdrant", StringComparison.OrdinalIgnoreCase),
                sourceCharacters = input.PayloadJson.Length
            };
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                node.Id,
                JsonSerializer.Serialize(summary, JsonOptions),
                component.ResultShape));
        }
    }
}
