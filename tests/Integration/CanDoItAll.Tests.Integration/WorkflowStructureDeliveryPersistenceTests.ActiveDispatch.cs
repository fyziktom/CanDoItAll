using System.Data.Common;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class WorkflowStructureDeliveryPersistenceTests {
    [Theory]
    [InlineData(WorkflowRunState.Running)]
    [InlineData(WorkflowRunState.Completed)]
    [InlineData(WorkflowRunState.Failed)]
    public async Task Automatic_asset_delivery_does_not_take_dispatch_from_an_active_workflow(WorkflowRunState state) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app, prepareOutput: false);
        var request = fixture.Request with {
            ObjectType = ProjectObjectType.File,
            ObjectSubtype = "json",
            Media = new("active-dispatch.json", "application/json", Convert.ToBase64String("{\"enabled\":true}"u8.ToArray()))
        };
        var plan = fixture.Plan with { Kind = WorkflowStructureOutputKind.Asset };
        plan = plan with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(plan, request) };
        request = BindRequest(request, plan, fixture.Request.WorkflowMutationAdmission!.Authority);
        var runs = fixture.Services.GetRequiredService<IWorkflowRunStore>();
        var run = (await runs.GetRunAsync(plan.Identity.Occurrence.RunId))! with { State = state };
        await runs.SaveRunAsync(run);
        var outputs = OutputStore(fixture.Services);
        var prepared = await outputs.PrepareAsync(plan);
        var intent = new StoragePlacementIntentId(prepared.StoragePlacementIntentId!.Value);
        var storage = fixture.Services.GetRequiredService<StorageStablePlacementService>();
        var pause = new PausePreparedContribution();
        var owner = Owner(fixture.Services, Factory(fixture.Services, pause));
        var creating = owner.CreateWorkflowContributionAsync(plan, request, storagePlacementIntentId: intent.Value);
        try {
            await pause.Prepared.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.NotNull(await fixture.Owner.FindPreparedWorkflowContributionAsync(plan.Identity));
            Assert.Null(await storage.FindAsync(intent));
            using var delivery = ActivatorUtilities.CreateInstance<ProjectStructureWorkflowDeliveryWorker>(app.Services);
            await delivery.RunBatchAsync();
            var observed = await storage.FindAsync(intent);
            if (state == WorkflowRunState.Running) {
                Assert.Null(observed);
                Assert.Null(await fixture.Owner.FindWorkflowContributionAsync(plan.Identity));
            } else {
                Assert.Equal(StorageStablePlacementState.Completed, observed?.State);
                Assert.NotNull(await fixture.Owner.FindWorkflowContributionAsync(plan.Identity));
            }
        } finally {
            pause.Release.TrySetResult();
            await creating;
        }
        Assert.Equal(StorageStablePlacementState.Completed, (await storage.FindAsync(intent))!.State);
        Assert.Equivalent(run, await runs.GetRunAsync(run.RunId));
        await using var database = await fixture.Factory.CreateDbContextAsync();
        Assert.Single(await database.Set<ProjectObjectRecord>().Where(row => row.ProjectId == fixture.ProjectId && row.Title == request.Title).ToListAsync());
    }

    private sealed class PausePreparedContribution : DbTransactionInterceptor {
        internal TaskCompletionSource Prepared { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int commits;

        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (Interlocked.Increment(ref commits) == 1) {
                Prepared.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
        }
    }
}
