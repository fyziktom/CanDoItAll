# Implementation Plan

Status on 2026-03-21:

- Phase 1 completed.
- Phase 2 substantially completed. `postWaitPolicy` remains a follow-up item.
- Phase 3 completed.
- Phase 4 completed.

## Phase 1: Contract honesty and safety blockers

Priority: release-blocking

1. Repair failure-status mapping in `SshOpsTools` so public envelopes express meaningful contract outcomes.
2. Add centralized compose command compatibility handling and stop assuming only `docker compose` exists.
3. Add a real `compose_exec` command policy instead of forwarding arbitrary commands.
4. Harden remote write and bootstrap flows for pack-intended elevated paths.

Exit criteria:

- Public failure envelopes are actionable.
- Compose tools work on the Raspberry Pi target.
- Root-owned path scenarios are no longer structurally broken.

Outcome:

- Completed and validated on `rpi3-test`.

## Phase 2: Tool-behavior parity

Priority: high

1. Improve `target_audit` to surface compose readiness and required network state honestly.
2. Make `compose_ps` return degraded status when service health is bad.
3. Repair `ipfs_status` gateway reachability semantics.
4. Either implement `postWaitPolicy` or clearly mark it as unsupported in the contract and docs.
5. Tighten bootstrap behavior so created roots remain usable by later tools.

Exit criteria:

- The code matches the tool contract closely enough that the checklist can mark the feature complete instead of partial.

Outcome:

- Completed for the repaired items.
- Remaining follow-up: `postWaitPolicy` is still advisory rather than enforced server-side.

## Phase 3: Remote validation harness

Priority: high

1. Add automated or scripted validation that exercises the repaired SshOps code directly against `rpi3-test`.
2. Stand up a scratch Docker Compose stack on the Raspberry Pi for compose, rollback, and PostgreSQL checks.
3. Add a deterministic detached-job check for operation status, wait, logs, and cancel semantics.

Exit criteria:

- The repaired implementation is proven against the real Raspberry Pi target, not only by static inspection.

Outcome:

- Completed through `RemoteValidationRunner` plus targeted `RemoteJobDiagnostic`.

## Phase 4: Documentation sync

Priority: medium

1. Update this folder after repair so findings/checklists reflect the real post-fix state.
2. Reconcile any remaining contract over-promises with the repaired implementation.

Exit criteria:

- Docs, plan, prompts, and server behavior tell the same story.

Outcome:

- Completed in this folder update.
