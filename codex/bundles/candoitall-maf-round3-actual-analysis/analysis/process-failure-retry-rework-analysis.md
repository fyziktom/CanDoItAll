# Process Failure, Retry, and Rework Analysis

## Does the system rerun the whole process?

No. The process engine does not rerun the whole process after an agent failure. It reruns or recovers the current process step. This is the right high-level approach.

The dispatch loop can pick up an in-progress step, inspect the latest automation execution run, and decide whether to retry the current step. A recovery worker scans active runs and triggers dispatch. Manual rerun also targets a specific blocked/failed agent-owned step.

## What happens after an agent failure?

There are several layers:

### 1. Structured output format repair

Inside workspace execution, invalid structured output may be repaired by extracting a valid JSON object from wrapped/prose output. This is bounded and revalidated. It is intentionally not semantic repair.

This is correct for format errors such as:

```text
Here is the result:
{ ...valid JSON... }
```

It is not appropriate for missing business fields, bad branches, missing proof tools, or incomplete implementation.

### 2. Failed run inspection

The process dispatcher catches failed agent execution, loads execution details, inspects tool receipts/outcome text, and computes completion status.

### 3. Step retry

The dispatcher retries the current step up to a bounded limit. It resets `automationChatSessionId` to `null`, which creates a fresh session for the next attempt.

This is generally good. Failed chat sessions can contain stale tool-call loops, hallucinated intermediate assumptions, or bad provider conversation state. The process state, artifacts, and receipts should be the source of truth, not the failed chat history.

### 4. Text recovery directive

The next attempt receives a text directive containing missing tools, critical failures, and a short prior summary. The directive is useful, but it is not a typed contract.

### 5. Tool carry-forward

The system carries forward some successful tool names across attempts. It intentionally excludes current-attempt proof tools for concrete implementation/browser proof cases. This is safe, but too coarse. A tool name does not prove that the old receipt is still valid after a change.

### 6. Provider fallback

Provider failures can switch assigned technical agents to fallback providers. The retry starts fresh and includes provider-repair guidance.

## What happens when QA returns work?

The process model can branch to repair paths, and manual rerun can request a fresh attempt. However, the repair context is still mainly text-based. There is no strong typed packet that carries:

- exact QA findings;
- affected artifacts/files;
- expected minimal change;
- previous implementation receipts;
- failed proof receipts;
- reusable proof receipts;
- proof requirements to rerun;
- prohibited actions such as regenerating the whole scaffold.

The missing piece is not another generic retry. It is a typed rework continuation.

## Correct efficient model

Use three levels of recovery:

### A. Format repair

- No new agent run.
- No new session.
- Only clean/extract/normalize the final JSON output.
- Revalidate immediately.

### B. Fresh step retry

- New MAF session.
- Used for provider failure, tool loop, poisoned context, missing required tools, invalid finalizer, or unrecoverable output.
- Includes durable process state, artifacts, receipts, and a compact recovery directive.

### C. Rework continuation

- New or controlled MAF session, not blind failed-session replay.
- Uses a persisted typed `AgentReworkPacket`.
- The agent repairs the existing work rather than solving the whole step again.
- Reruns only invalidated proof tools.
- Returns a finalizer/structured output that references the packet and repair evidence.

## Session recommendation

Do not generally reuse failed chat sessions. Keep fresh sessions for retries, but make them rich with typed durable context.

Use the same MAF session mainly for approval continuation, where MAF approval flow requires the approval/rejection content to be returned to the same session/conversation context.

## Rework packet should include

```csharp
public sealed class AgentReworkPacket
{
    public required Guid Id { get; init; }
    public required Guid ProcessRunId { get; init; }
    public required Guid StepRunId { get; init; }
    public required string SourceExecutionRunId { get; init; }
    public required AgentRecoveryMode RecoveryMode { get; init; }
    public required string FailureCategory { get; init; }
    public required string Objective { get; init; }
    public required IReadOnlyList<AgentReworkFinding> Findings { get; init; }
    public required IReadOnlyList<AgentReworkArtifactRef> ArtifactsToInspect { get; init; }
    public required IReadOnlyList<AgentToolReceiptRef> FailedToolReceipts { get; init; }
    public required IReadOnlyList<AgentProofRequirement> ProofsToRerun { get; init; }
    public required IReadOnlyList<AgentReusableProofRef> ReusableProofs { get; init; }
    public required IReadOnlyList<string> MinimalNextActions { get; init; }
    public required IReadOnlyList<string> ProhibitedActions { get; init; }
    public string? HumanDirective { get; init; }
}
```

All source-code comments must be in English.
