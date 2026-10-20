using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectTaskPlanPlanningContent {
    internal static AgentPlanningPromptContract Prompt { get; } = new(
        "Plan new flat manual tasks using the original request and supplied complete task basis. Return only the schema object. " +
        "Do not repeat existing tasks. Do not add assignments, dependencies, estimates, parent changes or executable commands.",
        "project_task_plan", """
        {"type":"object","additionalProperties":false,"properties":{"version":{"type":"integer","const":1},
        "tasks":{"type":"array","minItems":1,"maxItems":100,"items":{"type":"object","additionalProperties":false,
        "properties":{"title":{"type":"string","minLength":1,"maxLength":200},"description":{"type":"string"},
        "notes":{"type":"string"},"priority":{"type":"integer","minimum":0,"maximum":6}},
        "required":["title","description","notes","priority"]}}},"required":["version","tasks"]}
        """);

    internal static string Describe(ProjectTaskPlanCommand command)
        => $"Original project: {command.Basis.Project.Expectation.Project.ProjectId:D}\n\n" +
            string.Join("\n\n", command.Proposal.Tasks.Select((task, index) =>
                $"{index + 1}. {task.Title}\nDescription: {task.Description}\nNotes: {task.Notes}\nPriority: {task.Priority}"));

    internal static AgentPlanningResponsePreparation Prepare(Func<AgentToolPreparedCall> prepare) {
        try {
            return new AgentPlanningResponsePreparation.Publication(prepare());
        } catch (JsonException) {
            return new AgentPlanningResponsePreparation.Refused(AgentPlanningOutputRefusal.InvalidShape);
        } catch (ArgumentException) {
            return new AgentPlanningResponsePreparation.Refused(AgentPlanningOutputRefusal.LimitExceeded);
        } catch (ProjectTaskPlanBasisException exception) {
            return new AgentPlanningResponsePreparation.Refused(exception.Reason == ProjectTaskPlanBasisRefusal.LimitExceeded
                ? AgentPlanningOutputRefusal.LimitExceeded : AgentPlanningOutputRefusal.InvalidShape);
        }
    }
}
