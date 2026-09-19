using System.Text.Json;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProjectStructureTaskUpdateAgentInputTests
{
    private const string TaskId = "custom:0c7d1f2a9b8e4c6d8a1b2c3d4e5f6a7b";

    [Fact]
    public void A_model_shaped_update_binds_and_builds_the_Gantt_contract_with_its_task_and_schedule()
    {
        var function = CreateTool();
        var arguments = ToArguments(CreateJson(proposedEnd: "2026-09-23T17:00:00Z"));

        Assert.False(MafToolArgumentBindingFailureMapper.TryCreatePreInvocationFailure(function, arguments, out var failure),
            failure?.Message);
        var input = ((JsonElement)arguments["request"]!).Deserialize<ProjectStructureTaskUpdateAgentInput>(function.JsonSerializerOptions)!;
        var request = input.ToRequest();

        Assert.Equal(TaskId, request.TaskId.Value);
        var schedule = Assert.IsType<GanttTaskScheduleChangeRequest>(request.ScheduleChange);
        Assert.Equal(request.TaskId, schedule.TaskId);
        Assert.Equal(GanttScheduleGesture.SetInterval, schedule.Gesture);
        var moved = Assert.Single(schedule.AffectedTasks);
        Assert.Equal(request.TaskId, moved.TaskId);
        Assert.Equal(DateTimeOffset.Parse("2026-09-21T09:00:00Z"), moved.ProposedStart);
        Assert.Equal(DateTimeOffset.Parse("2026-09-23T17:00:00Z"), moved.ProposedEnd);
        Assert.Null(request.CurrentCostBasis);
    }

    [Theory]
    [InlineData(" ", "2026-09-23T17:00:00Z")]
    [InlineData(TaskId, "2026-09-20T17:00:00Z")]
    public void An_update_the_Gantt_contract_rejects_is_a_correctable_failure_with_no_effect(string taskId, string proposedEnd)
    {
        var function = CreateTool();
        var arguments = ToArguments(CreateJson(proposedEnd, taskId));
        var input = ((JsonElement)arguments["request"]!).Deserialize<ProjectStructureTaskUpdateAgentInput>(function.JsonSerializerOptions)!;

        var rejected = Assert.Throws<ProjectStructureAgentException>(() => input.ToRequest());

        Assert.Equal("TaskUpdateRequestInvalid", rejected.ErrorCode);
        Assert.True(rejected.IsSafeToExpose);
        Assert.True(rejected.CanRetryWithCorrectedInput);
        Assert.Equal(AgentToolEffectState.None, rejected.EffectState);
        Assert.DoesNotContain("(Parameter", rejected.Message, StringComparison.Ordinal);
    }

    private static AIFunction CreateTool()
        => AIFunctionFactory.Create(
            (Guid projectId, ProjectStructureTaskUpdateAgentInput request) => request.TaskId,
            ProjectStructureToolPolicy.ProjectTaskUpdate);

    private static AIFunctionArguments ToArguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new AIFunctionArguments(document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone()));
    }

    private static string CreateJson(string proposedEnd, string taskId = TaskId)
        => $$"""
            {
              "projectId": "99d218dc-701a-4fac-9305-2e040f1fb3a7",
              "request": {
                "taskId": "{{taskId}}",
                "currentTitle": "Deterministic game loop",
                "proposedTitle": "Deterministic game loop",
                "currentProgressPercent": -1,
                "proposedProgressPercent": 0,
                "currentEstimate": { "expectedEffortHours": 16, "expectedEffortUnit": "Hours", "expectedCostAmount": null, "expectedCostCurrencyCode": "" },
                "proposedEstimate": { "expectedEffortHours": 16, "expectedEffortUnit": "Hours", "expectedCostAmount": null, "expectedCostCurrencyCode": "" },
                "scheduleChange": {
                  "gesture": "SetInterval",
                  "affectedTasks": [{
                    "taskId": "{{TaskId}}",
                    "previousStart": "2026-09-18T09:00:00Z",
                    "previousEnd": "2026-09-19T17:00:00Z",
                    "proposedStart": "2026-09-21T09:00:00Z",
                    "proposedEnd": "{{proposedEnd}}",
                    "isCritical": false
                  }]
                },
                "assigneeChanged": false,
                "proposedAssignee": null,
                "currentExecution": { "state": "NotStarted", "actualStartedAtUtc": null, "actualEndedAtUtc": null },
                "proposedExecution": { "state": "NotStarted", "actualStartedAtUtc": null, "actualEndedAtUtc": null },
                "currentCostBasis": null,
                "currentDirectAssignmentRevision": 0
              }
            }
            """;
}
