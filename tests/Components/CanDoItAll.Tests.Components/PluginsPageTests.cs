using System.Net;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Plugins.Pages;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class PluginsPageTests
{
    [Fact]
    public async Task Plugins_page_shows_workflow_executors_from_plugin_descriptor()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/plugins");
        var cut = harness.Context.Render<PluginsPage>();

        cut.WaitForElement("[data-testid='plugins-list-item-office365-mail']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='plugins-list-item-office365-mail']").Click());
        await cut.InvokeAsync(() => cut.Find("[data-testid='plugins-tab-executors']").Click());
        cut.WaitForElement("[data-testid='plugin-executor-office365-mail-office365-messages-by-category']");

        Assert.Contains("Loaded from plugin descriptor", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(Office365PluginConstants.DownloadByCategoryExecutorId.Value, cut.Markup, StringComparison.Ordinal);
        Assert.Contains(Office365PluginConstants.DownloadByAddressExecutorId.Value, cut.Markup, StringComparison.Ordinal);
        Assert.Contains(Office365PluginConstants.MarkProcessedExecutorId.Value, cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Downloads a bounded batch of Microsoft Graph mail messages that have the configured Outlook category.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Adds the processed Outlook category to a Microsoft 365 message and optionally removes the source category.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plugins_page_groups_plugins_by_descriptor_tags()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/plugins");
        var cut = harness.Context.Render<PluginsPage>();

        cut.WaitForElement("[data-testid='plugins-tree-tag-email']");
        var emailGroup = cut.Find("[data-testid='plugins-tree-tag-children-email']");

        Assert.Contains("Gmail", emailGroup.TextContent, StringComparison.Ordinal);
        Assert.Contains("Office365 Mail", emailGroup.TextContent, StringComparison.Ordinal);
        Assert.Contains("email", cut.Find("[data-testid='plugins-tree-tag-email']").TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Plugins_page_shows_empty_executor_state_for_plugins_without_workflow_executors()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.AddSingleton<ICanDoItAllPlugin, NoWorkflowExecutorPlugin>();
        });
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/plugins");
        var cut = harness.Context.Render<PluginsPage>();

        cut.WaitForElement("[data-testid='plugins-list-item-ui-executor-empty']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='plugins-list-item-ui-executor-empty']").Click());
        await cut.InvokeAsync(() => cut.Find("[data-testid='plugins-tab-executors']").Click());
        cut.WaitForElement("[data-testid='plugins-executors-empty']");

        Assert.Contains("No workflow executors", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("This plugin does not expose workflow executor descriptors.", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("plugin-executor-ui-executor-empty", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plugins_page_lists_plugins_and_saves_connection_settings()
    {
        const string clientId = "2f2a235f-7970-477b-93ba-656be29a8d03";

        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var settingsService = harness.Context.Services.GetRequiredService<PluginSettingsService>();

        navigation.NavigateTo("/plugins");
        var cut = harness.Context.Render<PluginsPage>();

        cut.WaitForElement("[data-testid='plugins-list-item-office365-mail']");
        await cut.InvokeAsync(() =>
            cut.Find("[data-testid='plugins-list-item-office365-mail']").Click());
        await cut.InvokeAsync(() =>
            cut.Find("[data-testid='plugins-tab-settings']").Click());
        cut.WaitForElement("[data-testid='plugin-setting-office365-mail-office365-clientId']");

        await cut.InvokeAsync(() =>
            cut.Find("[data-testid='plugin-setting-office365-mail-office365-clientId']").Change(clientId));
        await cut.InvokeAsync(() =>
            cut.Find("[data-testid='plugin-connection-save-office365-mail-office365']").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Saved Office365 account settings", cut.Markup, StringComparison.Ordinal);
        });

        var settings = await settingsService.GetSettingsAsync(Office365PluginConstants.PluginId);
        Assert.NotNull(settings);
        var connection = Assert.Single(settings!.Connections, item => item.ConnectionKey == Office365PluginConstants.ConnectionKey);
        var state = ConfigurationState.FromJson(connection.SettingsJson);

        Assert.Equal(clientId, state.GetText(PluginOAuthConnectionSettingKeys.ClientId));
    }

    [Fact]
    public async Task Plugins_page_opens_oauth_login_in_new_tab()
    {
        const string clientId = "2f2a235f-7970-477b-93ba-656be29a8d03";

        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalogService = harness.Context.Services.GetRequiredService<PluginCatalogService>();
        var settingsService = harness.Context.Services.GetRequiredService<PluginSettingsService>();

        var installResult = await catalogService.InstallAsync(
            Office365PluginConstants.PluginId,
            new PluginInstallRequest(Enable: true, Actor: "component-test"));
        Assert.True(installResult.IsSuccess);

        var grantResult = await settingsService.UpdateGrantAsync(
            Office365PluginConstants.PluginId,
            new PluginGrantUpdateRequest(PluginCapabilityKind.OAuth2, PluginGrantState.Granted),
            "component-test");
        Assert.True(grantResult.IsSuccess);

        var settingsState = new ConfigurationState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PluginOAuthConnectionSettingKeys.ClientId] = clientId
        });
        var saveResult = await settingsService.SaveConnectionAsync(
            Office365PluginConstants.PluginId,
            new PluginConnectionSaveRequest(
                Id: null,
                Office365PluginConstants.ConnectionKey,
                "Office365 account",
                settingsState.ToJson(),
                IsEnabled: true),
            "component-test");
        Assert.True(saveResult.IsSuccess);

        navigation.NavigateTo("/plugins");
        var cut = harness.Context.Render<PluginsPage>();

        cut.WaitForElement("[data-testid='plugins-list-item-office365-mail']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='plugins-list-item-office365-mail']").Click());
        await cut.InvokeAsync(() => cut.Find("[data-testid='plugins-tab-connections']").Click());
        await cut.InvokeAsync(() => cut.Find("[data-testid='plugin-oauth-login-office365-mail-office365']").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(harness.Context.JSInterop.Invocations, invocation => invocation.Identifier == "open");
        });

        var openInvocation = harness.Context.JSInterop.Invocations.Single(invocation => invocation.Identifier == "open");
        var authorizationUrl = Assert.IsType<string>(openInvocation.Arguments[0]);
        var decodedAuthorizationUrl = WebUtility.UrlDecode(authorizationUrl);

        Assert.Equal("_blank", Assert.IsType<string>(openInvocation.Arguments[1]));
        Assert.Equal("noopener,noreferrer", Assert.IsType<string>(openInvocation.Arguments[2]));
        Assert.Contains("login.microsoftonline.com/common/oauth2/v2.0/authorize", authorizationUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(clientId, decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains(Office365PluginConstants.OpenIdScope, decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains(Office365PluginConstants.MailReadWriteScope, decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains(Office365PluginConstants.MailboxSettingsReadWriteScope, decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains("prompt=consent", decodedAuthorizationUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plugins_page_installs_catalog_package_and_requests_restart()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("plugins-page-package-tests");
        var profile = environment.CreatePostgreSqlProfile("primary");
        var packagePaths = CreatePackagePathOverrides(environment.RootPath);
        Directory.CreateDirectory(packagePaths.CatalogRootPath);
        var manifest = CreatePackageManifest();
        await File.WriteAllBytesAsync(
            Path.Combine(packagePaths.CatalogRootPath, "page-runtime-package.zip"),
            CreatePackageArchive(manifest));

        await using var harness = await ComponentTestHarness.CreateAsync(options: new TestHarnessOptions
        {
            TestEnvironment = environment,
            ActiveProfile = profile,
            ConfigurationOverrides = packagePaths.ConfigurationOverrides
        });
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var restartService = harness.Context.Services.GetRequiredService<PluginRuntimeRestartService>();
        var lifetime = harness.Context.Services.GetRequiredService<TestHostApplicationLifetime>();

        navigation.NavigateTo("/plugins");
        var cut = harness.Context.Render<PluginsPage>();

        Assert.DoesNotContain("plugin-package-upload", cut.Markup, StringComparison.Ordinal);
        await cut.InvokeAsync(() => cut.Find("[data-testid='plugin-packages-open']").Click());
        cut.WaitForElement("[data-testid='plugin-package-upload']");
        cut.WaitForElement("[data-testid='plugin-package-install-page-runtime-package']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='plugin-package-install-page-runtime-package']").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Restart is required", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Plugin runtime restart required", cut.Markup, StringComparison.Ordinal);
        });

        var status = await restartService.GetStatusAsync();
        Assert.True(status.IsRestartRequired);

        await cut.InvokeAsync(() => cut.Find("[data-testid='plugin-runtime-restart']").Click());
        await Task.Delay(TimeSpan.FromMilliseconds(1500));

        Assert.True(lifetime.ApplicationStopping.IsCancellationRequested);
    }

    [Fact]
    public async Task Plugins_page_shows_selected_plugin_installation_and_runtime_logs()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var logStore = harness.Context.Services.GetRequiredService<PluginLogStore>();

        await logStore.WriteAsync(new PluginLogWriteRequest(
            PluginLogStreamKind.Installation,
            PluginLogOperationKind.PluginInstall,
            PluginLogSeverity.Information,
            "Installed",
            "Office365 install recorded",
            "{}",
            Office365PluginConstants.PluginId,
            Office365PluginConstants.PackageId));
        await logStore.WriteAsync(new PluginLogWriteRequest(
            PluginLogStreamKind.Runtime,
            PluginLogOperationKind.ExecutorCompleted,
            PluginLogSeverity.Information,
            "Completed",
            "Office365 runtime recorded",
            "{}",
            Office365PluginConstants.PluginId,
            WorkflowExecutorId: Office365PluginConstants.DownloadByCategoryExecutorId));

        navigation.NavigateTo("/plugins");
        var cut = harness.Context.Render<PluginsPage>();

        cut.WaitForElement("[data-testid='plugins-list-item-office365-mail']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='plugins-list-item-office365-mail']").Click());
        await cut.InvokeAsync(() => cut.Find("[data-testid='plugins-tab-logs']").Click());

        cut.WaitForElement("[data-testid='plugins-logs-installation-row']");
        cut.WaitForElement("[data-testid='plugins-logs-runtime-row']");
        Assert.Contains("Office365 install recorded", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Office365 runtime recorded", cut.Markup, StringComparison.Ordinal);
    }

    private static PluginPackagePathOverrides CreatePackagePathOverrides(string rootPath)
    {
        var packageRootPath = Path.Combine(rootPath, "plugin-packages");
        var catalogRootPath = Path.Combine(packageRootPath, "catalogue");
        var installedRootPath = Path.Combine(packageRootPath, "installed");
        var runtimeStateRootPath = Path.Combine(packageRootPath, "state");
        return new PluginPackagePathOverrides(
            CatalogRootPath: catalogRootPath,
            ConfigurationOverrides: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["PluginPackages:RootPath"] = packageRootPath,
                ["PluginPackages:CatalogRootPath"] = catalogRootPath,
                ["PluginPackages:InstalledRootPath"] = installedRootPath,
                ["PluginPackages:RuntimeStateRootPath"] = runtimeStateRootPath,
                ["PluginPackages:MaxPackageBytes"] = (20 * 1024 * 1024).ToString()
            });
    }

    private static PluginPackageManifest CreatePackageManifest()
        => new()
        {
            Plugin = new PluginDescriptor(
                new PluginId("page.runtime"),
                "Page runtime package",
                "Runtime plugin package used by component tests.",
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
                    new PluginPackageId("page.runtime.package"),
                    "1.0.0",
                    "1.0.0",
                    "sha256-test",
                    "signature-test")),
            IconPath = "icon.svg",
            RequiresRestart = true
        };

    private static byte[] CreatePackageArchive(PluginPackageManifest manifest)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddArchiveEntry(
                archive,
                PluginPackageManifestStore.ManifestFileName,
                JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            AddArchiveEntry(
                archive,
                manifest.IconPath,
                Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><rect width=\"16\" height=\"16\"/></svg>"));
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

    private sealed record PluginPackagePathOverrides(
        string CatalogRootPath,
        IReadOnlyDictionary<string, string?> ConfigurationOverrides);

    private sealed class NoWorkflowExecutorPlugin : ICanDoItAllPlugin
    {
        public PluginDescriptor Descriptor { get; } = new(
            new PluginId("ui.executor.empty"),
            "Executor empty plugin",
            "Plugin without workflow executors for UI tests.",
            "1.0.0",
            "CanDoItAll",
            PluginSourceKind.Bundled,
            PluginTrustLevel.Bundled,
            "1.0.0",
            PluginCapabilityKind.None,
            [],
            PluginSettingsDescriptor.Empty,
            [],
            Icon: UiIconDescriptor.MaterialIcon("extension", "Executor empty plugin"))
        {
            Tags = ["test"]
        };
    }
}
