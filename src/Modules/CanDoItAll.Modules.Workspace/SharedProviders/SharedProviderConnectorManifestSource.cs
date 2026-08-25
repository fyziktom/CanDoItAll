namespace CanDoItAll.Modules.Workspace;

public sealed class SharedProviderConnectorManifestSource : IConnectorManifestSource
{
    private static readonly ConnectorPluginManifest PluginManifest = new(
        SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey,
        "CanDoItAll shared provider",
        "1.0.0",
        ConnectorManifestCapability.ProviderExecution |
        ConnectorManifestCapability.AgentExposure,
        new ConnectorConfigurationSchema(
            SharedProviderReconciliationCoordinator.ImportedConfigurationSchemaVersion,
            []),
        [],
        new ConnectorHealthCheckDescriptor(
            "shared-provider-status",
            "Checks the managed source, catalog identity, publication availability, and upstream health."),
        new ConnectorAgentExposure(
            "workspace.prompt.send",
            true,
            true,
            "Allows approved execution through a source-managed shared provider profile."),
        null);

    private static readonly IReadOnlyList<ConnectorPluginManifest> Manifests =
        Array.AsReadOnly<ConnectorPluginManifest>([PluginManifest]);

    public IReadOnlyList<ConnectorPluginManifest> ListManifests() => Manifests;
}
