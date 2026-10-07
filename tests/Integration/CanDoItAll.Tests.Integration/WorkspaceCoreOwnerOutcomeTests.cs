using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Workspace.UI;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "HostPlatform")]
public sealed class WorkspaceCoreOwnerOutcomeTests {
    [Fact]
    public async Task Retired_secret_write_finishes_only_in_its_original_profile_and_cannot_edit_the_successor() {
        await using var original = await TestApplication.CreateAsync();
        await using var successor = await TestApplication.CreateAsync();
        await using var originalScope = original.Services.CreateAsyncScope();
        await using var successorScope = successor.Services.CreateAsyncScope();
        var services = originalScope.ServiceProvider;
        var canonical = new MutableCanonical(services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        var activity = new ActivityBoundary { Hold = true };
        var owner = ActivatorUtilities.CreateInstance<SecretService>(services, activity);
        using var state = new WorkspaceSecretsController(new WorkspaceSecretsOwner(owner, canonical));
        state.Draft.Model.Name = "Original profile write";
        state.Draft.Model.SecretValue = Guid.NewGuid().ToString("N");
        var pending = state.SaveAsync();
        await activity.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        canonical.Profile = successorScope.ServiceProvider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        canonical.Generation++;
        state.Dispose();
        using var next = new WorkspaceSecretsController(successorScope.ServiceProvider.GetRequiredService<IWorkspaceSecretsOwner>());
        next.Draft.Model.Name = "Successor draft";
        activity.Release.TrySetResult();
        await pending;
        var receipt = Assert.Single(state.Receipts);
        Assert.Equal(SettingsEffect.Committed, receipt.Effect);
        Assert.NotNull(await owner.GetAsync(receipt.RecordId!.Value));
        Assert.Null(await successorScope.ServiceProvider.GetRequiredService<SecretService>().GetAsync(receipt.RecordId.Value));
        Assert.Equal("Successor draft", next.Draft.Model.Name);
        Assert.Null(next.Draft.Model.Id);
        Assert.Empty(state.Draft.Model.SecretValue);
    }

    [Fact]
    public async Task Retired_defaults_commit_stays_in_the_original_profile_without_populating_the_successor() {
        await using var original = await TestApplication.CreateAsync();
        await using var successor = await TestApplication.CreateAsync();
        await using var originalScope = original.Services.CreateAsyncScope();
        await using var successorScope = successor.Services.CreateAsyncScope();
        var services = originalScope.ServiceProvider;
        var canonical = new MutableCanonical(services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        var activity = new ActivityBoundary { Hold = true };
        var workspace = ActivatorUtilities.CreateInstance<WorkspaceService>(services, activity);
        var owner = new WorkspaceDefaultsOwner(workspace, services.GetRequiredService<IWorkspaceProviderCatalog>(), canonical);
        using var state = new WorkspaceDefaultsController(owner);
        await state.RefreshAsync();
        state.Draft.Model.WorkspaceName = "Original profile defaults";
        var pending = state.SaveAsync();
        await activity.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        canonical.Profile = successorScope.ServiceProvider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        canonical.Generation++;
        state.Dispose();
        using var next = new WorkspaceDefaultsController(successorScope.ServiceProvider.GetRequiredService<IWorkspaceDefaultsOwner>());
        await next.RefreshAsync();
        var successorName = next.Draft.Model.WorkspaceName;
        activity.Release.TrySetResult();
        await pending;
        Assert.Equal(SettingsEffect.Committed, Assert.Single(state.Receipts).Effect);
        Assert.Equal("Original profile defaults", (await workspace.GetSettingsAsync()).WorkspaceName);
        Assert.Equal(successorName, next.Draft.Model.WorkspaceName);
        Assert.NotEqual("Original profile defaults", successorName);
        var refused = await owner.SaveAsync(new(), CancellationToken.None);
        Assert.Equal(SettingsDiagnostic.Retired, refused.Diagnostic);
    }

    private sealed class MutableCanonical(ResolvedDatabaseProfile profile) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile { get; set; } = profile;
        public long Generation { get; set; }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Secret_secondary_failure_preserves_exact_committed_identity_and_stage(bool delete, bool activityFailure) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var real = services.GetRequiredService<SecretService>();
        var created = await real.SaveAsync(new() { Name = "Owned stage fixture", SecretValue = Guid.NewGuid().ToString("N") });
        Assert.True(created.IsSuccess);
        var vault = new VaultBoundary(services.GetRequiredService<ISecretVault>()) { FailDelete = !activityFailure };
        var activity = new ActivityBoundary { Fail = activityFailure };
        var owner = ActivatorUtilities.CreateInstance<SecretService>(services, vault, activity);
        var value = Guid.NewGuid().ToString("N");
        var failure = await Assert.ThrowsAsync<SecretCommittedException>(() => delete ? owner.DeleteAsync(created.Value)
            : owner.SaveEditorAsync(new() { Id = created.Value, Name = "Changed stage fixture", SecretValue = value }));
        Assert.Equal(created.Value, failure.SecretId);
        Assert.Equal(delete, failure.Deleted);
        Assert.Equal(activityFailure ? SecretCommittedStage.PayloadCleanup : SecretCommittedStage.Metadata, failure.Stage);
        var observed = await real.GetAsync(created.Value);
        if (delete) {
            Assert.Null(observed);
        } else {
            Assert.Equal(value, observed!.SecretValue);
        }
        Assert.DoesNotContain(value, failure.ToString());
        Assert.DoesNotContain("vault", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Secret_owner_captures_before_vault_wait_and_ui_missing_record_cannot_upsert() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var vault = new VaultBoundary(services.GetRequiredService<ISecretVault>()) { HoldSet = true };
        var owner = ActivatorUtilities.CreateInstance<SecretService>(services, vault);
        var command = new SecretEditorModel { Name = "Captured", SecretValue = Guid.NewGuid().ToString("N"), MetadataJson = "{\"owned\":true}" };
        var originalValue = command.SecretValue;
        var pending = owner.SaveEditorAsync(command);
        await vault.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        command.Name = "Later";
        command.SecretValue = Guid.NewGuid().ToString("N");
        command.MetadataJson = "{ later";
        vault.Release.TrySetResult();
        var saved = await pending;
        Assert.True(saved.IsSuccess);
        var observed = await owner.GetAsync(saved.Value);
        Assert.Equal("Captured", observed!.Name);
        Assert.Equal(originalValue, observed.SecretValue);
        Assert.Equal("{\"owned\":true}", observed.MetadataJson);
        var missing = Guid.NewGuid();
        command.Id = missing;
        var refused = await owner.SaveEditorAsync(command);
        Assert.False(refused.IsSuccess);
        Assert.Null(await owner.GetAsync(missing));
        Assert.True((await owner.SaveAsync(command)).IsSuccess);
    }

    [Fact]
    public async Task Defaults_secondary_failure_exposes_normalized_saved_snapshot() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = ActivatorUtilities.CreateInstance<WorkspaceService>(scope.ServiceProvider, new ActivityBoundary { Fail = true });
        var warning = await Assert.ThrowsAsync<WorkspaceSettingsCommittedException>(() => owner.SaveSettingsAsync(new() {
            WorkspaceName = " Normalized ", CurrencyCode = "eur", CurrencyCultureName = "de-DE", Notes = " notes "
        }));
        Assert.Equal("Normalized", warning.Saved.WorkspaceName);
        Assert.Equal("EUR", warning.Saved.CurrencyCode);
        Assert.Equal("notes", warning.Saved.Notes);
        Assert.Equal(SettingsDiagnostic.ActivityPending, warning.Diagnostic);
        Assert.Equal("Normalized", (await owner.GetSettingsAsync()).WorkspaceName);
    }
    [Fact]
    public async Task Failed_defaults_persistence_does_not_publish_uncommitted_currency() {
        var fault = new CommitBoundary();
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = fault.Configure });
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        await service.SaveSettingsAsync(new());
        var state = scope.ServiceProvider.GetRequiredService<CurrencyDisplayState>();
        var original = state.Current;
        fault.RefuseDefaults = true;
        await Assert.ThrowsAsync<IOException>(() => service.SaveSettingsAsync(new() { CurrencyCode = "EUR", CurrencyCultureName = "de-DE" }));
        Assert.Equal(original, state.Current);
        Assert.Equal("USD", (await service.GetSettingsAsync()).CurrencyCode);
    }

    [Fact]
    public async Task Lost_metadata_acknowledgement_does_not_delete_a_referenced_secret_payload() {
        var fault = new CommitBoundary();
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = fault.Configure });
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<SecretService>();
        var id = Guid.NewGuid();
        var value = Guid.NewGuid().ToString("N");
        fault.LoseSecretAcknowledgement = true;
        var unknown = await Assert.ThrowsAsync<SecretMutationUnknownException>(() => service.SaveAsync(new() { Id = id, Name = "Acknowledgement fixture", SecretValue = value }));
        Assert.Equal(id, unknown.SecretId);
        var observed = await service.GetAsync(id);
        Assert.NotNull(observed);
        Assert.Equal(value, observed.SecretValue);
    }

    private sealed class CommitBoundary : SaveChangesInterceptor {
        public bool RefuseDefaults { get; set; }
        public bool LoseSecretAcknowledgement { get; set; }

        public void Configure(IServiceCollection services) {
            services.AddSingleton<IDbContextFactory<WorkspaceSettingsDbContext>>(provider =>
                new PooledDbContextFactory<WorkspaceSettingsDbContext>(new DbContextOptionsBuilder<WorkspaceSettingsDbContext>(
                    provider.GetRequiredService<DbContextOptions<WorkspaceSettingsDbContext>>()).AddInterceptors(this).Options));
            services.AddSingleton<IDbContextFactory<SecurityDbContext>>(provider =>
                new PooledDbContextFactory<SecurityDbContext>(new DbContextOptionsBuilder<SecurityDbContext>(
                    provider.GetRequiredService<DbContextOptions<SecurityDbContext>>()).AddInterceptors(this).Options));
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (eventData.Context is WorkspaceSettingsDbContext && RefuseDefaults) {
                RefuseDefaults = false;
                throw new IOException("Owned precommit failure.");
            }
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default) {
            if (eventData.Context is SecurityDbContext && LoseSecretAcknowledgement) {
                LoseSecretAcknowledgement = false;
                throw new IOException("Owned acknowledgement failure.");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class VaultBoundary(ISecretVault actual) : ISecretVault {
        public bool FailDelete { get; init; }
        public bool HoldSet { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) => actual.GetAsync(key, cancellationToken);
        public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default) {
            if (HoldSet) {
                Entered.TrySetResult();
                await Release.Task;
            }
            await actual.SetAsync(key, value, cancellationToken);
        }
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => FailDelete
            ? Task.FromException(new IOException("Owned cleanup fault.")) : actual.DeleteAsync(key, cancellationToken);
    }

    private sealed class ActivityBoundary : IActivityStream {
        public bool Fail { get; init; }
        public bool Hold { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default) {
            if (Hold) {
                Entered.TrySetResult();
                await Release.Task;
            }
            if (Fail) {
                throw new IOException("Owned activity fault.");
            }
        }
    }
}
