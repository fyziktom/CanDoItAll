using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Integration.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Fact]
    public async Task Historical_interactive_four_receipts_use_two_catalog_decodes_and_preserve_final_SQL_target_fence() {
        var clock = new NativeClock();
        await using var application = await TestApplication.CreateAsync(ToolProducerHarness(clock));
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var seed = await SeedHistoricalReceiptsAsync(services, clock, process: false);
        var originalFile = await HistoricalFileDigestAsync(seed.RunPath);
        var page = new HistoricalPage(services);
        AssertHistoricalAccepted(seed, page, await page.ReadAsync(seed), 2);
        Assert.Equal(originalFile, await HistoricalFileDigestAsync(seed.RunPath));
        await AssertHistoricalReceiptsUnchangedAsync(services, seed);

        var projects = services.GetRequiredService<ProjectsService>();
        var afterAccess = new HistoricalPage(services);
        ProjectWriteAdmission? replacement = null;
        afterAccess.Sql.BeforeFinalTarget = async token => {
            afterAccess.Sql.BeforeFinalTarget = null;
            await new ProjectLifecycleTestDriver(services).DeleteAsync(seed.Owner.Project.ProjectId);
            Assert.True((await projects.CreateAsync(seed.Owner.Project.ProjectId, new() { Name = "Replaced after receipt access" })).IsSuccess);
            replacement = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(seed.Owner.Project.ProjectId, token);
        };
        var refused = await afterAccess.ReadAsync(seed);
        Assert.NotNull(replacement);
        Assert.NotEqual(seed.Owner.Project.LifetimeId, replacement.LifetimeId);
        Assert.Equal(1, afterAccess.Sql.FinalTargets);
        Assert.NotEmpty(afterAccess.Receipts.Observations);
        Assert.All(afterAccess.Receipts.Observations, observation => Assert.Equal(AgentHistoricalReceiptKnowledge.ReadUnavailable, observation.Knowledge));
        Assert.Equal(AgentHistoricalReceiptReviewStop.ReadDenied, refused.Stop);
        Assert.Empty(refused.Observations);
        Assert.Null(refused.NextCursor);
        Assert.Equal(originalFile, await HistoricalFileDigestAsync(seed.RunPath));
    }

    [Fact]
    public async Task Historical_Process_four_receipts_select_actual_original_source_and_use_three_or_two_catalog_decodes() {
        var clock = new NativeClock();
        var harness = ToolProducerHarness(clock);
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
            harness.ConfigureServices!(services);
            services.AddScoped(provider => new EfProcessExecutionAuthorityQuery(
                provider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>(), clock));
        } });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        foreach (var sharedExecutor in new[] { false, true }) {
            clock.Now = DateTimeOffset.UtcNow;
            var seed = await SeedHistoricalReceiptsAsync(services, clock, process: true, sharedExecutor: sharedExecutor);
            Assert.Equal(sharedExecutor, seed.Run.AgentId == seed.Owner.Agent.Id);
            var source = ProjectDefinitionToolProposalCodec.Read(seed.Run.ToolAdmission!.Batches[0].Proposals[0].Payload).Source;
            var dispatch = Assert.IsType<ProcessExecutionDispatchAuthority>(source.Process);
            var principal = Assert.IsType<CanDoItAll.Processes.Runtime.ProcessLaunchPrincipal.AgentExecution>(dispatch.SourceAuthority!.Principal);
            Assert.Equal(seed.Owner.Agent.Id, principal.Ceiling.AgentId);
            Assert.Equal(seed.Run.AgentId, dispatch.Evidence.ExecutorAgentId);
            var originalReader = services.GetRequiredService<IProcessOriginalReceiptAuthorityReader>();
            var active = await originalReader.ObserveOriginalForReceiptReadAsync(dispatch.Evidence);
            Assert.NotNull(active.Snapshot);
            Assert.True(active.Snapshot.ObservedCurrentDispatch);
            await using (var database = seed.Owner.Context()) {
                var claim = await database.DispatchClaims.AsNoTracking().SingleAsync(item => item.ClaimToken == dispatch.Evidence.DispatchClaimToken);
                clock.Now = claim.ExpiresAtUtc.AddSeconds(1);
            }
            await seed.Owner.RevokeAsync(Revocation.Write);
            var expired = await originalReader.ObserveOriginalForReceiptReadAsync(dispatch.Evidence);
            Assert.NotNull(expired.Snapshot);
            Assert.False(expired.Snapshot.ObservedCurrentDispatch);
            var originalFile = await HistoricalFileDigestAsync(seed.RunPath);
            var page = new HistoricalPage(services);
            AssertHistoricalAccepted(seed, page, await page.ReadAsync(seed), sharedExecutor ? 2 : 3);
            Assert.Equal(originalFile, await HistoricalFileDigestAsync(seed.RunPath));
            await AssertHistoricalReceiptsUnchangedAsync(services, seed);
            await seed.Owner.RevokeAsync(Revocation.Read);
            var denied = new HistoricalPage(services);
            var result = await denied.ReadAsync(seed);
            Assert.Equal(AgentHistoricalReceiptReviewStop.ReadDenied, result.Stop);
            Assert.Empty(result.Observations);
            Assert.Null(result.NextCursor);
            Assert.Empty(denied.Receipts.Claims);
            Assert.Equal(sharedExecutor ? 1 : 2, denied.CatalogBytes.Count);
        }
    }

    [Fact]
    public async Task Historical_catalog_change_after_real_receipt_and_unexpected_source_after_seal_discard_the_entire_page() {
        var clock = new NativeClock();
        await using var application = await TestApplication.CreateAsync(ToolProducerHarness(clock));
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var seed = await SeedHistoricalReceiptsAsync(services, clock, process: false);
        var originalCatalog = await File.ReadAllBytesAsync(seed.CatalogPath);
        var originalRun = await HistoricalFileDigestAsync(seed.RunPath);
        var changed = new HistoricalPage(services, async (page, _, token) => {
            if (page.Receipts.Observations.Count != 1) {
                return;
            }
            Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, page.Receipts.Observations[0].Knowledge);
            var document = JsonNode.Parse(originalCatalog)!.AsObject();
            var revision = document["catalogDataRevision"]!.ToJsonString();
            var agent = document["agents"]!.AsArray().Single(item => item!["id"]!.GetValue<Guid>() == seed.Run.AgentId)!.AsObject();
            agent["configurationJson"] = AgentProjectStructureAccessMetadata.Write(agent["configurationJson"]!.GetValue<string>(), new());
            Assert.Equal(revision, document["catalogDataRevision"]!.ToJsonString());
            await File.WriteAllTextAsync(seed.CatalogPath, document.ToJsonString(), token);
        });
        try {
            var result = await changed.ReadAsync(seed);
            Assert.Equal(AgentHistoricalReceiptReviewStop.SnapshotChanged, result.Stop);
            Assert.Empty(result.Observations);
            Assert.Null(result.NextCursor);
            Assert.Null(result.RunRevision);
            Assert.Equal(4, changed.Receipts.Observations.Count);
            Assert.Equal(4, changed.Sql.FinalTargets);
            Assert.Equal(2, changed.CatalogBytes.Count);
            Assert.All(changed.Receipts.Observations, observation => Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, observation.Knowledge));
            var fresh = new HistoricalPage(services);
            Assert.Equal(AgentHistoricalReceiptReviewStop.ReadDenied, (await fresh.ReadAsync(seed)).Stop);
            Assert.Empty(fresh.Receipts.Claims);
        } finally {
            await File.WriteAllBytesAsync(seed.CatalogPath, originalCatalog);
        }

        var unselected = Guid.NewGuid();
        var unexpected = new HistoricalPage(services, async (page, _, token) => {
            Assert.Single(page.Receipts.Observations);
            await page.Basis.GetOriginalProcessSourceAsync(unselected, token);
        });
        var denied = await unexpected.ReadAsync(seed);
        Assert.Equal(AgentHistoricalReceiptReviewStop.ReadDenied, denied.Stop);
        Assert.Empty(denied.Observations);
        Assert.Null(denied.NextCursor);
        Assert.Equal(1, unexpected.CatalogBytes.Count);
        Assert.Equal(1, unexpected.Sql.FinalTargets);
        Assert.Equal(originalRun, await HistoricalFileDigestAsync(seed.RunPath));
        Assert.Throws<InvalidOperationException>(() => unexpected.Basis.RequireCurrent());
        await AssertHistoricalReceiptsUnchangedAsync(services, seed);
    }

    [Fact]
    public async Task Historical_real_receipt_cancellation_drains_and_concurrent_pages_do_not_share_catalog_basis_or_claims() {
        var clock = new NativeClock();
        await using var application = await TestApplication.CreateAsync(ToolProducerHarness(clock));
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var first = await SeedHistoricalReceiptsAsync(services, clock, process: false);
        var second = await SeedHistoricalReceiptsAsync(services, clock, process: false);
        var firstFile = await HistoricalFileDigestAsync(first.RunPath);
        var secondFile = await HistoricalFileDigestAsync(second.RunPath);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var drained = false;
        using var cancellation = new CancellationTokenSource();
        var cancelling = new HistoricalPage(services, async (page, claim, token) => {
            Assert.Single(page.Receipts.Observations);
            Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, page.Receipts.Observations[0].Knowledge);
            Assert.Equal(first.Run.Id, (await claim.RequireAsync(token)).Request.ExecutionRunId);
            using var registration = token.Register(() => cancelled.TrySetResult());
            entered.TrySetResult();
            try {
                await release.Task;
                token.ThrowIfCancellationRequested();
            } finally {
                drained = true;
            }
        });
        var pending = cancelling.ReadAsync(first, cancellation.Token);
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.False(drained);
        } finally {
            release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        var stopped = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(drained);
        Assert.Equal(AgentHistoricalReceiptReviewStop.Cancelled, stopped.Stop);
        Assert.Empty(stopped.Observations);
        Assert.Null(stopped.NextCursor);
        Assert.Throws<InvalidOperationException>(() => cancelling.Basis.RequireCurrent());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Assert.Single(cancelling.Receipts.Claims).RequireAsync().AsTask());

        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        async ValueTask PauseFirst(HistoricalPage page, AgentToolHistoricalReceiptReadClaim claim, CancellationToken token) {
            if (page.Receipts.Observations.Count != 1) {
                return;
            }
            await claim.RequireAsync(token);
            if (Interlocked.Increment(ref arrived) == 2) {
                both.TrySetResult();
            }
            await resume.Task.WaitAsync(token);
        }
        var left = new HistoricalPage(services, PauseFirst);
        var right = new HistoricalPage(services, PauseFirst);
        var leftRead = left.ReadAsync(first);
        var rightRead = right.ReadAsync(second);
        try {
            await both.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotSame(left.Basis, right.Basis);
            Assert.Equal(first.Run.AgentId, left.Basis.RequireSelected(first.Run.AgentId).Id);
            Assert.Equal(second.Run.AgentId, right.Basis.RequireSelected(second.Run.AgentId).Id);
            Assert.Throws<AgentToolReceiptAccessDeniedException>(() => left.Basis.RequireSelected(second.Run.AgentId));
            Assert.Throws<AgentToolReceiptAccessDeniedException>(() => right.Basis.RequireSelected(first.Run.AgentId));
            Assert.False(leftRead.IsCompleted);
            Assert.False(rightRead.IsCompleted);
        } finally {
            resume.TrySetResult();
            await Task.WhenAll(leftRead, rightRead).WaitAsync(TimeSpan.FromSeconds(10));
        }
        AssertHistoricalAccepted(first, left, await leftRead, 2);
        AssertHistoricalAccepted(second, right, await rightRead, 2);
        Assert.Equal(firstFile, await HistoricalFileDigestAsync(first.RunPath));
        Assert.Equal(secondFile, await HistoricalFileDigestAsync(second.RunPath));
        Assert.All(left.Receipts.Claims, claim => Assert.DoesNotContain(claim, right.Receipts.Claims));

        var runtime = Assert.IsType<DatabaseRuntimeState>(services.GetRequiredService<IDatabaseRuntimeState>());
        var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var before = runtime.GetSnapshot();
        var advance = true;
        var longLived = new HistoricalPage(services, (_, _, _) => {
            if (advance) {
                advance = false;
                runtime.PublishRestartObserved(before, profile);
            }
            return ValueTask.CompletedTask;
        });
        var interrupted = await longLived.ReadAsync(first);
        var retired = longLived.Basis;
        Assert.Equal(AgentHistoricalReceiptReviewStop.ContextChanged, interrupted.Stop);
        Assert.Equal(new DatabaseProfileGeneration(before.Generation), interrupted.CurrentContext.Generation);
        Assert.Empty(interrupted.Observations);
        Assert.Null(interrupted.NextCursor);
        var next = await longLived.ReadAsync(first);
        AssertHistoricalAccepted(first, longLived, next, 2);
        Assert.NotSame(retired, longLived.Basis);
        Assert.Equal(profile.Profile.Id, next.CurrentContext.ProfileId);
        Assert.Equal(new DatabaseProfileGeneration(before.Generation + 1), next.CurrentContext.Generation);
        Assert.Throws<InvalidOperationException>(() => retired.RequireCurrent());
        Assert.Equal(firstFile, await HistoricalFileDigestAsync(first.RunPath));
        await AssertHistoricalReceiptsUnchangedAsync(services, first);
        await AssertHistoricalReceiptsUnchangedAsync(services, second);
    }
}
