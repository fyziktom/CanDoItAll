using System.Net;
using System.Net.Http.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Api;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessRootCancellationAdmissionApiTests;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class ProcessRootCancellationAdmissionWorkerTests {
    [Theory]
    [InlineData(ReadBasisMode.StepSnapshot)]
    [InlineData(ReadBasisMode.RunSnapshot)]
    public async Task HTTP_returns_while_a_real_child_is_locked_then_the_registered_worker_finishes_original_accounting(ReadBasisMode mode) {
        await using var environment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var profile = environment.CreatePostgreSqlProfile("primary");
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, environment, profile, freezeClock: false);
        await using var host = await ApiTestHost.CreateAsync(true, sharedTestEnvironment: environment, sharedActiveProfile: profile,
            apiConfiguration: ProcessRootCancellationAdmissionApiTests.Configuration());
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ExecuteProcesses]);
        var services = fixture.Services;
        var layout = new FileSandboxWorkspaceStorageLayout(services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            WorkspaceScopeDescriptor.Organization(fixture.Entry.Files.Sql.Project.DatabaseProfileId.ToString("N")));
        var key = Assert.Single(FileSandboxWorkspaceWriteSet.Run(null, fixture.Waiting with { ChatSessionId = null }, readsAgent: false).LockPaths(layout));
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var token = deadline.Token;
        var original = await File.ReadAllBytesAsync(layout.RunPath(fixture.Waiting.Id), token);
        IAsyncDisposable? blocker = await new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory())
            .AcquireCoordinationAsync(layout.RootPath, key, TimeSpan.FromSeconds(1), false, token);
        try {
            using var response = await host.Client.PostAsync(Route(fixture.State.RunId.Value), null, token);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var admitted = (await response.Content.ReadFromJsonAsync<ProcessCancellationAdmissionApiView>(token))!;
            Assert.NotNull(admitted.BarrierCommandId);
            await using var inspection = Context(services);
            while (!await inspection.CancellationRecoveries.AsNoTracking().AnyAsync(item => item.LeaseGeneration > 0, token)) {
                await Task.Delay(25, token);
            }
            var pending = (await host.Client.GetFromJsonAsync<ProcessCancellationApiView>(Route(fixture.State.RunId.Value), token))!;
            Assert.Equal(nameof(ProcessCancellationTracking.Pending), pending.Tracking);
            Assert.Equal(admitted.BarrierCommandId, pending.Recovery!.BarrierCommandId);
            Assert.Equal(original, await File.ReadAllBytesAsync(layout.RunPath(fixture.Waiting.Id), token));
            await blocker.DisposeAsync();
            blocker = null;
            ProcessCancellationApiView completed;
            do {
                await Task.Delay(25, token);
                completed = (await host.Client.GetFromJsonAsync<ProcessCancellationApiView>(Route(fixture.State.RunId.Value), token))!;
            } while (completed.Tracking != nameof(ProcessCancellationTracking.Completed));
            Assert.Equal(nameof(ProcessRuntimeStatus.Cancelled), completed.RuntimeStatus);
            Assert.Equal(admitted.BarrierCommandId, completed.Recovery!.BarrierCommandId);
            var run = (await fixture.Entry.Files.Store.GetExecutionRunAsync(fixture.Waiting.Id, token))!;
            Assert.Equal(RunOutcome.Cancelled, run.Outcome);
            var child = await inspection.DecisionChildren.AsNoTracking().SingleAsync(token);
            var allocation = await inspection.DecisionBudgetAllocations.AsNoTracking().SingleAsync(
                item => item.BudgetId == child.BudgetId && item.AllocationId == child.AllocationId, token);
            Assert.Equal(run.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks, allocation.ConsumedWorkTicks);
            Assert.NotNull(child.OwnerRevision);
            Assert.Equal(child.OwnerRevision, allocation.OwnerWorkRevision);
            Assert.Equal(0, (await new EfProcessDecisionBudgetStore(inspection).ReadAsync(fixture.State.RunId, null, token))!.ReservedWorkTicks);
            var receipts = await inspection.IdempotencyKeys.CountAsync(token);
            using var replay = await host.Client.PostAsync(Route(fixture.State.RunId.Value), null, token);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal(receipts, await inspection.IdempotencyKeys.CountAsync(token));
            Assert.Single(fixture.Entry.Wire.Requests);
            await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
        } finally {
            if (blocker is not null) {
                await blocker.DisposeAsync();
            }
        }
    }
}
