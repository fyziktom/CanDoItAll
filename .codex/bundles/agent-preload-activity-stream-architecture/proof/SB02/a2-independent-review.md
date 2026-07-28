# SB02 Initial Independent A2 Review

## Decision

- Result: `Fail`
- Date: `2026-07-27`
- Proof tier reviewed: `Governed`
- Downstream authorization: **Denied; SB03 remains blocked**

This file preserves the first independent result. Later repair evidence must not rewrite
this decision; a separate reviewer records any final decision in
`bundle://proof/SB02/a2-closure-gate.md`.

## Findings

| ID | Severity | Initial finding | Why it failed A2 | Candidate repair evidence now present |
| --- | --- | --- | --- | --- |
| A2-F01 | Blocker | Orphan operation ids and coordinator-bypassed direct execution paths | Several callers could mint an activity id merely to satisfy a request shape while raw Core/current-profile paths did not necessarily admit a canonical operation. Nullable coordinator/lease paths made “no activity producer” a valid production state. Identity without admission does not prove an activity stream. | Required coordinator/workspace identity, admitted raw/current-profile facades, non-null operation-bound methods, default-id pre-I/O rejection, and zero bypass patterns are cited in SA-06, SA-07, SA-10, SA-11 and the 52/52/static transcripts. |
| A2-F02 | Blocker | Profile authorization/service race | Authorization, organization-scope resolution, and workspace-service resolution could observe different current profiles. A profile switch between checks could authorize one partition and dispatch/read through another profile. | Double-checked reader authorization around profile subscription, profile-bound cancellation, confirmed scope/service resolution, and pinned dispatch tests are cited in SB02-INV-04. |
| A2-F03 | Blocker | Replay-from-zero contract was not actually proven | The requirement says replay begins at sequence zero. Treating the first event as sequence one without a valid zero cursor, or opening at `First`, permits a shallow implementation that reports an immediate gap or skips acceptance. | `StreamSequence.Beginning = 0`, zero-cursor special handling, blocked-context handle proof, contiguous sequences, and retention-gap tests are cited in SB02-INV-01/02. |
| A2-F04 | Blocker | A slow `ExecutionUpdated` subscriber could still block canonical work or later subscribers | Catching exceptions alone does not isolate latency. Sequential inline callback delivery lets one UI subscriber delay event-sink publication, runtime entry, activity terminalization, and other subscribers. | Per-subscriber bounded mailboxes, independent queued workers, explicit overflow logging, slow/throwing unit tests, and the 5/5 persistence/runtime integration transcript are cited in SB02-INV-06. |
| A2-F05 | Major | Activity lacked typed context source and version | Agent/session/run ids do not identify which module snapshot produced the operation. Without typed source plus version, downstream adapters/UI/SSE would infer context from messages or an object bag and could not detect stale context. | `AgentExecutionActivityContextIdentity`, single-assignment `BindContext`, and orchestrator binding of captured `AgentChatContextSource` plus snapshot version are cited in SB02-INV-05. |
| A2-F06 | Blocker | Governed proof was missing | No SB02 manifest, hashes, invariant contract, producer/consumer/lifecycle matrix, source assertions, architecture snapshot record, command transcripts, anti-stub audit, or durable A2 decision existed. Green local output in chat is not a Governed proof pack. | The proof pack now exists, but hashes and independent re-review remain pending; non-compatibility command-level failing-first evidence was not preserved. |

## Initial progression result

A2 failed. Repair work could continue inside SB02, but SB03 could not borrow trust from
the stream foundation. The closure reviewer must re-read every finding against final
source, tests, hashes, and transcripts; passing commands alone are insufficient.
