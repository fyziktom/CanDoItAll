# SB14 Semantic Invariants

## INV-SB14-001 Autonomous Production Process

- Raw note: agents must finish the multiteam process without external help; the observer-manager only runs and reads analytics.
- Expected behavior: automation dispatch completes the Tetris process with bound provider-backed agent runs, current-run receipts, and artifact lineage.
- Disallowed shallow implementation: manual transitions/rework, harness-written product source, detached chat/provider tests, or stale receipts.
- Failing-first proof: incident evidence in the supplied bundles plus `bundle://proof/SB14/transcripts/process-api.txt` if a new defect is reproduced.
- Passing proof: `bundle://proof/SB14/transcripts/process-api.txt` and `bundle://proof/SB14/transcripts/agent-api.txt`.
- Production assertions: exact run/step/execution ids and provider usage observations.
- Red-team negative case: `bundle://proof/SB14/red-team-review.md`.
- Downstream dependency: final bundle closure.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative case |
|---|---|---|---|---|
| Agent execution run | production process dispatcher | process step result handling | scheduled dispatch through terminal state | detached provider test is rejected |
| Tool receipt/artifact | bound agent execution | completion gates and downstream steps | current execution/run/step lineage | stale or manually seeded receipt is rejected |

## Closure Result

- Result: `Passed`.
- Root process run: `4749e033-4326-4b58-acdf-61a5cf372563`, terminal `Completed`, zero diagnostics.
- Hierarchy: seven process runs, all `Completed`, zero diagnostics.
- Agent execution: 42 terminal process-bound executions, seven agents, `OpenAI chat completions`, model `gpt-5.4-mini`, zero pending approvals.
- Observer boundary: no manual transition, approval, rework, dispatch repair, cancellation, or Tetris source edit was performed.
- Current-run evidence: root and screenshot-child snapshot/console artifacts are clean; final screenshot shows the working Tetris UI without a fatal banner.
- Escalation invariant: the real QA defect took `repair-required`; the final run did not select `repair-escalation`.
