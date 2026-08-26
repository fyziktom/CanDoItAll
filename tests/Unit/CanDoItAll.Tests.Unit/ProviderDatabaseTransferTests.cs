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
