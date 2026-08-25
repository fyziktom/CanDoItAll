using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.Plugins;

public sealed class ConnectorPluginRegistryTests
{
    [Fact]
    public void List_aggregates_provider_and_resource_manifests_from_registered_sources()
    {
        using var services = BuildServiceProvider();
        var registry = services.GetRequiredService<ConnectorPluginRegistry>();
        var pluginKeys = registry.List()
            .Select(item => item.PluginKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(ProviderConnectorKeys.OpenAi, pluginKeys);
        Assert.Contains(ProviderConnectorKeys.Ollama, pluginKeys);
        Assert.Contains(ProviderConnectorKeys.OllamaRemote, pluginKeys);
        Assert.Contains(WebhookResourceConnectorPlugin.PluginKey, pluginKeys);
        Assert.Contains(
            SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey,
            pluginKeys);

        var sharedManifest = registry.Resolve(
            SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey);
        Assert.Equal("CanDoItAll shared provider", sharedManifest.DisplayName);
        Assert.Equal(
            ConnectorManifestCapability.ProviderExecution |
            ConnectorManifestCapability.AgentExposure,
            sharedManifest.Capabilities);
        Assert.Equal(
            SharedProviderReconciliationCoordinator.ImportedConfigurationSchemaVersion,
            sharedManifest.ConfigurationSchema.Version);
        Assert.Empty(sharedManifest.ConfigurationSchema.Fields);
        Assert.Empty(sharedManifest.SecretRequirements);
        Assert.Equal("shared-provider-status", sharedManifest.HealthCheck.OperationName);
        Assert.True(sharedManifest.AgentExposure.IsExposed);
        Assert.True(sharedManifest.AgentExposure.RequiresApproval);
        Assert.Null(sharedManifest.WorkbenchNodeHook);
        Assert.Contains(
            services.GetServices<IConnectorManifestSource>(),
            source => source is SharedProviderConnectorManifestSource);
        Assert.DoesNotContain(
            services.GetServices<IProviderAdapter>(),
            adapter => string.Equals(
                adapter.Manifest.PluginKey,
                SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey,
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_returns_webhook_manifest_with_schema_policy_and_workbench_hook()
    {
        using var services = BuildServiceProvider();
        var registry = services.GetRequiredService<ConnectorPluginRegistry>();

        var manifest = registry.Resolve(WebhookResourceConnectorPlugin.PluginKey);

        Assert.Equal("1.0", manifest.ConfigurationSchema.Version);
        Assert.Contains(manifest.ConfigurationSchema.Fields, field => field.Key == "endpointUrl" && field.FieldType == ConnectorConfigFieldType.Url && field.IsRequired);
        Assert.Contains(manifest.ConfigurationSchema.Fields, field => field.Key == "method" && field.IsRequired);
        Assert.Single(manifest.SecretRequirements);
        Assert.Equal("authorization", manifest.SecretRequirements[0].Key);
        Assert.False(manifest.AgentExposure.IsExposed);
        Assert.True(manifest.AgentExposure.RequiresApproval);
        Assert.NotNull(manifest.WorkbenchNodeHook);
        Assert.Equal(ProjectObjectType.Connector, manifest.WorkbenchNodeHook!.ObjectType);
        Assert.Equal("webhook-endpoint", manifest.WorkbenchNodeHook.ObjectSubtype);
    }

    [Fact]
    public void Resource_registry_prefers_plugin_key_over_legacy_resource_kind()
    {
        var registry = new ResourceConnectorPluginRegistry([new WebhookResourceConnectorPlugin()]);

        var connectorPlugin = registry.Resolve(WebhookResourceConnectorPlugin.PluginKey, ResourceKind.WebLink);

        Assert.Equal(WebhookResourceConnectorPlugin.PluginKey, connectorPlugin.Manifest.PluginKey);
        Assert.Equal(ProjectObjectType.Connector, connectorPlugin.ResolveWorkbenchObjectType(new ProjectResource()));
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddAgentFrameworkProviderManagement();
        services.AddWorkspaceModule();
        services.AddResourcesModule();
        return services.BuildServiceProvider();
    }
}
