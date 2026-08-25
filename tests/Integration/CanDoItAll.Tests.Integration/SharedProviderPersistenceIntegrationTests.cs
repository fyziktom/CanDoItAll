using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CanDoItAll.Tests.Integration.SharedProviders;

public sealed class SharedProviderPersistenceIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Model_has_explicit_shared_provider_indexes_foreign_keys_and_no_content_columns()
    {
        using var dbContext = CreateModelContext();
        var model = dbContext.Model;

        Assert.NotNull(model.FindEntityType(typeof(ProviderSharePublication)));
        Assert.NotNull(model.FindEntityType(typeof(SharedProviderSource)));
        Assert.NotNull(model.FindEntityType(typeof(SharedProviderImport)));
        Assert.NotNull(model.FindEntityType(typeof(SharedProviderServiceIdentity)));
        var invocation = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IReadOnlyEntityType>(
            model.FindEntityType(typeof(SharedProviderInvocationRecord)));
        var persistedNames = invocation.GetProperties().Select(property => property.Name).ToArray();

        Assert.Contains(
            "PublicId",
            GetUniqueConstraintPropertySets<ProviderSharePublication>(model).SelectMany(value => value));
        Assert.Contains(
            new[] { "PublicId", "ProviderProfileId" },
            GetUniqueConstraintPropertySets<ProviderSharePublication>(model),
            StringArrayComparer.Instance);
        Assert.Contains(
            new[] { "SourceId", "RemotePublicationId" },
            GetUniqueConstraintPropertySets<SharedProviderImport>(model),
            StringArrayComparer.Instance);
        var publicationForeignKey = Assert.Single(
            invocation.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(ProviderSharePublication));
        Assert.Equal(
            new[] { "PublicationId", "ProviderProfileId" },
            publicationForeignKey.Properties.Select(property => property.Name),
            StringComparer.Ordinal);
        Assert.DoesNotContain(
            persistedNames,
            name => name.Contains("Prompt", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Response", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Body", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Attachment", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("ToolArguments", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostgreSql_clean_database_migrates_shared_provider_schema()
    {
        await using var database = await SharedProviderTestDatabase.CreateAsync("sharedprovider-migrate", migrate: true);
        await using var dbContext = database.Factory.CreateDbContext();

        var tableNames = await dbContext.Database
            .SqlQueryRaw<string>(
                """
                SELECT table_name AS "Value"
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND (table_name LIKE 'Workspace_SharedProvider%'
                    OR table_name = 'Workspace_ProviderSharePublications')
                ORDER BY table_name
                """)
            .ToArrayAsync();

        Assert.Contains("Workspace_ProviderSharePublications", tableNames);
        Assert.Contains("Workspace_SharedProviderSources", tableNames);
        Assert.Contains("Workspace_SharedProviderImports", tableNames);
        Assert.Contains("Workspace_SharedProviderInvocations", tableNames);
        Assert.Contains("Workspace_SharedProviderServiceIdentity", tableNames);
    }

    [Fact]
    public async Task Service_and_publication_identity_are_stable_across_concurrent_contexts()
    {
        await using var database = await SharedProviderTestDatabase.CreateAsync("sharedprovider-identity");
        var firstStore = new SharedProviderServiceIdentityStore(database.Factory, new FixedClock(Now));
        var secondStore = new SharedProviderServiceIdentityStore(database.Factory, new FixedClock(Now.AddDays(1)));

        var identities = await Task.WhenAll(
            firstStore.GetOrCreateAsync(),
            secondStore.GetOrCreateAsync());

        Guid providerProfileId;
        await using (var setup = database.Factory.CreateDbContext())
        {
            var profile = new ProviderProfile
            {
                Name = "Concurrent publication owner",
                ProviderKind = ProviderKind.OpenAi,
                ConnectorPluginKey = OpenAiProviderAdapter.PluginKey,
                ConfigSchemaVersion = "1.0",
                BaseUrl = "https://provider.example.test/v1",
                DefaultModel = "upstream-model",
                IsEnabled = true
            };
            setup.Add(profile);
            await setup.SaveChangesAsync();
            providerProfileId = profile.Id;
        }

        var firstPublicationStore = new SharedProviderPublicationStore(
            database.Factory,
            new FixedClock(Now));
        var secondPublicationStore = new SharedProviderPublicationStore(
            database.Factory,
            new FixedClock(Now.AddSeconds(1)));
        var publications = await Task.WhenAll(
            firstPublicationStore.GetOrCreateAsync(providerProfileId),
            secondPublicationStore.GetOrCreateAsync(providerProfileId));

        Assert.Equal(identities[0], identities[1]);
        Assert.Equal(publications[0].Id, publications[1].Id);
        Assert.Equal(publications[0].PublicId, publications[1].PublicId);
        await using var dbContext = database.Factory.CreateDbContext();
        Assert.Equal(1, await dbContext.Set<SharedProviderServiceIdentity>().CountAsync());
        Assert.Equal(1, await dbContext.Set<ProviderSharePublication>().CountAsync());
    }

    [Fact]
    public async Task Source_create_normalizes_uri_and_persists_one_secret_reference()
    {
        await using var database = await SharedProviderTestDatabase.CreateAsync("sharedprovider-source");
        var service = CreateSourceService(database);
        var secretId = await CreateSecretAsync(database);

        var result = await service.CreateAsync(new SharedProviderSourceWriteRequest(
            "Central source",
            new Uri("https://CENTRAL.example.test/reverse-proxy"),
            secretId,
            IsEnabled: true,
            AllowInsecurePrivateNetwork: false));

        await using var dbContext = database.Factory.CreateDbContext();
        var source = await dbContext.Set<SharedProviderSource>().SingleAsync(item => item.Id == result.Id);
        Assert.Equal("https://central.example.test/reverse-proxy/", source.BaseUri);
        Assert.Equal(secretId, source.ApiTokenSecretId);
        Assert.DoesNotContain("token", source.BaseUri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Source_update_propagates_endpoint_and_secret_reference_to_all_import_profiles()
    {
        await using var database = await SharedProviderTestDatabase.CreateAsync("sharedprovider-propagation");
        var sourceService = CreateSourceService(database);
        var source = await CreateSourceAsync(database, sourceService);
        var reconciliation = CreateReconciliation(database);
        await reconciliation.ReconcileAsync(
            CreateReconciliationRequest(source.Id, CreateCatalogWithTwoPublications(), selectPublication: true));
        Dictionary<Guid, (string Name, bool IsEnabled)> localIntent;
        await using (var setupContext = database.Factory.CreateDbContext())
        {
            var profiles = await setupContext.Set<ProviderProfile>()
                .OrderBy(profile => profile.DefaultModel)
                .ToArrayAsync();
            Assert.Equal(2, profiles.Length);
            profiles[0].Name = "Local primary alias";
            profiles[0].IsEnabled = false;
            profiles[1].Name = "Local secondary alias";
            profiles[1].IsEnabled = true;
            await setupContext.SaveChangesAsync();
            localIntent = profiles.ToDictionary(
                profile => profile.Id,
                profile => (profile.Name, profile.IsEnabled));
        }

        var newSecretId = await CreateSecretAsync(database, "Moved source token");
        var currentSource = await LoadSourceIdentityAsync(database, source.Id);

        await sourceService.UpdateAsync(
            source.Id,
            currentSource.ConcurrencyToken,
            new SharedProviderSourceWriteRequest(
                "Moved source",
                new Uri("https://moved.example.test/root"),
                newSecretId,
                IsEnabled: true,
                AllowInsecurePrivateNetwork: false));

        await using var dbContext = database.Factory.CreateDbContext();
        var updatedProfiles = await dbContext.Set<ProviderProfile>().ToArrayAsync();
        Assert.Equal(2, updatedProfiles.Length);
        Assert.Equal(2, await dbContext.Set<SharedProviderImport>().CountAsync());
        Assert.All(updatedProfiles, profile =>
        {
            Assert.Equal("https://moved.example.test/root/api/shared-providers/openai/v1", profile.BaseUrl);
            Assert.Equal(newSecretId, profile.ApiKeySecretId);
            Assert.Equal(localIntent[profile.Id].Name, profile.Name);
            Assert.Equal(localIntent[profile.Id].IsEnabled, profile.IsEnabled);
        });
    }

    [Fact]
    public async Task Reconciliation_creates_import_and_profile_once_and_is_idempotent()
    {
        await using var database = await SharedProviderTestDatabase.CreateAsync("sharedprovider-idempotent");
        var source = await CreateSourceAsync(database);
        var reconciliation = CreateReconciliation(database);
        var request = CreateReconciliationRequest(source.Id, CreateCatalog(), selectPublication: true);

        var first = await reconciliation.ReconcileAsync(request);
        var second = await reconciliation.ReconcileAsync(request);

        await using var dbContext = database.Factory.CreateDbContext();
        Assert.Equal(SharedProviderReconciliationOutcome.Applied, first.Outcome);
        Assert.Equal(SharedProviderReconciliationOutcome.Applied, second.Outcome);
        Assert.Equal(1, await dbContext.Set<SharedProviderImport>().CountAsync());
        Assert.Equal(1, await dbContext.Set<ProviderProfile>().CountAsync());

        var existingImport = await dbContext.Set<SharedProviderImport>()
            .AsNoTracking()
            .SingleAsync();
        var duplicateProfile = new ProviderProfile
        {
            Name = "Duplicate import profile",
            ProviderKind = ProviderKind.OpenAi,
            ConnectorPluginKey = SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey,
            ConfigSchemaVersion = "1.0",
            BaseUrl = "https://central.example.test/api/shared-providers/openai/v1",
            DefaultModel = existingImport.RemoteDefaultModelId.Value,
            IsEnabled = true
        };
        dbContext.Add(duplicateProfile);
        dbContext.Add(new SharedProviderImport
        {
            SourceId = existingImport.SourceId,
            RemotePublicationId = existingImport.RemotePublicationId,
            ProviderProfileId = duplicateProfile.Id,
            RemoteDisplayName = existingImport.RemoteDisplayName,
            RemoteRevision = existingImport.RemoteRevision,
            RemotePurpose = existingImport.RemotePurpose,
            RemoteTransport = existingImport.RemoteTransport,
            RemoteDefaultModelId = existingImport.RemoteDefaultModelId,
            RemoteCatalogSnapshotJson = existingImport.RemoteCatalogSnapshotJson,
            SelectionState = SharedProviderSelectionState.Selected,
            AvailabilityState = SharedProviderAvailabilityState.Available,
            LastSeenAtUtc = Now,
            LastSyncAtUtc = Now,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        await using var verification = database.Factory.CreateDbContext();
        Assert.Equal(1, await verification.Set<SharedProviderImport>().CountAsync());
        Assert.Equal(1, await verification.Set<ProviderProfile>().CountAsync());
    }

    [Fact]
    public async Task Reconciliation_preserves_local_profile_alias_enabled_and_id()
    {
        await using var database = await SharedProviderTestDatabase.CreateAsync("sharedprovider-local-intent");
        var source = await CreateSourceAsync(database);
        var reconciliation = CreateReconciliation(database);
        await reconciliation.ReconcileAsync(
            CreateReconciliationRequest(source.Id, CreateCatalog("Remote original"), selectPublication: true));
        Guid profileId;
        await using (var dbContext = database.Factory.CreateDbContext())
        {
            var profile = await dbContext.Set<ProviderProfile>().SingleAsync();
            profileId = profile.Id;
            profile.Name = "My local alias";
            profile.IsEnabled = false;
            await dbContext.SaveChangesAsync();
        }

        await reconciliation.ReconcileAsync(
            CreateReconciliationRequest(source.Id, CreateCatalog("Remote renamed"), selectPublication: false));

        await using var verification = database.Factory.CreateDbContext();
        var actual = await verification.Set<ProviderProfile>().SingleAsync();
        var import = await verification.Set<SharedProviderImport>().SingleAsync();
        Assert.Equal(profileId, actual.Id);
        Assert.Equal("My local alias", actual.Name);
        Assert.False(actual.IsEnabled);
        Assert.Equal("Remote renamed", import.RemoteDisplayName);
    }

    [Fact]
    public async Task Authoritative_absence_marks_missing_without_deleting_profile()
    {
        await using var database = await SharedProviderTestDatabase.CreateAsync("sharedprovider-missing");
        var source = await CreateSourceAsync(database);
        var reconciliation = CreateReconciliation(database);
        await reconciliation.ReconcileAsync(
            CreateReconciliationRequest(source.Id, CreateCatalog(), selectPublication: true));

        await reconciliation.ReconcileAsync(
            CreateReconciliationRequest(source.Id, CreateEmptyCatalog(), selectPublication: false));

        await using var dbContext = database.Factory.CreateDbContext();
        var import = await dbContext.Set<SharedProviderImport>().SingleAsync();
        Assert.Equal(SharedProviderAvailabilityState.Missing, import.AvailabilityState);
        Assert.Equal(1, await dbContext.Set<ProviderProfile>().CountAsync());
    }

    [Fact]
    public async Task Transient_failure_preserves_import_remote_state_and_profile()
    {
        await using var database = await SharedProviderTestDatabase.CreateAsync("sharedprovider-transient");
        var sourceService = CreateSourceService(database);
        var source = await CreateSourceAsync(database, sourceService);
        var reconciliation = CreateReconciliation(database);
        await reconciliation.ReconcileAsync(
            CreateReconciliationRequest(source.Id, CreateCatalog(), selectPublication: true));

        await sourceService.RecordFailureAsync(
            source.Id,
            new SharedProviderSourceFailure(
                SharedProviderSourceFailureKind.Connectivity,
                StatusCode: 503,
                SanitizedMessage: "Central source is temporarily unavailable."));

        await using var dbContext = database.Factory.CreateDbContext();
        var import = await dbContext.Set<SharedProviderImport>().SingleAsync();
        Assert.Equal(SharedProviderAvailabilityState.SourceOffline, import.AvailabilityState);
        Assert.NotEqual(SharedProviderAvailabilityState.Missing, import.AvailabilityState);
        Assert.Equal(1, await dbContext.Set<ProviderProfile>().CountAsync());
    }

    [Fact]
    public async Task Reappearance_reuses_import_and_profile_identity()
    {
        await using var database = await SharedProviderTestDatabase.CreateAsync("sharedprovider-reappearance");
        var source = await CreateSourceAsync(database);
        var reconciliation = CreateReconciliation(database);
        await reconciliation.ReconcileAsync(
            CreateReconciliationRequest(source.Id, CreateCatalog(), selectPublication: true));
        var ids = await LoadImportIdentityAsync(database);
        await reconciliation.ReconcileAsync(
            CreateReconciliationRequest(source.Id, CreateEmptyCatalog(), selectPublication: false));

        await reconciliation.ReconcileAsync(
            CreateReconciliationRequest(source.Id, CreateCatalog("Returned"), selectPublication: false));

        var actual = await LoadImportIdentityAsync(database);
        Assert.Equal(ids, actual);
        await using var dbContext = database.Factory.CreateDbContext();
        Assert.Equal(
            SharedProviderAvailabilityState.Available,
            (await dbContext.Set<SharedProviderImport>().SingleAsync()).AvailabilityState);
    }

    [Fact]
    public async Task Source_identity_mismatch_blocks_remote_mutation_and_marks_imports()
    {
        await using var database = await SharedProviderTestDatabase.CreateAsync("sharedprovider-identity-mismatch");
        var source = await CreateSourceAsync(database);
        var reconciliation = CreateReconciliation(database);
        await reconciliation.ReconcileAsync(
            CreateReconciliationRequest(source.Id, CreateCatalog("Trusted"), selectPublication: true));
        var mismatchedCatalog = CreateCatalog(
            "Untrusted replacement",
            sourceInstanceId: new SharedProviderSourceInstanceId(Guid.Parse("99999999-9999-4999-8999-999999999999")));

        var result = await reconciliation.ReconcileAsync(
            CreateReconciliationRequest(source.Id, mismatchedCatalog, selectPublication: false));

        Assert.Equal(SharedProviderReconciliationOutcome.SourceIdentityMismatch, result.Outcome);
        await using var dbContext = database.Factory.CreateDbContext();
        var import = await dbContext.Set<SharedProviderImport>().SingleAsync();
        Assert.Equal("Trusted", import.RemoteDisplayName);
        Assert.Equal(SharedProviderAvailabilityState.SourceIdentityMismatch, import.AvailabilityState);
    }

    [Fact]
    public async Task Stale_source_concurrency_token_raises_typed_conflict()
    {
        await using var database = await SharedProviderTestDatabase.CreateAsync("sharedprovider-concurrency");
        var sourceService = CreateSourceService(database);
        var original = await CreateSourceAsync(database, sourceService);
        var appliedSecretId = await CreateSecretAsync(database, "Concurrent source token");
        var appliedRequest = new SharedProviderSourceWriteRequest(
            "Concurrent source",
            new Uri("https://central.example.test/"),
            appliedSecretId,
            IsEnabled: true,
            AllowInsecurePrivateNetwork: false);
        var applied = await sourceService.UpdateAsync(
            original.Id,
            original.ConcurrencyToken,
            appliedRequest);
        var rejectedSecretId = await CreateSecretAsync(database, "Rejected source token");
        var rejectedRequest = new SharedProviderSourceWriteRequest(
            "Rejected stale source",
            new Uri("https://rejected.example.test/root"),
            rejectedSecretId,
            IsEnabled: false,
            AllowInsecurePrivateNetwork: false);

        await Assert.ThrowsAsync<SharedProviderConcurrencyException>(() =>
            sourceService.UpdateAsync(original.Id, original.ConcurrencyToken, rejectedRequest));

        await using var dbContext = database.Factory.CreateDbContext();
        var persisted = await dbContext.Set<SharedProviderSource>()
            .AsNoTracking()
            .SingleAsync(source => source.Id == original.Id);
        Assert.Equal("Concurrent source", persisted.Name);
        Assert.Equal("https://central.example.test/", persisted.BaseUri);
        Assert.Equal(appliedSecretId, persisted.ApiTokenSecretId);
        Assert.True(persisted.IsEnabled);
        Assert.Equal(applied.ConcurrencyToken, persisted.ConcurrencyToken);
        Assert.NotEqual(rejectedSecretId, persisted.ApiTokenSecretId);
    }

    [Fact]
    public async Task Invocation_audit_is_metadata_only_and_finalization_is_idempotent()
    {
        await using var database = await SharedProviderTestDatabase.CreateAsync("sharedprovider-audit");
        var service = new SharedProviderInvocationAuditService(database.Factory, new FixedClock(Now));
        const string requestId = "invocation-001";
        var publicationId = new SharedProviderPublicationId(Guid.Parse("11111111-1111-4111-8111-111111111111"));
        var providerProfileId = await SeedInvocationOwnerAsync(database, publicationId);
        var modelId = SharedProviderRoutingModelIdCodec.Create(publicationId, "upstream-model");
        var start = new SharedProviderInvocationStartRequest(
            requestId,
            publicationId,
            providerProfileId,
            "subject-42",
            AccessContextReference.Parse("opaque-context"),
            "trace-01",
            "correlation-01",
            SharedProviderRelayOperation.Responses,
            modelId,
            "upstream-model",
            Now.AddDays(30));
        var completion = new SharedProviderInvocationCompletion(
            SharedProviderInvocationOutcome.Succeeded,
            Now.AddSeconds(2),
            FailureCategory: null,
            InputTokenCount: 10,
            OutputTokenCount: 5,
            UsageCompleteness: SharedProviderMetadataCompleteness.Complete,
            Price: null,
            PricingCompleteness: SharedProviderMetadataCompleteness.Unavailable);

        Guid mismatchedProviderProfileId;
        await using (var setup = database.Factory.CreateDbContext())
        {
            var profile = new ProviderProfile
            {
                Name = "Mismatched invocation owner",
                ProviderKind = ProviderKind.OpenAi,
                ConnectorPluginKey = OpenAiProviderAdapter.PluginKey,
                ConfigSchemaVersion = "1.0",
                BaseUrl = "https://other-provider.example.test/v1",
                DefaultModel = "upstream-model",
                IsEnabled = true
            };
            setup.Add(profile);
            await setup.SaveChangesAsync();
            mismatchedProviderProfileId = profile.Id;
        }

        var mismatchedStart = start with
        {
            RequestId = "invocation-mismatched-owner",
            ProviderProfileId = mismatchedProviderProfileId
        };
        var mismatch = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BeginAsync(mismatchedStart));
        Assert.Contains("does not own", mismatch.Message, StringComparison.Ordinal);

        await service.BeginAsync(start);
        await service.FinalizeAsync(requestId, completion);
        await service.FinalizeAsync(requestId, completion);
        const string imageRequestId = "invocation-image-001";
        var imageStart = start with
        {
            RequestId = imageRequestId,
            Operation = SharedProviderRelayOperation.ImageGenerations
        };
        var imageCompletion = new SharedProviderInvocationCompletion(
            SharedProviderInvocationOutcome.Succeeded,
            Now.AddSeconds(3),
            FailureCategory: null,
            InputTokenCount: null,
            OutputTokenCount: null,
            UsageCompleteness: SharedProviderMetadataCompleteness.Complete,
            Price: null,
            PricingCompleteness: SharedProviderMetadataCompleteness.Unavailable)
        {
            ImageCount = 2
        };
        await service.BeginAsync(imageStart);
        await service.FinalizeAsync(imageRequestId, imageCompletion);

        await using var dbContext = database.Factory.CreateDbContext();
        var records = await dbContext.Set<SharedProviderInvocationRecord>()
            .AsNoTracking()
            .ToDictionaryAsync(record => record.RequestId, StringComparer.Ordinal);
        var record = records[requestId];
        var imageRecord = records[imageRequestId];
        Assert.Equal(10, record.InputTokenCount);
        Assert.Equal(5, record.OutputTokenCount);
        Assert.Null(record.ImageCount);
        Assert.Equal(SharedProviderMetadataCompleteness.Complete, record.UsageCompleteness);
        Assert.Equal(SharedProviderMetadataCompleteness.Unavailable, record.PricingCompleteness);
        Assert.Null(record.Price);
        Assert.Null(imageRecord.InputTokenCount);
        Assert.Null(imageRecord.OutputTokenCount);
        Assert.Equal(2, imageRecord.ImageCount);
        Assert.Equal(SharedProviderMetadataCompleteness.Complete, imageRecord.UsageCompleteness);

        await using (var invalidCrossOperation = database.Factory.CreateDbContext())
        {
            var invalidRecord = await invalidCrossOperation.Set<SharedProviderInvocationRecord>()
                .SingleAsync(candidate => candidate.RequestId == requestId);
            invalidRecord.Operation = SharedProviderRelayOperation.ImageGenerations;
            await Assert.ThrowsAsync<DbUpdateException>(() => invalidCrossOperation.SaveChangesAsync());
        }

        await using (var invalidZeroImageCount = database.Factory.CreateDbContext())
        {
            var invalidRecord = await invalidZeroImageCount.Set<SharedProviderInvocationRecord>()
                .SingleAsync(candidate => candidate.RequestId == imageRequestId);
            invalidRecord.ImageCount = 0;
            await Assert.ThrowsAsync<DbUpdateException>(() => invalidZeroImageCount.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Reconciliation_notifies_observers_only_after_committed_profiles()
    {
        await using var database = await SharedProviderTestDatabase.CreateAsync("sharedprovider-observer");
        var source = await CreateSourceAsync(database);
        var observer = new PersistedProfileObserver(database.Factory);
        var reconciliation = new SharedProviderReconciliationCoordinator(
            database.Factory,
            new FixedClock(Now),
            [observer]);

        var result = await reconciliation.ReconcileAsync(
            CreateReconciliationRequest(source.Id, CreateCatalog(), selectPublication: true));

        Assert.Single(result.AffectedProviderProfileIds);
        Assert.Equal(result.AffectedProviderProfileIds, observer.SavedProviderIds);
        Assert.True(observer.AllProfilesWereCommitted);
    }

    private static AppDbContext CreateModelContext()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=shared_provider_model;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }

    private static IReadOnlyList<string[]> GetUniqueConstraintPropertySets<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.IModel model)
    {
        var entity = model.FindEntityType(typeof(TEntity))!;
        return entity.GetKeys()
            .Select(key => key.Properties.Select(property => property.Name).ToArray())
            .Concat(entity.GetIndexes()
                .Where(index => index.IsUnique)
                .Select(index => index.Properties.Select(property => property.Name).ToArray()))
            .ToArray();
    }

    private static SharedProviderSourceService CreateSourceService(SharedProviderTestDatabase database)
        => new(database.Factory, new FixedClock(Now), [], new SharedProviderSourceUriPolicy());

    private static SharedProviderReconciliationCoordinator CreateReconciliation(SharedProviderTestDatabase database)
        => new(database.Factory, new FixedClock(Now), []);

    private static async Task<SharedProviderSourceWriteResult> CreateSourceAsync(
        SharedProviderTestDatabase database,
        SharedProviderSourceService? service = null)
    {
        service ??= CreateSourceService(database);
        return await service.CreateAsync(new SharedProviderSourceWriteRequest(
            "Central source",
            new Uri("https://central.example.test/root"),
            await CreateSecretAsync(database),
            IsEnabled: true,
            AllowInsecurePrivateNetwork: false));
    }

    private static SharedProviderReconciliationRequest CreateReconciliationRequest(
        Guid sourceId,
        SharedProviderCatalogDocument catalog,
        bool selectPublication)
    {
        var selected = selectPublication
            ? catalog.Providers.Select(publication => publication.PublicationId).ToHashSet()
            : [];
        return new SharedProviderReconciliationRequest(
            sourceId,
            catalog,
            SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision),
            selected);
    }

    private static SharedProviderCatalogDocument CreateCatalog(
        string displayName = "Remote provider",
        SharedProviderSourceInstanceId? sourceInstanceId = null)
    {
        var publication = CreateCatalogPublication(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            displayName,
            "upstream-model");
        return CreateCatalogDocument(sourceInstanceId, [publication]);
    }

    private static SharedProviderCatalogDocument CreateCatalogWithTwoPublications()
        => CreateCatalogDocument(
            sourceInstanceId: null,
            [
                CreateCatalogPublication(
                    Guid.Parse("11111111-1111-4111-8111-111111111111"),
                    "Remote primary provider",
                    "upstream-primary"),
                CreateCatalogPublication(
                    Guid.Parse("33333333-3333-4333-8333-333333333333"),
                    "Remote secondary provider",
                    "upstream-secondary")
            ]);

    private static SharedProviderCatalogPublication CreateCatalogPublication(
        Guid publicationValue,
        string displayName,
        string upstreamModelId)
    {
        var publicationId = new SharedProviderPublicationId(publicationValue);
        var modelId = SharedProviderRoutingModelIdCodec.Create(publicationId, upstreamModelId);
        var publication = new SharedProviderCatalogPublication(
            publicationId,
            new SharedProviderPublicRevision($"sha256:{new string('a', 64)}"),
            displayName,
            SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible,
            modelId,
            [
                new SharedProviderCatalogModel(
                    modelId,
                    "Remote model",
                    [SharedProviderCapability.Responses, SharedProviderCapability.Streaming])
            ],
            new SharedProviderCatalogHealth(SharedProviderHealthState.Available));
        publication = publication with
        {
            Revision = SharedProviderCanonicalRevision.ComputePublication(publication)
        };
        return publication;
    }

    private static SharedProviderCatalogDocument CreateCatalogDocument(
        SharedProviderSourceInstanceId? sourceInstanceId,
        IReadOnlyList<SharedProviderCatalogPublication> publications)
    {
        var catalog = new SharedProviderCatalogDocument(
            SharedProviderProtocolVersion.Current,
            sourceInstanceId ?? new SharedProviderSourceInstanceId(
                Guid.Parse("22222222-2222-4222-8222-222222222222")),
            new SharedProviderPublicRevision($"sha256:{new string('b', 64)}"),
            new SharedProviderProtocolDescriptor(SharedProviderRoutes.OpenAiBase),
            publications);
        return catalog with
        {
            CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(catalog)
        };
    }

    private static SharedProviderCatalogDocument CreateEmptyCatalog()
    {
        var catalog = new SharedProviderCatalogDocument(
            SharedProviderProtocolVersion.Current,
            new SharedProviderSourceInstanceId(Guid.Parse("22222222-2222-4222-8222-222222222222")),
            new SharedProviderPublicRevision($"sha256:{new string('c', 64)}"),
            new SharedProviderProtocolDescriptor(SharedProviderRoutes.OpenAiBase),
            []);
        return catalog with
        {
            CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(catalog)
        };
    }

    private static async Task<(Guid ImportId, Guid ProviderProfileId)> LoadImportIdentityAsync(
        SharedProviderTestDatabase database)
    {
        await using var dbContext = database.Factory.CreateDbContext();
        var import = await dbContext.Set<SharedProviderImport>().SingleAsync();
        return (import.Id, import.ProviderProfileId);
    }

    private static async Task<SharedProviderSourceWriteResult> LoadSourceIdentityAsync(
        SharedProviderTestDatabase database,
        Guid sourceId)
    {
        await using var dbContext = database.Factory.CreateDbContext();
        return await dbContext.Set<SharedProviderSource>()
            .AsNoTracking()
            .Where(source => source.Id == sourceId)
            .Select(source => new SharedProviderSourceWriteResult(source.Id, source.ConcurrencyToken))
            .SingleAsync();
    }

    private static async Task<Guid> CreateSecretAsync(
        SharedProviderTestDatabase database,
        string name = "Shared source token reference")
    {
        await using var dbContext = database.Factory.CreateDbContext();
        var secret = new SecretRecord
        {
            Name = name,
            Kind = SecretKind.Token,
            EncryptedPayload = "vault-reference:test-only",
            Scope = "workspace",
            MetadataJson = "{}",
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
        dbContext.Add(secret);
        await dbContext.SaveChangesAsync();
        return secret.Id;
    }

    private static async Task<Guid> SeedInvocationOwnerAsync(
        SharedProviderTestDatabase database,
        SharedProviderPublicationId publicationId)
    {
        await using var dbContext = database.Factory.CreateDbContext();
        var profile = new ProviderProfile
        {
            Name = "Invocation owner",
            ProviderKind = ProviderKind.OpenAi,
            ConnectorPluginKey = OpenAiProviderAdapter.PluginKey,
            ConfigSchemaVersion = "1.0",
            BaseUrl = "https://provider.example.test/v1",
            DefaultModel = "upstream-model",
            IsEnabled = true
        };
        var publication = SharedProviderPublicationTransitions.Create(
            profile.Id,
            publicationId,
            Now);
        SharedProviderPublicationTransitions.Publish(publication, Now);
        dbContext.Add(profile);
        dbContext.Add(publication);
        await dbContext.SaveChangesAsync();
        return profile.Id;
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PersistedProfileObserver(SharedProviderDbContextFactory factory)
        : IWorkspaceProviderProfileCommitObserver
    {
        public List<Guid> SavedProviderIds { get; } = [];

        public bool AllProfilesWereCommitted { get; private set; } = true;

        public async Task ProviderSavedAsync(Guid providerId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = factory.CreateDbContext();
            AllProfilesWereCommitted &= await dbContext.Set<ProviderProfile>()
                .AsNoTracking()
                .AnyAsync(profile => profile.Id == providerId, cancellationToken);
            SavedProviderIds.Add(providerId);
        }

        public Task ProviderDeletedAsync(Guid providerId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StringArrayComparer : IEqualityComparer<string[]>
    {
        public static StringArrayComparer Instance { get; } = new();

        public bool Equals(string[]? left, string[]? right)
            => left is not null && right is not null && left.SequenceEqual(right, StringComparer.Ordinal);

        public int GetHashCode(string[] value)
            => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, StringComparer.Ordinal.GetHashCode(item)));
    }

    private sealed class SharedProviderTestDatabase : IAsyncDisposable
    {
        private readonly PostgresTestDatabaseLease lease;

        private SharedProviderTestDatabase(PostgresTestDatabaseLease lease)
        {
            this.lease = lease;
            Factory = new SharedProviderDbContextFactory(lease.CreateAppDbContextOptions());
        }

        public SharedProviderDbContextFactory Factory { get; }

        public static async Task<SharedProviderTestDatabase> CreateAsync(string key, bool migrate = false)
        {
            AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
            var database = new SharedProviderTestDatabase(PostgresTestDatabaseLease.Create(key));
            await using var dbContext = database.Factory.CreateDbContext();
            if (migrate)
            {
                await dbContext.Database.GetService<IMigrator>().MigrateAsync();
            }
            else
            {
                await dbContext.Database.EnsureCreatedAsync();
            }

            return database;
        }

        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }

    private sealed class SharedProviderDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
