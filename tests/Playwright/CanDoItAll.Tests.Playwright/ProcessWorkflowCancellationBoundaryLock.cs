using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Playwright;

internal sealed class ProcessWorkflowCancellationBoundaryLock(WorkflowDbContext context, IDbContextTransaction transaction,
    IDbContextFactory<WorkflowDbContext> factory, int backendId) : IAsyncDisposable {
    internal static async Task<ProcessWorkflowCancellationBoundaryLock> AcquireAsync(IServiceProvider services, WorkflowRunId runId) {
        var factory = services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
        var context = await factory.CreateDbContextAsync();
        try {
            context.Database.SetCommandTimeout(5);
            var transaction = await context.Database.BeginTransactionAsync();
            var requests = await context.Database.SqlQuery<Guid>($"""
                SELECT "Id" AS "Value" FROM "AgentFramework_WorkflowExternalRequests"
                WHERE "RunId" = {runId.Value} AND "RespondedAtUtc" IS NULL ORDER BY "Id" LIMIT 2 FOR UPDATE
                """).ToArrayAsync();
            Assert.Single(requests);
            var backendId = await context.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
            return new(context, transaction, factory, backendId);
        } catch {
            await context.DisposeAsync();
            throw;
        }
    }

    internal async Task WaitForCancellationAsync() {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var observer = await factory.CreateDbContextAsync(deadline.Token);
        observer.Database.SetCommandTimeout(5);
        while (!await observer.Database.SqlQuery<bool>($"""
                SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database()
                    AND {backendId} = ANY(pg_blocking_pids(pid)) AND query LIKE '%AgentFramework_WorkflowExternalRequests%') AS "Value"
                """).SingleAsync(deadline.Token)) {
            await Task.Delay(50, deadline.Token);
        }
    }

    public async ValueTask DisposeAsync() {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }
}
