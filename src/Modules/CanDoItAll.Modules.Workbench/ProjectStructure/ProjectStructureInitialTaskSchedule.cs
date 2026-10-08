using System.Text.Json.Serialization;
using System.ComponentModel;
using CanDoItAll.Components.Gantt;

namespace CanDoItAll.Modules.Workbench;

[Description("Initial planned interval for a canonical task whose stored start and end are both null. Read the node's dates and duration first, send all five members, and use this instead of scheduleChange. A changed snapshot is rejected as StaleTask; dependency constraints still apply.")]
public sealed record ProjectStructureInitialTaskSchedule(
    [property: JsonRequired, Description("Stored start from nodes[].startUtc in the latest canonical structure read. Required and must be null for initial scheduling; a different persisted value rejects the write.")] DateTimeOffset? CurrentStartUtc,
    [property: JsonRequired, Description("Stored end from nodes[].endUtc in the latest canonical structure read. Required and must be null for initial scheduling; a different persisted value rejects the write.")] DateTimeOffset? CurrentEndUtc,
    [property: JsonRequired, Description("Stored nullable duration in seconds from nodes[].durationSeconds. Required, including explicit null; compared exactly with the persisted value.")] int? CurrentDurationSeconds,
    [property: JsonRequired, Description("Requested first planned start as an ISO 8601 instant with an offset; required and earlier than proposedEndUtc.")] DateTimeOffset ProposedStartUtc,
    [property: JsonRequired, Description("Requested first planned end as an ISO 8601 instant with an offset; required and later than proposedStartUtc.")] DateTimeOffset ProposedEndUtc) {
    public void Validate() {
        if (CurrentStartUtc.HasValue || CurrentEndUtc.HasValue) {
            throw new ArgumentException("Initial scheduling requires a task with no persisted start or end; use scheduleChange for an existing interval.");
        }
        if (ProposedEndUtc <= ProposedStartUtc) {
            throw new ArgumentException("The initial planned end must be later than its start.");
        }
    }

    public void RequireCurrentState(GanttTaskId taskId, DateTimeOffset? startUtc, DateTimeOffset? endUtc, int? durationSeconds) {
        if (startUtc != CurrentStartUtc || endUtc != CurrentEndUtc || durationSeconds != CurrentDurationSeconds) {
            throw new ProjectStructureGanttMutationException(ProjectStructureGanttMutationErrorCode.StaleTask,
                $"Task '{ProjectStructureGanttMutationConventions.Mask(taskId.Value)}' changed its schedule since the caller's read.");
        }
    }
}
