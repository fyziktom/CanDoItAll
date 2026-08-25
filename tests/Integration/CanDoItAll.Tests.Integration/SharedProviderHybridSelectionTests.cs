using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using AgentFrameworkProviderProfileEditorModel =
    CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using WorkspaceProviderProfile = CanDoItAll.Modules.Workspace.ProviderProfile;

public sealed class SharedProviderHybridSelectionTests(
    SharedProviderHybridSelectionFixture fixture) :
    IClassFixture<SharedProviderHybridSelectionFixture>
{
    [Fact]
    public async Task Personal_and_shared_profiles_coexist_in_the_production_registry()
    {
        var hybrid = await SeedHybridAsync(fixture.Primary);

        var providers = await LoadProvidersAsync(
            fixture.Primary,
            hybrid.PersonalProviderId,
            hybrid.Shared.ProviderProfileId);

        Assert.Equal(2, providers.Count);
        Assert.Contains(providers, provider =>
            provider.Id == hybrid.PersonalProviderId &&
            provider.ConnectorPluginKey == OpenAiProviderAdapter.PluginKey &&
            provider.IsEnabled);
        Assert.Contains(providers, provider =>
            provider.Id == hybrid.Shared.ProviderProfileId &&
            provider.ConnectorPluginKey ==
                SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey &&
            provider.IsEnabled);
    }

    [Fact]
    public async Task Identical_alias_and_model_text_do_not_collapse_profile_identity()
    {
        const string alias = "Identical provider display";
        var catalog = CreateCatalog(modelDisplayName: "Identical model display");
        var shared = await SeedSharedAsync(
            fixture.Primary,
            catalog,
            alias,
            isEnabled: true);
        var personalProviderId = await SeedPersonalAsync(
            fixture.Primary,
            alias,
            shared.DefaultModelId.Value);

        var providers = await LoadProvidersAsync(
            fixture.Primary,
            personalProviderId,
            shared.ProviderProfileId);
        var personal = providers.Single(provider => provider.Id == personalProviderId);
        var imported = providers.Single(provider =>
            provider.Id == shared.ProviderProfileId);

        Assert.Equal(personal.Name, imported.Name);
        Assert.Equal(personal.DefaultModel, imported.DefaultModel);
        Assert.NotEqual(personal.Id, imported.Id);
        Assert.NotEqual(personal.ConnectorPluginKey, imported.ConnectorPluginKey);
        var constraint = Assert.IsType<ProviderModelSelectionConstraint>(
            imported.ModelSelectionConstraint);
        Assert.Equal(imported.SuggestedModels, constraint.AllowedModels);
        ProviderModelSelectionPolicy.EnsureAllowed(
            imported,
            imported.DefaultModel);
        var foreignModel = SharedProviderRoutingModelIdCodec.Create(
            new SharedProviderPublicationId(Guid.NewGuid()),
            "shared-upstream-model").Value;
        var exception = Assert.Throws<ProviderModelSelectionException>(() =>
            ProviderModelSelectionPolicy.EnsureAllowed(imported, foreignModel));
        Assert.Equal(imported.Id, exception.ProviderProfileId);
        Assert.Equal(foreignModel, exception.RequestedModel);
        Assert.Equal(
            ProviderModelSelectionException.PublicMessage,
            exception.Message);
        var whitespaceModel = $" {imported.DefaultModel} ";
        var whitespaceException = Assert.Throws<
            ProviderModelSelectionException>(() =>
            ProviderModelSelectionPolicy.EnsureAllowed(
                imported,
                whitespaceModel));
        Assert.Equal(
            whitespaceModel,
            whitespaceException.RequestedModel);
        Assert.Null(personal.ModelSelectionConstraint);
        ProviderModelSelectionPolicy.EnsureAllowed(personal, foreignModel);
        Assert.Equal(
            "Identical model display",
            catalog.Providers.Single().Models.Single().DisplayName);
    }

    [Fact]
    public async Task Explicit_shared_selection_wins_when_workspace_default_is_personal()
    {
        var hybrid = await SeedHybridAsync(fixture.Primary);
        await SetDefaultProviderAsync(
            fixture.Primary,
            hybrid.PersonalProviderId);

        var prepared = await PrepareAsync(
            fixture.Primary,
            hybrid.Shared.ProviderProfileId,
            hybrid.Shared.DefaultModelId.Value);

        Assert.Equal(
            hybrid.PersonalProviderId,
            await LoadDefaultProviderAsync(fixture.Primary));
        Assert.Equal(
            hybrid.Shared.ProviderProfileId,
            prepared.Blueprint.Provider.Id);
        Assert.Equal(
            hybrid.Shared.ProviderProfileId,
            prepared.Blueprint.Agent.ProviderProfileId);
        var preparedConstraint = Assert.IsType<
            ProviderModelSelectionConstraint>(
            prepared.Blueprint.Provider.ModelSelectionConstraint);
        Assert.True(
            preparedConstraint.Allows(
                hybrid.Shared.DefaultModelId.Value));
    }

    [Fact]
    public async Task Explicit_personal_selection_wins_when_workspace_default_is_shared()
    {
        var hybrid = await SeedHybridAsync(fixture.Primary);
        await SetDefaultProviderAsync(
            fixture.Primary,
            hybrid.Shared.ProviderProfileId);

        var prepared = await PrepareAsync(
            fixture.Primary,
            hybrid.PersonalProviderId,
            hybrid.PersonalDefaultModel);

        Assert.Equal(
            hybrid.Shared.ProviderProfileId,
            await LoadDefaultProviderAsync(fixture.Primary));
        Assert.Equal(
            hybrid.PersonalProviderId,
            prepared.Blueprint.Provider.Id);
        Assert.Equal(
            hybrid.PersonalProviderId,
            prepared.Blueprint.Agent.ProviderProfileId);
    }

    [Fact]
    public async Task Source_outage_retains_the_shared_profile_as_disabled()
    {
        var shared = await SeedSharedAsync(fixture.Primary);
        await MarkSourceOfflineAsync(fixture.Primary, shared);

        var provider = await LoadProviderAsync(
            fixture.Primary,
            shared.ProviderProfileId);

        Assert.False(provider.IsEnabled);
        Assert.Equal(
            nameof(SharedProviderRuntimeProfileAvailability.SourceOffline),
            provider.HealthStatus);
        Assert.Contains(shared.DefaultModelId.Value, provider.SuggestedModels);
    }

    [Fact]
    public async Task Authoritative_unpublish_retains_the_shared_profile_as_disabled()
    {
        var shared = await SeedSharedAsync(fixture.Primary);
        await MarkUnpublishedAsync(fixture.Primary, shared.ImportId);

        var provider = await LoadProviderAsync(
            fixture.Primary,
            shared.ProviderProfileId);

        Assert.False(provider.IsEnabled);
        Assert.Equal(
            nameof(SharedProviderRuntimeProfileAvailability.Unpublished),
            provider.HealthStatus);
        Assert.Contains(shared.DefaultModelId.Value, provider.SuggestedModels);
    }

    [Fact]
    public async Task Retirement_and_identity_mismatch_retain_distinct_disabled_profiles()
    {
        var retired = await SeedSharedAsync(fixture.Primary);
        var mismatched = await SeedSharedAsync(fixture.Primary);
        await ReconcileAsync(
            fixture.Primary,
            retired.SourceId,
            retired.Catalog,
            Selection(),
            SharedProviderSelectionMode.Replace);
        var replacementIdentityCatalog = CreateCatalogDocument(
            new SharedProviderSourceInstanceId(Guid.NewGuid()),
            mismatched.Catalog.Providers);
        var mismatch = await ReconcileAsync(
            fixture.Primary,
            mismatched.SourceId,
            replacementIdentityCatalog,
            Selection(mismatched.PublicationId));

        var providers = await LoadProvidersAsync(
            fixture.Primary,
            retired.ProviderProfileId,
            mismatched.ProviderProfileId);
        var retiredProvider = providers.Single(provider =>
            provider.Id == retired.ProviderProfileId);
        var mismatchedProvider = providers.Single(provider =>
            provider.Id == mismatched.ProviderProfileId);

        Assert.Equal(
            SharedProviderReconciliationOutcome.SourceIdentityMismatch,
            mismatch.Outcome);
        Assert.False(retiredProvider.IsEnabled);
        Assert.Equal(
            nameof(SharedProviderRuntimeProfileAvailability.Retired),
            retiredProvider.HealthStatus);
        Assert.False(mismatchedProvider.IsEnabled);
        Assert.Equal(
            nameof(SharedProviderRuntimeProfileAvailability.SourceIdentityMismatch),
            mismatchedProvider.HealthStatus);
        Assert.NotEqual(retiredProvider.Id, mismatchedProvider.Id);
    }

    [Fact]
    public async Task Reappearance_reuses_import_and_profile_identity()
    {
        const string localAlias = "Locally retained shared alias";
        var shared = await SeedSharedAsync(
            fixture.Primary,
            alias: localAlias,
            isEnabled: true);
        var emptyCatalog = CreateCatalogDocument(
            shared.Catalog.SourceInstanceId,
            []);
        await ReconcileAsync(
            fixture.Primary,
            shared.SourceId,
            emptyCatalog,
            Selection());

        var missing = await LoadProviderAsync(
            fixture.Primary,
            shared.ProviderProfileId);
        Assert.False(missing.IsEnabled);
        Assert.Equal(
            nameof(SharedProviderRuntimeProfileAvailability.Missing),
            missing.HealthStatus);

        await ReconcileAsync(
            fixture.Primary,
            shared.SourceId,
            shared.Catalog,
            Selection(shared.PublicationId));
        var persisted = await LoadSharedIdentityAsync(
            fixture.Primary,
            shared.SourceId,
            shared.PublicationId);
        var reappeared = await LoadProviderAsync(
            fixture.Primary,
            shared.ProviderProfileId);

        Assert.Equal(shared.ImportId, persisted.ImportId);
        Assert.Equal(shared.ProviderProfileId, persisted.ProviderProfileId);
        Assert.Equal(localAlias, persisted.Alias);
        Assert.True(persisted.LocalEnabledIntent);
        Assert.True(reappeared.IsEnabled);
        Assert.Equal(
            nameof(SharedProviderRuntimeProfileAvailability.Available),
            reappeared.HealthStatus);
    }

    [Fact]
    public async Task Unavailable_shared_selection_never_falls_back_to_personal()
    {
        var hybrid = await SeedHybridAsync(fixture.Primary);
        await SetDefaultProviderAsync(
            fixture.Primary,
            hybrid.PersonalProviderId);
        await MarkSourceOfflineAsync(fixture.Primary, hybrid.Shared);
        var personal = await LoadProviderAsync(
            fixture.Primary,
            hybrid.PersonalProviderId);
        Assert.True(personal.IsEnabled);

        var exception = await Assert.ThrowsAsync<
            ProviderRuntimeProfileUnavailableException>(() => PrepareAsync(
                fixture.Primary,
                hybrid.Shared.ProviderProfileId,
                hybrid.Shared.DefaultModelId.Value));

        Assert.Equal(hybrid.Shared.ProviderProfileId, exception.ProviderId);
        Assert.NotEqual(hybrid.PersonalProviderId, exception.ProviderId);
    }

    [Fact]
    public async Task Independent_client_databases_keep_local_import_identity_and_intent_separate()
    {
        var catalog = CreateCatalog(
            publicationId: new SharedProviderPublicationId(Guid.NewGuid()),
            sourceInstanceId: new SharedProviderSourceInstanceId(Guid.NewGuid()));
        var clientA = await SeedSharedAsync(
            fixture.Primary,
            catalog,
            "Client A alias",
            isEnabled: false);
        var secondApplication = await fixture.GetSecondaryAsync();
        var clientB = await SeedSharedAsync(
            secondApplication,
            catalog,
            "Client B alias",
            isEnabled: true);

        var providerA = await LoadProviderAsync(
            fixture.Primary,
            clientA.ProviderProfileId);
        var providerB = await LoadProviderAsync(
            secondApplication,
            clientB.ProviderProfileId);
        var identityA = await LoadSharedIdentityAsync(
            fixture.Primary,
            clientA.SourceId,
            clientA.PublicationId);
        var identityB = await LoadSharedIdentityAsync(
            secondApplication,
            clientB.SourceId,
            clientB.PublicationId);

        Assert.Equal(clientA.PublicationId, clientB.PublicationId);
        Assert.Equal(clientA.DefaultModelId, clientB.DefaultModelId);
        Assert.Equal(identityA.RemoteSourceInstanceId, identityB.RemoteSourceInstanceId);
        Assert.NotEqual(clientA.SourceId, clientB.SourceId);
        Assert.NotEqual(clientA.ImportId, clientB.ImportId);
        Assert.NotEqual(clientA.ProviderProfileId, clientB.ProviderProfileId);
        Assert.NotEqual(clientA.SourceTokenSecretId, clientB.SourceTokenSecretId);
        Assert.Equal("Client A alias", providerA.Name);
        Assert.False(providerA.IsEnabled);
        Assert.Equal("Client B alias", providerB.Name);
        Assert.True(providerB.IsEnabled);
    }

    private static async Task<HybridSeed> SeedHybridAsync(
        TestApplication application)
    {
        var shared = await SeedSharedAsync(application);
        const string personalModel = "personal-model";
        var personalProviderId = await SeedPersonalAsync(
            application,
            $"Personal provider {Guid.NewGuid():N}",
            personalModel);
        return new HybridSeed(shared, personalProviderId, personalModel);
    }

    private static async Task<SharedSeed> SeedSharedAsync(
        TestApplication application,
        SharedProviderCatalogDocument? catalog = null,
        string? alias = null,
        bool isEnabled = true)
    {
        catalog ??= CreateCatalog();
        var publication = Assert.Single(catalog.Providers);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var now = DateTimeOffset.UtcNow;
        var secret = new SecretRecord
        {
            Name = $"Shared source token {Guid.NewGuid():N}",
            Kind = SecretKind.Token,
            EncryptedPayload = "vault-reference:hybrid-test-only",
            Scope = "workspace",
            MetadataJson = "{}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var source = SharedProviderSourceTransitions.Create(
            $"Shared source {Guid.NewGuid():N}",
            "https://central.shared.example.test/tenant/client/",
            secret.Id,
            allowInsecurePrivateNetwork: false,
            isEnabled: true,
            timestampUtc: now);
        await using (var dbContext =
            await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Add(secret);
            dbContext.Add(source);
            await dbContext.SaveChangesAsync();
        }

        var reconciliation = await scope.ServiceProvider
            .GetRequiredService<SharedProviderReconciliationCoordinator>()
            .ReconcileAsync(new SharedProviderReconciliationRequest(
                source.Id,
                catalog,
                SharedProviderCatalogEntityTag.FromRevision(
                    catalog.CatalogRevision),
                Selection(publication.PublicationId),
                SharedProviderSelectionMode.Replace));
        Assert.Equal(
            SharedProviderReconciliationOutcome.Applied,
            reconciliation.Outcome);

        await using var editContext =
            await dbContextFactory.CreateDbContextAsync();
        var import = await editContext.Set<SharedProviderImport>()
            .SingleAsync(item =>
                item.SourceId == source.Id &&
                item.RemotePublicationId == publication.PublicationId);
        var profile = await editContext.Set<WorkspaceProviderProfile>()
            .SingleAsync(item => item.Id == import.ProviderProfileId);
        profile.Name = alias ?? profile.Name;
        profile.IsEnabled = isEnabled;
        await editContext.SaveChangesAsync();

        return new SharedSeed(
            source.Id,
            import.Id,
            profile.Id,
            secret.Id,
            publication.PublicationId,
            publication.DefaultModelId,
            catalog);
    }

    private static async Task<Guid> SeedPersonalAsync(
        TestApplication application,
        string name,
        string defaultModel)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var now = DateTimeOffset.UtcNow;
        var secret = new SecretRecord
        {
            Name = $"Personal provider token {Guid.NewGuid():N}",
            Kind = SecretKind.ApiKey,
            EncryptedPayload = "vault-reference:personal-test-only",
            Scope = "workspace",
            MetadataJson = "{}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await using (var dbContext =
            await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Add(secret);
            await dbContext.SaveChangesAsync();
        }

        return await scope.ServiceProvider
            .GetRequiredService<IProviderProfileRegistry>()
            .SaveProviderAsync(new AgentFrameworkProviderProfileEditorModel
            {
                Name = name,
                Kind = AgentFrameworkProviderKind.OpenAi,
                BaseUrl = "https://personal.example.test/v1",
                ApiKeyEnvironmentVariable = $"secret:{secret.Id:D}",
                DefaultModel = defaultModel,
                Transport = ProviderTransportKind.Responses,
                Purpose = ProviderProfilePurpose.Chat,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsTools = true,
                PreferFrameworkManagedChatHistory = true,
                SupportsBackgroundResponses = false,
                ConfigurationJson = "{}"
            });
    }

    private static async Task<IReadOnlyList<
        CanDoItAll.AgentFramework.Models.ProviderProfile>> LoadProvidersAsync(
        TestApplication application,
        params Guid[] providerIds)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var providers = await scope.ServiceProvider
            .GetRequiredService<IProviderProfileRegistry>()
            .ListProvidersAsync();
        var expectedIds = providerIds.ToHashSet();
        return providers
            .Where(provider => expectedIds.Contains(provider.Id))
            .ToArray();
    }

    private static async Task<CanDoItAll.AgentFramework.Models.ProviderProfile>
        LoadProviderAsync(
            TestApplication application,
            Guid providerId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var provider = await scope.ServiceProvider
            .GetRequiredService<IProviderProfileRegistry>()
            .GetProviderAsync(providerId);
        Assert.NotNull(provider);
        return provider!;
    }

    private static async Task<AgentExecutionPreparationSnapshot> PrepareAsync(
        TestApplication application,
        Guid providerId,
        string model)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var store = scope.ServiceProvider
            .GetRequiredService<ISandboxWorkspaceCatalogStore>();
        var agent = CreateAgent(providerId, model);
        await store.UpdateCatalogAsync(catalog => catalog with
        {
            Agents = catalog.Agents.Append(agent).ToArray()
        });
        await scope.ServiceProvider
            .GetRequiredService<IProviderRuntimeProfileSnapshotInitializer>()
            .InitializeAsync();
        var preparation = new AgentExecutionPreparationService(
            store,
            scope.ServiceProvider.GetRequiredService<
                IProviderRuntimeProfileSnapshotSource>(),
            scope.ServiceProvider.GetRequiredService<
                IAgentExecutionPreparationCache>(),
            scope.ServiceProvider.GetRequiredService<
                IAgentExecutionProfileGenerationSource>(),
            AgentExecutionActivityWorkspaceIdentity.CreateHostLifetime(
                WorkspaceScopeDescriptor.Organization(
                    "shared-provider-hybrid-selection")));
        return await preparation.AcquireForAtomicConsumerAsync(agent.Id);
    }

    private static AgentDefinition CreateAgent(Guid providerId, string model)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            $"Hybrid selection agent {Guid.NewGuid():N}",
            "Hybrid provider selection",
            "Exercises explicit personal and shared provider selection.",
            "Use only the explicitly selected provider.",
            AgentLifecycleStatus.Active,
            providerId,
            model,
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static async Task SetDefaultProviderAsync(
        TestApplication application,
        Guid providerId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var settings = await dbContext.Set<WorkspaceSettings>()
            .FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new WorkspaceSettings();
            dbContext.Add(settings);
        }

        settings.DefaultProviderProfileId = providerId;
        settings.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid?> LoadDefaultProviderAsync(
        TestApplication application)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        return await dbContext.Set<WorkspaceSettings>()
            .OrderByDescending(settings => settings.UpdatedAtUtc)
            .Select(settings => settings.DefaultProviderProfileId)
            .FirstOrDefaultAsync();
    }

    private static async Task MarkSourceOfflineAsync(
        TestApplication application,
        SharedSeed shared)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var source = await dbContext.Set<SharedProviderSource>()
            .SingleAsync(item => item.Id == shared.SourceId);
        var import = await dbContext.Set<SharedProviderImport>()
            .SingleAsync(item => item.Id == shared.ImportId);
        var latestTimestamp = source.UpdatedAtUtc >= import.UpdatedAtUtc
            ? source.UpdatedAtUtc
            : import.UpdatedAtUtc;
        var transitionTimestamp = latestTimestamp.AddTicks(1);
        SharedProviderSourceTransitions.ApplyFailure(
            source,
            SharedProviderSourceStatus.SourceOffline,
            statusCode: 503,
            sanitizedMessage: "The shared source is temporarily unavailable.",
            timestampUtc: transitionTimestamp);
        SharedProviderImportTransitions.MarkTransientlyUnavailable(
            import,
            SharedProviderAvailabilityState.SourceOffline,
            transitionTimestamp);
        await dbContext.SaveChangesAsync();
    }

    private static async Task MarkUnpublishedAsync(
        TestApplication application,
        Guid importId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var import = await dbContext.Set<SharedProviderImport>()
            .SingleAsync(item => item.Id == importId);
        SharedProviderImportTransitions.MarkAuthoritativelyAbsent(
            import,
            SharedProviderAvailabilityState.Unpublished,
            import.UpdatedAtUtc.AddTicks(1));
        await dbContext.SaveChangesAsync();
    }

    private static async Task<SharedProviderReconciliationResult> ReconcileAsync(
        TestApplication application,
        Guid sourceId,
        SharedProviderCatalogDocument catalog,
        IReadOnlySet<SharedProviderPublicationId> selectedPublicationIds,
        SharedProviderSelectionMode selectionMode =
            SharedProviderSelectionMode.AddOrReactivate)
    {
        await using var scope = application.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<SharedProviderReconciliationCoordinator>()
            .ReconcileAsync(new SharedProviderReconciliationRequest(
                sourceId,
                catalog,
                SharedProviderCatalogEntityTag.FromRevision(
                    catalog.CatalogRevision),
                selectedPublicationIds,
                selectionMode));
    }

    private static async Task<PersistedSharedIdentity> LoadSharedIdentityAsync(
        TestApplication application,
        Guid sourceId,
        SharedProviderPublicationId publicationId)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var import = await dbContext.Set<SharedProviderImport>()
            .AsNoTracking()
            .SingleAsync(item =>
                item.SourceId == sourceId &&
                item.RemotePublicationId == publicationId);
        var source = await dbContext.Set<SharedProviderSource>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == sourceId);
        var profile = await dbContext.Set<WorkspaceProviderProfile>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == import.ProviderProfileId);
        return new PersistedSharedIdentity(
            import.Id,
            profile.Id,
            profile.Name,
            profile.IsEnabled,
            source.RemoteInstanceId);
    }

    private static SharedProviderCatalogDocument CreateCatalog(
        SharedProviderPublicationId? publicationId = null,
        SharedProviderSourceInstanceId? sourceInstanceId = null,
        string modelDisplayName = "Shared model display")
    {
        var resolvedPublicationId = publicationId ??
            new SharedProviderPublicationId(Guid.NewGuid());
        var defaultModelId = SharedProviderRoutingModelIdCodec.Create(
            resolvedPublicationId,
            "shared-upstream-model");
        var draftPublication = new SharedProviderCatalogPublication(
            resolvedPublicationId,
            PlaceholderRevision(),
            "Shared provider display",
            SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible,
            defaultModelId,
            [
                new SharedProviderCatalogModel(
                    defaultModelId,
                    modelDisplayName,
                    [
                        SharedProviderCapability.Responses,
                        SharedProviderCapability.Streaming,
                        SharedProviderCapability.FunctionTools,
                        SharedProviderCapability.StructuredOutput
                    ])
            ],
            new SharedProviderCatalogHealth(
                SharedProviderHealthState.Available));
        var publication = draftPublication with
        {
            Revision = SharedProviderCanonicalRevision.ComputePublication(
                draftPublication)
        };
        return CreateCatalogDocument(
            sourceInstanceId ??
                new SharedProviderSourceInstanceId(Guid.NewGuid()),
            [publication]);
    }

    private static SharedProviderCatalogDocument CreateCatalogDocument(
        SharedProviderSourceInstanceId sourceInstanceId,
        IReadOnlyList<SharedProviderCatalogPublication> publications)
    {
        var draft = new SharedProviderCatalogDocument(
            SharedProviderProtocolVersion.Current,
            sourceInstanceId,
            PlaceholderRevision(),
            new SharedProviderProtocolDescriptor(
                SharedProviderRoutes.OpenAiBase),
            publications);
        return draft with
        {
            CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(
                draft)
        };
    }

    private static SharedProviderPublicRevision PlaceholderRevision()
        => new(
            $"{SharedProviderPublicRevision.Prefix}{new string('0', SharedProviderPublicRevision.HashLength)}");

    private static IReadOnlySet<SharedProviderPublicationId> Selection(
        params SharedProviderPublicationId[] publicationIds)
        => publicationIds.ToHashSet();

    private sealed record HybridSeed(
        SharedSeed Shared,
        Guid PersonalProviderId,
        string PersonalDefaultModel);

    private sealed record SharedSeed(
        Guid SourceId,
        Guid ImportId,
        Guid ProviderProfileId,
        Guid SourceTokenSecretId,
        SharedProviderPublicationId PublicationId,
        SharedProviderRoutingModelId DefaultModelId,
        SharedProviderCatalogDocument Catalog);

    private sealed record PersistedSharedIdentity(
        Guid ImportId,
        Guid ProviderProfileId,
        string Alias,
        bool LocalEnabledIntent,
        SharedProviderSourceInstanceId? RemoteSourceInstanceId);
}

