using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.External;

public sealed class ConnectorPluginIntegrationTests
{
    [Fact]
    public async Task SaveAsync_persists_custom_resource_plugin_and_projects_connector_node()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var resources = scope.ServiceProvider.GetRequiredService<ResourcesService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Webhook connector projection");
        var saveResult = await resources.SaveAsync(new ResourceEditorModel
        {
            ProjectId = projectId,
            ConnectorPluginKey = WebhookResourceConnectorPlugin.PluginKey,
            ConfigSchemaVersion = "1.0",
            Name = "Order status webhook",
            Description = "Webhook connector added through the plugin registry path.",
            ConfigJson = """
                         {"endpointUrl":"https://example.com/hooks/orders","healthPath":"/health","method":"post"}
                         """,
            ValidationStatus = ResourceValidationStatus.Valid,
            Sensitivity = ResourceSensitivity.Normal
        });

        Assert.True(saveResult.IsSuccess);

        var editor = await resources.GetAsync(saveResult.Value);
        Assert.Equal(WebhookResourceConnectorPlugin.PluginKey, editor.ConnectorPluginKey);
        Assert.Equal("1.0", editor.ConfigSchemaVersion);
        Assert.Contains("endpointUrl", editor.ConfigJson, StringComparison.Ordinal);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var resource = await dbContext.Set<ProjectResource>()
            .SingleAsync(item => item.Id == saveResult.Value);
        Assert.Equal(WebhookResourceConnectorPlugin.PluginKey, resource.ConnectorPluginKey);
        Assert.Equal("1.0", resource.ConfigSchemaVersion);
        Assert.Equal("https://example.com/hooks/orders", resource.LocationOrIdentifier);

        var structure = await workbench.GetStructureAsync(projectId);
        var node = Assert.Single(structure.Nodes, item => item.ArtifactId == resource.Id);
        Assert.Equal(ProjectObjectType.Connector, node.ObjectType);
        Assert.Equal("webhook-endpoint", node.ObjectSubtype);
    }

    [Fact]
    public async Task SaveAsync_rejects_webhook_plugin_when_required_endpoint_is_missing()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var resources = scope.ServiceProvider.GetRequiredService<ResourcesService>();

        var projectId = await CreateProjectAsync(projects, "Webhook connector validation");
        var saveResult = await resources.SaveAsync(new ResourceEditorModel
        {
            ProjectId = projectId,
            ConnectorPluginKey = WebhookResourceConnectorPlugin.PluginKey,
            ConfigSchemaVersion = "1.0",
            Name = "Broken webhook",
            ConfigJson = JsonSerializer.Serialize(new
            {
                method = "POST"
            }),
            ValidationStatus = ResourceValidationStatus.Invalid
        });

        Assert.False(saveResult.IsSuccess);
        Assert.NotEmpty(saveResult.Errors);
        Assert.Contains("endpointUrl", saveResult.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
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
}
