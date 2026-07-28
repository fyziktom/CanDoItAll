# SB05 Production Anti-Stub Audit

## Scope

The audit covers the AgentFramework execution/preparation/provider/file-store path
and the Process application/projection/persistence path used by the A5 gate.

## Commands and result

The exact scans and classification are preserved in
`bundle://proof/SB05/transcripts/anti-stub-scan.md`.

- No `TODO`, `FIXME`, `HACK`, `NotImplementedException`, fixture-specific branch, or
  template-only marker was found.
- Seven `Task.CompletedTask`/`ValueTask.CompletedTask` lines were reviewed.
- Explicit `NotSupportedException` boundaries were reviewed rather than counted as
  implementations.

## Completion-task classification

| Site | Classification |
| --- | --- |
| cross-process lock releaser | Disposes the owned stream synchronously; completed `ValueTask` correctly represents no asynchronous cleanup |
| provider delete observer | Performs the immutable removal synchronously before returning |
| null checkpoint/event/governance services | Explicitly named optional policies; not used to claim typed activity, atomic persistence, or query behavior |
| buffered event sink | Enqueues and bounds the item synchronously before returning |

The typed agent activity producer is not a null sink: the coordinator admits a
partition, publishes `Accepted`, owns binding/transition rules, and terminalizes the
operation.

## Unsupported-boundary classification

- Non-PostgreSQL atomic process claim operations fail explicitly; they do not report a
  fake successful claim.
- Projection codecs throw for unsupported payload types.
- optional workspace capability defaults throw rather than silently emulating
  durability or provisioning.
- operation-bound execution guards reject unsupported/mismatched entry paths.

No unsupported boundary is used as positive proof for SB05.

## Fixture and production-flow audit

- Startup operation counts are diagnostics in the integration harness, not production
  state.
- Positive proof exercises production producers: activity admission, atomic store
  journals, provider loader/snapshot publication, and process batch APIs.
- Recovery tests inject failures at production commit stages and then construct a new
  store to recover. They do not seed the final recovered projection directly.
- No production branch checks a test class, fixture name, hard-coded test run ID, or
  expected sample count.

## Decision

Pass. No stub, template-only path, or fixture-specific branch is carrying an A5
acceptance claim.
