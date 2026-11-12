using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.SchedulerPlanner;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class SchedulerAgentRuntimeToolProviderTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scheduler_does_not_invent_an_acknowledgement_when_the_owner_does_not_return(bool lostAfterSave) {
        var workflowId = Guid.NewGuid();
        var owner = new RecordingSchedulerPlannerService(new SchedulerPlannerWorkspace([], [],
            [new(SchedulerPlanTargetKind.Workflow, workflowId, Guid.NewGuid(), "Scheduled workflow", "Fixture target.", "Active")],
            new CanvasCalendarSurface { SurfaceId = "schedule-acknowledgement" })) {
            RejectBeforeSave = !lostAfterSave, LoseAcknowledgement = lostAfterSave
        };
        var harness = CreateHarness(owner);
        var tool = Assert.IsAssignableFrom<AIFunction>((await harness.Provider.CreateToolsAsync(harness.Context, default))
            .Single(item => item.Name == SchedulerToolPolicy.SchedulerWorkflowScheduleCreate));
        using var capture = AgentToolInvocationEffectScope.Begin();
        await Assert.ThrowsAsync<IOException>(() => tool.InvokeAsync(new AIFunctionArguments {
            ["request"] = new SchedulerWorkflowScheduleCreateInput(workflowId, "Acknowledged schedule", "0 0 15 ? * MON-FRI", "UTC")
        }).AsTask());
        Assert.Equal(lostAfterSave, owner.SavedEditor is not null);
        Assert.Null(capture.CommittedEffect);
    }

    [Fact]
    public async Task Scheduler_acknowledges_exact_committed_plan_before_propagating_projection_failure() {
        var workflowId = Guid.NewGuid();
        var owner = new RecordingSchedulerPlannerService(new SchedulerPlannerWorkspace([], [],
            [new(SchedulerPlanTargetKind.Workflow, workflowId, Guid.NewGuid(), "Scheduled workflow", "Fixture target.", "Active")],
            new CanvasCalendarSurface { SurfaceId = "schedule-known-commit" })) { KnownCommitFollowupFailure = true };
        var harness = CreateHarness(owner);
        var tool = Assert.IsAssignableFrom<AIFunction>((await harness.Provider.CreateToolsAsync(harness.Context, default))
            .Single(item => item.Name == SchedulerToolPolicy.SchedulerWorkflowScheduleCreate));
        using var capture = AgentToolInvocationEffectScope.Begin();
        var failure = await Assert.ThrowsAsync<SchedulerPlanCommittedException>(() => tool.InvokeAsync(new AIFunctionArguments {
            ["request"] = new SchedulerWorkflowScheduleCreateInput(workflowId, "Acknowledged schedule", "0 0 15 ? * MON-FRI", "UTC")
        }).AsTask());
        Assert.Equal(new AgentToolCommittedEffect("scheduler-plan", owner.SavedPlanId.ToString("D")), capture.CommittedEffect);
        Assert.Equal(owner.SavedPlanId, failure.Fact.PlanId);
        Assert.Null(failure.Fact.Plan.NextPlannedFireAtUtc);
    }

    private static void AssertOwnerAcknowledgement<T>(string toolName, T result, AgentToolCommittedEffect? actual) {
        AgentToolCommittedEffect? expected = toolName == SchedulerToolPolicy.SchedulerWorkflowScheduleCreate
            ? new("scheduler-plan", Assert.IsType<SchedulerWorkflowScheduleCreateResult>(result).PlanId.ToString("D"))
            : null;
        Assert.Equal(expected, actual);
    }
}
