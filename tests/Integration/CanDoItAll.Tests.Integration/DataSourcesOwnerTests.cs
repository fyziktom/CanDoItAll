using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.DataSources.Contracts;
using CanDoItAll.Tests.Integration.Persistence;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workspace.DataSources.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "HostPlatform")]
public sealed class DataSourcesOwnerTests {
    [Fact]
    public async Task Exact_profile_keeps_encrypted_password_and_deleting_configuration_keeps_physical_database() {
        await using var environment = CanDoItAllTestEnvironment.Create("data-sources-owner");
        await using var services = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(environment);
        var profiles = services.GetRequiredService<IDatabaseProfileService>();
        var a = await profiles.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(environment.CreatePostgreSqlProfile("source"), "A"));
        Assert.True(a.IsSuccess);
        var target = environment.CreatePostgreSqlProfile("target");
        var b = await profiles.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(target, "B"));
        Assert.True(b.IsSuccess);
        await using var scope = services.CreateAsyncScope();
        var owner = Owner(scope.ServiceProvider);
        var editor = await owner.ReadEditorAsync(b.Value, CancellationToken.None);
        var password = new NpgsqlConnectionStringBuilder(target.ConnectionString).Password!;
        Assert.True(editor.HasPassword);
        Assert.False(editor.ToString().Contains(password, StringComparison.Ordinal));
        using var intent = new ProfilePasswordIntent(string.Empty);
        var saved = await owner.SaveAsync(owner.Context, editor.Values with { DisplayName = "B renamed" }, intent);
        Assert.Equal(DataSourceOutcome.Confirmed, saved.Outcome);
        var stored = await profiles.GetEditorAsync(b.Value);
        Assert.True(stored.PostgresPassword == password);
        stored.PostgresPassword = string.Empty;
        var removed = await owner.ExecuteAsync(new(owner.Context, b.Value, DataSourceAction.Delete));
        Assert.Equal(DataSourceOutcome.Confirmed, removed.Outcome);
        Assert.True(await ExistsAsync(target.ConnectionString));
        var missing = await Assert.ThrowsAsync<DataSourcesException>(() => owner.ReadEditorAsync(b.Value, CancellationToken.None));
        Assert.Equal(DataSourceFailure.Missing, missing.Failure);
        using var missingPassword = new ProfilePasswordIntent(string.Empty);
        Assert.Equal(DataSourceFailure.Missing, (await Assert.ThrowsAsync<DataSourcesException>(() => owner.SaveAsync(owner.Context, editor.Values, missingPassword))).Failure);
        Assert.DoesNotContain(await profiles.ListAsync(), item => item.Id == b.Value);
    }

    [Fact]
    public async Task Current_and_persisted_pending_profiles_cannot_be_deleted_through_the_UI_owner() {
        await using var environment = CanDoItAllTestEnvironment.Create("data-sources-delete");
        await using var services = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(environment);
        var profiles = services.GetRequiredService<IDatabaseProfileService>();
        var a = await profiles.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(environment.CreatePostgreSqlProfile("source"), "A"));
        Assert.True(a.IsSuccess);
        await using var scope = services.CreateAsyncScope();
        var owner = Owner(scope.ServiceProvider);
        var original = owner.Context;
        var b = await profiles.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(environment.CreatePostgreSqlProfile("target"), "B"));
        Assert.True(b.IsSuccess);
        Assert.True((await profiles.ActivateAsync(b.Value)).IsSuccess);
        Assert.Equal(b.Value, (await owner.ReadRuntimeAsync(CancellationToken.None)).PendingRestartProfileId);
        foreach (var id in new[] { a.Value, b.Value }) {
            Assert.Equal(DataSourceFailure.Locked, (await Assert.ThrowsAsync<DataSourcesException>(
                () => owner.ExecuteAsync(new(original, id, DataSourceAction.Delete)))).Failure);
        }
        Assert.Equal(original, owner.Context);
        Assert.Equal(2, (await profiles.ListAsync()).Count);
    }

    [Theory]
    [InlineData(DataSourceAction.CreateEmpty)]
    [InlineData(DataSourceAction.ApplySchema)]
    public async Task Bootstrap_failure_retains_the_real_created_database_and_masks_exception_details(DataSourceAction action) {
        await using var environment = CanDoItAllTestEnvironment.Create("data-sources-bootstrap");
        await using var services = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(environment);
        var profiles = services.GetRequiredService<IDatabaseProfileService>();
        Assert.True((await profiles.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(environment.CreatePostgreSqlProfile("source"), "A"))).IsSuccess);
        await using var targetLease = PostgresTestDatabaseLease.Create("data-sources-partial");
        var target = environment.CreatePostgreSqlProfile("target", targetLease.ConnectionString);
        var targetEditor = TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(target, "B");
        targetEditor.PostgresAdminDatabaseName = "postgres";
        var saved = await profiles.SaveAsync(targetEditor);
        Assert.True(saved.IsSuccess);
        await targetLease.DisposeAsync();
        Assert.False(await ExistsAsync(target.ConnectionString));
        await using var scope = services.CreateAsyncScope();
        var log = new SafeLog();
        var workspace = ActivatorUtilities.CreateInstance<DatabaseProfileWorkspaceService>(scope.ServiceProvider, new FailedBootstrap(), log);
        var owner = new WorkspaceDataSourcesOwner(workspace, services.GetRequiredService<ICanonicalRuntimeDatabase>(), NullLogger<WorkspaceDataSourcesOwner>.Instance);
        var result = await owner.ExecuteAsync(new(owner.Context, saved.Value, action));
        Assert.Equal(DataSourceOutcome.Unknown, result.Outcome);
        Assert.Equal(saved.Value, result.ProfileId);
        Assert.True(await ExistsAsync(target.ConnectionString));
        Assert.DoesNotContain(FailedBootstrap.Poison, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(FailedBootstrap.Poison, string.Join('\n', log.Messages), StringComparison.Ordinal);
        Assert.Contains(log.Messages, message => message.Contains(saved.Value.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Startup_override_refuses_all_mutating_profile_commands() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = Owner(scope.ServiceProvider);
        var runtime = await owner.ReadRuntimeAsync(CancellationToken.None);
        Assert.True(runtime.IsRuntimeLocked);
        var editor = await owner.ReadEditorAsync(runtime.RuntimeProfileId, CancellationToken.None);
        using var password = new ProfilePasswordIntent(string.Empty);
        Assert.Equal(DataSourceFailure.Locked, (await Assert.ThrowsAsync<DataSourcesException>(
            () => owner.SaveAsync(owner.Context, editor.Values, password))).Failure);
        foreach (var action in Enum.GetValues<DataSourceAction>().Except([DataSourceAction.Save, DataSourceAction.Transfer])) {
            Assert.Equal(DataSourceFailure.Locked, (await Assert.ThrowsAsync<DataSourcesException>(
                () => owner.ExecuteAsync(new(owner.Context, runtime.RuntimeProfileId, action)))).Failure);
        }
    }

    [Fact]
    public async Task Source_revoked_after_preview_refuses_the_original_closed_transfer_without_target_progress() {
        await using var environment = CanDoItAllTestEnvironment.Create("data-sources-revoked");
        await using var services = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(environment);
        var profiles = services.GetRequiredService<IDatabaseProfileService>();
        var a = await profiles.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(environment.CreatePostgreSqlProfile("source"), "A"));
        var b = await profiles.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(environment.CreatePostgreSqlProfile("target"), "B"));
        Assert.True(a.IsSuccess && b.IsSuccess);
        var accessor = services.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var bootstrap = services.GetRequiredService<IAppDatabaseBootstrapper>();
        await bootstrap.EnsureProfileReadyAsync(accessor.ResolveProfile(a.Value));
        await bootstrap.EnsureProfileReadyAsync(accessor.ResolveProfile(b.Value));
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<WorkspaceService>().SaveSettingsAsync(new() { DefaultProviderProfileId = Guid.NewGuid() });
        var real = Owner(scope.ServiceProvider);
        var options = new DbContextOptionsBuilder<WorkspaceSettingsDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, accessor.ResolveProfile(b.Value));
        await using var target = new WorkspaceSettingsDbContext(options.Options);
        var before = await target.Set<WorkspaceSettings>().OrderBy(item => item.Id).Select(item => item.DefaultProviderProfileId).ToArrayAsync();
        var held = new HeldTransferOwner(real);
        var ledger = new DataSourceOperationLedger();
        using var session = new DataSourcesSession(held, ledger);
        await session.InitializeAsync();
        await session.SelectAsync(b.Value);
        await session.OpenTransferAsync(b.Value);
        var original = session.Transfer!;
        var key = new TransferGroupKey(scope.ServiceProvider.GetServices<IDatabaseTransferHandler>().OfType<WorkspaceDefaultProviderDatabaseTransferHandler>().Single().Descriptor.Key);
        original.Select(key, true);
        Assert.True(original.CanTransfer);
        var pending = original.TransferAsync();
        await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            session.CloseTransfer();
            await session.OpenTransferAsync(b.Value);
            Assert.NotSame(original, session.Transfer);
            Assert.True(ledger.IsBusy);
            Assert.False(session.Transfer!.CanTransfer);
            Assert.True((await profiles.DeleteAsync(a.Value)).IsSuccess);
        } finally {
            held.Release.TrySetResult();
            await pending;
        }
        var result = Assert.Single(ledger.Receipts);
        Assert.Equal(a.Value, result.SourceProfileId);
        Assert.Equal(b.Value, result.TargetProfileId);
        Assert.Equal(DataSourceOutcome.Refused, result.Result!.Outcome);
        Assert.Null(session.Transfer!.Result);
        Assert.Equal(before, await target.Set<WorkspaceSettings>().OrderBy(item => item.Id).Select(item => item.DefaultProviderProfileId).ToArrayAsync());
    }

    private static WorkspaceDataSourcesOwner Owner(IServiceProvider services) => new(services.GetRequiredService<DatabaseProfileWorkspaceService>(),
        services.GetRequiredService<ICanonicalRuntimeDatabase>(), NullLogger<WorkspaceDataSourcesOwner>.Instance);

    private static async Task<bool> ExistsAsync(string targetConnection) {
        var target = new NpgsqlConnectionStringBuilder(targetConnection);
        var database = target.Database;
        target.Database = "postgres";
        await using var connection = new NpgsqlConnection(target.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)", connection);
        command.Parameters.AddWithValue("name", database!);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private sealed class FailedBootstrap : IAppDatabaseBootstrapper {
        public const string Poison = "Password=synthetic-private-error";
        public Task EnsureCurrentProfileReadyAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EnsureProfileReadyAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default) => throw new IOException(Poison);
    }

    private sealed class HeldTransferOwner(IDataSourcesOwner inner) : IDataSourcesOwner {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DataSourcesContext Context => inner.Context;
        public Task<RuntimeSelection> ReadRuntimeAsync(CancellationToken cancellationToken) => inner.ReadRuntimeAsync(cancellationToken);
        public Task<IReadOnlyList<ProfileSummary>> ListAsync(CancellationToken cancellationToken) => inner.ListAsync(cancellationToken);
        public Task<ProfileEditor> ReadEditorAsync(Guid id, CancellationToken cancellationToken) => inner.ReadEditorAsync(id, cancellationToken);
        public Task<SchemaHealth> ReadSchemaAsync(Guid id, CancellationToken cancellationToken) => inner.ReadSchemaAsync(id, cancellationToken);
        public Task<IReadOnlyList<TransferSource>> ListSourcesAsync(Guid targetId, CancellationToken cancellationToken) => inner.ListSourcesAsync(targetId, cancellationToken);
        public Task<IReadOnlyList<TransferPreview>> PreviewAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken) => inner.PreviewAsync(sourceId, targetId, cancellationToken);
        public Task<DataSourceResult> SaveAsync(DataSourcesContext context, ProfileValues values, ProfilePasswordIntent password) => inner.SaveAsync(context, values, password);
        public Task<DataSourceResult> ExecuteAsync(DataSourceCommand command) => inner.ExecuteAsync(command);
        public async Task<DataSourceResult> TransferAsync(TransferRequest request) {
            Entered.TrySetResult();
            await Release.Task;
            return await inner.TransferAsync(request);
        }
    }

    private sealed class SafeLog : ILogger<DatabaseProfileWorkspaceService> {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            Messages.Add(formatter(state, exception));
            Assert.Null(exception);
        }
    }
}
