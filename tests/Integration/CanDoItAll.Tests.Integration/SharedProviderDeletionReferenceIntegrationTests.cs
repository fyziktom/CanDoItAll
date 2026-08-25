using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.SharedProviders;

public sealed class SharedProviderDeletionReferenceIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Workspace_delete_reports_publication_reference_and_preserves_profile()
    {
        await AssertPolicyBlocksAsync(
            SharedProviderProfileReferenceKinds.Publication,
            static (services, providerId) => services
                .GetRequiredService<IProviderAdministrationService>()
                .DeleteProviderAsync(providerId));
    }

    [Fact]
    public async Task Workspace_delete_reports_import_reference_and_preserves_profile()
    {
        await AssertPolicyBlocksAsync(
            SharedProviderProfileReferenceKinds.Import,
            static (services, providerId) => services
                .GetRequiredService<IProviderAdministrationService>()
                .DeleteProviderAsync(providerId));
    }

    [Fact]
    public async Task Agent_registry_delete_reports_publication_reference_and_preserves_profile()
    {
        await AssertPolicyBlocksAsync(
            SharedProviderProfileReferenceKinds.Publication,
            static (services, providerId) => services
                .GetRequiredService<IProviderProfileRegistry>()
                .DeleteProviderAsync(providerId));
    }

    [Fact]
    public async Task Agent_registry_delete_reports_import_reference_and_preserves_profile()
    {
        await AssertPolicyBlocksAsync(
            SharedProviderProfileReferenceKinds.Import,
            static (services, providerId) => services
                .GetRequiredService<IProviderProfileRegistry>()
                .DeleteProviderAsync(providerId));
    }

    [Fact]
    public async Task Database_restrict_remains_authoritative_for_publication_and_import_references()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var publicationProviderId = await SeedProviderAsync(
            dbContextFactory,
            SharedProviderProfileReferenceKinds.Publication);
        var importProviderId = await SeedProviderAsync(
            dbContextFactory,
            SharedProviderProfileReferenceKinds.Import);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            DeleteDirectlyAsync(dbContextFactory, publicationProviderId));
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            DeleteDirectlyAsync(dbContextFactory, importProviderId));

        await AssertProfilesExistAsync(
            dbContextFactory,
            publicationProviderId,
            importProviderId);

        await using var sourceDbContext = await dbContextFactory.CreateDbContextAsync();
        await using var targetDbContext = await dbContextFactory.CreateDbContextAsync();
        var transferContext = CreateTransferContext(sourceDbContext, targetDbContext);
        var transferHandler = new AiProvidersDatabaseTransferHandler(
            [new SharedProviderDatabaseTransferGuard()]);
        var preview = await transferHandler.PreviewAsync(transferContext);
        Assert.False(preview.IsAvailable);
        Assert.Contains(
            "shared-provider publications or imports",
            Assert.IsType<string>(preview.Warning),
            StringComparison.Ordinal);
        var transferException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transferHandler.TransferAsync(transferContext));
        Assert.Contains(
            "shared-provider publications or imports",
            transferException.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Both_production_paths_delete_unreferenced_profiles()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var workspaceProviderId = await SeedProviderAsync(
            dbContextFactory,
            SharedProviderProfileReferenceKinds.None);
        var registryProviderId = await SeedProviderAsync(
            dbContextFactory,
            SharedProviderProfileReferenceKinds.None);

        await using (var sourceDbContext = await dbContextFactory.CreateDbContextAsync())
        await using (var targetDbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var transferContext = CreateTransferContext(sourceDbContext, targetDbContext);
            var preview = await new AiProvidersDatabaseTransferHandler(
                [new SharedProviderDatabaseTransferGuard()]).PreviewAsync(transferContext);
            Assert.True(preview.IsAvailable);
            Assert.Null(preview.Warning);
        }

        await scope.ServiceProvider
            .GetRequiredService<IProviderAdministrationService>()
            .DeleteProviderAsync(workspaceProviderId);
        await scope.ServiceProvider
            .GetRequiredService<IProviderProfileRegistry>()
            .DeleteProviderAsync(registryProviderId);

        await using var verification = await dbContextFactory.CreateDbContextAsync();
        Assert.False(await verification.Set<ProviderProfile>()
            .AnyAsync(profile =>
                profile.Id == workspaceProviderId ||
                profile.Id == registryProviderId));
    }

    private static async Task AssertPolicyBlocksAsync(
        SharedProviderProfileReferenceKinds referenceKinds,
        Func<IServiceProvider, Guid, Task> deleteAsync)
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var providerId = await SeedProviderAsync(dbContextFactory, referenceKinds);

        var exception = await Assert.ThrowsAsync<SharedProviderProfileDeletionBlockedException>(() =>
            deleteAsync(scope.ServiceProvider, providerId));

        Assert.Equal(providerId, exception.ProviderProfileId);
        Assert.Equal(referenceKinds, exception.ReferenceKinds);
        await AssertProfilesExistAsync(dbContextFactory, providerId);
    }

    private static async Task<Guid> SeedProviderAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        SharedProviderProfileReferenceKinds referenceKinds)
    {
        var provider = new ProviderProfile
        {
            Id = Guid.NewGuid(),
            Name = "Shared provider deletion fixture",
            ConnectorPluginKey = "provider.openai",
            ConfigSchemaVersion = "1.0",
            BaseUrl = "https://provider.example.test",
            DefaultModel = "fixture-model",
            ConcurrencyToken = Guid.NewGuid()
        };
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.Add(provider);

        if (referenceKinds.HasFlag(SharedProviderProfileReferenceKinds.Publication))
        {
            dbContext.Add(new ProviderSharePublication
            {
                Id = Guid.NewGuid(),
                ProviderProfileId = provider.Id,
                PublicId = new SharedProviderPublicationId(Guid.NewGuid()),
                IsPublished = false,
                CreatedAtUtc = Now,
                UpdatedAtUtc = Now,
                ConcurrencyToken = Guid.NewGuid()
            });
        }

        if (referenceKinds.HasFlag(SharedProviderProfileReferenceKinds.Import))
        {
            var secret = CreateSecret();
            var source = CreateSource(secret.Id);
            var publication = CreateRemotePublication();
            dbContext.Add(secret);
            dbContext.Add(source);
            dbContext.Add(new SharedProviderImport
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id,
                RemotePublicationId = publication.PublicationId,
                ProviderProfileId = provider.Id,
                RemoteDisplayName = publication.DisplayName,
                RemoteRevision = publication.Revision,
                RemotePurpose = publication.Purpose,
                RemoteTransport = publication.Transport,
                RemoteDefaultModelId = publication.DefaultModelId,
                RemoteCatalogSnapshotJson = JsonSerializer.Serialize(
                    publication,
                    SharedProviderProtocolJson.Options),
                SelectionState = SharedProviderSelectionState.Selected,
                AvailabilityState = SharedProviderAvailabilityState.Available,
                LastSeenAtUtc = Now,
                LastSyncAtUtc = Now,
                CreatedAtUtc = Now,
                UpdatedAtUtc = Now,
                ConcurrencyToken = Guid.NewGuid()
            });
        }

        await dbContext.SaveChangesAsync();
        return provider.Id;
    }

    private static SecretRecord CreateSecret()
    {
        return new SecretRecord
        {
            Id = Guid.NewGuid(),
            Name = "Shared-provider deletion fixture credential",
            Kind = SecretKind.Token,
            EncryptedPayload = "encrypted-fixture-payload",
            Scope = "workspace",
            MetadataJson = "{}",
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
    }

    private static SharedProviderSource CreateSource(Guid apiTokenSecretId)
    {
        return new SharedProviderSource
        {
            Id = Guid.NewGuid(),
            Name = "Central deletion fixture",
            BaseUri = "https://central.example.test/",
            ApiTokenSecretId = apiTokenSecretId,
            IsEnabled = true,
            AllowInsecurePrivateNetwork = false,
            Status = SharedProviderSourceStatus.NeverSynchronized,
            LastStatusMessage = string.Empty,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    private static SharedProviderCatalogPublication CreateRemotePublication()
    {
        var publicationId = new SharedProviderPublicationId(Guid.NewGuid());
        var modelId = SharedProviderRoutingModelIdCodec.Create(publicationId, "fixture-model");
        var publication = new SharedProviderCatalogPublication(
            publicationId,
            new SharedProviderPublicRevision($"sha256:{new string('a', 64)}"),
            "Remote deletion fixture",
            SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible,
            modelId,
            [
                new SharedProviderCatalogModel(
                    modelId,
                    "Fixture model",
                    [SharedProviderCapability.Responses])
            ],
            new SharedProviderCatalogHealth(SharedProviderHealthState.Available));
        return publication with
        {
            Revision = SharedProviderCanonicalRevision.ComputePublication(publication)
        };
    }

    private static async Task DeleteDirectlyAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid providerId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var provider = await dbContext.Set<ProviderProfile>().SingleAsync(item => item.Id == providerId);
        dbContext.Remove(provider);
        await dbContext.SaveChangesAsync();
    }

    private static async Task AssertProfilesExistAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        params Guid[] providerIds)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedIds = await dbContext.Set<ProviderProfile>()
            .Where(profile => providerIds.Contains(profile.Id))
            .Select(profile => profile.Id)
            .ToArrayAsync();
        Assert.Equal(
            providerIds.Order(),
            persistedIds.Order());
    }

    private static DatabaseTransferContext CreateTransferContext(
        AppDbContext sourceDbContext,
        AppDbContext targetDbContext)
    {
        var sourceProfile = new ResolvedDatabaseProfile(
            new DatabaseProfileRecord
            {
                DisplayName = "Shared-provider transfer source"
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            "test-source");
        var targetProfile = new ResolvedDatabaseProfile(
            new DatabaseProfileRecord
            {
                DisplayName = "Shared-provider transfer target"
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            "test-target");
        return new DatabaseTransferContext(
            sourceProfile,
            targetProfile,
            sourceDbContext,
            targetDbContext,
            ReplaceExisting: true);
    }
}
