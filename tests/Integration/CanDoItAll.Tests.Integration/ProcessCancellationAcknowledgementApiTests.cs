using System.Data.Common;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Integration.Api;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessCancellationAcknowledgementApiTests {
    [Fact]
    public async Task HTTP_lost_commit_acknowledgement_returns_uncertainty_and_GET_recovers_the_original_barrier() {
        BarrierAcknowledgementLoss loss = new();
        await using var host = await ApiTestHost.CreateAsync(true,
            services => services.ConfigureDbContext<ProcessPersistenceDbContext>((_, options) => options.AddInterceptors(loss, new BarrierFlush(loss))),
            apiConfiguration: ProcessRootCancellationAdmissionApiTests.Configuration());
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ExecuteProcesses]);
        await using var context = Context(host.App.Services);
        var initial = await new EfProcessRuntimeUnitOfWork(context).CommitAsync(ProcessProjectAdmissionFixture.Initial());
        Assert.True(initial.CommittedNow);
        loss.RunId = initial.State.RunId.Value;
        var route = ProcessRootCancellationAdmissionApiTests.Route(loss.RunId);
        using var response = await host.Client.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(route, response.Headers.Location?.OriginalString);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(ProcessRootCancellationAdmissionStatus.Unconfirmed.ToString(),
            (await response.Content.ReadFromJsonAsync<ProcessCancellationAdmissionApiView>())!.Status);
        Assert.Equal(1, loss.Failures);
        var barrier = (await context.CancellationRecoveries.AsNoTracking().SingleAsync()).BarrierCommandId;
        Assert.NotNull(barrier);
        var progress = (await host.Client.GetFromJsonAsync<ProcessCancellationApiView>(route))!;
        Assert.Contains(progress.Tracking, new[] { nameof(ProcessCancellationTracking.Pending), nameof(ProcessCancellationTracking.Completed) });
        Assert.Equal(barrier, progress.Recovery!.BarrierCommandId);
        Assert.Equal(1, await context.RuntimeEvents.CountAsync(item => item.ActorId == "process-review-manager"));
    }

    private sealed class BarrierAcknowledgementLoss : DbTransactionInterceptor {
        internal Guid RunId { get; set; }
        internal int Failures;
        internal ConcurrentDictionary<Guid, byte> FlushedContexts { get; } = new();
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (eventData.Context is { } context && FlushedContexts.TryRemove(context.ContextId.InstanceId, out _) &&
                    Interlocked.CompareExchange(ref Failures, 1, 0) == 0) {
                throw new IOException("The original barrier committed before its acknowledgement was lost.");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class BarrierFlush(BarrierAcknowledgementLoss loss) : SaveChangesInterceptor {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (eventData.Context is { } context && context.ChangeTracker.Entries<ProcessRuntimeStateEntity>().Any(entry =>
                    entry.Entity.RunId == loss.RunId && entry.Entity.Status == ProcessRuntimeStatus.CancelRequested)) {
                loss.FlushedContexts.TryAdd(context.ContextId.InstanceId, 0);
            }
            return ValueTask.FromResult(result);
        }
    }
}
