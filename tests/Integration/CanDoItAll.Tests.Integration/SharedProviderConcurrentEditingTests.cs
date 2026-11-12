using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;

namespace CanDoItAll.Tests.Integration.SharedProviders;

public sealed class SharedProviderConcurrentEditingTests(SharedProviderRuntimeProjectionFixture fixture)
    : IClassFixture<SharedProviderRuntimeProjectionFixture> {
    [Fact]
    public async Task Publication_emits_targeted_lifecycle_metadata_without_changing_the_provider_configuration() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<CanDoItAll.AgentFramework.Core.IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(LocalEditor());
        var owner = services.GetRequiredService<ISharedProviderManagementService>();
        var original = await owner.GetProfileSharingAsync(id);
        var topic = new EditingTopic(services.GetRequiredService<ProviderEditingChanges>().Scope, EditingOwner.ProviderProfile, id);
        using var subscription = services.GetRequiredService<EditingChangeFeed>().Subscribe(topic);
        await owner.SetPublicationAsync(id, SharedProviderPublicationAction.Publish, null, original.ProviderConcurrencyToken!.Value);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var change = Assert.Single((await subscription.ReadAsync(timeout.Token)).Changes);
        Assert.Equal(topic, change.Resource);
        Assert.Equal(EditingChangeKind.LifecycleChanged, change.Kind);
        Assert.Equal(original.ProviderConcurrencyToken, (await owner.GetProfileSharingAsync(id)).ProviderConcurrencyToken);
    }

    [Fact]
    public async Task Publication_returns_exact_acceptance_before_a_later_peer_configuration_and_unpublication() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<CanDoItAll.AgentFramework.Core.IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(LocalEditor());
        var peer = services.GetRequiredService<ISharedProviderManagementService>();
        var original = await peer.GetProfileSharingAsync(id);
        var observer = new AfterPublication(async () => {
            var editor = await registry.GetProviderEditorAsync(id);
            editor.Name = "Later peer publication configuration";
            await registry.SaveProviderAsync(editor);
            var current = await peer.GetProfileSharingAsync(id);
            await peer.SetPublicationAsync(id, SharedProviderPublicationAction.Unpublish, current.Publication!.ConcurrencyToken,
                current.ProviderConcurrencyToken!.Value);
        });
        var application = ActivatorUtilities.CreateInstance<SharedProviderPublicationApplicationService>(services,
            (IEnumerable<ISharedProviderPublicationCommitObserver>)[observer]);
        var owner = ActivatorUtilities.CreateInstance<SharedProviderManagementService>(services, application);
        var accepted = await owner.SetPublicationAsync(id, SharedProviderPublicationAction.Publish, null, original.ProviderConcurrencyToken!.Value);
        Assert.Null(observer.Failure);
        Assert.True(accepted.Publication!.IsPublished);
        Assert.Equal(original.ProviderConcurrencyToken, accepted.ProviderConcurrencyToken);
        var latest = await peer.GetProfileSharingAsync(id);
        Assert.False(latest.Publication!.IsPublished);
        Assert.NotEqual(accepted.ProviderConcurrencyToken, latest.ProviderConcurrencyToken);
        Assert.NotEqual(accepted.Publication.ConcurrencyToken, latest.Publication.ConcurrencyToken);
    }

    [Fact]
    public async Task Publication_cannot_omit_its_original_provider_configuration() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<CanDoItAll.AgentFramework.Core.IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(LocalEditor());
        await Assert.ThrowsAsync<SharedProviderConcurrencyException>(() => services.GetRequiredService<SharedProviderPublicationApplicationService>()
            .ChangeAsync(new(id, SharedProviderPublicationAction.Publish, null)));
        Assert.Null((await services.GetRequiredService<ISharedProviderManagementService>().GetProfileSharingAsync(id)).Publication);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publication_rejects_an_original_provider_configuration_changed_by_a_peer(bool unpublish) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<CanDoItAll.AgentFramework.Core.IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(LocalEditor());
        var original = await registry.GetProviderEditorAsync(id);
        var application = services.GetRequiredService<SharedProviderPublicationApplicationService>();
        var published = unpublish ? await application.ChangeAsync(new(id, SharedProviderPublicationAction.Publish, null) {
            ExpectedProviderConcurrencyToken = original.ExpectedConcurrencyToken
        }) : null;
        var peer = await registry.GetProviderEditorAsync(id);
        peer.Name = "Peer configuration must survive stale publication";
        await registry.SaveProviderAsync(peer);
        await Assert.ThrowsAsync<SharedProviderConcurrencyException>(() => application.ChangeAsync(new(id,
            unpublish ? SharedProviderPublicationAction.Unpublish : SharedProviderPublicationAction.Publish, published?.ConcurrencyToken) {
            ExpectedProviderConcurrencyToken = original.ExpectedConcurrencyToken
        }));
        var current = await services.GetRequiredService<ISharedProviderManagementService>().GetProfileSharingAsync(id);
        Assert.Equal(unpublish, current.Publication?.IsPublished == true);
        Assert.Equal(peer.Name, (await registry.GetProviderEditorAsync(id)).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publication_preserves_original_authority_before_and_after_SQL(bool denyAfterSql) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<CanDoItAll.AgentFramework.Core.IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(LocalEditor());
        var original = await registry.GetProviderEditorAsync(id);
        var authorized = !denyAfterSql;
        var interceptor = new AfterSql(() => authorized = false);
        var factory = new SourceFactory(new DbContextOptionsBuilder<ProvidersDbContext>(
            services.GetRequiredService<DbContextOptions<ProvidersDbContext>>()).AddInterceptors(interceptor).Options);
        var owner = ActivatorUtilities.CreateInstance<SharedProviderPublicationApplicationService>(services, factory);
        var changes = services.GetRequiredService<ProviderEditingChanges>();
        authorized = denyAfterSql;
        using (services.GetRequiredService<ProviderEditingWrites>().Enter(new(changes.Scope, EditingOwner.ProviderProfile, id),
            new(Guid.NewGuid(), Guid.NewGuid()), _ => authorized ? ValueTask.CompletedTask
                : ValueTask.FromException(new UnauthorizedAccessException("Original publication actor has ended.")))) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => owner.ChangeAsync(new(id, SharedProviderPublicationAction.Publish, null) {
                ExpectedProviderConcurrencyToken = original.ExpectedConcurrencyToken
            }));
        }
        Assert.Equal(denyAfterSql, interceptor.Called);
        Assert.Null((await services.GetRequiredService<ISharedProviderManagementService>().GetProfileSharingAsync(id)).Publication);
    }

    private static CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel LocalEditor() => new() {
        Name = $"Owned publication {Guid.NewGuid():N}", Kind = CanDoItAll.AgentFramework.Models.ProviderKind.Ollama,
        Transport = CanDoItAll.AgentFramework.Models.ProviderTransportKind.ChatCompletions,
        BaseUrl = "http://127.0.0.1:11434", DefaultModel = "owned-model", SuggestedModels = ["owned-model"], IsEnabled = true
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import_authority_loss_after_SQL_rolls_back_the_original_settings_or_retirement(bool retire) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var before = await CreateImportAsync(services);
        var authorized = true;
        var interceptor = new AfterSql(() => authorized = false);
        var factory = new SourceFactory(new DbContextOptionsBuilder<ProvidersDbContext>(
            services.GetRequiredService<DbContextOptions<ProvidersDbContext>>()).AddInterceptors(interceptor).Options);
        var owner = ActivatorUtilities.CreateInstance<SharedProviderManagementService>(services, factory);
        var changes = services.GetRequiredService<ProviderEditingChanges>();
        using (services.GetRequiredService<ProviderEditingWrites>().Enter(new(changes.Scope, EditingOwner.ProviderProfile, before.ProviderProfileId),
            new(Guid.NewGuid(), Guid.NewGuid()), _ => authorized ? ValueTask.CompletedTask
                : ValueTask.FromException(new UnauthorizedAccessException("Original imported-provider actor was retired after SQL.")))) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => {
                if (retire) {
                    await owner.RetireImportedProfileAsync(new(before.ImportId, before.ProviderProfileId,
                        before.ImportConcurrencyToken, before.ProviderConcurrencyToken));
                } else {
                    await owner.UpdateImportedProfileAsync(new(before.ImportId, before.ProviderProfileId, "Rejected local alias", false,
                        before.ImportConcurrencyToken, before.ProviderConcurrencyToken));
                }
            });
        }
        Assert.True(interceptor.Called);
        var current = (await services.GetRequiredService<ISharedProviderManagementService>().GetProfileSharingAsync(before.ProviderProfileId)).Import!;
        Assert.Equal(before.ImportId, current.ImportId);
        Assert.Equal(before.SourceId, current.SourceId);
        Assert.Equal(before.ProviderProfileId, current.ProviderProfileId);
        Assert.Equal(before.ImportConcurrencyToken, current.ImportConcurrencyToken);
        Assert.Equal(before.ProviderConcurrencyToken, current.ProviderConcurrencyToken);
        Assert.Equal(before.LocalAlias, current.LocalAlias);
        Assert.Equal(before.IsEnabled, current.IsEnabled);
        Assert.Equal(before.SelectionState, current.SelectionState);
    }

    [Fact]
    public async Task Import_save_returns_its_exact_native_acceptance_before_a_later_observer_peer_write() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var before = await CreateImportAsync(services);
        var peer = services.GetRequiredService<ISharedProviderManagementService>();
        var observer = new AfterImportSaved(async id => {
            var original = (await peer.GetProfileSharingAsync(id)).Import!;
            await peer.UpdateImportedProfileAsync(new(original.ImportId, id, "Later peer alias", false,
                original.ImportConcurrencyToken, original.ProviderConcurrencyToken));
        });
        var owner = ActivatorUtilities.CreateInstance<SharedProviderManagementService>(services,
            (IEnumerable<IProviderProfileCommitObserver>)[observer]);
        var accepted = (await owner.UpdateImportedProfileAsync(new(before.ImportId, before.ProviderProfileId, "  Accepted alias  ", true,
            before.ImportConcurrencyToken, before.ProviderConcurrencyToken))).Import!;
        Assert.Equal("Accepted alias", accepted.LocalAlias);
        Assert.True(accepted.IsEnabled);
        Assert.Null(observer.Failure);
        var current = (await peer.GetProfileSharingAsync(before.ProviderProfileId)).Import!;
        Assert.Equal("Later peer alias", current.LocalAlias);
        Assert.False(current.IsEnabled);
        Assert.NotEqual(accepted.ProviderConcurrencyToken, current.ProviderConcurrencyToken);
    }

    [Fact]
    public async Task Import_retirement_publishes_targeted_source_selection_metadata() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var before = await CreateImportAsync(services);
        var changes = services.GetRequiredService<ProviderEditingChanges>();
        var topic = new EditingTopic(changes.Scope, EditingOwner.SharedProviderSource, before.SourceId);
        using var subscription = services.GetRequiredService<EditingChangeFeed>().Subscribe(topic);
        await services.GetRequiredService<ISharedProviderManagementService>().RetireImportedProfileAsync(new(before.ImportId,
            before.ProviderProfileId, before.ImportConcurrencyToken, before.ProviderConcurrencyToken));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(topic, Assert.Single((await subscription.ReadAsync(timeout.Token)).Changes).Resource);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reconciliation_uses_original_authority_and_publishes_source_metadata(bool denyAfterSql) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CreateSourceAsync(services);
        var authorized = true;
        var interceptor = new AfterSql(() => authorized = !denyAfterSql);
        var factory = new SourceFactory(new DbContextOptionsBuilder<ProvidersDbContext>(
            services.GetRequiredService<DbContextOptions<ProvidersDbContext>>()).AddInterceptors(interceptor).Options);
        var coordinator = ActivatorUtilities.CreateInstance<SharedProviderReconciliationCoordinator>(services, factory);
        var initial = new SharedProviderCatalogDocument(SharedProviderProtocolVersion.Current,
            new(Guid.NewGuid()), new($"sha256:{new string('b', 64)}"), new(SharedProviderRoutes.OpenAiBase), []);
        var catalog = initial with { CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(initial) };
        var request = new SharedProviderReconciliationRequest(source.Id, catalog, SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision),
            new HashSet<SharedProviderPublicationId>(), expectedSourceConcurrencyToken: source.ConcurrencyToken,
            originalSelection: new(source.ConcurrencyToken, []));
        var changes = services.GetRequiredService<ProviderEditingChanges>();
        var topic = new EditingTopic(changes.Scope, EditingOwner.SharedProviderSource, source.Id);
        using var subscription = services.GetRequiredService<EditingChangeFeed>().Subscribe(topic);
        using (services.GetRequiredService<ProviderEditingWrites>().Enter(topic, new(Guid.NewGuid(), Guid.NewGuid()),
            _ => authorized ? ValueTask.CompletedTask : ValueTask.FromException(new UnauthorizedAccessException("Original synchronization actor was retired after SQL.")))) {
            if (denyAfterSql) {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => coordinator.ReconcileAsync(request));
            } else {
                await coordinator.ReconcileAsync(request);
            }
        }
        Assert.True(interceptor.Called);
        var current = await services.GetRequiredService<SharedProviderSourceService>().GetAsync(source.Id);
        if (denyAfterSql) {
            Assert.Equal(source.ConcurrencyToken, current.ConcurrencyToken);
            Assert.Null(current.RemoteInstanceId);
        } else {
            Assert.NotEqual(source.ConcurrencyToken, current.ConcurrencyToken);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Assert.Equal(topic, Assert.Single((await subscription.ReadAsync(timeout.Token)).Changes).Resource);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Source_authority_loss_after_SQL_rolls_back_the_original_mutation(bool delete) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CreateSourceAsync(services);
        var authorized = true;
        var interceptor = new AfterSql(() => authorized = false);
        var factory = new SourceFactory(new DbContextOptionsBuilder<ProvidersDbContext>(
            services.GetRequiredService<DbContextOptions<ProvidersDbContext>>()).AddInterceptors(interceptor).Options);
        var owner = ActivatorUtilities.CreateInstance<SharedProviderSourceService>(services, factory);
        var changes = services.GetRequiredService<ProviderEditingChanges>();
        using (services.GetRequiredService<ProviderEditingWrites>().Enter(new(changes.Scope, EditingOwner.SharedProviderSource, source.Id),
            new(Guid.NewGuid(), Guid.NewGuid()), _ => authorized ? ValueTask.CompletedTask
                : ValueTask.FromException(new UnauthorizedAccessException("Original source actor was retired after SQL.")))) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => {
                if (delete) {
                    await owner.DeleteAsync(source.Id, source.ConcurrencyToken);
                } else {
                    await owner.UpdateAsync(source.Id, source.ConcurrencyToken,
                        new("Rejected source SQL", source.BaseUri, source.ApiTokenSecretId, true, false));
                }
            });
        }
        Assert.True(interceptor.Called);
        var current = await services.GetRequiredService<SharedProviderSourceService>().GetAsync(source.Id);
        Assert.Equal(source.Name, current.Name);
        Assert.Equal(source.ConcurrencyToken, current.ConcurrencyToken);
    }

    [Fact]
    public async Task Source_authority_from_another_dataset_cannot_publish() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CreateSourceAsync(services);
        var changes = services.GetRequiredService<ProviderEditingChanges>();
        using var admission = services.GetRequiredService<ProviderEditingWrites>().Enter(
            new(changes.Scope with { Id = Guid.NewGuid() }, EditingOwner.SharedProviderSource, source.Id), new(Guid.NewGuid(), Guid.NewGuid()), _ => ValueTask.CompletedTask);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => services.GetRequiredService<SharedProviderSourceService>()
            .UpdateAsync(source.Id, source.ConcurrencyToken, new("Foreign source must not save", source.BaseUri, source.ApiTokenSecretId, true, false)));
    }

    [Fact]
    public async Task Source_commit_publishes_catalog_metadata_even_without_imported_profiles() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CreateSourceAsync(services);
        var changes = services.GetRequiredService<ProviderEditingChanges>();
        var topic = new EditingTopic(changes.Scope, EditingOwner.SharedProviderSource, source.Id);
        using var subscription = services.GetRequiredService<EditingChangeFeed>().Subscribe(topic);
        var origin = new EditingOperationIdentity(Guid.NewGuid(), Guid.NewGuid());
        using (services.GetRequiredService<ProviderEditingWrites>().Enter(topic, origin, _ => ValueTask.CompletedTask)) {
            await services.GetRequiredService<SharedProviderSourceService>().UpdateAsync(source.Id, source.ConcurrencyToken,
                new("Metadata source update", source.BaseUri, source.ApiTokenSecretId, true, false));
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var hint = Assert.Single((await subscription.ReadAsync(timeout.Token)).Changes);
        Assert.Equal(EditingChangeKind.MetadataChanged, hint.Kind);
        Assert.Equal(topic, hint.Resource);
        Assert.Equal(origin.EditorId, hint.OriginEditorId);
    }

    [Fact]
    public async Task Source_save_returns_its_exact_native_accepted_values_and_version() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var source = await CreateSourceAsync(scope.ServiceProvider);
        var owner = scope.ServiceProvider.GetRequiredService<ISharedProviderManagementService>();
        var accepted = await owner.SaveSourceAsync(new(source.Id, source.ConcurrencyToken, "  Accepted source name  ",
            source.BaseUri, source.ApiTokenSecretId, true, false));
        Assert.NotNull(accepted.AcceptedSource);
        Assert.Equal(source.Id, accepted.AcceptedSource.Id);
        Assert.Equal("Accepted source name", accepted.AcceptedSource.Name);
        Assert.Equal(accepted.ConcurrencyToken, accepted.AcceptedSource.ConcurrencyToken);
        var peer = await owner.SaveSourceAsync(new(source.Id, accepted.ConcurrencyToken, "Later source peer", source.BaseUri,
            source.ApiTokenSecretId, true, false));
        Assert.NotEqual(peer.ConcurrencyToken, accepted.AcceptedSource.ConcurrencyToken);
        Assert.Equal("Accepted source name", accepted.AcceptedSource.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Management_refuses_a_missing_original_or_absence_claim_for_an_existing_source(bool absence) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var source = await CreateSourceAsync(scope.ServiceProvider);
        var request = new SharedProviderSourceEditorRequest(source.Id, absence ? Guid.Empty : null, source.Name,
            source.BaseUri, source.ApiTokenSecretId, source.IsEnabled, false);

        await Assert.ThrowsAsync<SharedProviderConcurrencyException>(() => scope.ServiceProvider
            .GetRequiredService<ISharedProviderManagementService>().SaveSourceAsync(request));

        Assert.Equal(source.ConcurrencyToken, (await scope.ServiceProvider.GetRequiredService<SharedProviderSourceService>().GetAsync(source.Id)).ConcurrencyToken);
    }

    [Fact]
    public async Task An_existing_source_with_matching_values_is_not_a_receipt_for_a_new_create() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var source = await CreateSourceAsync(scope.ServiceProvider);
        var request = new SharedProviderSourceWriteRequest(source.Name, source.BaseUri, source.ApiTokenSecretId,
            source.IsEnabled, false, source.Id);

        await Assert.ThrowsAsync<SharedProviderConcurrencyException>(() => scope.ServiceProvider.GetRequiredService<SharedProviderSourceService>().CreateAsync(request));
    }

    [Fact]
    public async Task A_peer_reaching_the_requested_source_values_does_not_establish_an_earlier_attempt_commit() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var source = await CreateSourceAsync(scope.ServiceProvider);
        var owner = scope.ServiceProvider.GetRequiredService<SharedProviderSourceService>();
        var request = new SharedProviderSourceEditorRequest(source.Id, source.ConcurrencyToken, "Peer reaches the target",
            source.BaseUri, source.ApiTokenSecretId, source.IsEnabled, false);
        var attempt = new SharedProviderSourceMutationAttempt(source.Id, SharedProviderSourceMutationKind.Update,
            new(source, []), request);
        await owner.UpdateAsync(source.Id, source.ConcurrencyToken, new(request.Name, request.BaseUri,
            request.ApiTokenSecretId, request.IsEnabled, request.AllowInsecurePrivateNetwork));
        var current = await owner.GetAsync(source.Id);

        var result = SharedProviderSourceVerification.Evaluate(attempt, [new(current, [])]);

        Assert.Equal(ProviderVerificationDisposition.StillUnconfirmed, result.Disposition);
        Assert.Null(result.Change);
    }

    [Fact]
    public async Task Catalog_reconciliation_cannot_omit_its_original_source_version() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var source = await CreateSourceAsync(scope.ServiceProvider);
        var initial = new SharedProviderCatalogDocument(SharedProviderProtocolVersion.Current,
            new(Guid.NewGuid()), new($"sha256:{new string('b', 64)}"), new(SharedProviderRoutes.OpenAiBase), []);
        var catalog = initial with { CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(initial) };
        var request = new SharedProviderReconciliationRequest(source.Id, catalog,
            SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision), new HashSet<SharedProviderPublicationId>());

        await Assert.ThrowsAsync<SharedProviderConcurrencyException>(() => scope.ServiceProvider
            .GetRequiredService<SharedProviderReconciliationCoordinator>().ReconcileAsync(request));

        Assert.Equal(source.ConcurrencyToken, (await scope.ServiceProvider.GetRequiredService<SharedProviderSourceService>().GetAsync(source.Id)).ConcurrencyToken);
    }

    [Fact]
    public async Task A_source_failure_without_its_original_cannot_change_current_availability() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var source = await CreateSourceAsync(scope.ServiceProvider);
        var owner = scope.ServiceProvider.GetRequiredService<SharedProviderSourceService>();

        await Assert.ThrowsAsync<SharedProviderConcurrencyException>(() => owner.RecordFailureAsync(source.Id,
            new(SharedProviderSourceFailureKind.Connectivity, null, "An earlier connection failed.")));

        Assert.Equal(source.ConcurrencyToken, (await owner.GetAsync(source.Id)).ConcurrencyToken);
    }

    private static async Task<SharedProviderImportedProfileSnapshot> CreateImportAsync(IServiceProvider services) {
        var source = await CreateSourceAsync(services);
        var id = new SharedProviderPublicationId(Guid.NewGuid());
        var model = SharedProviderRoutingModelIdCodec.Create(id, "owned-import-model");
        var publication = new SharedProviderCatalogPublication(id, new($"sha256:{new string('a', 64)}"), "Owned imported provider",
            SharedProviderPurpose.Chat, SharedProviderTransport.OpenAiCompatible, model,
            [new(model, "Owned import model", [SharedProviderCapability.Responses])], new(SharedProviderHealthState.Available));
        publication = publication with { Revision = SharedProviderCanonicalRevision.ComputePublication(publication) };
        var catalog = new SharedProviderCatalogDocument(SharedProviderProtocolVersion.Current, new(Guid.NewGuid()),
            new($"sha256:{new string('b', 64)}"), new(SharedProviderRoutes.OpenAiBase), [publication]);
        catalog = catalog with { CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(catalog) };
        await services.GetRequiredService<SharedProviderReconciliationCoordinator>().ReconcileAsync(new(source.Id, catalog,
            SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision), new HashSet<SharedProviderPublicationId> { id },
            expectedSourceConcurrencyToken: source.ConcurrencyToken, originalSelection: new(source.ConcurrencyToken, [])));
        return (await services.GetRequiredService<ISharedProviderManagementService>().ListSourcesAsync())
            .Single(item => item.Source.Id == source.Id).Imports.Single();
    }

    private sealed class AfterImportSaved(Func<Guid, Task> saved) : IProviderProfileCommitObserver {
        public Exception? Failure { get; private set; }
        public async Task ProviderSavedAsync(Guid providerId, CancellationToken cancellationToken = default) {
            try {
                await saved(providerId);
            } catch (Exception exception) {
                Failure = exception;
                throw;
            }
        }
        public Task ProviderDeletedAsync(Guid providerId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AfterPublication(Func<Task> saved) : ISharedProviderPublicationCommitObserver {
        public Exception? Failure { get; private set; }
        public async Task PublicationChangedAsync(Guid providerId, CancellationToken cancellationToken = default) {
            try {
                await saved();
            } catch (Exception exception) {
                Failure = exception;
                throw;
            }
        }
    }

    private static async Task<SharedProviderSourceSnapshot> CreateSourceAsync(IServiceProvider services) {
        await using var db = await services.GetRequiredService<IDbContextFactory<SecurityDbContext>>().CreateDbContextAsync();
        var secret = new SecretRecord {
            Name = "Owned concurrent source credential", Kind = SecretKind.Token, Scope = "workspace", MetadataJson = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Set<SecretRecord>().Add(secret);
        await db.SaveChangesAsync();
        var owner = services.GetRequiredService<SharedProviderSourceService>();
        var result = await owner.CreateAsync(new("Owned concurrent source", new("https://source.example.test/"), secret.Id, true, false));
        return await owner.GetAsync(result.Id);
    }

    private sealed class SourceFactory(DbContextOptions<ProvidersDbContext> options) : IDbContextFactory<ProvidersDbContext> {
        public ProvidersDbContext CreateDbContext() => new(options);
        public Task<ProvidersDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ProvidersDbContext(options));
    }

    private sealed class AfterSql(Action retire) : SaveChangesInterceptor {
        public bool Called { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            Called = true;
            retire();
            return ValueTask.FromResult(result);
        }
    }
}
