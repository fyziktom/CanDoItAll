using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        Assert.Single(initialCatalog);
        Assert.Equal(PluginInstallationStateKind.NotInstalled, initialCatalog[0].InstallationState);
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
        var installed = Assert.Single(items);

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

        using var openApiPayload = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = openApiPayload.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/plugins/catalog", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/{pluginId}/install", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/{pluginId}/enable", out _));
        Assert.True(paths.TryGetProperty("/api/plugins/{pluginId}/disable", out _));
    }

    private static async Task<ServiceProvider> BuildServiceProviderAsync(
        TestDatabaseProfile profile,
        IReadOnlyList<PluginDescriptor> descriptors)
    {
        return await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.PluginCatalog.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.AddScoped<IPluginCatalogSource>(_ => new StaticPluginCatalogSource(descriptors));
            });
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

    private sealed class StaticPluginCatalogSource(IReadOnlyList<PluginDescriptor> descriptors) : IPluginCatalogSource
    {
        public ValueTask<IReadOnlyList<PluginDescriptor>> ListPluginsAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(descriptors);
    }
}
