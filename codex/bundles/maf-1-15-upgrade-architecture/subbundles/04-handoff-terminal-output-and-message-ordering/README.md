# SB04 — Handoff Terminal Output and Message Ordering

## Status

- `Ready after A2`

## Objective

Make the terminal handoff/workflow output authoritative on the real streaming runtime path while preserving intermediate activity, depth limits, message order, and persisted history.

## Success Criteria

- Direct inner non-streaming, inner streaming, depth-guard, and full-runtime fixtures are compared.
- Explicit terminal output is the authoritative machine/user result.
- Intermediate participant output remains available only as activity where intended.
- Tool call/result adjacency and reasoning/text order pass.
- Max handoff depth remains fail-closed.
- No workflow executes twice.
- Response and persisted history have a documented, compatible contract.
- Any removed merge workaround has failing-first proof.

## Covered Requirements

- R04, R10, R11, R18, R22

## Prerequisites

- A2 GO;
- deterministic 1.13 handoff fixture;
- MAF 1.15 package build;
- response snapshot/streaming code located.

## Exact Source References

- `MafHandoffWorkflowFactory.cs`
- `MafAgentRuntime.cs`
- provider streaming runner
- response snapshotter
- `MafRuntimeResponseAssembler.cs`
- workflow adapter tests
- activity/event sink

## Deliverables

- deterministic handoff fixture;
- authoritative terminal-output design;
- revised depth guard or workflow transition guard;
- activity/result separation;
- message order/adjacency tests;
- history comparison;
- finalizer trigger comparison;
- `proof/SB04/handoff-projection-decision.md`.

## Implementation Steps

1. Run the six-path characterization fixture.
2. Record raw update/event types and terminal metadata.
3. Confirm where MAF 1.15 internal history differs from caller-visible merge.
4. Select Design A, B, or C from the architecture analysis.
5. Implement the smallest supported projection without reflection or duplicate execution.
6. Move or preserve depth enforcement at a safe run-scoped boundary.
7. Ensure ordinary non-workflow agents retain their current streaming response behavior.
8. Ensure approvals/finalizers inspect the authoritative response.
9. Test response IDs, author names, usage, reasoning, and tool adjacency.
10. Compare persisted workflow history.
11. Remove only redundant merge transforms with failing-first proof.
12. Run cancellation/disposal/concurrency tests.
13. Record before/after finalizer repair rate.

## Do Not Do

- do not run a workflow twice;
- do not sort by timestamp;
- do not use reflection into MAF internals as permanent design;
- do not hide intermediate activity by deleting events;
- do not remove depth guard;
- do not treat every streamed participant message as terminal;
- do not rewrite finalizer governance.

## Acceptance Checklist

- [ ] all paths characterized
- [ ] terminal output authoritative
- [ ] activity retained
- [ ] no duplicate execution
- [ ] depth enforced
- [ ] tool/result adjacency
- [ ] reasoning/text order
- [ ] response/history contract
- [ ] ordinary agents unaffected
- [ ] finalizer semantics preserved
- [ ] concurrency/disposal pass

## Proof Tier

- `Governed`
- Runtime correctness and mutation safety.

## Proof Required

- Materialize every evidence path listed under `Deliverables`; do not leave proof only in chat or terminal scrollback.
- Record exact commands, exit codes, repository SHA, relevant environment details, and timestamps.
- Preserve failing-first evidence before the passing result whenever behavior changes.
- Hash persisted-state fixtures and redact secrets or sensitive payloads.
- Link the final proof from `reviews/01-execution-report.md`.

## Progression Gate

SB05 may proceed in parallel only for independent fixtures, but A3 cannot pass until SB04 is complete.

## Reopen Triggers

- workflow definitions change terminal outputs;
- activity event schema changes;
- MEAI version changes;
- new handoff tool/event shape appears;
- returned response diverges from history.

## Suggested Agent Prompt

```text
Implement SB04 only. Characterize direct and full streaming handoff paths, make one terminal workflow output authoritative without duplicate execution, preserve activity and max-depth enforcement, validate message/tool ordering and history, retain finalizer governance, and remove no workaround without failing-first proof.
```
