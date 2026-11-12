using System.Reflection;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.ExceptionServices;
using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.Extensions.DependencyInjection;
using Editor = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using Entity = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;
using RuntimeKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using AdministrationEditor = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfileEditorModel;

namespace CanDoItAll.Tests.Integration.SharedProviders;

public sealed class ProviderMutationCommitIntegrationTests(SharedProviderRuntimeProjectionFixture fixture)
    : IClassFixture<SharedProviderRuntimeProjectionFixture> {
    [Fact]
    public async Task An_unsigned_client_token_claim_never_establishes_a_committed_mutation() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var current = await registry.GetProviderEditorAsync(id);
        var manufactured = new ProviderMutationAttempt(Guid.NewGuid(), id, ProviderMutationKind.Update,
            Guid.NewGuid(), current.ExpectedConcurrencyToken);

        var result = await services.GetRequiredService<IProviderMutationVerification>().VerifyAsync(manufactured);

        Assert.Equal(ProviderVerificationDisposition.StillUnconfirmed, result.Disposition);
        Assert.Null(result.ConcurrencyToken);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task A_secondary_failure_does_not_hide_the_commit_or_skip_later_observers(bool administration, bool delete) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var canonical = services.GetRequiredService<IProviderProfileRegistry>();
        var id = await canonical.SaveProviderAsync(NewEditor());
        var trailing = new FailingObserver();
        var observers = new IProviderProfileCommitObserver[] { new FailingObserver { Fail = true } }
            .Concat(services.GetServices<IProviderProfileCommitObserver>()).Append(trailing).ToArray();
        var topic = new EditingTopic(services.GetRequiredService<ProviderEditingChanges>().Scope, EditingOwner.ProviderProfile, id);
        using var subscription = services.GetRequiredService<EditingChangeFeed>().Subscribe(topic);
        var failure = await Assert.ThrowsAnyAsync<ProviderMutationCommittedException>(async () => {
            if (administration) {
                var owner = ActivatorUtilities.CreateInstance<ProviderAdministrationService>(services, (IEnumerable<IProviderProfileCommitObserver>)observers);
                var editor = await owner.GetProviderAsync(id);
                if (delete) {
                    await owner.DeleteProviderAsync(id, editor.ExpectedConcurrencyToken!.Value);
                } else {
                    editor.Name = "Administration committed despite observer";
                    await owner.SaveProviderAsync(editor);
                }
            } else {
                var owner = ActivatorUtilities.CreateInstance<DatabaseProviderProfileRegistry>(services, (IEnumerable<IProviderProfileCommitObserver>)observers);
                var editor = await owner.GetProviderEditorAsync(id);
                if (delete) {
                    await owner.DeleteProviderAsync(id, editor.ExpectedConcurrencyToken!.Value);
                } else {
                    editor.Name = "Runtime committed despite observer";
                    await owner.SaveProviderAsync(editor);
                }
            }
        });
        Assert.Equal(id, failure.ProviderId);
        Assert.True(failure.CanonicalCommitSucceeded);
        Assert.Equal(1, trailing.Calls);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var hint = Assert.Single((await subscription.ReadAsync(timeout.Token)).Changes);
        Assert.Equal(delete ? EditingChangeKind.Deleted : EditingChangeKind.EditableContentChanged, hint.Kind);
        Assert.Equal(delete, await canonical.GetProviderAsync(id) is null);
    }

    [Fact]
    public async Task Model_maintenance_preserves_the_peer_configuration_and_reports_known_remote_completion() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var completed = new ProviderModelMaintenanceEditorResult("created-model", "seam-model", "Owned instruction", 4096, "Owned modelfile", "Created remotely");
        var diagnostics = DispatchProxy.Create<IProviderDiagnosticsService, DiagnosticProxy>();
        var proxy = (DiagnosticProxy)(object)diagnostics;
        proxy.Maintenance = async _ => {
            var peer = await registry.GetProviderEditorAsync(id);
            peer.Name = "Peer changed during remote model creation";
            peer.DefaultModel = "peer-model";
            peer.SuggestedModels = ["peer-model"];
            await registry.SaveProviderAsync(peer);
            return completed;
        };
        var runtime = ActivatorUtilities.CreateInstance<ProviderRuntimeAdministrationService>(services, diagnostics);

        var failure = await Assert.ThrowsAsync<ProviderModelMaintenancePersistenceException>(() => runtime.CreateOrUpdateProviderModelAsync(id,
            new("seam-model", "created-model", "Owned instruction", 4096)));

        Assert.Equal(ProviderModelMaintenancePersistenceState.Conflict, failure.State);
        Assert.Same(completed, failure.Result);
        Assert.Equal(id, failure.ProviderId);
        var current = await registry.GetProviderEditorAsync(id);
        Assert.Equal("Peer changed during remote model creation", current.Name);
        Assert.Equal("peer-model", current.DefaultModel);
        Assert.DoesNotContain("created-model", current.SuggestedModels);
        Assert.Equal(1, proxy.MaintenanceCalls);
    }

    [Fact]
    public async Task Model_maintenance_updates_the_original_configuration_once_after_remote_completion() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<IProviderProfileRegistry>();
        var draft = NewEditor();
        draft.ConfigurationJson = "{\"ownedExtension\":\"preserved\"}";
        var id = await registry.SaveProviderAsync(draft);
        var original = await registry.GetProviderEditorAsync(id);
        var completed = new ProviderModelMaintenanceEditorResult("created-model", "seam-model", "Owned instruction", 4096, "Owned modelfile", "Created remotely");
        var diagnostics = DispatchProxy.Create<IProviderDiagnosticsService, DiagnosticProxy>();
        var proxy = (DiagnosticProxy)(object)diagnostics;
        proxy.Maintenance = _ => Task.FromResult(completed);
        var runtime = ActivatorUtilities.CreateInstance<ProviderRuntimeAdministrationService>(services, diagnostics);

        Assert.Same(completed, await runtime.CreateOrUpdateProviderModelAsync(id, new("seam-model", "created-model", "Owned instruction", 4096)));

        var current = await registry.GetProviderEditorAsync(id);
        Assert.Equal("created-model", current.DefaultModel);
        Assert.Contains("created-model", current.SuggestedModels);
        Assert.Contains("preserved", current.ConfigurationJson, StringComparison.Ordinal);
        Assert.NotEqual(original.ExpectedConcurrencyToken, current.ExpectedConcurrencyToken);
        Assert.Equal(1, proxy.MaintenanceCalls);
    }

    [Fact]
    public async Task A_health_probe_keeps_the_editable_version_and_does_not_store_discovered_models() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await registry.GetProviderEditorAsync(id);
        var diagnostics = DispatchProxy.Create<IProviderDiagnosticsService, DiagnosticProxy>();
        ((DiagnosticProxy)(object)diagnostics).Probe = _ => Task.FromResult(new ProviderHealthResult(true, "Diagnostic complete", ["probe-only-model"]));
        var runtime = ActivatorUtilities.CreateInstance<ProviderRuntimeAdministrationService>(services, diagnostics);
        var topic = new EditingTopic(services.GetRequiredService<ProviderEditingChanges>().Scope, EditingOwner.ProviderProfile, id);
        using var subscription = services.GetRequiredService<EditingChangeFeed>().Subscribe(topic);

        var result = await runtime.TestProviderAsync(id);

        Assert.Equal(["probe-only-model"], result.SuggestedModels);
        var current = await registry.GetProviderEditorAsync(id);
        Assert.Equal(original.ExpectedConcurrencyToken, current.ExpectedConcurrencyToken);
        Assert.Equal(original.SuggestedModels, current.SuggestedModels);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        var persisted = await db.Set<Entity>().SingleAsync(provider => provider.Id == id);
        Assert.NotNull(persisted.LastHealthCheckAtUtc);
        Assert.Equal("Healthy", persisted.LastHealthStatus, ignoreCase: true);
        Assert.Equal(1, ((DiagnosticProxy)(object)diagnostics).Calls);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(EditingChangeKind.MetadataChanged, Assert.Single((await subscription.ReadAsync(timeout.Token)).Changes).Kind);
        var runtimeProfile = await services.GetRequiredService<IProviderRuntimeProfileSource>().GetProviderAsync(id);
        Assert.Equal("Healthy", runtimeProfile!.HealthStatus, ignoreCase: true);
    }

    [Fact]
    public async Task Health_metadata_rolls_back_when_the_original_actor_is_retired_after_SQL() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await registry.GetProviderEditorAsync(id);
        var allowed = true;
        var interceptor = new AfterProviderSql(() => allowed = false);
        var factory = new InterceptingFactory(new DbContextOptionsBuilder<ProvidersDbContext>(
            services.GetRequiredService<DbContextOptions<ProvidersDbContext>>()).AddInterceptors(interceptor).Options);
        var persistence = ActivatorUtilities.CreateInstance<ProviderDiagnosticPersistence>(services, factory);
        var diagnostics = DispatchProxy.Create<IProviderDiagnosticsService, DiagnosticProxy>();
        ((DiagnosticProxy)(object)diagnostics).Probe = _ => Task.FromResult(new ProviderHealthResult(true, "Diagnostic complete", []));
        IProviderProfileRegistry diagnosticRegistry = ActivatorUtilities.CreateInstance<DatabaseProviderProfileRegistry>(services, persistence);
        var runtime = ActivatorUtilities.CreateInstance<ProviderRuntimeAdministrationService>(services, diagnosticRegistry, diagnostics);
        var topic = new EditingTopic(services.GetRequiredService<ProviderEditingChanges>().Scope, EditingOwner.ProviderProfile, id);
        using (services.GetRequiredService<ProviderEditingWrites>().Enter(topic, new(Guid.NewGuid(), Guid.NewGuid()), _ => {
            if (!allowed) {
                throw new UnauthorizedAccessException("Original health editor retired after SQL.");
            }
            return ValueTask.CompletedTask;
        })) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => runtime.TestProviderAsync(id));
        }
        Assert.True(interceptor.Called);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        var current = await db.Set<Entity>().SingleAsync(provider => provider.Id == id);
        Assert.Null(current.LastHealthCheckAtUtc);
        Assert.Equal(original.ExpectedConcurrencyToken, current.ConcurrencyToken);
        Assert.Equal(1, ((DiagnosticProxy)(object)diagnostics).Calls);
    }

    [Fact]
    public async Task An_ambiguous_health_commit_cannot_be_verified_as_absent_from_an_unchanged_configuration_token() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await registry.GetProviderEditorAsync(id);
        var interceptor = new AfterHealthCommit();
        var factory = new InterceptingFactory(new DbContextOptionsBuilder<ProvidersDbContext>(
            services.GetRequiredService<DbContextOptions<ProvidersDbContext>>()).AddInterceptors(interceptor).Options);
        var persistence = ActivatorUtilities.CreateInstance<ProviderDiagnosticPersistence>(services, factory);
        var diagnostics = DispatchProxy.Create<IProviderDiagnosticsService, DiagnosticProxy>();
        ((DiagnosticProxy)(object)diagnostics).Probe = _ => Task.FromResult(new ProviderHealthResult(true, "Diagnostic complete", []));
        IProviderProfileRegistry diagnosticRegistry = ActivatorUtilities.CreateInstance<DatabaseProviderProfileRegistry>(services, persistence);
        var runtime = ActivatorUtilities.CreateInstance<ProviderRuntimeAdministrationService>(services, diagnosticRegistry, diagnostics);

        var failure = await Assert.ThrowsAsync<ProviderMutationUnconfirmedException>(() => runtime.TestProviderAsync(id));

        Assert.True(interceptor.Called);
        Assert.Equal(ProviderMutationKind.HealthPersistence, failure.Attempt.Kind);
        var verification = await services.GetRequiredService<IProviderMutationVerification>().VerifyAsync(failure.Attempt);
        Assert.Equal(ProviderVerificationDisposition.StillUnconfirmed, verification.Disposition);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        var current = await db.Set<Entity>().SingleAsync(provider => provider.Id == id);
        Assert.NotNull(current.LastHealthCheckAtUtc);
        Assert.Equal(original.ExpectedConcurrencyToken, current.ConcurrencyToken);
        Assert.Equal(1, ((DiagnosticProxy)(object)diagnostics).Calls);
    }

    [Fact]
    public async Task A_held_health_probe_cannot_update_a_peer_configuration_and_never_repeats_the_probe() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var diagnostics = DispatchProxy.Create<IProviderDiagnosticsService, DiagnosticProxy>();
        ((DiagnosticProxy)(object)diagnostics).Probe = async _ => {
            var peer = await registry.GetProviderEditorAsync(id);
            peer.Name = "Peer provider while health request was running";
            peer.BaseUrl = "http://127.0.0.1:11435";
            peer.SuggestedModels = ["seam-model", "peer-model"];
            await registry.SaveProviderAsync(peer);
            return new(true, "Old endpoint diagnostic", ["old-probe-model"]);
        };
        var runtime = ActivatorUtilities.CreateInstance<ProviderRuntimeAdministrationService>(services, diagnostics);

        await Assert.ThrowsAsync<ProviderProfileConcurrencyException>(() => runtime.TestProviderAsync(id));

        var current = await registry.GetProviderEditorAsync(id);
        Assert.Equal("Peer provider while health request was running", current.Name);
        Assert.Equal("http://127.0.0.1:11435", current.BaseUrl);
        Assert.Contains("peer-model", current.SuggestedModels);
        Assert.DoesNotContain("old-probe-model", current.SuggestedModels);
        Assert.Equal(1, ((DiagnosticProxy)(object)diagnostics).Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Original_provider_authority_is_rechecked_after_SQL_and_rolls_back(bool delete) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var canonical = services.GetRequiredService<IProviderProfileRegistry>();
        var id = await canonical.SaveProviderAsync(NewEditor());
        var original = await canonical.GetProviderEditorAsync(id);
        var authorized = true;
        var interceptor = new AfterProviderSql(() => authorized = false);
        var factory = new InterceptingFactory(new DbContextOptionsBuilder<ProvidersDbContext>(
            services.GetRequiredService<DbContextOptions<ProvidersDbContext>>()).AddInterceptors(interceptor).Options);
        IProviderProfileRegistry registry = ActivatorUtilities.CreateInstance<DatabaseProviderProfileRegistry>(services, factory);
        var changes = services.GetRequiredService<ProviderEditingChanges>();
        var writes = services.GetRequiredService<ProviderEditingWrites>();
        using (writes.Enter(new(changes.Scope, EditingOwner.ProviderProfile, id), new(Guid.NewGuid(), Guid.NewGuid()), _ => {
            if (!authorized) {
                throw new UnauthorizedAccessException("The original actor was retired after SQL.");
            }
            return ValueTask.CompletedTask;
        })) {
            original.Name = "Must roll back provider SQL";
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => delete
                ? registry.DeleteProviderAsync(id, original.ExpectedConcurrencyToken!.Value)
                : registry.SaveProviderAsync(original));
        }
        Assert.True(interceptor.Called);
        var current = await canonical.GetProviderEditorAsync(id);
        Assert.NotEqual(original.Name, current.Name);
        Assert.Equal(original.ExpectedConcurrencyToken, current.ExpectedConcurrencyToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Administration_original_authority_is_rechecked_after_SQL_and_rolls_back(bool delete) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var canonical = services.GetRequiredService<IProviderProfileRegistry>();
        var id = await canonical.SaveProviderAsync(NewEditor());
        var authorized = true;
        var interceptor = new AfterProviderSql(() => authorized = false);
        var factory = new InterceptingFactory(new DbContextOptionsBuilder<ProvidersDbContext>(
            services.GetRequiredService<DbContextOptions<ProvidersDbContext>>()).AddInterceptors(interceptor).Options);
        var administration = ActivatorUtilities.CreateInstance<ProviderAdministrationService>(services, factory);
        var original = await administration.GetProviderAsync(id);
        var changes = services.GetRequiredService<ProviderEditingChanges>();
        using (services.GetRequiredService<ProviderEditingWrites>().Enter(new(changes.Scope, EditingOwner.ProviderProfile, id),
            new(Guid.NewGuid(), Guid.NewGuid()), _ => authorized ? ValueTask.CompletedTask
                : ValueTask.FromException(new UnauthorizedAccessException("Original administration actor was retired after SQL.")))) {
            original.Name = "Must roll back administration SQL";
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => {
                if (delete) {
                    await administration.DeleteProviderAsync(id, original.ExpectedConcurrencyToken!.Value);
                } else {
                    await administration.SaveProviderAsync(original);
                }
            });
        }
        Assert.True(interceptor.Called);
        var current = await canonical.GetProviderEditorAsync(id);
        Assert.NotEqual(original.Name, current.Name);
        Assert.Equal(original.ExpectedConcurrencyToken, current.ExpectedConcurrencyToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Provider_native_admission_refuses_another_dataset_or_resource(bool wrongDataset) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await registry.GetProviderEditorAsync(id);
        var changes = services.GetRequiredService<ProviderEditingChanges>();
        var topic = new EditingTopic(wrongDataset ? changes.Scope with { Id = Guid.NewGuid() } : changes.Scope,
            EditingOwner.ProviderProfile, wrongDataset ? id : Guid.NewGuid());
        using var admission = services.GetRequiredService<ProviderEditingWrites>().Enter(topic,
            new(Guid.NewGuid(), Guid.NewGuid()), _ => ValueTask.CompletedTask);
        original.Name = "Must refuse foreign provider admission";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => registry.SaveProviderAsync(original));
        Assert.NotEqual(original.Name, (await registry.GetProviderEditorAsync(id)).Name);
    }

    [Fact]
    public async Task Provider_commit_publishes_targeted_metadata_with_original_editor() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await registry.GetProviderEditorAsync(id);
        var changes = services.GetRequiredService<ProviderEditingChanges>();
        var topic = new EditingTopic(changes.Scope, EditingOwner.ProviderProfile, id);
        using var subscription = services.GetRequiredService<EditingChangeFeed>().Subscribe(topic);
        var origin = new EditingOperationIdentity(Guid.NewGuid(), Guid.NewGuid());
        using (services.GetRequiredService<ProviderEditingWrites>().Enter(topic, origin, _ => ValueTask.CompletedTask)) {
            original.Name += " metadata changed";
            await registry.SaveProviderAsync(original);
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var batch = await subscription.ReadAsync(timeout.Token);
        var hint = Assert.Single(batch.Changes);
        Assert.Equal(topic, hint.Resource);
        Assert.Equal(origin.EditorId, hint.OriginEditorId);
        Assert.Equal(origin.OperationId, hint.OperationId);
        Assert.Equal(EditingChangeKind.EditableContentChanged, hint.Kind);
        Assert.False(batch.RequiresResync);
    }

    [Fact]
    public async Task Exact_accepted_configuration_precedes_a_peer_commit_from_a_secondary_observer() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var canonical = services.GetRequiredService<IProviderProfileRegistry>();
        var peerWriter = new PeerCommitObserver(async id => {
            var peer = await canonical.GetProviderEditorAsync(id);
            peer.Name = "Later peer after canonical acceptance";
            await canonical.SaveProviderAsync(peer);
        });
        var observers = services.GetServices<IProviderProfileCommitObserver>().Append(peerWriter).ToArray();
        IProviderProfileRegistry registry = ActivatorUtilities.CreateInstance<DatabaseProviderProfileRegistry>(
            services, (IEnumerable<IProviderProfileCommitObserver>)observers);
        var request = NewEditor();
        request.Name = "  Exact accepted provider 東京  ";
        var accepted = await registry.SaveProviderEditorAsync(request);
        Assert.Equal("Exact accepted provider 東京", accepted.Name);
        Assert.NotNull(accepted.Id);
        Assert.NotNull(accepted.ExpectedConcurrencyToken);
        var current = await canonical.GetProviderEditorAsync(accepted.Id);
        Assert.Equal("Later peer after canonical acceptance", current.Name);
        Assert.NotEqual(accepted.ExpectedConcurrencyToken, current.ExpectedConcurrencyToken);
        Assert.Equal(1, peerWriter.Calls);
        Assert.NotEqual(request.ConfigurationJson, accepted.ConfigurationJson);
        var projection = await services.GetRequiredService<ISandboxWorkspaceStore>().LoadCatalogAsync();
        Assert.Equal(current.Name, projection.Providers.Single(provider => provider.Id == accepted.Id).Name);
    }

    [Fact]
    public async Task Administration_original_can_save_once_and_only_the_reviewed_head_can_be_deleted() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var administration = scope.ServiceProvider.GetRequiredService<IProviderAdministrationService>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await administration.GetProviderAsync(id);
        Assert.NotNull(original.ExpectedConcurrencyToken);
        original.Name += " administration winner";
        var saved = await administration.SaveProviderAsync(original);
        Assert.True(saved.IsSuccess);
        await Assert.ThrowsAsync<ProviderProfileConcurrencyException>(() => administration.SaveProviderAsync(original));
        await Assert.ThrowsAsync<ProviderProfileConcurrencyException>(() => administration.DeleteProviderAsync(id, original.ExpectedConcurrencyToken!.Value));
        var reviewed = await administration.GetProviderAsync(id);
        Assert.Equal(original.Name, reviewed.Name);
        await administration.DeleteProviderAsync(id, reviewed.ExpectedConcurrencyToken!.Value);
        Assert.Null(await registry.GetProviderAsync(id));
    }

    [Fact]
    public async Task Administration_missing_target_read_is_explicit_and_cannot_become_a_new_editor() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var administration = scope.ServiceProvider.GetRequiredService<IProviderAdministrationService>();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => administration.GetProviderAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Administration_replacement_cannot_omit_the_original_and_erase_a_peer_commit() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var administration = scope.ServiceProvider.GetRequiredService<IProviderAdministrationService>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await administration.GetProviderAsync(id);
        var peer = await registry.GetProviderEditorAsync(id);
        peer.Name += " administration peer";
        await registry.SaveProviderAsync(peer);
        var tokenless = new AdministrationEditor {
            Id = id, Name = "Rejected alternate replacement", ConnectorPluginKey = original.ConnectorPluginKey,
            ConfigSchemaVersion = original.ConfigSchemaVersion, Configuration = original.Configuration,
            ApiKeySecretId = original.ApiKeySecretId, IsEnabled = original.IsEnabled, ModelPrices = original.ModelPrices
        };
        await Assert.ThrowsAsync<ProviderProfileConcurrencyException>(() => administration.SaveProviderAsync(tokenless));
        Assert.Equal(peer.Name, (await registry.GetProviderEditorAsync(id)).Name);
    }

    [Fact]
    public async Task Administration_identity_only_delete_cannot_remove_an_existing_provider() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var administration = scope.ServiceProvider.GetRequiredService<IProviderAdministrationService>();
        var id = await registry.SaveProviderAsync(NewEditor());
        await Assert.ThrowsAsync<ProviderProfileConcurrencyException>(() => administration.DeleteProviderAsync(id));
        Assert.NotNull(await registry.GetProviderAsync(id));
    }

    [Fact]
    public async Task A_deleted_editor_with_no_original_cannot_be_recreated_as_a_create_command() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await registry.GetProviderEditorAsync(id);
        await registry.DeleteProviderAsync(id, original.ExpectedConcurrencyToken!.Value);
        original.ExpectedConcurrencyToken = null;
        await Assert.ThrowsAsync<ProviderProfileConcurrencyException>(() => registry.SaveProviderAsync(original));
        Assert.Null(await registry.GetProviderAsync(id));
    }

    [Theory]
    [InlineData(ProviderMutationKind.Create, false)]
    [InlineData(ProviderMutationKind.Create, true)]
    [InlineData(ProviderMutationKind.Delete, false)]
    public async Task Existence_or_absence_without_an_attempt_witness_cannot_confirm_historical_publication(ProviderMutationKind kind, bool present) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = Guid.NewGuid();
        Guid? expected = Guid.Empty;
        if (present || kind == ProviderMutationKind.Delete) {
            var peer = NewEditor();
            peer.Id = id;
            peer.ExpectedConcurrencyToken = Guid.Empty;
            await registry.SaveProviderAsync(peer);
            var current = await registry.GetProviderEditorAsync(id);
            expected = kind == ProviderMutationKind.Delete ? current.ExpectedConcurrencyToken : Guid.Empty;
            if (!present) {
                await registry.DeleteProviderAsync(id, current.ExpectedConcurrencyToken!.Value);
            }
        }
        var unknown = new ProviderMutationAttempt(Guid.NewGuid(), id, kind, expected);
        var outcome = await scope.ServiceProvider.GetRequiredService<IProviderMutationVerification>().VerifyAsync(unknown);
        Assert.Equal(ProviderVerificationDisposition.StillUnconfirmed, outcome.Disposition);
        Assert.Equal(id, outcome.ProviderId);
        Assert.Equal(present, await registry.GetProviderAsync(id) is not null);
    }

    [Fact]
    public async Task Conditional_delete_keeps_a_peer_update_and_accepts_only_the_reviewed_version() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await registry.GetProviderEditorAsync(id);
        var peer = await registry.GetProviderEditorAsync(id);
        peer.Name += " peer";
        await registry.SaveProviderAsync(peer);
        await Assert.ThrowsAsync<ProviderProfileConcurrencyException>(() => registry.DeleteProviderAsync(id, original.ExpectedConcurrencyToken!.Value));
        var reviewed = await registry.GetProviderEditorAsync(id);
        Assert.Equal(peer.Name, reviewed.Name);
        await registry.DeleteProviderAsync(id, reviewed.ExpectedConcurrencyToken!.Value);
        Assert.Null(await registry.GetProviderAsync(id));
    }

    [Fact]
    public async Task HTTP_delete_requires_the_original_query_token_and_preserves_a_changed_profile() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await registry.GetProviderEditorAsync(id);
        using var missing = await fixture.Client.DeleteAsync($"/api/agents/providers/{id:D}");
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        var peer = await registry.GetProviderEditorAsync(id);
        peer.Name += " HTTP peer";
        await registry.SaveProviderAsync(peer);
        using var stale = await fixture.Client.DeleteAsync($"/api/agents/providers/{id:D}?expectedConcurrencyToken={original.ExpectedConcurrencyToken:D}");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var reviewed = await registry.GetProviderEditorAsync(id);
        Assert.Equal(peer.Name, reviewed.Name);
        using var accepted = await fixture.Client.DeleteAsync($"/api/agents/providers/{id:D}?expectedConcurrencyToken={reviewed.ExpectedConcurrencyToken:D}");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Null(await registry.GetProviderAsync(id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_provider_requires_its_original_token_and_keeps_the_winning_configuration(bool emptyToken) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var winner = await registry.GetProviderEditorAsync(id);
        winner.Name += " winner";
        await registry.SaveProviderAsync(winner);
        var current = await registry.GetProviderEditorAsync(id);
        var request = await registry.GetProviderEditorAsync(id);
        request.ExpectedConcurrencyToken = emptyToken ? Guid.Empty : null;
        request.Name = "Rejected tokenless replacement";
        await Assert.ThrowsAsync<ProviderProfileConcurrencyException>(() => registry.SaveProviderAsync(request));
        var retained = await registry.GetProviderEditorAsync(id);
        Assert.Equal(current.Name, retained.Name);
        Assert.Equal(current.ExpectedConcurrencyToken, retained.ExpectedConcurrencyToken);
    }

    [Fact]
    public async Task Identity_only_delete_cannot_remove_an_existing_provider() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await registry.GetProviderEditorAsync(id);
        await Assert.ThrowsAsync<ProviderProfileConcurrencyException>(() => registry.DeleteProviderAsync(id));
        Assert.Equal(original.ExpectedConcurrencyToken, (await registry.GetProviderEditorAsync(id)).ExpectedConcurrencyToken);
    }

    [Fact]
    public async Task Tokenless_HTTP_replacement_returns_conflict_without_publishing_its_fields() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var original = await registry.GetProviderEditorAsync(id);
        var request = await registry.GetProviderEditorAsync(id);
        request.ExpectedConcurrencyToken = null;
        request.Name = "Rejected HTTP tokenless provider";
        using var response = await fixture.Client.PostAsJsonAsync("/api/agents/providers", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("agents.provider-concurrency-conflict", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(original.Name, (await registry.GetProviderEditorAsync(id)).Name);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Canonical_commit_survives_secondary_failure_and_repair_never_replays_write(bool observerFailure, bool delete) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var observer = new FailingObserver();
        var store = DispatchProxy.Create<ISandboxWorkspaceStore, StoreProxy>();
        var projection = (StoreProxy)(object)store;
        projection.Inner = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceStore>();
        var observers = scope.ServiceProvider.GetServices<IProviderProfileCommitObserver>().Append(observer).ToArray();
        var registry = ActivatorUtilities.CreateInstance<DatabaseProviderProfileRegistry>(
            scope.ServiceProvider, store, (IEnumerable<IProviderProfileCommitObserver>)observers);
        var model = NewEditor();
        if (delete) {
            model.Id = await registry.SaveProviderAsync(model);
            model = await registry.GetProviderEditorAsync(model.Id.Value);
        }
        observer.Fail = observerFailure;
        projection.Fail = !observerFailure;
        var exception = await Assert.ThrowsAnyAsync<ProviderMutationCommittedException>(async () => {
            if (delete) {
                await registry.DeleteProviderAsync(model.Id!.Value, model.ExpectedConcurrencyToken!.Value);
            } else {
                await registry.SaveProviderAsync(model);
            }
        });
        Assert.True(exception.CanonicalCommitSucceeded);
        Assert.NotEqual(Guid.Empty, exception.ProviderId);
        Assert.Equal(delete ? ProviderCatalogProjectionOperationKind.Delete : ProviderCatalogProjectionOperationKind.Upsert,
            exception.OperationKind);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        var row = await db.Set<Entity>().AsNoTracking().SingleOrDefaultAsync(item => item.Id == exception.ProviderId);
        Assert.Equal(delete, row is null);
        var token = row?.ConcurrencyToken;
        var count = await db.Set<Entity>().CountAsync(item => item.Name == model.Name);
        observer.Fail = false;
        projection.Fail = false;
        await registry.ReconcileAsync(exception.ProviderId);
        Assert.Equal(count, await db.Set<Entity>().CountAsync(item => item.Name == model.Name));
        Assert.Equal(token, (await db.Set<Entity>().AsNoTracking().SingleOrDefaultAsync(item => item.Id == exception.ProviderId))?.ConcurrencyToken);
        Assert.Equal(delete, await registry.GetProviderAsync(exception.ProviderId) is null);
    }

    [Fact]
    public async Task Validation_rejection_is_precommit_and_correction_can_be_saved() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var model = NewEditor();
        model.BaseUrl = "not a URI";
        await Assert.ThrowsAsync<ProviderProfileValidationException>(() => registry.SaveProviderAsync(model));
        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.False(await db.Set<Entity>().AnyAsync(item => item.Name == model.Name));
        model.BaseUrl = "http://127.0.0.1:11434";
        var id = await registry.SaveProviderAsync(model);
        Assert.True(await db.Set<Entity>().AnyAsync(item => item.Id == id));
    }

    [Fact]
    public async Task Owner_precancellation_does_not_create_a_provider() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var model = NewEditor();
        using var owner = new CancellationTokenSource();
        owner.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registry.SaveProviderAsync(model, owner.Token));
        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.False(await db.Set<Entity>().AnyAsync(item => item.Name == model.Name));
    }

    [Fact]
    public async Task Editor_concurrency_token_rejects_a_stale_write_without_overwriting_the_winner() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var first = await registry.GetProviderEditorAsync(id);
        var stale = await registry.GetProviderEditorAsync(id);
        Assert.NotNull(first.ExpectedConcurrencyToken);
        first.Name += " winner";
        await registry.SaveProviderAsync(first);
        stale.Name += " stale";
        await Assert.ThrowsAsync<ProviderProfileConcurrencyException>(() => registry.SaveProviderAsync(stale));
        Assert.Equal(first.Name, (await registry.GetProviderEditorAsync(id)).Name);
    }

    [Fact]
    public async Task Opening_sharing_without_publishing_does_not_create_identity_or_block_delete() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var sharing = await scope.ServiceProvider.GetRequiredService<ISharedProviderManagementService>().GetProfileSharingAsync(id);
        Assert.Null(sharing.Publication);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.False(await db.Set<ProviderSharePublication>().AnyAsync(item => item.ProviderProfileId == id));
        var original = await registry.GetProviderEditorAsync(id);
        await registry.DeleteProviderAsync(id, original.ExpectedConcurrencyToken!.Value);
        Assert.Null(await registry.GetProviderAsync(id));
    }

    [Fact]
    public async Task Seam_publication_identity_is_explicit_permanent_and_retained_after_unpublish() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var sharing = scope.ServiceProvider.GetRequiredService<ISharedProviderManagementService>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var read = await sharing.GetProfileSharingAsync(id);
        Assert.Null(read.Publication);
        Assert.True(read.Eligibility!.IsEligible, read.Eligibility.SanitizedReason);
        var published = await sharing.SetPublicationAsync(id, SharedProviderPublicationAction.Publish, null, read.ProviderConcurrencyToken!.Value);
        Assert.True(published.Publication!.IsPublished);
        await Assert.ThrowsAsync<SharedProviderProfileDeletionBlockedException>(() => registry.DeleteProviderAsync(id));
        var unpublished = await sharing.SetPublicationAsync(id, SharedProviderPublicationAction.Unpublish,
            published.Publication.ConcurrencyToken, published.ProviderConcurrencyToken!.Value);
        Assert.False(unpublished.Publication!.IsPublished);
        Assert.Equal(published.Publication.PublicId, unpublished.Publication.PublicId);
        await Assert.ThrowsAsync<SharedProviderProfileDeletionBlockedException>(() => registry.DeleteProviderAsync(id));
        var response = await fixture.Client.DeleteAsync($"/api/agents/providers/{id:D}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("agents.provider-reference-conflict", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.Single(await db.Set<ProviderSharePublication>().Where(x => x.ProviderProfileId == id).ToListAsync());
    }

    [Fact]
    public async Task Seam_api_returns_stable_concurrency_and_missing_target_errors() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var stale = await registry.GetProviderEditorAsync(id);
        var current = await registry.GetProviderEditorAsync(id);
        current.Name += " winner";
        await registry.SaveProviderAsync(current);
        var response = await fixture.Client.PostAsJsonAsync("/api/agents/providers", stale);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("agents.provider-concurrency-conflict", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var missing = await fixture.Client.GetAsync($"/api/agents/providers/{Guid.NewGuid():D}/editor");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static Editor NewEditor() => new() {
        Name = $"Provider seam proof {Guid.NewGuid():N}",
        Kind = RuntimeKind.Ollama,
        Transport = ProviderTransportKind.ChatCompletions,
        BaseUrl = "http://127.0.0.1:11434",
        DefaultModel = "seam-model",
        SuggestedModels = ["seam-model"],
        IsEnabled = true
    };

    private sealed class FailingObserver : IProviderProfileCommitObserver {
        public bool Fail { get; set; }
        public int Calls { get; private set; }
        public Task ProviderSavedAsync(Guid providerId, CancellationToken cancellationToken = default) => Complete();
        public Task ProviderDeletedAsync(Guid providerId, CancellationToken cancellationToken = default) => Complete();
        private Task Complete() {
            Calls++;
            return Fail ? Task.FromException(new IOException("Synthetic observer failure.")) : Task.CompletedTask;
        }
    }

    public class DiagnosticProxy : DispatchProxy {
        public int Calls { get; private set; }
        public int MaintenanceCalls { get; private set; }
        public Func<CanDoItAll.AgentFramework.Models.ProviderProfile, Task<ProviderModelMaintenanceEditorResult>>? Maintenance { get; set; }
        public Func<CanDoItAll.AgentFramework.Models.ProviderProfile, Task<ProviderHealthResult>> Probe { get; set; } = _ => throw new InvalidOperationException("Unexpected diagnostic.");
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name == nameof(IProviderDiagnosticsService.TestProviderAsync)) {
                Calls++;
                return Probe((CanDoItAll.AgentFramework.Models.ProviderProfile)args![0]!);
            }
            if (targetMethod?.Name == nameof(IProviderDiagnosticsService.CreateOrUpdateProviderModelAsync) && Maintenance is not null) {
                MaintenanceCalls++;
                return Maintenance((CanDoItAll.AgentFramework.Models.ProviderProfile)args![0]!);
            }
            throw new InvalidOperationException("This test does not dispatch external model maintenance or chat.");
        }
    }

    private sealed class InterceptingFactory(DbContextOptions<ProvidersDbContext> options) : IDbContextFactory<ProvidersDbContext> {
        public ProvidersDbContext CreateDbContext() => new(options);
        public Task<ProvidersDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ProvidersDbContext(options));
    }

    private sealed class AfterProviderSql(Action retire) : SaveChangesInterceptor {
        public bool Called { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            Called = true;
            retire();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class AfterHealthCommit : DbTransactionInterceptor {
        public bool Called { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            Called = true;
            throw new IOException("Synthetic lost acknowledgement after committed health metadata.");
        }
    }

    private sealed class PeerCommitObserver(Func<Guid, Task> saved) : IProviderProfileCommitObserver {
        public int Calls { get; private set; }
        public Task ProviderSavedAsync(Guid providerId, CancellationToken cancellationToken = default) {
            Calls++;
            return saved(providerId);
        }
        public Task ProviderDeletedAsync(Guid providerId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public class StoreProxy : DispatchProxy {
        public ISandboxWorkspaceStore Inner { get; set; } = null!;
        public bool Fail { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (Fail && targetMethod?.Name == nameof(ISandboxWorkspaceStore.UpdateCatalogAsync)) {
                throw new IOException("Synthetic projection failure.");
            }
            try {
                return targetMethod!.Invoke(Inner, args);
            } catch (TargetInvocationException exception) when (exception.InnerException is not null) {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
