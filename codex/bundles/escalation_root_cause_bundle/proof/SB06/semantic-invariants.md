# SB06 Semantic Invariants

## Child State Propagation

- A stopped child run is parent-visible evidence, not a child run to ignore.
- `Blocked`, `Escalated`, and `WaitingForUser` child runtime states map to `ChildStoppedBlocked`.
- Failed terminal child runtime states map to `ChildStoppedFailed`.
- Parent diagnostics preserve the child run id, child step key, child step instance id, child diagnostic code, child safe summary, evidence hash, and recovery budget state.
- The parent adapter does not reinvoke the parent agent when an existing child is stopped with root-cause diagnostics.

## Ledger-First Artifact Bridge

- Accepted child output bridging requires a child assignment whose produced artifact slots intersect a produced-artifact receipt in the child runtime ledger.
- Physical child output file existence is insufficient by itself.
- Physical file readback remains a defensive guard only after ledger acceptance is present.
- A file that contains `Runtime Captured Structured Outcome` but lacks `Runtime Accepted Completion Gates` cannot bridge to the parent.
- No-go child outputs remain separate from blocked child diagnostics and accepted child outputs.

## Production Behavior Artifact Matrix

| Child runtime state | Child ledger receipt | Child file state | Parent bridge result | Parent result |
| --- | --- | --- | --- | --- |
| Completed | Accepted produced slot | File missing | `AcceptedChildOutputBridged` | Parent can synthesize completion from ledger evidence. |
| Completed | Accepted produced slot | Captured marker only | `ChildCompletedWithoutAcceptedOutput` | Parent rejects staged-only child output. |
| Completed | No accepted produced slot | File exists | `ChildCompletedWithoutAcceptedOutput` | Parent rejects physical-file-only evidence. |
| Completed | Accepted no-go produced slot | File optional | `NoGoChildOutputFound` | Parent preserves no-go branch semantics. |
| Blocked/Escalated/WaitingForUser | Diagnostic receipt | Any | `ChildStoppedBlocked` | Parent emits `process.adapter.subprocess_child_blocked` with child root cause. |
| Failed/Cancelled | Diagnostic receipt optional | Any | `ChildStoppedFailed` | Parent emits `process.adapter.subprocess_child_failed`. |

## Incident Closure Signal

- The child missing-helper diagnostic (`process.adapter.product_required_file_content_missing`) is preserved through the parent packet.
- The missing `workspace_pwsh_run_script` receipt text is visible in parent diagnostics when it appears in the child aggregate diagnostic.
- Retry budget/recovery decision state is preserved for manager escalation packets.

## Architecture

- `ParentSubprocessArtifactBridge` owns child-to-parent transfer semantics.
- Runtime artifact lifecycle and acceptance markers remain owned by `AgentFrameworkProcessExecutionAdapter.ManagedArtifacts`.
- Subprocess adapter partial remains plumbing-oriented: it translates typed bridge results into existing completion issues.
- No new project references were introduced; CodeAnalytics snapshot `snap-20260708193105-60b7e58e` reported no scoped dependency cycles.


## Completed Validator Contract

- Invariant ID: SB06-FINAL-001
- Source raw note: GPTPro Extended escalation root-cause analysis and the user's broader process/template/artifact repair requirement.
- Expected behavior: The completed subbundle behavior remains implemented, tested, and represented by typed proof artifacts.
- Disallowed shallow implementation: Do not close the phase with prose-only proof, build-only proof, or hidden prompt-only gates.
- Failing-first test: N/A process/non-production final proof uses adversarial negative tests or preserved subbundle evidence in proof/SB06/transcripts/00-validator-metadata.txt.
- Passing test: Completed proof metadata is recorded in proof/SB06/transcripts/00-validator-metadata.txt and the subbundle manifest.
- Changed source files: bundle://subbundles/06-sb06-subprocess-child-diagnostics-ledger-bridge/README.md and bundle://proof/SB06/manifest.md.
- Production assertions: Runtime/template/process behavior remains covered by the subbundle proof manifest and final bundle validation.
- Red-team negative case: Shallow final closure without proof metadata, semantic invariant labels, and transcripts is rejected by the completed validator.
- Downstream dependency check: Final bundle validation and recorded CodeAnalytics snapshots verify no unresolved downstream gate remains.

