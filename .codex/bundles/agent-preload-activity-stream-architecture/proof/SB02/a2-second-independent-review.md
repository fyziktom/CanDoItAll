# SB02 Second Independent A2 Review

## Decision

- Result: `Fail`
- Date: `2026-07-27`
- Proof tier reviewed: `Governed`
- Downstream authorization: **Denied; SB03 remains blocked**
- Reviewed source state: the source and test hashes recorded in
  `bundle://proof/SB02/manifest.md` before the repairs required below.

## Blocking findings

| ID | Severity | Finding | Required closure evidence |
| --- | --- | --- | --- |
| A2-R01 | Blocker | Current-profile direct execution resolves the workspace service before coordinator admission. Cold workspace-root or service construction can therefore delay or fail before an `Accepted` activity exists. | Admit after confirming profile/scope identity but before workspace-service resolution. Prove that `Accepted` is readable while cold resolution is blocked and that resolution failure terminalizes and disposes the admitted operation. |
| A2-R02 | Blocker | The scope-lifetime compatibility dispatcher can deliver old-profile events that were queued before a profile switch. Detaching the upstream service does not clear or generation-fence its queued envelopes. | Add profile/generation fencing or a profile-owned relay lifetime. Prove that an old event queued behind a blocked subscriber cannot surface after the switch. |
| A2-R03 | Blocker | The Governed proof pack lacks command-level failing-first transcripts for the non-compatibility behavior changes. The initial source review is not a command transcript. | Capture legitimate red output against an identifiable pre-fix state or a controlled shallow mutant, followed by matching green output. Do not reconstruct command output from prose. |
| A2-R04 | Major | The invariant wording that every new production entry requires an operation identity is broader than the inspected workspace-execution boundary. `MafWorkflowLlmComponentInvoker` invokes `IAgentRuntime` directly and the runtime option remains nullable. | Narrow the invariant explicitly to workspace execution-run entry points and assign the workflow-runtime adapter to a later module-adapter boundary, or add an owned activity lifecycle to that caller. |

## Original finding recheck

| Original finding | Result |
| --- | --- |
| A2-F01 orphan ids/admission before I/O | **Fail** — raw Core admission is sound; current-profile cold resolution still precedes admission. |
| A2-F02 authorization/service-selection race | Pass for typed dispatch/readers. The compatibility-queue ownership defect is separately blocking. |
| A2-F03 replay from zero/gap semantics | Pass. |
| A2-F04 slow/throwing compatibility consumers | Pass for canonical outcome isolation. |
| A2-F05 typed context source/version | Pass. |
| A2-F06 proof completeness | **Fail** — paths and hashes verify, but mandatory failing-first evidence is absent. |

## Architecture observations

- Added stream/activity project dependency direction remains acyclic.
- All 77 recorded source/test/proof hashes matched the reviewed source state.
- All 154 durable `repo://` and `bundle://` references resolved.
- The 1,054-line current-profile facade is not a blocker by line count. Its concrete
  gate defects are admission order and compatibility mailbox lifetime.
- `ExecutionUpdated` remains a lossy compatibility projection and must not become the
  canonical UI or SSE source.

## Progression result

A2 failed. SB03 remains blocked until A2-R01 through A2-R04 are closed with durable
red/green evidence and a fresh independent re-review.
