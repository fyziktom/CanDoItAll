# Critical Failure Points

## UI Blind Spots

- Outbox records can be pending, retrying, leased, or dead-lettered, but Process Workspace does not show those states.
- Dispatcher attempt count, max attempts, retry reason, and next retry action are not first-class UI state.
- Missing artifact summaries are embedded in transition reasons and logs rather than exposed as a per-step artifact obligation ledger.
- Active execution summaries omit stranded/dead-lettered automation when no active AgentFramework run exists.
- Manual step controls can transition states, but there is no specialized "retry agent step" or "retry with recovery directive" action.

## Artifact Transfer Failure Points

- AgentFramework artifact path missing: generic artifacts are skipped; if required, the process can later block without showing the skipped path as a UI diagnostic.
- Artifact unreadable: generic artifacts log a warning and continue; required artifact satisfaction is indirect.
- Storage placement failure: projection can throw, then dispatcher marks the step `Failed`; UI only receives failed step state and decision/conformance summaries.
- Mock artifact mismatch: deterministic mock projection throws for no match or multiple matches, which fails the step.
- Artifact auto-projection from response text can satisfy some expectations, but the UI does not explain that the process artifact came from response text rather than a file.
- Duplicate `ExternalReferenceKey` prevents repeat projection, but the UI does not show duplicate suppression or whether a previous artifact was reused.

## Agent Crash And Context Loss Failure Points

- Host restart can mark in-flight AgentFramework runs failed/cancelled. Process recovery can redispatch old in-progress steps, but the operator cannot see the recovery classification.
- If dispatcher catches an execution exception and marks the step `Failed`, the outbox can still complete because the dispatch service swallowed the exception after recording failure. UI needs process-health state, not just outbox-health state.
- Failed steps are terminal for automatic recovery worker scans. A user needs a deliberate retry/rerun workflow with reason and updated instructions.
- Context window loss is only addressed indirectly through fresh chat retries and recovery prompt text. There is no durable compact recovery context visible to users.
- Recovery prompt guidance is implementation-heavy and hidden inside dispatcher code. Operators cannot inspect or adjust the next attempt instructions.

## Outbox And Worker Failure Points

- Outbox dead-letter records have `LastError`, attempts, and status, but no Process Workspace read model.
- Process recovery worker logs failures and continues, but there is no UI health indicator for repeated recovery scan failures.
- In-memory step dispatch guards do not protect across multiple app processes. Outbox leases and execution adoption reduce duplicate work but do not give operators a conflict view.
- Lease renewal failure is logged; operator state is not updated.
- A record can be pending with a future `NextAttemptAtUtc`; the UI cannot distinguish normal backoff from a stuck run.

## Validation Gaps

- The deterministic E2E test proves backend service/outbox behavior, not UI launch/observe/interact behavior.
- Current component tests cover authoring and some workspace behaviors, but not agent process run diagnostics and recovery controls.
- Existing Playwright tests do not cover process run with agents.
