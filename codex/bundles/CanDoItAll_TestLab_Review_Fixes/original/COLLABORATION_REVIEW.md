# Collaboration follow-up: source review and closure decision

Review date: 2026-09-28. Repository `fyziktom/CanDoItAll`, branch `components-decoupling`, HEAD `97989b9d13b9a6fa16280da98ec5b005a2f968c7`.

## Decision

The specific Collaboration corrections requested in the previous review are present, coherent with the existing seam and supported by meaningful new test code. **Proceed to TestLab instead of scheduling another Collaboration extraction/fix-only bundle.** This is a source-review conclusion, not a fresh runtime certification. A separately identified conversation-shell disposal race remains in the current shared host and is the narrow S0 prerequisite in [the next prompt](prompt.md).

The reviewed HEAD is a merge. Its first parent is the signed Collaboration correction commit `f50c958c3ac9df2ec9df43a76963a8753178735f`; the other is development commit `5a27c2cd14bd27868aa7712da24ba817c0f6644b`. The correction's parent is `67413ee3a44c765cae7047576cf4be864b2ddb42`. GitHub returned valid signature verification for the correction and merge commits. The source review uses files from the **merge HEAD**, not only the pre-merge patch. [P01–P02](SOURCES.md#p01)

## Resolution of the previous findings

| Finding | Current implementation and test evidence | Review status |
|---|---|---|
| R1: automatic unread/refresh realignment drops an unsent reply | `CollaborationReplyPolicy.MustRetain` considers locking, modified `EditContext` and nonempty message text. Realignment retains the original target while that draft needs retention. `HandleReplyInput` captures changes before blur. Tests hold reconciliation with both an empty next list and B still visible, verify draft/context/validation identity, and prove explicit navigation or Clear still works. [C02–C05, C07–C09](SOURCES.md#c02) | Source-supported correction; no new blocking defect found in this path. |
| R2a: sandbox drops a dispatched reply after navigation | The scenario applies the accepted write to its original in-memory thread before suppressing stale **view** effects. Tests switch A -> B and A -> B -> A, check one durable fake message per admitted write, and protect the successor's text, busy state and notifications. Disposal releases owned pending waits. [C06, C09](SOURCES.md#c06) | Correct separation of fake storage from active UI state. |
| R2b: late sandbox create overrides section/filter intent | Effective section/filter changes advance intent generation even if the same thread remains visible. Repeated no-op setters do not pretend to be new intent. Tests cover both axes, both effective/no-op cases and persisted created identity. [C06, C09](SOURCES.md#c06) | Source-supported correction. |
| R3: disabled Mark-read button mistaken for persistence completion | The browser first requires the selected target to be ready and unread; after clicking it waits for that same target's ready/read snapshot, then checks owner state. A controlled real-renderer test holds the write and subsequent read separately and proves disabled/old-ready is insufficient. [C08, C10](SOURCES.md#c08) | The original weak barrier has been replaced. |
| C1: missing real escalation creation and draft transition proof | Production browser code now creates an escalation through the real create form, checks the selected thread and owner message/type, visits Escalations and the durable link, and covers dirty selection transitions. Seeded sandbox data is no longer offered as a substitute for this create path. [C10](SOURCES.md#c10) | Source coverage added. |

These checks retain the existing contract/UI/module/sandbox architecture rather than introduce a new state framework. The corrected retention policy is shared at the UI layer; the scenario does not acquire a production backend dependency to reproduce it.

## What was actually reported versus independently verified

The repository boundary record reports this focused correction run: [C01](SOURCES.md#c01)

| Recorded lane | Recorded passing cases |
|---|---:|
| Collaboration workspace session | 35 |
| Lightweight renderer/scenario tests | 25 |
| Production host/layout and controlled real-form reconciliation | 8 |
| DbContext compatibility | 5 |
| Integration | 7 |
| Production and sandbox browser journeys | 2 |
| **Reported total** | **82** |

The record states zero failures/skips for that final selection. It also describes five successful affected builds, PostgreSQL 18 provenance, source-mode graph/watch checks, static enforcement and cleanup. Those are **the implementation author's historical reports**. This reviewer did not run them and did not receive the ignored local TRX files, screenshots or timing logs. The record was written before the final correction commit and subsequent merge; it is not automatically fresh runtime evidence for the merge HEAD.

The Actions query for the merge SHA returned `total_count: 0`. Current CI is triggered on main/development pushes and relevant PRs, not every feature-branch push. Absence of such runs does not disprove a local test run, but it also provides no independent CI pass for this commit. [P03, G05](SOURCES.md#p03)

The review independently inspected the corrected source, deterministic session/real-form/scenario regression bodies, production browser assertions and current repository rules. It also checked package integrity separately. No C# build, application test, live browser, SQL read-back or dev-loop benchmark was performed in the review environment.

## S0: remaining shared-shell defect

The correction report itself records a production-only diagnostic failure involving `ObjectDisposedException` in `ConversationShellHost.InitializeContributorsAsync`. A previous generic unhandled-rendering failure was not conclusively attributed. The final successful paired run does not prove this intermittent lifetime defect is gone. [C01](SOURCES.md#c01)

The current shell source independently permits the documented disposed-source path: [S01](SOURCES.md#s01)

```text
Initialize contributors -> await first contributor
Dispose host -> cancel and dispose lifetime source
First contributor returns despite cancellation
Loop attempts next contributor using lifetime.Token
Token getter can throw because its source has already been disposed
```

The loop lacks a fresh host-lifetime check before that next dispatch. The initialization task is discarded, and queued event callbacks deserve a disposal check at the point where they execute. Microsoft documents both component reentrancy across incomplete awaits and the disposed-source Token getter exception. [E01–E03](SOURCES.md#e01)

This is a **confirmed source-level lifetime defect with a matching implementation report**, not a new execution by the reviewer. It can affect any production route hosting the shared shell, including the next TestLab browser proof. The existing shell tests cover catalog routing and focused-window behavior with immediately completing contributors; they do not exercise delayed initialization across disposal. [S02](SOURCES.md#s02)

The next bundle authorizes only a small lifecycle repair and deterministic regression, isolated from the TestLab extraction. Do not silence the log, skip shutdown validation, remove contributors or refactor their runtimes. Test cancellation-ignoring and cancellation-observing completion, genuine live failures and queued events. Keep disposal nonblocking and resource ownership correct.

## Why not hold the next module indefinitely?

The prior Collaboration-specific findings are resolved in source. The remaining defect is in a shared host, not in Collaboration's contract split or backend-free sandbox. A bounded prerequisite plus the next coherent module avoids both hiding the defect and repeatedly reopening a completed extraction. The final production proof remains mandatory; blocked proof must be reported separately rather than converted to success.
