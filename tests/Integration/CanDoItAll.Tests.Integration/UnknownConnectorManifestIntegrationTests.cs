using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.External;

public sealed class UnknownConnectorManifestIntegrationTests
{
    private const string ProviderBaseUrlFieldKey = "baseUrl";
    private const string ProviderDefaultModelFieldKey = "defaultModel";
    private const string ProviderTimeoutSecondsFieldKey = "timeoutSeconds";

    [Fact]
    public async Task Unknown_connector_manifest_fields_round_trip_without_page_specific_code()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-tests");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("unknown-connector-roundtrip");
        await using var services = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: RegisterUnknownConnectorPlugins);
        await using var scope = services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var providerAdministration = scope.ServiceProvider.GetRequiredService<IProviderAdministrationService>();
        var resources = scope.ServiceProvider.GetRequiredService<ResourcesService>();
        var secrets = scope.ServiceProvider.GetRequiredService<SecretService>();

        var projectId = await CreateProjectAsync(projects, "Unknown connector round trip");
        var secretResult = await secrets.SaveAsync(new SecretEditorModel
        {
            Name = "Round trip secret",
            Kind = SecretKind.Generic,
            SecretValue = "round-trip-secret",
            Scope = "workspace"
        });
        Assert.True(secretResult.IsSuccess);
        var secretId = secretResult.Value;

        var saveProviderResult = await providerAdministration.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = "Round trip provider",
            ConnectorPluginKey = UnknownManifestProviderAdapter.PluginKey,
            ConfigSchemaVersion = "1.0",
            IsEnabled = true,
            Configuration = new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderBaseUrlFieldKey] = "https://provider.example.com/v1",
                [ProviderDefaultModelFieldKey] = "wave-10",
                [ProviderTimeoutSecondsFieldKey] = "45",
                ["enableAuditing"] = bool.TrueString,
                ["jsonPayload"] = """{"mode":"strict"}""",
                ["sharedSecret"] = secretId.ToString("D")
            })
        });
        Assert.True(saveProviderResult.IsSuccess);

        var providerEditor = await providerAdministration.GetProviderAsync(saveProviderResult.Value);
        Assert.Equal(UnknownManifestProviderAdapter.PluginKey, providerEditor.ConnectorPluginKey);
        Assert.Equal("https://provider.example.com/v1", providerEditor.Configuration.GetText(ProviderBaseUrlFieldKey));
        Assert.Equal("wave-10", providerEditor.Configuration.GetText(ProviderDefaultModelFieldKey));
        Assert.Equal(45, providerEditor.Configuration.GetNumber(ProviderTimeoutSecondsFieldKey));
        Assert.True(providerEditor.Configuration.GetBoolean("enableAuditing"));
        Assert.Equal("""{"mode":"strict"}""", providerEditor.Configuration.GetText("jsonPayload"));
        Assert.Equal(secretId.ToString("D"), providerEditor.Configuration.GetText("sharedSecret"));

        var saveResourceResult = await resources.SaveAsync(new ResourceEditorModel
        {
            ProjectId = projectId,
            Name = "Round trip resource",
            ConnectorPluginKey = UnknownManifestResourceConnectorPlugin.PluginKey,
            ConfigSchemaVersion = "1.0",
            ValidationStatus = ResourceValidationStatus.Valid,
            Configuration = new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["endpointUrl"] = "https://resource.example.com/hook",
                ["displayNameHint"] = "Resource hint",
                ["retryCount"] = "7",
                ["verifyTls"] = bool.TrueString,
                ["structuredMetadata"] = """{"owner":"platform"}""",
                ["sharedSecret"] = secretId.ToString("D")
            })
        });
        Assert.True(saveResourceResult.IsSuccess);

        var resourceEditor = await resources.GetAsync(saveResourceResult.Value);
        Assert.Equal(UnknownManifestResourceConnectorPlugin.PluginKey, resourceEditor.ConnectorPluginKey);
        Assert.Equal("https://resource.example.com/hook", resourceEditor.Configuration.GetText("endpointUrl"));
        Assert.Equal("Resource hint", resourceEditor.Configuration.GetText("displayNameHint"));
        Assert.Equal(7, resourceEditor.Configuration.GetNumber("retryCount"));
        Assert.True(resourceEditor.Configuration.GetBoolean("verifyTls"));
        Assert.Equal("""{"owner":"platform"}""", resourceEditor.Configuration.GetText("structuredMetadata"));
        Assert.Equal(secretId.ToString("D"), resourceEditor.Configuration.GetText("sharedSecret"));
    }

    private static void RegisterUnknownConnectorPlugins(IServiceCollection services)
    {
        services.AddScoped<IProviderAdapter, UnknownManifestProviderAdapter>();
        services.AddScoped<IResourceConnectorPlugin, UnknownManifestResourceConnectorPlugin>();
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Execution"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private sealed class UnknownManifestProviderAdapter : IProviderAdapter
    {
        public const string PluginKey = "provider.test-unknown-manifest";

        private static readonly ConnectorPluginManifest PluginManifest = new(
            PluginKey,
            "Unknown provider manifest",
            "1.0.0",
            ConnectorManifestCapability.ProviderExecution,
            new ConnectorConfigurationSchema(
                "1.0",
                [
                    new ConnectorConfigFieldDescriptor(ProviderBaseUrlFieldKey, "Base URL", ConnectorConfigFieldType.Url, true, "Provider endpoint root."),
                    new ConnectorConfigFieldDescriptor(ProviderDefaultModelFieldKey, "Default model", ConnectorConfigFieldType.Text, true, "Model used by default."),
                    new ConnectorConfigFieldDescriptor(ProviderTimeoutSecondsFieldKey, "Timeout", ConnectorConfigFieldType.Number, true, "HTTP timeout in seconds."),
                    new ConnectorConfigFieldDescriptor("enableAuditing", "Enable auditing", ConnectorConfigFieldType.Boolean, false, "Enable auditing for test calls."),
                    new ConnectorConfigFieldDescriptor("jsonPayload", "Custom JSON payload", ConnectorConfigFieldType.Json, false, "Extra JSON payload for the provider."),
                    new ConnectorConfigFieldDescriptor("sharedSecret", "Shared secret reference", ConnectorConfigFieldType.SecretReference, false, "Optional shared secret reference.")
                ]),
            [],
            new ConnectorHealthCheckDescriptor("test", "Exposes every shared connector field type through the provider editor."),
            new ConnectorAgentExposure("workspace.prompt.send", false, false, "Not exposed for tests."),
            null);

        public ConnectorPluginManifest Manifest => PluginManifest;

        public ProviderKind? LegacyProviderKind => null;

        public Task<ProviderHealthCheckResult> CheckHealthAsync(ProviderProfile profile, string? secretValue, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderHealthCheckResult(true, "Healthy"));
        }

        public Task<Result<ProviderPromptExecutionResponse>> SendAsync(
            ProviderProfile profile,
            ProviderPromptExecutionRequest request,
            string? secretValue,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<ProviderPromptExecutionResponse>.Success(new ProviderPromptExecutionResponse(
                profile.Name,
                profile.DefaultModel,
                "ok",
                request.OutputFormat,
                false)));
        }
    }

    private sealed class UnknownManifestResourceConnectorPlugin : IResourceConnectorPlugin
    {
        public const string PluginKey = "resource.test-unknown-manifest";

        private static readonly ConnectorPluginManifest PluginManifest = new(
            PluginKey,
            "Unknown resource manifest",
            "1.0.0",
            ConnectorManifestCapability.ProjectResource | ConnectorManifestCapability.WorkbenchProjection,
            new ConnectorConfigurationSchema(
                "1.0",
                [
                    new ConnectorConfigFieldDescriptor("endpointUrl", "Endpoint URL", ConnectorConfigFieldType.Url, true, "Resource endpoint URL."),
                    new ConnectorConfigFieldDescriptor("displayNameHint", "Display hint", ConnectorConfigFieldType.Text, false, "Optional display hint."),
                    new ConnectorConfigFieldDescriptor("retryCount", "Retry count", ConnectorConfigFieldType.Number, false, "Retry count for transient failures."),
                    new ConnectorConfigFieldDescriptor("verifyTls", "Verify TLS", ConnectorConfigFieldType.Boolean, false, "Verify TLS when contacting the endpoint."),
                    new ConnectorConfigFieldDescriptor("structuredMetadata", "Structured metadata", ConnectorConfigFieldType.Json, false, "Additional metadata stored as JSON."),
                    new ConnectorConfigFieldDescriptor("sharedSecret", "Shared secret reference", ConnectorConfigFieldType.SecretReference, false, "Optional shared secret reference.")
                ]),
            [],
            new ConnectorHealthCheckDescriptor("test", "Exposes every shared connector field type through the resource editor."),
            new ConnectorAgentExposure("resource.test", false, false, "Not exposed for tests."),
            new ConnectorWorkbenchNodeHook(ProjectObjectType.Connector, "unknown-resource", "Unknown resource"));

        public ConnectorPluginManifest Manifest => PluginManifest;

        public ResourceKind? LegacyResourceKind => null;

        public Error? ValidateEditor(ResourceEditorModel model)
        {
            return string.IsNullOrWhiteSpace(model.Configuration.GetText("endpointUrl"))
                ? Error.Validation("endpointUrl is required.")
                : null;
        }

        public string BuildLocation(ResourceEditorModel model)
        {
            return model.Configuration.GetText("endpointUrl").Trim();
        }

        public string SerializeConfig(ResourceEditorModel model)
        {
            return model.Configuration.ToJson();
        }

        public void ApplyConfig(ResourceEditorModel model, string configJson)
        {
            model.Configuration = ConnectorConfigState.FromJson(configJson);
        }

        public ProjectObjectType ResolveWorkbenchObjectType(ProjectResource resource)
        {
            return ProjectObjectType.Connector;
        }

        public string ResolveWorkbenchObjectSubtype(ProjectResource resource)
        {
            return "unknown-resource";
        }
    }
}
