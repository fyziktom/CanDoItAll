using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class HrAgentWorkspaceToolAccessAdministrationTests
{
    [Fact]
    public async Task Create_update_and_settings_readback_preserve_canonical_workspace_access()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("hr-workspace-access");
        var profile = environment.CreateInMemoryProfile("primary");
        var configuration = TestApplicationBootstrap.BuildConfiguration(profile);
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            configuration,
            environment.CreateHostEnvironment("CanDoItAll.HrWorkspaceAccessTests"));
        await using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await TestApplicationBootstrap.InitializeSchemaAsync(
            serviceProvider,
            TestSchemaBootstrapModules.None);
        await using var scope = serviceProvider.CreateAsyncScope();
        var workspace = scope.ServiceProvider
            .GetRequiredService<ICanDoItAllAgentWorkspaceFactory>()
            .GetOrganizationWorkspaceService();
        var externalTargetPathRegistry = scope.ServiceProvider
            .GetRequiredService<IExternalTargetPathRegistry>();
        var administration = new HrAgentAdministrationService(
            workspace,
            externalTargetPathRegistry,
            NullLogger<HrAgentAdministrationService>.Instance);

        var createResult = await administration.CreateAsync(
            HrAgentIdentity.AgentId,
            CreateInput(new HrAgentWorkspaceToolAccessInput()),
            CancellationToken.None);
        var created = await FindAgentAsync(workspace, createResult.AgentId);
        var persistedCreatedAccess = AgentWorkspaceToolAccessMetadata.Read(created.ConfigurationJson);
        var createdReadback = await administration.GetSettingsAsync(
            created.Id,
            CancellationToken.None);

        AssertNoWorkspaceAccess(persistedCreatedAccess);
        AssertNoWorkspaceAccess(createdReadback.WorkspaceToolAccess);

        var firstStorageId = Guid.NewGuid();
        var secondStorageId = Guid.NewGuid();
        var externalPath = Path.Combine(Path.GetTempPath(), "GardenPlanner");
        var expectedAlias = Assert.IsType<string>(
            AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
                externalPath,
                externalTargetPathRegistry));
        await administration.UpdateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentSettingsUpdateInput(
                created.Id,
                created.UpdatedAtUtc,
                WorkspaceToolAccess: new HrAgentWorkspaceToolAccessPatch(
                    CanRunLocalScripts: true,
                    CanTransformArtifacts: true,
                    AllowedExternalTargetAliases: [externalPath, expectedAlias],
                    CanWriteStorage: true,
                    AllowedStorageCatalogIds:
                    [
                        secondStorageId,
                        firstStorageId,
                        secondStorageId
                    ])),
            CancellationToken.None);
        var firstUpdate = await FindAgentAsync(workspace, created.Id);
        var firstReadback = await administration.GetSettingsAsync(
            created.Id,
            CancellationToken.None);

        Assert.Equal(AgentWorkspaceToolProfileKind.Custom, firstReadback.WorkspaceToolAccess.Profile);
        Assert.True(firstReadback.WorkspaceToolAccess.CanReadFiles);
        Assert.True(firstReadback.WorkspaceToolAccess.CanWriteFiles);
        Assert.False(firstReadback.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.True(firstReadback.WorkspaceToolAccess.CanRunLocalScripts);
        Assert.False(firstReadback.WorkspaceToolAccess.CanScaffoldProjects);
        Assert.False(firstReadback.WorkspaceToolAccess.CanManageWorkspacePaths);
        Assert.True(firstReadback.WorkspaceToolAccess.CanTransformArtifacts);
        Assert.Equal([expectedAlias], firstReadback.WorkspaceToolAccess.AllowedExternalTargetAliases);
        Assert.True(firstReadback.WorkspaceToolAccess.CanReadStorage);
        Assert.True(firstReadback.WorkspaceToolAccess.CanWriteStorage);
        Assert.False(firstReadback.WorkspaceToolAccess.AllowAllStorageCatalogs);
        Assert.Equal(
            new[] { firstStorageId, secondStorageId }.OrderBy(id => id),
            firstReadback.WorkspaceToolAccess.AllowedStorageCatalogIds);

        await administration.UpdateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentSettingsUpdateInput(
                created.Id,
                firstUpdate.UpdatedAtUtc,
                WorkspaceToolAccess: new HrAgentWorkspaceToolAccessPatch(
                    CanRunValidationCommands: true)),
            CancellationToken.None);
        var secondReadback = await administration.GetSettingsAsync(
            created.Id,
            CancellationToken.None);

        Assert.True(secondReadback.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.True(secondReadback.WorkspaceToolAccess.CanRunLocalScripts);
        Assert.True(secondReadback.WorkspaceToolAccess.CanTransformArtifacts);
        Assert.Equal([expectedAlias], secondReadback.WorkspaceToolAccess.AllowedExternalTargetAliases);
        Assert.True(secondReadback.WorkspaceToolAccess.CanWriteStorage);
        Assert.Equal(
            firstReadback.WorkspaceToolAccess.AllowedStorageCatalogIds,
            secondReadback.WorkspaceToolAccess.AllowedStorageCatalogIds);
    }

    [Fact]
    public async Task Null_patch_preserves_legacy_default_and_invalid_workspace_requests_fail()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("hr-workspace-validation");
        var profile = environment.CreateInMemoryProfile("primary");
        var configuration = TestApplicationBootstrap.BuildConfiguration(profile);
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            configuration,
            environment.CreateHostEnvironment("CanDoItAll.HrWorkspaceValidationTests"));
        await using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await TestApplicationBootstrap.InitializeSchemaAsync(
            serviceProvider,
            TestSchemaBootstrapModules.None);
        await using var scope = serviceProvider.CreateAsyncScope();
        var workspace = scope.ServiceProvider
            .GetRequiredService<ICanDoItAllAgentWorkspaceFactory>()
            .GetOrganizationWorkspaceService();
        var administration = new HrAgentAdministrationService(
            workspace,
            scope.ServiceProvider.GetRequiredService<IExternalTargetPathRegistry>(),
            NullLogger<HrAgentAdministrationService>.Instance);

        var createResult = await administration.CreateAsync(
            HrAgentIdentity.AgentId,
            CreateInput(workspaceToolAccess: null),
            CancellationToken.None);
        var created = await FindAgentAsync(workspace, createResult.AgentId);
        var initialReadback = await administration.GetSettingsAsync(
            created.Id,
            CancellationToken.None);

        Assert.Equal(AgentWorkspaceToolProfileKind.Custom, initialReadback.WorkspaceToolAccess.Profile);
        Assert.True(initialReadback.WorkspaceToolAccess.CanReadFiles);
        Assert.False(initialReadback.WorkspaceToolAccess.CanWriteFiles);

        await administration.UpdateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentSettingsUpdateInput(
                created.Id,
                created.UpdatedAtUtc,
                Summary: "Summary-only update"),
            CancellationToken.None);
        var afterNullPatch = await administration.GetSettingsAsync(
            created.Id,
            CancellationToken.None);

        AssertWorkspaceAccessEqual(
            initialReadback.WorkspaceToolAccess,
            afterNullPatch.WorkspaceToolAccess);

        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.CreateAsync(
            HrAgentIdentity.AgentId,
            CreateInput(new HrAgentWorkspaceToolAccessInput(
                Profile: (AgentWorkspaceToolProfileKind)int.MaxValue)),
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.CreateAsync(
            HrAgentIdentity.AgentId,
            CreateInput(new HrAgentWorkspaceToolAccessInput(
                AllowedExternalTargetAliases: [@"C:\"])),
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.CreateAsync(
            HrAgentIdentity.AgentId,
            CreateInput(new HrAgentWorkspaceToolAccessInput(
                AllowedStorageCatalogIds: [Guid.Empty])),
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.CreateAsync(
            HrAgentIdentity.AgentId,
            CreateInput(new HrAgentWorkspaceToolAccessInput(
                AllowAllStorageCatalogs: true,
                AllowedStorageCatalogIds: [Guid.NewGuid()])),
            CancellationToken.None));

        var afterSummaryUpdate = await FindAgentAsync(workspace, created.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.UpdateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentSettingsUpdateInput(
                created.Id,
                afterSummaryUpdate.UpdatedAtUtc,
                WorkspaceToolAccess: new HrAgentWorkspaceToolAccessPatch(
                    AllowedExternalTargetAliases: ["external-target/C/../invalid"])),
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.UpdateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentSettingsUpdateInput(
                created.Id,
                afterSummaryUpdate.UpdatedAtUtc,
                WorkspaceToolAccess: new HrAgentWorkspaceToolAccessPatch(
                    AllowAllStorageCatalogs: true,
                    AllowedStorageCatalogIds: [Guid.NewGuid()])),
            CancellationToken.None));
    }

    [Fact]
    public async Task Existing_legacy_external_alias_is_migrated_when_an_unrelated_setting_is_saved_and_reloaded()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string legacyAlias = "external-target/C/work/apps/Inventory";
        await using var environment = CanDoItAllTestEnvironment.Create("hr-workspace-legacy-migration");
        var profile = environment.CreateInMemoryProfile("primary");
        var configuration = TestApplicationBootstrap.BuildConfiguration(profile);
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            configuration,
            environment.CreateHostEnvironment("CanDoItAll.HrWorkspaceLegacyMigrationTests"));
        await using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await TestApplicationBootstrap.InitializeSchemaAsync(
            serviceProvider,
            TestSchemaBootstrapModules.None);
        await using var scope = serviceProvider.CreateAsyncScope();
        var workspace = scope.ServiceProvider
            .GetRequiredService<ICanDoItAllAgentWorkspaceFactory>()
            .GetOrganizationWorkspaceService();
        var administration = new HrAgentAdministrationService(
            workspace,
            scope.ServiceProvider.GetRequiredService<IExternalTargetPathRegistry>(),
            NullLogger<HrAgentAdministrationService>.Instance);
        var createResult = await administration.CreateAsync(
            HrAgentIdentity.AgentId,
            CreateInput(new HrAgentWorkspaceToolAccessInput(CanWriteFiles: true)),
            CancellationToken.None);
        var created = await FindAgentAsync(workspace, createResult.AgentId);
        var store = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceStore>();

        await store.UpdateWorkspaceAsync(document =>
        {
            var configurationRoot = JsonNode.Parse(created.ConfigurationJson)?.AsObject() ?? new JsonObject();
            var workspaceTools = configurationRoot["workspaceTools"]?.AsObject() ?? new JsonObject();
            workspaceTools["allowedExternalTargetAliases"] = new JsonArray(JsonValue.Create(legacyAlias));
            workspaceTools.Remove("externalTargetRootBindings");
            configurationRoot["workspaceTools"] = workspaceTools;
            return document with
            {
                Agents = document.Agents
                    .Select(agent => agent.Id == created.Id
                        ? agent with { ConfigurationJson = configurationRoot.ToJsonString() }
                        : agent)
                    .ToArray()
            };
        });

        var persistedLegacy = await FindAgentAsync(workspace, created.Id);
        Assert.Equal(
            [legacyAlias],
            AgentWorkspaceToolAccessMetadata.Read(persistedLegacy.ConfigurationJson).AllowedExternalTargetAliases);

        await administration.UpdateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentSettingsUpdateInput(
                created.Id,
                persistedLegacy.UpdatedAtUtc,
                Summary: "Migration-triggering edit"),
            CancellationToken.None);

        var reloaded = await FindAgentAsync(workspace, created.Id);
        var migratedAccess = AgentWorkspaceToolAccessMetadata.Read(reloaded.ConfigurationJson);
        var migratedAlias = Assert.Single(migratedAccess.AllowedExternalTargetAliases);
        Assert.Matches("^external-target/v1/[0-9a-f]{24}$", migratedAlias);
        Assert.DoesNotContain(legacyAlias, reloaded.ConfigurationJson, StringComparison.OrdinalIgnoreCase);
        Assert.Single(migratedAccess.ExternalTargetRootBindings);
    }

    private static HrAgentCreateInput CreateInput(
        HrAgentWorkspaceToolAccessInput? workspaceToolAccess)
    {
        return new HrAgentCreateInput(
            "Temporary garden specialist",
            "Garden specialist",
            "Plans gardens and explains edible growing basics.",
            "Use the assigned garden skills and report assumptions explicitly.",
            WorkspaceToolAccess: workspaceToolAccess);
    }

    private static async Task<AgentDefinition> FindAgentAsync(
        IAgentFrameworkWorkspaceService workspace,
        Guid agentId)
    {
        return Assert.Single(
            await workspace.ListAgentsAsync(includeTemplates: true),
            candidate => candidate.Id == agentId);
    }

    private static void AssertNoWorkspaceAccess(AgentWorkspaceToolAccessSettings access)
    {
        Assert.Equal(AgentWorkspaceToolProfileKind.Custom, access.Profile);
        Assert.False(access.CanReadFiles);
        Assert.False(access.CanWriteFiles);
        Assert.False(access.CanRunValidationCommands);
        Assert.False(access.CanRunLocalScripts);
        Assert.False(access.CanScaffoldProjects);
        Assert.False(access.CanManageWorkspacePaths);
        Assert.False(access.CanTransformArtifacts);
        Assert.Empty(access.AllowedExternalTargetAliases);
        Assert.False(access.CanReadStorage);
        Assert.False(access.CanWriteStorage);
        Assert.False(access.AllowAllStorageCatalogs);
        Assert.Empty(access.AllowedStorageCatalogIds);
    }

    private static void AssertNoWorkspaceAccess(HrAgentSafeWorkspaceToolAccess access)
    {
        Assert.Equal(AgentWorkspaceToolProfileKind.Custom, access.Profile);
        Assert.False(access.CanReadFiles);
        Assert.False(access.CanWriteFiles);
        Assert.False(access.CanRunValidationCommands);
        Assert.False(access.CanRunLocalScripts);
        Assert.False(access.CanScaffoldProjects);
        Assert.False(access.CanManageWorkspacePaths);
        Assert.False(access.CanTransformArtifacts);
        Assert.Empty(access.AllowedExternalTargetAliases);
        Assert.False(access.CanReadStorage);
        Assert.False(access.CanWriteStorage);
        Assert.False(access.AllowAllStorageCatalogs);
        Assert.Empty(access.AllowedStorageCatalogIds);
    }

    private static void AssertWorkspaceAccessEqual(
        HrAgentSafeWorkspaceToolAccess expected,
        HrAgentSafeWorkspaceToolAccess actual)
    {
        Assert.Equal(expected.Profile, actual.Profile);
        Assert.Equal(expected.CanReadFiles, actual.CanReadFiles);
        Assert.Equal(expected.CanWriteFiles, actual.CanWriteFiles);
        Assert.Equal(expected.CanRunValidationCommands, actual.CanRunValidationCommands);
        Assert.Equal(expected.CanRunLocalScripts, actual.CanRunLocalScripts);
        Assert.Equal(expected.CanScaffoldProjects, actual.CanScaffoldProjects);
        Assert.Equal(expected.CanManageWorkspacePaths, actual.CanManageWorkspacePaths);
        Assert.Equal(expected.CanTransformArtifacts, actual.CanTransformArtifacts);
        Assert.Equal(expected.AllowedExternalTargetAliases, actual.AllowedExternalTargetAliases);
        Assert.Equal(expected.CanReadStorage, actual.CanReadStorage);
        Assert.Equal(expected.CanWriteStorage, actual.CanWriteStorage);
        Assert.Equal(expected.AllowAllStorageCatalogs, actual.AllowAllStorageCatalogs);
        Assert.Equal(expected.AllowedStorageCatalogIds, actual.AllowedStorageCatalogIds);
    }
}
