using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentPlanningDecisionExpectation {
    public AgentPlanningDecisionExpectation(Guid executionRunId, long runRevision, long journalRevision,
        Guid decisionId, int generation) {
        if (executionRunId == Guid.Empty || runRevision < 1 || journalRevision < 1 || decisionId == Guid.Empty || generation < 1) {
            throw new ArgumentException("A planning action requires the exact displayed run, journal and decision generation.");
        }
        ExecutionRunId = executionRunId;
        RunRevision = runRevision;
        JournalRevision = journalRevision;
        DecisionId = decisionId;
        Generation = generation;
    }

    public Guid ExecutionRunId { get; }
    [JsonConverter(typeof(AgentHistoricalReceiptRevisionJsonConverter))]
    public long RunRevision { get; }
    [JsonConverter(typeof(AgentHistoricalReceiptRevisionJsonConverter))]
    public long JournalRevision { get; }
    public Guid DecisionId { get; }
    public int Generation { get; }

    public static AgentPlanningDecisionExpectation From(ExecutionRunRecord run) {
        ArgumentNullException.ThrowIfNull(run);
        var journal = run.ToolAdmission ?? throw new ArgumentException("The run has no tool admission.", nameof(run));
        journal.Validate();
        var decision = journal.PlanningDecision ?? throw new ArgumentException("The run has no planning decision.", nameof(run));
        return new(run.Id, run.Revision, journal.Revision, decision.DecisionId, decision.CurrentGeneration);
    }
}
