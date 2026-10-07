using System.Text.Json;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "HostPlatform")]
public sealed class StorageCatalogUiOwnerTests {
    [Fact]
    public async Task Production_composition_creates_updates_routes_tests_and_deletes_exact_private_target() {
        await using var fixture = await OwnerFixture.CreateAsync();
        var owner = fixture.Scope.ServiceProvider.GetRequiredService<IStorageCatalogOwner>();
        var draft = fixture.Draft with { DefaultPurposes = [CatalogPurpose.ProjectAsset, CatalogPurpose.DeploymentMirror] };
        var created = await owner.ExecuteAsync(Command(owner, draft), default);
        Assert.Equal(CatalogWrite.Committed, created.Write);
        Assert.Equal(CatalogRouting.Complete, created.Routing);
        Assert.Equal(CatalogActivity.Complete, created.Activity);
        var id = created.CatalogId!.Value;
        var acquired = (await owner.ReadEditorAsync(id, default))!;
        Assert.Equal(id, acquired.Id);
        var changed = acquired with { Name = "Renamed catalog", DefaultPurposes = [] };
        var updated = await owner.ExecuteAsync(Command(owner, changed), default);
        Assert.Equal(id, updated.CatalogId);
        var tested = await owner.ExecuteAsync(Command(owner, changed, CatalogEffect.Test), default);
        Assert.Equal(CatalogDriver.Completed, tested.Driver);
        Assert.Equal(CatalogWrite.Committed, tested.Write);
        Assert.Equal(CatalogHealth.Unavailable, tested.Health!.Status);
        Assert.True(Directory.Exists(draft.EndpointOrRoot));
        await using (var db = fixture.Factory.CreateDbContext()) {
            Assert.Single(await db.Set<StorageCatalogRecord>().Where(row => !row.IsSystemDefault).ToArrayAsync());
            var rules = await db.Set<StorageRoutingRule>().Where(rule => rule.PreferredStorageId == id).OrderBy(rule => rule.Priority).ToArrayAsync();
            Assert.Equal(new[] { 100, 170 }, rules.Select(rule => rule.Priority));
            Assert.All(rules, rule => Assert.False(rule.IsEnabled));
        }
        var deleted = await owner.ExecuteAsync(Command(owner, changed, CatalogEffect.Delete), default);
        Assert.Equal(CatalogWrite.Committed, deleted.Write);
        Assert.Null(await owner.ReadEditorAsync(id, default));
        var missing = await owner.ExecuteAsync(Command(owner, changed), default);
        Assert.Equal(CatalogDiagnostic.Missing, missing.Diagnostic);
        Assert.Equal(CatalogWrite.NotAttempted, missing.Write);
        await using var verify = fixture.Factory.CreateDbContext();
        Assert.Empty(await verify.Set<StorageCatalogRecord>().Where(row => !row.IsSystemDefault).ToArrayAsync());
        var system = Assert.Single(await owner.ReadCatalogAsync(default));
        var protectedDraft = (await owner.ReadEditorAsync(system.Id, default))! with { IsSystemDefault = false };
        Assert.Equal(CatalogDiagnostic.Protected, (await owner.ExecuteAsync(Command(owner, protectedDraft), default)).Diagnostic);
        Assert.Equal(CatalogDiagnostic.Protected, (await owner.ExecuteAsync(Command(owner, protectedDraft, CatalogEffect.Delete), default)).Diagnostic);
    }