public sealed class SharedProviderHybridSelectionFixture : IAsyncLifetime
{
    private CanDoItAllTestEnvironment? environment;
    private TestApplication? primary;
    private TestApplication? secondary;

    internal TestApplication Primary
        => primary ?? throw new InvalidOperationException(
            "The primary hybrid-selection application is not initialized.");

    public async Task InitializeAsync()
    {
        environment = CanDoItAllTestEnvironment.Create(
            "shared-provider-hybrid-selection");
        var activeProfile = environment.CreatePostgreSqlProfile("client-a");
        primary = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            TestEnvironment = environment,
            ActiveProfile = activeProfile,
            ConfigurationOverrides = CreateControlPlaneOverrides(activeProfile)
        });
    }

    internal async Task<TestApplication> GetSecondaryAsync()
    {
        if (secondary is not null)
        {
            return secondary;
        }

        var currentEnvironment = environment ??
            throw new InvalidOperationException(
                "The hybrid-selection test environment is not initialized.");
        var activeProfile = currentEnvironment.CreatePostgreSqlProfile(
            "client-b");
        secondary = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            TestEnvironment = currentEnvironment,
            ActiveProfile = activeProfile,
            ConfigurationOverrides = CreateControlPlaneOverrides(activeProfile)
        });
        return secondary;
    }

    private static IReadOnlyDictionary<string, string?>
        CreateControlPlaneOverrides(TestDatabaseProfile activeProfile)
    {
        var controlPlaneRoot = Path.Combine(
            activeProfile.ProfileRootPath,
            "control-plane");
        return new Dictionary<string, string?>
        {
            ["ControlPlane:RootPath"] = controlPlaneRoot,
            ["ControlPlane:StateRootPath"] = Path.Combine(
                activeProfile.ProfileRootPath,
                "state"),
            ["ControlPlane:LogsRootPath"] = Path.Combine(
                activeProfile.ProfileRootPath,
                "logs"),
            ["ControlPlane:RuntimeTemporaryRootPath"] = Path.Combine(
                activeProfile.ProfileRootPath,
                "runtime")
        };
    }

    public async Task DisposeAsync()
    {
        if (secondary is not null)
        {
            await secondary.DisposeAsync();
        }

        if (primary is not null)
        {
            await primary.DisposeAsync();
        }

        if (environment is not null)
        {
            await environment.DisposeAsync();
        }
    }
}
