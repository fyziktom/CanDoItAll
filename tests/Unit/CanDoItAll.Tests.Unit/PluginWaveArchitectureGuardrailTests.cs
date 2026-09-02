using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Plugins;

public sealed class PluginWaveArchitectureGuardrailTests
{
    [Fact]
    public void Metadata_contract_removes_phase7_foreign_id_helpers_and_marker_set_payload()
    {
        AssertNoProperty(typeof(ProjectObjectMetadataEnvelope), "MarkerSet");
        AssertNoProperty(typeof(ProjectMeetingMetadata), "ParticipantIds");
        AssertNoProperty(typeof(ProjectRecordingMetadata), "MeetingNodeArtifactId");
        AssertNoProperty(typeof(ProjectRecordingMetadata), "TranscriptNodeArtifactId");
        AssertNoProperty(typeof(ProjectTranscriptMetadata), "RecordingNodeArtifactId");
        AssertNoProperty(typeof(ProjectTranscriptMetadata), "LastProviderProfileId");
        AssertNoProperty(typeof(ProjectParticipantMetadata), "ParentParticipantArtifactId");
        AssertNoProperty(typeof(ProjectWorkItemMetadata), "AssigneeParticipantArtifactId");
        AssertNoProperty(typeof(ProjectWorkItemMetadata), "RepositoryResourceId");
        AssertNoProperty(typeof(ProjectRepositoryMetadata), "ResourceId");
        AssertNoProperty(typeof(ProjectEnvironmentMetadata), "RepositoryResourceId");
        AssertNoProperty(typeof(ProjectInfrastructureMetadata), "SecretReferenceArtifactId");
        AssertNoProperty(typeof(ProjectInfrastructureMetadata), "StorageCatalogId");
    }

    [Fact]
    public void Provider_administration_catalog_prefers_connector_plugin_key_over_legacy_provider_kind()
    {
        var legacyConnector = new TestProviderAdministrationConnector(ProviderKind.OpenAi, "provider.openai");
        var pluginKeyConnector = new TestProviderAdministrationConnector(ProviderKind.OllamaLocal, "provider.custom");
        var catalog = new ProviderAdministrationConnectorCatalog([legacyConnector, pluginKeyConnector]);
        var profile = new ProviderProfile
        {
            ProviderKind = ProviderKind.OpenAi,
            ConnectorPluginKey = "provider.custom"
        };

        var resolved = catalog.Resolve(profile);

        Assert.Same(pluginKeyConnector, resolved);
    }

    [Fact]
    public void Resource_connector_registry_prefers_connector_plugin_key_over_legacy_kind()
    {
        var registry = new ResourceConnectorPluginRegistry(
        [
            new TestResourceConnectorPlugin("resource.custom", legacyResourceKind: null)
        ]);

        var resolved = registry.Resolve("resource.custom", ResourceKind.Repository);

        Assert.Equal("resource.custom", resolved.Manifest.PluginKey);
        Assert.Null(resolved.LegacyResourceKind);
    }

    private static void AssertNoProperty(Type type, string propertyName)
    {
        Assert.Null(type.GetProperty(propertyName));
    }

    private sealed class TestProviderAdministrationConnector(ProviderKind providerKind, string pluginKey) : IProviderAdministrationConnector
    {
        public ProviderKind? LegacyProviderKind => providerKind;

        public ConnectorPluginManifest Manifest { get; } = new(
            pluginKey,
            pluginKey,
            "1.0.0",
            ConnectorManifestCapability.ProviderExecution,
            new ConnectorConfigurationSchema("1.0", []),
            [],
            new ConnectorHealthCheckDescriptor("noop", "noop"),
            new ConnectorAgentExposure("noop", false, true, "noop"),
            null);

    }

    private sealed class TestResourceConnectorPlugin(string pluginKey, ResourceKind? legacyResourceKind) : IResourceConnectorPlugin
    {
        public ResourceKind? LegacyResourceKind => legacyResourceKind;

        public ConnectorPluginManifest Manifest { get; } = new(
            pluginKey,
            pluginKey,
            "1.0.0",
            ConnectorManifestCapability.ProjectResource,
            new ConnectorConfigurationSchema("1.0", []),
            [],
            new ConnectorHealthCheckDescriptor("noop", "noop"),
            new ConnectorAgentExposure("noop", false, true, "noop"),
            new ConnectorWorkbenchNodeHook(ProjectObjectType.Connector, string.Empty, "Connector"));

        public Error? ValidateEditor(ResourceEditorModel model) => null;

        public string BuildLocation(ResourceEditorModel model) => model.LocationOrIdentifier;

        public string SerializeConfig(ResourceEditorModel model) => model.ConfigJson;

        public void ApplyConfig(ResourceEditorModel model, string configJson)
        {
            model.ConfigJson = configJson;
        }

        public ProjectObjectType ResolveWorkbenchObjectType(ProjectResource resource) => ProjectObjectType.Connector;

        public string ResolveWorkbenchObjectSubtype(ProjectResource resource) => string.Empty;
    }
}
