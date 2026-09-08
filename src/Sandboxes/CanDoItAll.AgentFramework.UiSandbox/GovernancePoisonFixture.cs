using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.UiSandbox;

public static class GovernancePoisonFixture {
    public static ImmutableArray<string> Sentinels { get; } = [
        "governance-poison-input", "governance-poison-result", "governance-poison-approval",
        "governance-poison-arguments", "governance-poison-timeline", "governance-poison-exit",
        "governance-poison-state", "governance-poison-metadata", "governance-poison-directory",
        "governance-poison-path", "governance-poison-source"
    ];

    public static ExecutionRunDetail Create(ExecutionRunRecord? source = null) {
        var at = new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero);
        var run = (source ?? new(GovernanceSandboxFixture.Run1, GovernanceSandboxFixture.AgentA, null,
            "Governance poison fixture", "manual", "", "", "", "", "", "{}", "", "", "Fixture", "fixture-model",
            ExecutionState.Completed, RunOutcome.Succeeded, at, at, null, null, "", null, [])) with {
            InputSummary = Sentinels[0], ResultSummary = Sentinels[1], MetadataJson = Sentinels[7],
            SerializedSessionStateJson = Sentinels[6], StructuredOutputRawOutput = Sentinels[6],
            StructuredOutputValidationErrorsJson = Sentinels[6], SourceId = Sentinels[10],
            ProcessRunId = Sentinels[10], ProcessStepId = Sentinels[10],
            PendingApprovals = [new("pending", "call", "Review tool", "tool", Sentinels[2], Sentinels[3])]
        };
        return new(run, null,
            [new(Guid.Parse("9bc86ef0-1180-4852-a88e-01c778409001"), run.AgentId, null, at,
                ExecutionState.Completed, "Execution", Sentinels[4]) { ExecutionRunId = run.Id }], []) {
            Approvals = [new("approval", run.Id, "call", "Review tool", "tool", Sentinels[2], Sentinels[3],
                ExecutionApprovalStatus.Approved, at, at, "manual", "", Sentinels[2])],
            Artifacts = [new(Guid.Parse("9bc86ef0-1180-4852-a88e-01c778409002"), run.Id, "text", "Private artifact",
                "  /" + Sentinels[9] + "/output.txt  ", "text/plain", "Fixture", Sentinels[6], at)],
            ToolReceipts = [new(Guid.Parse("9bc86ef0-1180-4852-a88e-01c778409003"), run.Id,
                "workspace", "Review tool", "Read", "manual", Sentinels[6], Sentinels[6],
                "/" + Sentinels[8], Sentinels[5], at, at)],
            Checkpoints = [new(Guid.Parse("9bc86ef0-1180-4852-a88e-01c778409004"), run.Id,
                Sentinels[6], Sentinels[6], "Saved", ExecutionState.Completed, [Sentinels[3]],
                at, null, Sentinels[6], Sentinels[6], Sentinels[6], Sentinels[6], Sentinels[6],
                Sentinels[6], Sentinels[6], Sentinels[6], Sentinels[6])]
        };
    }
}
