using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.AgentFramework.WorkflowProcessCancellationFixture;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowProcessCancellationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_stop_keeps_original_request_and_siblings_and_repeated_stop_observes_one_commit(bool legacy) {
        await using var app = await TestApplication.CreateAsync();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        WorkflowProcessRunId process = new(Guid.NewGuid());
        var run = await RunAsync(services, process, legacy: legacy);
        var request = await RequestAsync(services, run);
        var sibling = await RunAsync(services, new(Guid.NewGuid()));
        var siblingRequest = await RequestAsync(services, sibling);
        var owner = Owner(services);
        Assert.Equal(run.RunId, await owner.FindNextAsync(process, null, default));
        Assert.Null(await owner.FindNextAsync(process, run.RunId, default));
        for (var attempt = 0; attempt < 2; attempt++) {
            Assert.Equal(WorkflowProcessCancellationStatus.Closed, await owner.StopAsync(process, run.RunId, default));
        }
        await using var database = Context(services);
        Assert.Equal(WorkflowRunState.Cancelled, (await database.Set<WorkflowRunRecordEntity>().SingleAsync(item => item.RunId == run.RunId.Value)).State);
        var retained = await database.Set<WorkflowExternalRequestRecordEntity>().SingleAsync(item => item.Id == request.Id.Value);
        Assert.Equal(Now, retained.RespondedAtUtc);
        Assert.Equal(string.Empty, retained.ResponseJson);
        Assert.Equal(1, await database.Set<WorkflowEventRecordEntity>().CountAsync(item => item.RunId == run.RunId.Value && item.Kind == WorkflowEventKind.Cancelled));
        Assert.Equal(WorkflowRunState.WaitingForInput, (await database.Set<WorkflowRunRecordEntity>().SingleAsync(item => item.RunId == sibling.RunId.Value)).State);
        Assert.Null((await database.Set<WorkflowExternalRequestRecordEntity>().SingleAsync(item => item.Id == siblingRequest.Id.Value)).RespondedAtUtc);
    }

    [Fact]
    public async Task Active_resume_lease_remains_pending_until_expiry_then_cancels_the_original_operation() {
        await using var app = await TestApplication.CreateAsync();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        WorkflowProcessRunId process = new(Guid.NewGuid());
        var run = await RunAsync(services, process);
        var request = await RequestAsync(services, run);
        var operation = await ClaimAsync(services, run, request);
        Assert.Equal(WorkflowProcessCancellationStatus.Pending, await Owner(services).StopAsync(process, run.RunId, default));
        await using var database = Context(services);
        Assert.False(await database.Set<WorkflowEventRecordEntity>().AnyAsync(item => item.Kind == WorkflowEventKind.Cancelled));
        Assert.Null((await database.Set<WorkflowExternalRequestRecordEntity>().AsNoTracking().SingleAsync()).RespondedAtUtc);
        Assert.Equal(WorkflowProcessCancellationStatus.Closed,
            await Owner(services, new Clock(Now.AddMinutes(2))).StopAsync(process, run.RunId, default));
        var retained = await database.Set<WorkflowExternalResponseOperationEntity>().AsNoTracking().SingleAsync();
        Assert.Equal(operation.Id.Value, retained.Id);
        Assert.Equal((int)WorkflowExternalResponseOperationState.Cancelled, retained.State);
        Assert.Null(retained.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_is_observed_from_the_same_run_without_replaying_an_effect() {
        await using var app = await TestApplication.CreateAsync();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        WorkflowProcessRunId process = new(Guid.NewGuid());
        var run = await RunAsync(services, process);
        await RequestAsync(services, run);
        var fault = new LostAcknowledgement();
        await Assert.ThrowsAsync<IOException>(() => Owner(services, interceptor: fault).StopAsync(process, run.RunId, default));
        Assert.Equal(1, fault.Commits);
        Assert.Equal(WorkflowProcessCancellationStatus.Closed, await Owner(services).StopAsync(process, run.RunId, default));
        await using var database = Context(services);
        Assert.Equal(1, await database.Set<WorkflowEventRecordEntity>().CountAsync(item => item.Kind == WorkflowEventKind.Cancelled));
        Assert.Equal(1, await database.Set<WorkflowExternalRequestRecordEntity>().CountAsync());
    }

    [Theory]
    [InlineData(true, WorkflowProcessCancellationStatus.Pending)]
    [InlineData(false, WorkflowProcessCancellationStatus.ReconciliationRequired)]
    public async Task Active_registry_signal_does_not_certify_a_durable_terminal_state(bool supportsCancellation, WorkflowProcessCancellationStatus expected) {
        await using var app = await TestApplication.CreateAsync();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        WorkflowProcessRunId process = new(Guid.NewGuid());
        var run = await RunAsync(services, process);
        Assert.True(services.GetRequiredService<IWorkflowActiveRunRegistry>().TryRegister(run.RunId, supportsCancellation, default, out var lease));
        using (lease) {
            Assert.Equal(expected, await Owner(services).StopAsync(process, run.RunId, default));
            Assert.Equal(supportsCancellation, lease.IsCancellationRequested);
        }
        await using var database = Context(services);
        Assert.Equal(WorkflowRunState.WaitingForInput, (await database.Set<WorkflowRunRecordEntity>().SingleAsync()).State);
        Assert.False(await database.Set<WorkflowEventRecordEntity>().AnyAsync(item => item.Kind == WorkflowEventKind.Cancelled));
    }

    [Theory]
    [InlineData(InvalidEvidence.WrongProcess)]
    [InlineData(InvalidEvidence.WrongScope)]
    [InlineData(InvalidEvidence.WrongProcessScope)]
    [InlineData(InvalidEvidence.WrongProfile)]
    [InlineData(InvalidEvidence.MalformedOrigin)]
    [InlineData(InvalidEvidence.OversizedOrigin)]
    [InlineData(InvalidEvidence.LegacyBoundary)]
    [InlineData(InvalidEvidence.MissingRequest)]
    [InlineData(InvalidEvidence.AmbiguousRequest)]
    public async Task Invalid_original_evidence_is_refused_without_touching_the_waiting_run(InvalidEvidence evidence) {
        await using var app = await TestApplication.CreateAsync();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        WorkflowProcessRunId process = new(Guid.NewGuid());
        var run = await RunAsync(services, process, scope: evidence switch {
            InvalidEvidence.WrongScope => WorkspaceScopeDescriptor.Organization("other-profile"),
            InvalidEvidence.WrongProcessScope => WorkspaceScopeDescriptor.Process(Guid.NewGuid().ToString("D")),
            _ => null
        }, profileId: evidence == InvalidEvidence.WrongProfile ? Guid.NewGuid() : null);
        if (evidence != InvalidEvidence.MissingRequest) {
            await RequestAsync(services, run, native: evidence != InvalidEvidence.LegacyBoundary);
        }
        if (evidence == InvalidEvidence.AmbiguousRequest) {
            await RequestAsync(services, run);
        }
        await using var database = Context(services);
        if (evidence is InvalidEvidence.MalformedOrigin or InvalidEvidence.OversizedOrigin) {
            var origin = evidence == InvalidEvidence.MalformedOrigin ? "{" : new string('x', 32769);
            await database.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "AgentFramework_WorkflowRuns" SET "OriginJson" = {origin} WHERE "RunId" = {run.RunId.Value}""");
        }
        Assert.Equal(WorkflowProcessCancellationStatus.ReconciliationRequired,
            await Owner(services).StopAsync(evidence == InvalidEvidence.WrongProcess ? new(Guid.NewGuid()) : process, run.RunId, default));
        Assert.Equal(WorkflowRunState.WaitingForInput, (await database.Set<WorkflowRunRecordEntity>().AsNoTracking().SingleAsync()).State);
        Assert.False(await database.Set<WorkflowExternalRequestRecordEntity>().AnyAsync(item => item.RespondedAtUtc != null));
        Assert.False(await database.Set<WorkflowEventRecordEntity>().AnyAsync(item => item.Kind == WorkflowEventKind.Cancelled));
    }

    public enum InvalidEvidence { WrongProcess, WrongScope, WrongProcessScope, WrongProfile, MalformedOrigin, OversizedOrigin, LegacyBoundary, MissingRequest, AmbiguousRequest }
}
