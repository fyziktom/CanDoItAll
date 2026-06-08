# Next Backlog Candidates And Reopen Triggers

## Status
- Subbundle: `SB059`
- Backlog decision: `Continue read-only path`.
- Runtime host registration candidate: `Blocked`.
- Execution-capable driver candidate: `Blocked`.
- Planning source: validation results through SB058.

## Validation Signals
- SB045 proves semantic adequacy and fake-proof resistance across the read-only verifier packages and corpus.
- SB048 proves runtime-host and execution-capable-driver documentation cannot imply approval.
- SB051 proves package source validation and dependency scans pass.
- SB054 proves bundle closure rows and critical proof cannot collapse into report-only claims.
- SB057 proves roadmaps deny premature runtime host and list explicit approval gates.
- SB058 decides production verification host registration is not ready and the next bundle must continue read-only adapters and projection planning.

## Candidate Backlog
| Candidate | Status | Scope | Required proof before implementation closes |
| --- | --- | --- | --- |
| Manager-visible read-only verification projection planning | `Ready for planning` | Define a read-only projection that consumes already-produced verifier responses for manager visibility only. No driver invocation, runtime-host state, scheduling, persistence of host state, workspace/storage writes, or process mutation. | Focused projection contract tests, no-mutation source scan, no runtime-host token scan, and report row proving it consumes existing responses only. |
| Read-only adapter hardening | `Ready for implementation` | Harden supplied-evidence boundaries, redaction summaries, no-mutation diagnostics, and adapter consistency without adding runtime host behavior. | Focused adapter tests, integration tests where relevant, source scan for external calls and mutation denial, and artifact-backed manifest. |
| Compatibility and descriptor guard hardening | `Ready for implementation` | Add compatibility tests, version-history checks, API snapshots, and migration notes for new descriptor families that remain verification-only. | Focused contract tests, public API hash updates, migration docs, and source scans proving Core and driver package boundaries remain clean. |
| Runtime-host approval pre-bundle | `Future approval candidate` | Design lifecycle ownership, audit persistence, sandbox boundary, command/external-call allow-list, approval/authorization, compatibility governance, and red-team proof before any registration work. | Dedicated approval bundle with failing-first and passing tests for every prerequisite, updated approval matrix, updated prerequisite status, source scans, and critical-gate manifest. |
| Production verification host registration | `Blocked` | Not eligible for the next bundle because prerequisites remain `Not satisfied`. | Only becomes eligible after the runtime-host approval pre-bundle completes and changes `architecture/10-runtime-host-approval-matrix.md` and `architecture/11-future-production-runtime-prerequisites.md` with proof. |
| Execution-capable driver contract line | `Blocked` | Not part of the current `v1.x verification-only alpha` line. | Only becomes eligible after sandbox, allow-lists, lifecycle, audit persistence, approval/authorization, compatibility governance, and red-team proof are complete in a future bundle. |

## Reopen Triggers
- Reopen SB059 if any backlog candidate changes `Runtime host registration candidate` from `Blocked` while prerequisites remain `Not satisfied`.
- Reopen SB059 if any backlog candidate changes `Execution-capable driver candidate` from `Blocked` without a separate execution-capable contract bundle.
- Reopen SB059 if manager-visible projection planning starts invoking drivers, scheduling work, registering services, persisting runtime-host state, writing workspace/storage, or mutating processes.
- Reopen SB059 if adapter hardening adds file reads, network calls, connector calls, package restore, shell execution, EF/storage writes, workspace writes, manager commands, scheduler hooks, workflow hooks, finalizer application, transition application, claim mutation, retry scheduling, provider repair, or process state mutation.
- Reopen SB059 if compatibility work changes public contract shape without updating `ProcessDriverContractVersion.Current`, public API snapshots, migration docs, focused tests, and source scans.
- Reopen SB059 if evidence-boundary work weakens supplied-content hash binding, approved URI enforcement, content type enforcement, bounded-size enforcement, redaction, audit facts, or no-mutation responses.
- Reopen SB059 if future proof uses status rows, report prose, non-empty diagnostics, fixture-only assertions, or roadmap text as approval without command transcripts and source-backed artifacts.
- Reopen SB059 if UI/media files change in this runtime/service/Core/driver bundle without explicit re-scope and browser proof.

## Handoff Notes
- The immediate next bundle should select one `Ready` candidate and keep the runtime-host approval pre-bundle separate.
- A production verification host registration bundle must not be merged into read-only adapter hardening.
- Execution-capable driver design must not share the current verification-only alpha contract line unless a future migration explicitly versions the split.
