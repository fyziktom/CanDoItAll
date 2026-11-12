using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

internal static class ProcessDecisionRefreshCaptureFixture {
    internal static async Task<RuntimeCommandId> AdmitAsync(ProjectTaskPlanReviewFixture fixture) {
        var selected = (await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>().ReadAsync(
            fixture.State.RunId, fixture.Request.Child.StepInstanceId, new(fixture.Waiting.Id)))!;
        var original = fixture.Request;
        ProcessDecisionRefreshRequest request = new(new(Guid.NewGuid()), original.Child, original.StrategyId, original.WaitingResultKey,
            original.Decision, original.Budget, selected.SharedScope, ProcessDecisionRefreshKind.ManagerRefresh);
        Assert.True((await fixture.Services.GetRequiredService<ProcessDecisionRefreshCoordinator>()
            .AdmitAsync(fixture.State, fixture.CommandContext, request)).CommittedNow);
        return request.CommandId;
    }

    internal static async Task<ProcessRuntimeStateSnapshot> AccountAsync(ProjectTaskPlanReviewFixture fixture, RuntimeCommandId admission) {
        Assert.Equal(ProcessDecisionChildStopResult.ReadyForAccounting, await Assert.Single(fixture.Services
            .GetServices<IProcessDecisionRefreshChildStopper>()).StopAsync(fixture.Parent, admission));
        var state = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
        return Assert.IsType<ProcessDecisionRefreshAccountingResult.Reconciled>(await fixture.Services
            .GetRequiredService<ProcessDecisionRefreshAccountingCoordinator>().ReconcileAsync(state, fixture.CommandContext, admission)).Commit.State;
    }
}
