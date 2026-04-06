using Bunit;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Resources.Pages;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ResourcesPageTests
{
    private const string FolderConnectorPluginKey = "resource.folder";

    [Fact]
    public async Task Saves_repository_resource_through_typed_editor()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var project = await projectsService.GetAsync(null);
        project.Name = "Resource Project";
        var savedProject = await projectsService.SaveAsync(project);
        Assert.True(savedProject.IsSuccess);

        var cut = harness.Context.RenderComponent<ResourcesPage>();
        cut.Find("[data-testid='resource-project-select']").Change(savedProject.Value!.ToString());
        cut.Find("[data-testid='resource-name-input']").Change("Main repository");
        cut.Find("[data-testid='resource-primary-input']").Change("https://github.com/example/repo.git");
        cut.Find("[data-testid='resource-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Resource saved.", cut.Markup);
            Assert.Contains("Main repository", cut.Markup);
            Assert.Contains("https://github.com/example/repo.git", cut.Markup);
        });
    }

    [Fact]
    public async Task Saves_folder_resource_after_switching_connector_plugin()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var resourcesService = harness.Context.Services.GetRequiredService<ResourcesService>();
        var project = await projectsService.GetAsync(null);
        project.Name = "Folder Resource Project";
        var savedProject = await projectsService.SaveAsync(project);
        Assert.True(savedProject.IsSuccess);

        var cut = harness.Context.RenderComponent<ResourcesPage>();
        cut.Find("[data-testid='resource-project-select']").Change(savedProject.Value!.ToString());
        cut.Find("[data-testid='resource-plugin-select']").Change(FolderConnectorPluginKey);
        cut.Find("[data-testid='resource-name-input']").Change("Working folder");
        cut.Find("[data-testid='resource-primary-input']").Change(@"C:\repositories\CanDoItAll\workspace");
        cut.Find("[data-testid='resource-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Resource saved.", cut.Markup);
            Assert.Contains("Working folder", cut.Markup);
            Assert.Contains(@"C:\repositories\CanDoItAll\workspace", cut.Markup);
        });

        var resources = await resourcesService.ListAsync();
        var resource = Assert.Single(resources, item => item.Name == "Working folder");
        Assert.Equal(FolderConnectorPluginKey, resource.ConnectorPluginKey);
    }

    [Fact]
    public async Task Resources_page_renders_unknown_resource_manifest_fields_through_shared_field_editor()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.AddScoped<IResourceConnectorPlugin, UnknownManifestResourceConnectorPlugin>();
        });
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var resourcesService = harness.Context.Services.GetRequiredService<ResourcesService>();
        var secretService = harness.Context.Services.GetRequiredService<SecretService>();
        var project = await projectsService.GetAsync(null);
        project.Name = "Unknown connector resource project";
        var savedProject = await projectsService.SaveAsync(project);
        Assert.True(savedProject.IsSuccess);

        var secretResult = await secretService.SaveAsync(new SecretEditorModel
        {
            Name = "Resource shared secret",
            Kind = SecretKind.Generic,
            SecretValue = "resource-secret",
            Scope = "workspace"
        });
        Assert.True(secretResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ResourcesPage>();

        cut.Find("[data-testid='resource-project-select']").Change(savedProject.Value!.ToString());
        cut.Find("[data-testid='resource-plugin-select']").Change(UnknownManifestResourceConnectorPlugin.PluginKey);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Unknown resource manifest", cut.Markup);
            Assert.Contains("Structured metadata", cut.Markup);
            Assert.Contains("Shared secret reference", cut.Markup);
            Assert.Contains(secretResult.Value.ToString("D"), cut.Markup, StringComparison.OrdinalIgnoreCase);
        });

        cut.Find("[data-testid='resource-name-input']").Change("Unknown manifest resource");
        cut.Find("[data-testid='resource-primary-input']").Change("https://resource.example.com/hook");
        cut.Find("[data-testid='resource-config-displayNameHint']").Change("Resource hint");
        cut.Find("[data-testid='resource-config-retryCount']").Change("7");
        cut.Find("[data-testid='resource-config-verifyTls']").Change(true);
        cut.Find("[data-testid='resource-config-structuredMetadata']").Change("""{"owner":"platform"}""");
        cut.Find("[data-testid='resource-config-sharedSecret']").Change(secretResult.Value.ToString("D"));
        cut.Find("[data-testid='resource-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Resource saved.", cut.Markup);
            Assert.Contains("Unknown manifest resource", cut.Markup);
        });

        var resources = await resourcesService.ListAsync();
        var resource = Assert.Single(resources, item => item.Name == "Unknown manifest resource");
        var editor = await resourcesService.GetAsync(resource.Id);

        Assert.Equal(UnknownManifestResourceConnectorPlugin.PluginKey, editor.ConnectorPluginKey);
        Assert.Equal("https://resource.example.com/hook", editor.Configuration.GetText("endpointUrl"));
        Assert.Equal("Resource hint", editor.Configuration.GetText("displayNameHint"));
        Assert.Equal(7, editor.Configuration.GetNumber("retryCount"));
        Assert.True(editor.Configuration.GetBoolean("verifyTls"));
        Assert.Equal("""{"owner":"platform"}""", editor.Configuration.GetText("structuredMetadata"));
        Assert.Equal(secretResult.Value.ToString("D"), editor.Configuration.GetText("sharedSecret"));
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


