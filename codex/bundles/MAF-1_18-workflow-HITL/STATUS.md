# Bundle Status

**Overall:** Prepared  
**Current wave:** Not started  
**Current subbundle:** SB00  
**Preparation baseline:** `5cdf1666dbafdcea975909101c1854773f5f3556`  
**Execution HEAD:** Not recorded  
**Last update:** 2026-08-20

| Subbundle | Title | Proof tier | Status | Dependency state |
|---|---|---:|---|---|
| SB00 | Re-anchor and baseline | Standard | Prepared | Ready |
| SB01 | MAF 1.18 package and compile migration | Standard | Prepared | Blocked by SB00 |
| SB02 | Agent/tool safety regressions | Behavioral | Prepared | Blocked by SB01 |
| SB03 | Native MAF workflow request/checkpoint foundation | Governed | Prepared | Blocked by SB02 |
| SB04 | Persistent checkpoint and response recovery state machine | Governed | Prepared | Blocked by SB03 |
| SB05 | Authorized and idempotent workflow HITL API | Governed | Prepared | Blocked by SB04 |
| SB06 | End-to-end proof, documentation, and frozen broad gate | Governed | Prepared | Blocked by SB05 |

## Active blockers

None at preparation time.

## Re-anchor record

Not executed. SB00 must record:

- repository path;
- branch;
- HEAD;
- clean or mixed worktree state;
- repository instruction files;
- installed .NET SDK;
- package source availability;
- baseline restore/build status;
- focused baseline test discovery and results;
- deviations from the preparation evidence.

## Decision log

| Decision | State | Rationale |
|---|---|---|
| Keep tool invocation serial by default | Accepted | Ordering and side effects are not generally commutative. |
| Upgrade and HITL in one bundle, separate waves | Accepted | 1.18 is small enough to share discovery, but HITL requires an independent review boundary. |
| Do not enable declaration-only tool storage experiment | Accepted | It is opt-in/experimental and unrelated to required behavior. |
| Use native MAF request ports and JSON checkpoints | Accepted | Exception-as-pause cannot rehydrate a disposed run. |
| Preserve `IsDurable = false` for in-process backend | Accepted | Persisted checkpoints do not create a durable orchestration host. |
| Exactly-once response acceptance, deduplicated side effects | Accepted | Arbitrary external side effects cannot be made exactly once by checkpointing alone. |
