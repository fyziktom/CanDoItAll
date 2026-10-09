using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
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
public sealed class ProcessAuthoringRecoveryTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_operation_status_and_explicit_same_id_recovery_distinguish_actual_commit_faults(bool afterCommit) {
        var fault = new CommitFault(afterCommit);
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
        var client = scope.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
        ProcessWorkspaceShellRequest request = new(ProcessWorkspaceShellScope.Global, new(null, null, null),
            new(null, null, ProcessDefinitionCatalogScopeKind.All, 50),
            new(null, ProcessTemplateCatalogCategoryKind.All, null, ProcessTemplateCatalogPreviewTabKind.Overview, 50), false);
        var editor = (await client.GetShellAsync(request)).DefinitionCatalog.SelectedEditor!;
        ProcessDefinitionEditorCommand command = new(request.Scope, editor.DefinitionKey, ProcessDefinitionEditorCommandKind.SaveDraft, editor.VersionToken,
            new(editor.DefinitionKey, editor.Identity with { Name = "Recoverable native definition" }, editor.Governance, editor.Contracts, editor.Simulation));
        fault.Armed = true;
        await Assert.ThrowsAsync<TimeoutException>(() => client.ExecuteDefinitionEditorCommandAsync(command));
        Assert.True(fault.Reached);
        await using var independent = app.Services.CreateAsyncScope();
        var recoveryClient = independent.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var query = new ProcessAuthoringOperationQuery(request.Scope, editor.DefinitionKey, editor.VersionToken.Value, command.OperationId);
        var status = await recoveryClient.GetAuthoringOperationAsync(query);
        Assert.Equal(afterCommit ? ProcessAuthoringOperationState.Committed : ProcessAuthoringOperationState.NotRecorded, status.State);
        var recovered = await recoveryClient.ExecuteDefinitionEditorCommandAsync(command);
        Assert.Equal(ProcessDefinitionEditorCommandStatus.Accepted, recovered.Receipt.Status);
        var aggregate = Assert.IsType<ProcessDefinitionEditorProjection>(recovered.Reconciliation!.Editor);
        Assert.Null(recovered.Reconciliation.Warning);
        Assert.Equal(1, aggregate.Observation!.Revision);
        Assert.Equal(aggregate.Observation, aggregate.RoleEditor!.Observation);
        Assert.Equal(aggregate.Observation, aggregate.StepEditor!.Observation);
        Assert.Equal(aggregate.Observation, aggregate.Canvas!.Observation);
        Assert.Equal(aggregate.Observation, aggregate.TemplateCatalog!.Observation);
        var replay = await recoveryClient.ExecuteDefinitionEditorCommandAsync(command);
        Assert.Equal(recovered.Receipt.ObservedAtUtc, replay.Receipt.ObservedAtUtc);
        Assert.Equal(recovered.Receipt.VersionToken, replay.Receipt.VersionToken);
        Assert.Equal(command.OperationId, replay.Receipt.ReceiptId);
        await using var database = await independent.ServiceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await database.AuthoringHeads.CountAsync());
        Assert.Equal(1, await database.AuthoringReceipts.CountAsync());
        Assert.Equal(ProcessAuthoringOperationState.Committed, (await recoveryClient.GetAuthoringOperationAsync(query)).State);
    }

    private sealed class ContextFactory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    private sealed class CommitFault(bool afterCommit) : DbTransactionInterceptor {
        public bool Armed { get; set; }
        public bool Reached { get; private set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            if (!afterCommit) {
                ThrowOnce();
            }
            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (afterCommit) {
                ThrowOnce();
            }
            return Task.CompletedTask;
        }

        private void ThrowOnce() {
            if (!Armed) {
                return;
            }
            Armed = false;
            Reached = true;
            throw new TimeoutException("Owned authoring COMMIT fault.");
        }
    }
}