    [Theory]
    [InlineData(FlushStage.BeforeRouting, 0)]
    [InlineData(FlushStage.AfterFirstRouting, 1)]
    public async Task Actual_per_purpose_failure_retains_catalog_id_and_possibly_partial_routing(FlushStage stage, int committedRules) {
        await using var fixture = await OwnerFixture.CreateAsync();
        fixture.Fault.Stage = stage;
        var draft = fixture.Draft with { DefaultPurposes = [CatalogPurpose.ProjectAsset, CatalogPurpose.Evidence] };
        var result = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, draft), default);
        Assert.Equal(CatalogWrite.Committed, result.Write);
        Assert.Equal(CatalogRouting.PossiblyPartial, result.Routing);
        Assert.Equal(CatalogActivity.NotAttempted, result.Activity);
        Assert.NotNull(result.CatalogId);
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Equal(result.CatalogId, (await db.Set<StorageCatalogRecord>().SingleAsync(row => !row.IsSystemDefault)).Id);
        Assert.Equal(committedRules, await db.Set<StorageRoutingRule>().CountAsync(rule => rule.PreferredStorageId == result.CatalogId));
        var writes = fixture.Fault.CatalogFlushes;
        await fixture.Owner.ReadEditorAsync(result.CatalogId.Value, default);
        await fixture.Owner.ReadRoutesAsync(default);
        Assert.Equal(writes, fixture.Fault.CatalogFlushes);
        Assert.Equal(0, fixture.Activity.Calls);
    }

    [Fact]
    public async Task Activity_failure_and_failure_to_log_cannot_erase_acknowledged_catalog_and_routes() {
        await using var fixture = await OwnerFixture.CreateAsync();
        fixture.Activity.Fail = true;
        var result = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, fixture.Draft with { DefaultPurposes = [CatalogPurpose.Evidence] }), default);
        Assert.Equal(CatalogWrite.Committed, result.Write);
        Assert.Equal(CatalogRouting.Complete, result.Routing);
        Assert.Equal(CatalogActivity.Failed, result.Activity);
        Assert.True(result.DiagnosticsUnavailable);
        await using var db = fixture.Factory.CreateDbContext();
        Assert.NotNull(await db.Set<StorageCatalogRecord>().SingleOrDefaultAsync(row => row.Id == result.CatalogId));
        Assert.Equal(result.CatalogId, (await db.Set<StorageRoutingRule>().SingleAsync(rule => rule.UsagePurpose == StorageUsagePurpose.Evidence)).PreferredStorageId);
    }

    [Fact]
    public async Task Lost_catalog_acknowledgement_remains_unknown_after_real_row_is_observed() {
        await using var fixture = await OwnerFixture.CreateAsync();
        fixture.Fault.Stage = FlushStage.AfterCatalog;
        var result = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, fixture.Draft), default);
        Assert.Equal(CatalogWrite.Unknown, result.Write);
        Assert.Null(result.CatalogId);
        Assert.Equal(CatalogRouting.NotAttempted, result.Routing);
        var actual = Assert.Single((await fixture.Owner.ReadCatalogAsync(default)).Where(row => !row.IsSystemDefault));
        Assert.NotNull(await fixture.Owner.ReadEditorAsync(actual.Id, default));
        Assert.Equal(CatalogWrite.Unknown, result.Write);
        Assert.Equal(1, fixture.Fault.CatalogFlushes);
        Assert.Equal(0, fixture.Activity.Calls);
    }

    [Fact]
    public async Task Concurrent_delete_between_acquisition_and_flush_cannot_insert_replacement() {
        await using var fixture = await OwnerFixture.CreateAsync();
        var created = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, fixture.Draft), default);
        var acquired = (await fixture.Owner.ReadEditorAsync(created.CatalogId!.Value, default))!;
        fixture.Fault.Stage = FlushStage.HoldCatalog;
        var saving = fixture.Owner.ExecuteAsync(Command(fixture.Owner, acquired with { Name = "Concurrent edit" }), default);
        await fixture.Fault.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try {
            await using var db = fixture.Application.Services.GetRequiredService<IDbContextFactory<StorageDbContext>>().CreateDbContext();
            await db.Set<StorageRoutingRule>().Where(rule => rule.PreferredStorageId == acquired.Id).ExecuteDeleteAsync();
            await db.Set<StorageCatalogRecord>().Where(row => row.Id == acquired.Id).ExecuteDeleteAsync();
        } finally {
            fixture.Fault.Release.TrySetResult();
        }
        var result = await saving;
        Assert.Equal(CatalogWrite.Unknown, result.Write);
        await using var verify = fixture.Factory.CreateDbContext();
        Assert.Empty(await verify.Set<StorageCatalogRecord>().Where(row => !row.IsSystemDefault).ToArrayAsync());
        Assert.Null(await fixture.Owner.ReadEditorAsync(acquired.Id!.Value, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_private_filesystem_test_preserves_unsaved_vs_existing_contract(bool existing) {
        await using var fixture = await OwnerFixture.CreateAsync();
        var draft = fixture.Draft;
        if (existing) {
            var saved = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, draft), default);
            draft = draft with { Id = saved.CatalogId };
        }
        var original = draft with { EndpointOrRoot = Path.Combine(fixture.Profile.WorkspaceRootPath, "tested-original"), Name = "Tested configuration" };
        Directory.CreateDirectory(original.EndpointOrRoot);
        var legacy = fixture.Scope.ServiceProvider.GetRequiredService<WorkspaceService>().CreateStorageDraft(StorageProviderKind.FileSystem);
        legacy.Name = original.Name;
        legacy.EndpointOrRoot = original.EndpointOrRoot;
        legacy.Id = original.Id;
        var legacyTest = await fixture.Scope.ServiceProvider.GetRequiredService<WorkspaceService>().TestStorageAsync(legacy);
        Assert.Equal(StorageHealthStatus.Unavailable, legacyTest.HealthStatus);
        var result = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, original, CatalogEffect.Test), default);
        Assert.Equal((CatalogHealth)legacyTest.HealthStatus, result.Health!.Status);
        Assert.Equal(CatalogDriver.Completed, result.Driver);
        Assert.Equal(existing ? CatalogWrite.Committed : CatalogWrite.NotAttempted, result.Write);
        Assert.Equal(CatalogHealth.Unavailable, result.Health!.Status);
        Assert.True(Directory.Exists(original.EndpointOrRoot));
        await using var db = fixture.Factory.CreateDbContext();
        var rows = await db.Set<StorageCatalogRecord>().Where(row => !row.IsSystemDefault).ToArrayAsync();
        if (existing) {
            Assert.Equal(original.EndpointOrRoot, Assert.Single(rows).EndpointOrRoot);
            Assert.Equal(original.Id, rows[0].Id);
            Assert.Equal(StorageHealthStatus.Unavailable, rows[0].HealthStatus);
        } else {
            Assert.Empty(rows);
        }
        Assert.Equal(1, fixture.Driver.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_credential_resolution_uses_storage_consumer_and_missing_material_never_dispatches(bool missing) {
        await using var fixture = await OwnerFixture.CreateAsync();
        var secret = await fixture.Scope.ServiceProvider.GetRequiredService<SecretService>().SaveEditorAsync(new() {
            Name = "Private test credential", Kind = SecretKind.Password, SecretValue = "synthetic-storage-test-material"
        });
        Assert.True(secret.IsSuccess);
        var draft = fixture.Draft with { CredentialSecretId = missing ? Guid.NewGuid() : secret.Value };
        var result = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, draft, CatalogEffect.Test), default);
        var request = Assert.Single(fixture.Secrets.Requests);
        Assert.Equal(SecretRuntimePurposes.StorageCredential, request.Purpose);
        Assert.Equal(SecretRuntimeConsumerTypes.StorageCredential, request.ConsumerType);
        Assert.Equal(new[] { draft.CredentialSecretId!.Value }, request.AllowedSecretIds);
        Assert.True(Guid.TryParse(request.ConsumerId, out _));
        Assert.Equal(missing ? 0 : 1, fixture.Driver.Calls);
        Assert.Equal(missing ? CatalogDiagnostic.CredentialDenied : CatalogDiagnostic.None, result.Diagnostic);
        Assert.DoesNotContain("synthetic-storage-test-material", JsonSerializer.Serialize(result), StringComparison.Ordinal);
        if (!missing) {
            Assert.Equal("synthetic-storage-test-material", fixture.Driver.Credential);
        }
    }

    [Fact]
    public async Task Test_health_acknowledgement_and_activity_failure_are_separate_from_driver_completion() {
        await using var fixture = await OwnerFixture.CreateAsync();
        var saved = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, fixture.Draft), default);
        fixture.Activity.Fail = true;
        var result = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, fixture.Draft with { Id = saved.CatalogId }, CatalogEffect.Test), default);
        Assert.Equal(CatalogDriver.Completed, result.Driver);
        Assert.Equal(CatalogWrite.Committed, result.Write);
        Assert.Equal(saved.CatalogId, result.CatalogId);
        Assert.Equal(CatalogActivity.Failed, result.Activity);
        fixture.Fault.Stage = FlushStage.AfterCatalog;
        var unknown = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, fixture.Draft with { Id = saved.CatalogId }, CatalogEffect.Test), default);
        Assert.Equal(CatalogDriver.Completed, unknown.Driver);
        Assert.Equal(CatalogWrite.Unknown, unknown.Write);
        Assert.NotNull(unknown.Health);
        Assert.Equal(2, fixture.Driver.Calls);
    }

    [Fact]
    public async Task Existing_profile_fence_keeps_all_held_stages_in_original_database_and_retires_later_commands() {
        await using var fixture = await OwnerFixture.CreateAsync();
        var nextProfile = fixture.Environment.CreatePostgreSqlProfile("next");
        await using var nextApplication = await TestApplication.CreateAsync(new() { TestEnvironment = fixture.Environment, ActiveProfile = nextProfile });
        await using var nextScope = nextApplication.Services.CreateAsyncScope();
        var nextOwner = nextScope.ServiceProvider.GetRequiredService<IStorageCatalogOwner>();
        await nextOwner.ReadCatalogAsync(default);
        var saved = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, fixture.Draft), default);
        fixture.Driver.Hold = true;
        var testing = fixture.Owner.ExecuteAsync(Command(fixture.Owner, fixture.Draft with { Id = saved.CatalogId }, CatalogEffect.Test), default);
        await fixture.Driver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var runtime = (DatabaseRuntimeState)fixture.Application.Services.GetRequiredService<IDatabaseRuntimeState>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var switching = Task.Run(() => {
            started.TrySetResult();
            runtime.PublishRestartObserved(runtime.GetSnapshot(), nextApplication.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        });
        await started.Task;
        Assert.False(switching.IsCompleted);
        fixture.Driver.Release.TrySetResult();
        var result = await testing;
        await switching;
        Assert.Equal(CatalogWrite.Committed, result.Write);
        Assert.Equal(CatalogActivity.Complete, result.Activity);
        Assert.False(fixture.Owner.IsCurrent);
        Assert.Equal(CatalogDiagnostic.Retired, (await fixture.Owner.ExecuteAsync(Command(fixture.Owner, fixture.Draft), default)).Diagnostic);
        Assert.Null(await nextOwner.ReadEditorAsync(saved.CatalogId!.Value, default));
        Assert.All(await nextOwner.ReadCatalogAsync(default), row => Assert.True(row.IsSystemDefault));
        await using var original = fixture.Factory.CreateDbContext();
        var retained = await original.Set<StorageCatalogRecord>().SingleAsync(row => row.Id == saved.CatalogId);
        Assert.Equal(fixture.Draft.EndpointOrRoot, retained.EndpointOrRoot);
        Assert.Equal(StorageHealthStatus.Unavailable, retained.HealthStatus);
    }

    private static CatalogCommand Command(IStorageCatalogOwner owner, CatalogEdit draft, CatalogEffect effect = CatalogEffect.Save) =>
        new(Guid.NewGuid(), Guid.NewGuid(), owner.Context, effect, draft);

    [Theory]
    [InlineData(StorageHealthStatus.Degraded)]
    [InlineData(StorageHealthStatus.Unavailable)]
    public async Task Driver_health_is_persisted_without_exposing_its_raw_message(StorageHealthStatus health) {
        await using var fixture = await OwnerFixture.CreateAsync();
        var saved = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, fixture.Draft), default);
        fixture.Driver.HealthOverride = health;
        var result = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, fixture.Draft with { Id = saved.CatalogId }, CatalogEffect.Test), default);
        Assert.Equal(CatalogDriver.Completed, result.Driver);
        Assert.Equal(CatalogWrite.Committed, result.Write);
        Assert.Equal((CatalogHealth)health, result.Health!.Status);
        Assert.Equal(1, fixture.Driver.Calls);
        await using var db = fixture.Factory.CreateDbContext();
        var row = await db.Set<StorageCatalogRecord>().SingleAsync(row => row.Id == saved.CatalogId);
        Assert.Equal(health, row.HealthStatus);
        Assert.DoesNotContain("synthetic-private-driver-detail", row.LastHealthMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-private-driver-detail", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_driver_refuses_without_writes_and_thrown_driver_remains_unknown() {
        await using var fixture = await OwnerFixture.CreateAsync();
        var unavailable = fixture.Draft with { ProviderKind = CatalogProvider.Ipfs };
        var missing = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, unavailable, CatalogEffect.Test), default);
        Assert.Equal(CatalogDiagnostic.DriverMissing, missing.Diagnostic);
        Assert.Equal(CatalogDriver.NotAttempted, missing.Driver);
        Assert.Equal(0, fixture.Driver.Calls);
        fixture.Driver.Throw = true;
        var failed = await fixture.Owner.ExecuteAsync(Command(fixture.Owner, fixture.Draft, CatalogEffect.Test), default);
        Assert.Equal(CatalogDiagnostic.DriverFailed, failed.Diagnostic);
        Assert.Equal(CatalogDriver.Unknown, failed.Driver);
        Assert.Equal(CatalogWrite.NotAttempted, failed.Write);
        Assert.True(failed.IsUnknown);
        Assert.DoesNotContain("synthetic-private-driver-detail", JsonSerializer.Serialize(failed), StringComparison.Ordinal);
        Assert.Equal(0, fixture.Fault.CatalogFlushes);
        Assert.Equal(0, fixture.Activity.Calls);
        Assert.Equal(1, fixture.Driver.Calls);
    }

    public enum FlushStage { None, BeforeRouting, AfterFirstRouting, AfterCatalog, HoldCatalog }
    private sealed class FlushFault : SaveChangesInterceptor {
        public FlushStage Stage { get; set; }
        public int CatalogFlushes { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool catalog;
        private bool routing;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            catalog = eventData.Context!.ChangeTracker.Entries<StorageCatalogRecord>().Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && !entry.Entity.IsSystemDefault);
            routing = eventData.Context.ChangeTracker.Entries<StorageRoutingRule>().Any(entry => entry.State is EntityState.Added or EntityState.Modified && entry.Entity.UsagePurpose != StorageUsagePurpose.Unknown);
            if (routing && Stage == FlushStage.BeforeRouting) {
                throw new InvalidOperationException("Private routing flush rejected.");
            }
            if (catalog && Stage == FlushStage.HoldCatalog) {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (catalog) {
                CatalogFlushes++;
            }
            if ((catalog && Stage == FlushStage.AfterCatalog) || (routing && Stage == FlushStage.AfterFirstRouting)) {
                throw new InvalidOperationException("Private acknowledgement lost after database flush.");
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class ThrowingLogger : Microsoft.Extensions.Logging.ILogger<StorageCatalogCommands> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("Private diagnostics failure.");
    }
    private sealed class Factory(DbContextOptions<StorageDbContext> options) : IDbContextFactory<StorageDbContext> {
        public StorageDbContext CreateDbContext() => new(options);
    }
    private sealed class ActivityProbe(IActivityStream inner) : IActivityStream {
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public async Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            if (Fail) {
                throw new InvalidOperationException("Private Activity fault.");
            }
            await inner.RecordAsync(request, cancellationToken);
        }
    }
    private sealed class SecretProbe(ISecretRuntimeResolver inner) : ISecretRuntimeResolver {
        public List<SecretRuntimeRequest> Requests { get; } = [];
        public Task<string?> ResolveValueAsync(SecretRuntimeRequest request, CancellationToken cancellationToken = default) {
            Requests.Add(request);
            return inner.ResolveValueAsync(request, cancellationToken);
        }
    }
    private sealed class DriverProbe(IStorageDriver inner) : IStorageDriver {
        public StorageProviderKind ProviderKind => inner.ProviderKind;
        public StorageCapability SupportedCapabilities => inner.SupportedCapabilities;
        public int Calls { get; private set; }
        public string? Credential { get; private set; }
        public bool Hold { get; set; }
        public bool Throw { get; set; }
        public StorageHealthStatus? HealthOverride { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<StorageConnectionTestResult> TestConnectionAsync(StorageDriverInput storage, string? secretValue, CancellationToken cancellationToken = default) {
            Calls++;
            Credential = secretValue;
            if (Hold) {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            if (Throw) {
                throw new InvalidOperationException("synthetic-private-driver-detail");
            }
            var result = await inner.TestConnectionAsync(storage, secretValue, cancellationToken);
            return HealthOverride is { } health ? result with { HealthStatus = health, Message = "synthetic-private-driver-detail" } : result;
        }
        public Task<StorageWriteResult> SaveAsync(StorageDriverInput storage, StorageWriteRequest request, CancellationToken cancellationToken = default) => inner.SaveAsync(storage, request, cancellationToken);
        public Task<Stream> OpenReadAsync(StorageDriverInput storage, StorageObjectReference reference, CancellationToken cancellationToken = default) => inner.OpenReadAsync(storage, reference, cancellationToken);
        public Task DeleteAsync(StorageDriverInput storage, StorageObjectReference reference, CancellationToken cancellationToken = default) => inner.DeleteAsync(storage, reference, cancellationToken);
    }
    private sealed class OwnerFixture : IAsyncDisposable {
        public CanDoItAllTestEnvironment Environment { get; } = CanDoItAllTestEnvironment.Create("storage-catalog-owner");
        public TestDatabaseProfile Profile { get; private set; } = default!;
        public TestApplication Application { get; private set; } = default!;
        public AsyncServiceScope Scope { get; private set; }
        public Factory Factory { get; private set; } = default!;
        public FlushFault Fault { get; } = new();
        public ActivityProbe Activity { get; private set; } = default!;
        public DriverProbe Driver { get; private set; } = default!;
        public SecretProbe Secrets { get; private set; } = default!;
        public WorkspaceStorageCatalogOwner Owner { get; private set; } = default!;
        public CatalogEdit Draft => Owner.Choices.Providers[0].Template with { Name = "Private catalog", EndpointOrRoot = Path.Combine(Profile.WorkspaceRootPath, "catalog") };
        public static async Task<OwnerFixture> CreateAsync() {
            var fixture = new OwnerFixture();
            fixture.Profile = fixture.Environment.CreatePostgreSqlProfile("original");
            Directory.CreateDirectory(Path.Combine(fixture.Profile.WorkspaceRootPath, "catalog"));
            fixture.Application = await TestApplication.CreateAsync(new() { TestEnvironment = fixture.Environment, ActiveProfile = fixture.Profile });
            fixture.Scope = fixture.Application.Services.CreateAsyncScope();
            var services = fixture.Scope.ServiceProvider;
            var options = new DbContextOptionsBuilder<StorageDbContext>(services.GetRequiredService<DbContextOptions<StorageDbContext>>()).AddInterceptors(fixture.Fault).Options;
            fixture.Factory = new(options);
            var catalog = new StorageCatalogService(fixture.Factory, services.GetRequiredService<IWorkspacePathResolver>(), services.GetRequiredService<IClock>(), options, services.GetRequiredService<CoordinatedDatabaseTransaction>());
            fixture.Activity = new(services.GetRequiredService<IActivityStream>());
            var registry = services.GetRequiredService<IStorageDriverRegistry>();
            Assert.True(registry.TryResolve(StorageProviderKind.FileSystem, out var filesystem));
            fixture.Driver = new(filesystem);
            fixture.Secrets = new(services.GetRequiredService<ISecretRuntimeResolver>());
            var commands = new StorageCatalogCommands(catalog, new StorageDriverRegistry([fixture.Driver]), fixture.Secrets, fixture.Activity,
                services.GetRequiredService<IDatabaseRuntimeWriteFence>(), new ThrowingLogger());
            fixture.Owner = new(services.GetRequiredService<WorkspaceService>(), catalog, services.GetRequiredService<ICanonicalRuntimeDatabase>(),
                services.GetRequiredService<IDatabaseRuntimeState>(), commands);
            await fixture.Owner.ReadCatalogAsync(default);
            return fixture;
        }
        public async ValueTask DisposeAsync() {
            Fault.Release.TrySetResult();
            Driver.Release.TrySetResult();
            await Scope.DisposeAsync();
            await Application.DisposeAsync();
            await Environment.DisposeAsync();
        }
    }
}
