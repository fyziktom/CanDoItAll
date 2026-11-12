using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessAuthoringUiAuthorityTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_ui_caller_survives_child_scopes_and_revocation_after_SQL_rolls_back_every_row(bool revokeAfterSql) {
        var fault = new RevokeAfterSave();
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
            services.AddScoped<IProcessAuthoringStore>(provider => {
                var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>();
                AppDbContextOptionsConfigurator.Configure(options, provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
                options.AddInterceptors(fault);
                return new EfProcessAuthoringStore(new ContextFactory(options.Options), provider.GetRequiredService<CoordinatedDatabaseTransaction>(),
                    provider.GetRequiredService<IProcessAuthoringAdmissionPolicy>(), TimeProvider.System);
            });
        } });
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var client = services.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var request = Request();
        var editor = (await client.GetShellAsync(request)).DefinitionCatalog.SelectedEditor!;
        var admitted = true;
        ProcessAuthoringUiAuthority authority = new(new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation),
            null, Guid.NewGuid(), "native-ui-operator-a", (_, _) => admitted ? ValueTask.CompletedTask : ValueTask.FromException(new UnauthorizedAccessException("Original UI actor retired.")));
        var bound = client.WithUiAuthority(authority);
        ProcessDefinitionEditorCommand command = new(request.Scope, editor.DefinitionKey, ProcessDefinitionEditorCommandKind.SaveDraft, editor.VersionToken,
            new(editor.DefinitionKey, editor.Identity with { Name = "UI authority retained through native scope" }, editor.Governance, editor.Contracts, editor.Simulation));
        if (revokeAfterSql) {
            fault.Revoke = () => admitted = false;
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => bound.ExecuteDefinitionEditorCommandAsync(command));
            Assert.True(fault.Reached);
        } else {
            Assert.Equal(ProcessDefinitionEditorCommandStatus.Accepted, (await bound.ExecuteDefinitionEditorCommandAsync(command)).Receipt.Status);
            var query = new ProcessAuthoringOperationQuery(request.Scope, editor.DefinitionKey, editor.VersionToken.Value, command.OperationId);
            Assert.Equal(ProcessAuthoringOperationState.Committed, (await bound.GetAuthoringOperationAsync(query)).State);
            var other = client.WithUiAuthority(authority with { CallerId = "native-ui-operator-b" });
            Assert.Equal(ProcessAuthoringOperationState.NotRecorded, (await other.GetAuthoringOperationAsync(query)).State);
        }
        await using var context = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        Assert.Equal(revokeAfterSql ? 0 : 1, await context.AuthoringHeads.CountAsync());
        Assert.Equal(revokeAfterSql ? 0 : 1, await context.AuthoringReceipts.CountAsync());
        if (!revokeAfterSql) {
            Assert.Equal(authority.CallerId, (await context.AuthoringReceipts.SingleAsync()).CallerId);
        }
    }

    [Fact]
    public async Task Actual_commits_notify_peer_sessions_once_while_conflicts_and_replays_emit_no_new_hint() {
        await using var app = await TestApplication.CreateAsync(new());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var feed = services.GetRequiredService<EditingChangeFeed>();
        using var interested = feed.Subscribe(new(new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation), EditingOwner.ProcessAuthoring));
        var client = services.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var request = Request();
        var editor = (await client.GetShellAsync(request)).DefinitionCatalog.SelectedEditor!;
        ProcessDefinitionEditorCommand command = new(request.Scope, editor.DefinitionKey, ProcessDefinitionEditorCommandKind.SaveDraft, editor.VersionToken,
            new(editor.DefinitionKey, editor.Identity with { Name = "Actual producer change" }, editor.Governance, editor.Contracts, editor.Simulation));
        Assert.Equal(ProcessDefinitionEditorCommandStatus.Accepted, (await client.ExecuteDefinitionEditorCommandAsync(command)).Receipt.Status);
        Assert.Equal(1, feed.DeliveredHints);
        Assert.Equal(ProcessDefinitionEditorCommandStatus.Accepted, (await client.ExecuteDefinitionEditorCommandAsync(command)).Receipt.Status);
        Assert.Equal(ProcessDefinitionEditorCommandStatus.Rejected, (await client.ExecuteDefinitionEditorCommandAsync(command with { OperationId = Guid.NewGuid() })).Receipt.Status);
        Assert.Equal(1, feed.DeliveredHints);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var batch = await interested.ReadAsync(timeout.Token);
        Assert.Single(batch.Changes);
        Assert.Equal(command.OperationId, batch.Changes[0].OperationId);
    }

    private static ProcessWorkspaceShellRequest Request() => new(ProcessWorkspaceShellScope.Global, new(null, null, null),
        new(null, null, ProcessDefinitionCatalogScopeKind.All, 50),
        new(null, ProcessTemplateCatalogCategoryKind.All, null, ProcessTemplateCatalogPreviewTabKind.Overview, 50), false);

    private sealed class ContextFactory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    private sealed class RevokeAfterSave : SaveChangesInterceptor {
        public Action? Revoke { get; set; }
        public bool Reached { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (eventData.Context is ProcessPersistenceDbContext && Revoke is { } revoke) {
                Reached = true;
                revoke();
                Revoke = null;
            }
            return ValueTask.FromResult(result);
        }
    }
}
