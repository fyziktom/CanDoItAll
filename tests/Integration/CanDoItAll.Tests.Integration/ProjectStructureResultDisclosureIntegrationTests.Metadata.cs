using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class ProjectStructureResultDisclosureIntegrationTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Registered_attachment_metadata_matches_its_actual_read_structure_or_task_tools(bool structureWrite, bool taskWrite) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await CreateProjectAsync(services, "Metadata selection");
        await using var fixture = await CreateJournalAsync(services, project.ProjectId);
        var agent = await SaveMetadataActorAsync(services, fixture.Agent, project, structureWrite, taskWrite);
        var context = Context(fixture, agent, project.ProjectId);
        var provider = MetadataProvider(services);
        Assert.Empty(provider.GetToolMetadata(context));

        var tools = await provider.CreateToolsAsync(context, default);
        var metadata = provider.GetToolMetadata(context);
        Assert.NotEmpty(tools);
        Assert.Equal(tools.Select(tool => tool.Name).Order(StringComparer.Ordinal),
            metadata.Select(item => item.ToolName).Order(StringComparer.Ordinal));
        Assert.All(metadata, item => Assert.NotNull(item.AuthorizeResultDisclosureAsync));
        Assert.Contains(metadata, item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureRead);
        Assert.Equal(structureWrite || taskWrite, metadata.Any(item => item.ToolName == ProjectStructureToolPolicy.ProjectTaskCreate));
        Assert.Equal(structureWrite, metadata.Any(item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureNodeCreate));
        Assert.Equal(structureWrite, metadata.Any(item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureNodeProcessStart));
        Assert.DoesNotContain(metadata, item => item.ToolName == ProjectStructureToolPolicy.ProjectPlanSummaryGet);
        Assert.DoesNotContain(metadata, item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureProjectCreate);
        Assert.DoesNotContain(metadata, item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureProjectLeaseAcquire);
        if (structureWrite) {
            Assert.NotNull(Assert.Single(metadata, item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureNodeProcessStart).PrepareAdmission);
        }
        var retained = (await services.GetRequiredService<ISandboxWorkspaceCatalogStore>().LoadCatalogSnapshotAsync())
            .Catalog.Agents.Single(item => item.Id == agent.Id);
        Assert.Equal(agent.ConfigurationJson, retained.ConfigurationJson);
    }

    [Fact]
    public async Task Attachment_metadata_is_isolated_by_context_identity_including_an_empty_attachment() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await CreateProjectAsync(services, "Independent attachments");
        await using var fixture = await CreateJournalAsync(services, project.ProjectId);
        var reader = await SaveMetadataActorAsync(services, fixture.Agent, project, false, false);
        var readContext = Context(fixture, reader, project.ProjectId);
        var writer = await SaveMetadataActorAsync(services, reader, project, true, false);
        var writeContext = Context(fixture, writer, project.ProjectId);
        var provider = MetadataProvider(services);
        var attached = await Task.WhenAll(provider.CreateToolsAsync(readContext, default).AsTask(),
            provider.CreateToolsAsync(writeContext, default).AsTask());
        var readNames = provider.GetToolMetadata(readContext).Select(item => item.ToolName).Order(StringComparer.Ordinal).ToArray();
        var writeNames = provider.GetToolMetadata(writeContext).Select(item => item.ToolName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(attached[0].Select(tool => tool.Name).Order(StringComparer.Ordinal), readNames);
        Assert.Equal(attached[1].Select(tool => tool.Name).Order(StringComparer.Ordinal), writeNames);
        Assert.DoesNotContain(ProjectStructureToolPolicy.ProjectStructureNodeCreate, readNames);
        Assert.Contains(ProjectStructureToolPolicy.ProjectStructureNodeCreate, writeNames);
        var equalButUnattached = readContext with { };
        Assert.Equal(readContext, equalButUnattached);
        Assert.NotSame(readContext, equalButUnattached);
        Assert.Empty(provider.GetToolMetadata(equalButUnattached));

        var denied = await SaveActorAsync(services, writer, project, canRead: false);
        var deniedContext = Context(fixture, denied, project.ProjectId);
        Assert.Empty(await provider.CreateToolsAsync(deniedContext, default));
        Assert.Empty(provider.GetToolMetadata(deniedContext));
        Assert.Equal(readNames, provider.GetToolMetadata(readContext).Select(item => item.ToolName).Order(StringComparer.Ordinal));
        Assert.Equal(writeNames, provider.GetToolMetadata(writeContext).Select(item => item.ToolName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Cancelled_recreation_clears_prior_metadata_and_a_completed_retry_restores_only_its_actual_tools() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await CreateProjectAsync(services, "Cancelled attachment");
        await using var fixture = await CreateJournalAsync(services, project.ProjectId);
        var agent = await SaveMetadataActorAsync(services, fixture.Agent, project, false, false);
        var context = Context(fixture, agent, project.ProjectId);
        var provider = MetadataProvider(services);
        await provider.CreateToolsAsync(context, default);
        Assert.NotEmpty(provider.GetToolMetadata(context));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.CreateToolsAsync(context, cancellation.Token).AsTask());
        Assert.Empty(provider.GetToolMetadata(context));
        var retried = await provider.CreateToolsAsync(context, default);
        Assert.Equal(retried.Select(tool => tool.Name).Order(StringComparer.Ordinal),
            provider.GetToolMetadata(context).Select(item => item.ToolName).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(provider.GetToolMetadata(context), item => item.ToolName == ProjectStructureToolPolicy.ProjectTaskCreate);
    }

    private static ProjectStructureAgentRuntimeToolProvider MetadataProvider(IServiceProvider services)
        => Assert.Single(services.GetServices<IAgentRuntimeToolProvider>().OfType<ProjectStructureAgentRuntimeToolProvider>());

    private static async Task<AgentDefinition> SaveMetadataActorAsync(IServiceProvider services, AgentDefinition original,
        ProjectWriteAdmission project, bool structureWrite, bool taskWrite) {
        var actor = await SaveActorAsync(services, original, project, canRead: true);
        var access = AgentProjectStructureAccessMetadata.Read(actor.ConfigurationJson);
        access.CanWrite = structureWrite;
        access.CanWriteTasks = taskWrite;
        actor = actor with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(actor.ConfigurationJson, access) };
        var saved = await services.GetRequiredService<ISandboxWorkspaceCatalogStore>().UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Where(item => item.Id != actor.Id).Append(actor).ToArray()
        });
        var retained = Assert.Single(saved.Agents, item => item.Id == actor.Id);
        var granted = AgentProjectStructureAccessMetadata.Read(retained.ConfigurationJson);
        Assert.Equal(structureWrite, granted.CanWrite);
        Assert.Equal(taskWrite, granted.CanWriteTasks);
        Assert.Equal(new AgentProjectStructureLifetime(project.DatabaseProfileId, project.ProjectId, project.LifetimeId),
            Assert.Single(granted.AllowedProjectLifetimes));
        return retained;
    }
}
