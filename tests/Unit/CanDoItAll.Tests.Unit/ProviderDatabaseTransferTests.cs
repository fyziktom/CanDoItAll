using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class ProviderDatabaseTransferTests
{
    [Fact]
    public void Provider_management_marker_discovers_the_compatible_provider_schema()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(ProviderManagementModuleAssemblyMarker).Assembly,
            typeof(SecretService).Assembly
        ]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"provider-schema-{Guid.NewGuid():N}")
            .Options;
        using var dbContext = new AppDbContext(options);
        (Type EntityType, string TableName)[] expectedTables =
        [
            (typeof(ProviderProfile), "Workspace_ProviderProfiles"),
            (typeof(ProviderSharePublication), "Workspace_ProviderSharePublications"),
            (typeof(SharedProviderSource), "Workspace_SharedProviderSources"),
            (typeof(SharedProviderImport), "Workspace_SharedProviderImports"),
            (typeof(SharedProviderInvocationRecord), "Workspace_SharedProviderInvocations"),
            (typeof(SharedProviderServiceIdentity), "Workspace_SharedProviderServiceIdentity")
        ];

        foreach (var expected in expectedTables)
        {
            var entity = dbContext.Model.FindEntityType(expected.EntityType);
            Assert.NotNull(entity);
            Assert.Equal(expected.TableName, entity.GetTableName());
            Assert.Equal([nameof(ProviderProfile.Id)], entity.FindPrimaryKey()!.Properties
                .Select(property => property.Name));
        }

        var provider = dbContext.Model.FindEntityType(typeof(ProviderProfile))!;
        Assert.Equal(200, provider.FindProperty(nameof(ProviderProfile.Name))!.GetMaxLength());
        Assert.Equal(500, provider.FindProperty(nameof(ProviderProfile.BaseUrl))!.GetMaxLength());
        Assert.Equal(160, provider.FindProperty(nameof(ProviderProfile.ConnectorPluginKey))!.GetMaxLength());
        Assert.Equal(40, provider.FindProperty(nameof(ProviderProfile.ConfigSchemaVersion))!.GetMaxLength());
        Assert.True(provider.FindProperty(nameof(ProviderProfile.ConcurrencyToken))!.IsConcurrencyToken);
        Assert.Equal(typeof(Guid?), provider.FindProperty(nameof(ProviderProfile.ApiKeySecretId))!.ClrType);
        Assert.Empty(provider.GetForeignKeys());

        Type[] concurrencyProtectedEntities =
        [
            typeof(ProviderSharePublication),
            typeof(SharedProviderSource),
            typeof(SharedProviderImport),
            typeof(SharedProviderInvocationRecord)
        ];
        foreach (var entityType in concurrencyProtectedEntities)
        {
            var entity = dbContext.Model.FindEntityType(entityType)!;
            Assert.True(entity.FindProperty("ConcurrencyToken")!.IsConcurrencyToken);
        }

        Type[] restrictDeleteEntities =
        [
            typeof(ProviderSharePublication),
            typeof(SharedProviderSource),
            typeof(SharedProviderImport),
            typeof(SharedProviderInvocationRecord)
        ];
        foreach (var entityType in restrictDeleteEntities)
        {
            var foreignKeys = dbContext.Model.FindEntityType(entityType)!.GetForeignKeys();
            Assert.NotEmpty(foreignKeys);
            Assert.All(foreignKeys, foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        }
    }

    [Fact]
    public async Task Workspace_default_provider_transfer_restores_opaque_id_without_provider_dependency()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(ProviderManagementModuleAssemblyMarker).Assembly,
            typeof(WorkspaceModuleAssemblyMarker).Assembly,
            typeof(SecretService).Assembly
        ]);
        var sourceOptions = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"workspace-provider-preference-source-{Guid.NewGuid():N}")
            .Options;
        var targetOptions = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"workspace-provider-preference-target-{Guid.NewGuid():N}")
            .Options;
        await using var source = new AppDbContext(sourceOptions);
        await using var target = new AppDbContext(targetOptions);
        var sourceProviderId = Guid.NewGuid();

        source.Add(new WorkspaceSettings
        {
            DefaultProviderProfileId = sourceProviderId,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await source.SaveChangesAsync();

        var context = new DatabaseTransferContext(
            CreateProfile("source"),
            CreateProfile("target"),
            source,
            target,
            ReplaceExisting: true);
        var handler = new WorkspaceDefaultProviderDatabaseTransferHandler();
        var preview = await handler.PreviewAsync(context);
        var result = await handler.TransferAsync(context);

        Assert.True(preview.IsAvailable);
        Assert.Null(preview.Warning);
        Assert.True(result.Success);
        Assert.Equal(
            sourceProviderId,
            await target.Set<WorkspaceSettings>()
                .Select(settings => settings.DefaultProviderProfileId)
                .SingleAsync());
        Assert.False(await target.Set<ProviderProfile>().AnyAsync());
    }

    [Fact]
    public async Task Provider_transfer_copies_profiles_and_referenced_secrets_but_not_workspace_preference()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(ProviderManagementModuleAssemblyMarker).Assembly,
            typeof(WorkspaceModuleAssemblyMarker).Assembly,
            typeof(SecretService).Assembly
        ]);
        var sourceOptions = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"provider-transfer-source-{Guid.NewGuid():N}")
            .Options;
        var targetOptions = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"provider-transfer-target-{Guid.NewGuid():N}")
            .Options;
        await using var source = new AppDbContext(sourceOptions);
        await using var target = new AppDbContext(targetOptions);
        var sourceProviderId = Guid.NewGuid();
        var sourceSecretId = Guid.NewGuid();
        var targetPreferenceId = Guid.NewGuid();

        source.Add(new SecretRecord
        {
            Id = sourceSecretId,
            Name = "Transferred provider secret",
            Kind = SecretKind.ApiKey,
            EncryptedPayload = "ciphertext",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        source.Add(new ProviderProfile
        {
            Id = sourceProviderId,
            Name = "Transferred provider",
            ConnectorPluginKey = ProviderConnectorKeys.OpenAi,
            ConfigSchemaVersion = "1.0",
            ApiKeySecretId = sourceSecretId,
            BaseUrl = "https://provider.example.test",
            DefaultModel = "test-model",
            ConcurrencyToken = Guid.NewGuid()
        });
        source.Add(new WorkspaceSettings
        {
            DefaultProviderProfileId = sourceProviderId,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        target.Add(new WorkspaceSettings
        {
            DefaultProviderProfileId = targetPreferenceId,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await source.SaveChangesAsync();
        await target.SaveChangesAsync();

        var context = new DatabaseTransferContext(
            CreateProfile("source"),
            CreateProfile("target"),
            source,
            target,
            ReplaceExisting: true);
        var result = await new AiProvidersDatabaseTransferHandler()
            .TransferAsync(context);

        Assert.True(result.Success);
        Assert.True(await target.Set<ProviderProfile>()
            .AnyAsync(profile => profile.Id == sourceProviderId));
        Assert.True(await target.Set<SecretRecord>()
            .AnyAsync(secret => secret.Id == sourceSecretId));
        Assert.Equal(
            targetPreferenceId,
            await target.Set<WorkspaceSettings>()
                .Select(settings => settings.DefaultProviderProfileId)
                .SingleAsync());
    }

    private static ResolvedDatabaseProfile CreateProfile(string name)
    {
        return new ResolvedDatabaseProfile(
            new DatabaseProfileRecord
            {
                DisplayName = name
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            $"test-{name}");
    }
}
