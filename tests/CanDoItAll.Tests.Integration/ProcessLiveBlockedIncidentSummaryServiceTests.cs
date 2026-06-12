using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessLiveBlockedIncidentSummaryServiceTests
{
    [Fact]
    public void BuildEscalationIncidentCards_collapses_duplicate_subprocess_blocks_and_uses_leaf_execution_cause()
    {
        var service = new ProcessLiveBlockedIncidentSummaryService();
        var now = DateTimeOffset.UtcNow;
        var rootRunId = Guid.NewGuid();
        var childRunId = Guid.NewGuid();
        var leafRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var childStepRunId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var runs = new[]
        {
            CreateRun(rootRunId, definitionId, parentRunId: null, hierarchyDepth: 0, "Main delivery run", blockedStepCount: 1, now.AddMinutes(-1)),
            CreateRun(childRunId, definitionId, rootRunId, hierarchyDepth: 1, "Implementation slice subprocess", blockedStepCount: 1, now.AddMinutes(-1)),
            CreateRun(leafRunId, definitionId, childRunId, hierarchyDepth: 2, "Implementation subprocess", blockedStepCount: 0, now)
        };
        var executionDetails = new[]
        {
            new ExecutionRunDetail(
                CreateExecutionRun(
                    leafRunId,
                    now,
                    """
                    {"status":"Blocked","reason":"The step required a runtime validation tool that is not available in the current toolset."}
                    """),
                ChatSession: null,
                ExecutionLog: [],
                Metrics: [])
        };
        var cards = service.BuildEscalationIncidentCards(
            [
                CreateEscalation(rootRunId, definitionId, stepRunId, now.AddMinutes(-2), ProcessEscalationStatus.ReworkRequested),
                CreateEscalation(rootRunId, definitionId, stepRunId, now.AddMinutes(-1), ProcessEscalationStatus.Open),
                CreateEscalation(childRunId, definitionId, childStepRunId, now, ProcessEscalationStatus.Open)
            ],
            runs,
            executionDetails);

        var card = Assert.Single(cards);
        Assert.Equal(ProcessEscalationStatus.Open, card.Status);
        Assert.Equal(rootRunId, card.RunId);
        Assert.Equal("Blocked subprocess needs attention", card.Title);
        Assert.StartsWith("escalation-incident:", card.Key, StringComparison.Ordinal);
        Assert.Contains("runtime validation tool", card.Reason);
        Assert.Contains("Next action:", card.Reason);

        var coveredRunIds = service.ResolveBlockedRunIdsCoveredByEscalations(cards, runs);
        Assert.Contains(rootRunId, coveredRunIds);
        Assert.Contains(childRunId, coveredRunIds);
        Assert.Contains(leafRunId, coveredRunIds);
    }

    [Fact]
    public void BuildRunHealthSummary_explains_blocked_run_without_blocked_steps()
    {
        var service = new ProcessLiveBlockedIncidentSummaryService();
        var run = CreateRun(
            Guid.NewGuid(),
            Guid.NewGuid(),
            parentRunId: null,
            hierarchyDepth: 0,
            "Feature subprocess",
            blockedStepCount: 0,
            DateTimeOffset.UtcNow);

        var summary = service.BuildRunHealthSummary(
            run,
            activeSummary: null,
            runEscalations: [],
            childRunsByParentRunId: new Dictionary<Guid, IReadOnlyList<ProcessRunListItem>>(),
            executionRunDetails: []);

        Assert.Contains("No step is currently marked blocked", summary);
        Assert.Contains("blocking branch", summary);
    }

    private static ProcessLiveEscalationCard CreateEscalation(
        Guid runId,
        Guid definitionId,
        Guid stepRunId,
        DateTimeOffset updatedAtUtc,
        ProcessEscalationStatus status)
    {
        return new ProcessLiveEscalationCard(
            Key: Guid.NewGuid().ToString("N"),
            RunId: runId,
            DefinitionId: definitionId,
            DefinitionName: "Generic process",
            RunName: "Main delivery run",
            RunStatus: ProcessRunStatus.Blocked,
            EscalationId: Guid.NewGuid(),
            StepRunId: stepRunId,
            StepTitle: "Run child subprocess",
            Kind: ProcessEscalationKind.BlockedStep,
            Severity: ProcessEscalationSeverity.Moderate,
            Status: status,
            Title: "Blocked step needs operator review",
            Reason: "Subprocess run 'Implementation subprocess' is blocked.",
            Owner: string.Empty,
            SourceExecutionRunId: string.Empty,
            SourceApprovalId: string.Empty,
            SourceToolName: string.Empty,
            CreatedAtUtc: updatedAtUtc.AddMinutes(-1),
            UpdatedAtUtc: updatedAtUtc,
            DueAtUtc: updatedAtUtc.AddHours(4),
            ManagerAgentId: null,
            ManagerAgentName: string.Empty);
    }

    private static ProcessRunListItem CreateRun(
        Guid runId,
        Guid definitionId,
        Guid? parentRunId,
        int hierarchyDepth,
        string name,
        int blockedStepCount,
        DateTimeOffset updatedAtUtc)
    {
        return new ProcessRunListItem(
            runId,
            definitionId,
            ProcessDefinitionVersionId: Guid.NewGuid(),
            parentRunId,
            ParentStepRunId: null,
            RootRunId: parentRunId ?? runId,
            hierarchyDepth,
            ProjectId: null,
            name,
            ProcessRunStatus.Blocked,
            ProcessOperatingMode.AssistedExecution,
            ManagerAgentId: null,
            ManagerAgentName: string.Empty,
            CompletedStepCount: 3,
            TotalStepCount: 6,
            blockedStepCount,
            CapabilityGapCount: 0,
            EstimatedCost: 0m,
            ActualCost: 0m,
            updatedAtUtc);
    }

    private static ExecutionRunRecord CreateExecutionRun(
        Guid processRunId,
        DateTimeOffset updatedAtUtc,
        string resultSummary)
    {
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Execution",
            SourceKind: "process-step",
            SourceId: processRunId.ToString("D"),
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: string.Empty,
            ResultSummary: resultSummary,
            ProviderName: "test",
            Model: "test",
            State: ExecutionState.Completed,
            Outcome: RunOutcome.Succeeded,
            CreatedAtUtc: updatedAtUtc.AddMinutes(-1),
            UpdatedAtUtc: updatedAtUtc,
            StartedAtUtc: updatedAtUtc.AddMinutes(-1),
            CompletedAtUtc: updatedAtUtc,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: processRunId.ToString("D"));
    }
}
