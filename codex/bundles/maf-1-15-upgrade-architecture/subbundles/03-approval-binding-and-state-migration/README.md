# SB03 — Approval Binding and State Migration

## Status

- `Complete`

## Objective

Integrate MAF 1.15 approval-response binding with CanDoItAll's existing
persistent approval/session model, preserve stable request and call IDs, handle
1.13 legacy state safely, and prove at-most-once execution.

## Success Criteria

- Effective binding middleware is proven for every provider path.
- Native 1.15 approval survives serialization, scrubbing, process restart, and exact continuation.
- A decision is admitted only for the complete current server-held pending snapshot.
- Every persisted pending request has the stable MAF request and call IDs needed
  by native binding.
- Missing/random request IDs cannot become approvable.
- Forged, substituted, replayed, duplicate, stale, and cross-session approvals execute nothing.
- Legacy 1.13 pending approvals are drained or reissued; they are never
  reconstructed into executable 1.15 state.
- The exact serialized MAF session and existing application record are persisted
  atomically; an in-process cache remains an optimization only.
- A2 gate passes.

## Covered Requirements

- R05, R06, R07, R08, R09, R12, R19, R20, R22

## Prerequisites

- package alignment complete;
- 1.13 approval fixtures available;
- actual pending approval store/API/UI located;
- state-store backup available.

## Exact Source References

- `MafApprovalContinuationDriver.cs`
- `MafRuntimeSessionBuilder.cs`
- `MafRuntimeSessionPersistenceDriver.cs`
- pending approval model/store/API/UI files discovered in SB01
- provider factory and chat-client option construction
- attachment scrubber
- application tool policy

## Deliverables

- stable request ID validation;
- stable call ID validation;
- atomic persistence of the exact serialized MAF session and application pending
  snapshot;
- native 1.15 continuation;
- legacy drain/reissue flow;
- security and restart test suite;
- telemetry;
- `proof/SB03/a2-decision.md`.

## Implementation Steps

1. Prove the effective 1.15 middleware order for each provider.
2. Add explicit tests that binding is active with the restored `AgentSession`.
3. Remove random approval ID generation and fail closed.
4. Require the complete current server-held pending snapshot before admitting a
   decision.
5. Persist the exact serialized MAF session and application pending snapshot as
   one consistency boundary.
6. Treat the in-process request cache as an optimization only.
7. Serialize a native pending request, scrub, restart, restore, and approve.
8. Detect the absence of native 1.15 session state without inspecting private
   framework JSON.
9. Drain or reissue every pre-1.15/incompatible pending approval.
10. Test function and MCP approvals.
11. Run the attack/replay/atomic-snapshot matrix.
12. Keep old mixed-call bypass disabled through A2.
13. Add diagnostics and redacted telemetry.
14. Complete independent security review.

## Do Not Do

- do not disable MAF binding;
- do not trust client-supplied tool name/arguments;
- do not apply a decision unless it is atomically bound to the complete current
  server-held pending snapshot;
- do not mutate opaque MAF JSON to fabricate state;
- do not inspect private MAF JSON to classify approval compatibility;
- do not reconstruct a legacy request as a compatibility bridge;
- do not introduce a per-ID migration DTO or persistence path for this package
  upgrade;
- do not silently discard a legacy approval;
- do not enable the new mixed-call bypass before parity/security proof;
- do not weaken CanDoItAll mutation policy.

## Acceptance Checklist

- [x] binding active on every provider
- [x] native function-approval restart continuation exact
- [x] complete current snapshot required
- [x] no random fallback
- [x] atomic persistence and at-most-once consumption
- [x] legacy reissue tested
- [x] no reconstructed compatibility bridge
- [x] scrubber preserves binding state
- [x] native function-approval test
- [x] dedicated hosted-MCP approval restart test
- [x] function-approval attack matrix passes
- [x] A2 independent review GO for the implemented path

The hosted-MCP fixture proves the MAF approval envelope and native binding
without claiming MCP transport or server execution.

## Proof Tier

- `Governed`
- Security-critical.

## Proof Required

- Materialize every evidence path listed under `Deliverables`; do not leave proof only in chat or terminal scrollback.
- Record exact commands, exit codes, repository SHA, relevant environment details, and timestamps.
- Preserve failing-first evidence before the passing result whenever behavior changes.
- Hash persisted-state fixtures and redact secrets or sensitive payloads.
- Link the final proof from `reviews/01-execution-report.md`.

## Progression Gate

A2 must record `GO`. No mutation-capable deployment or workaround cleanup before this gate.

## Reopen Triggers

- a provider path lacks binding;
- multiple approvals appear outside tested assumptions;
- state store changes;
- scrubber changes;
- MAF package version changes;
- any tool executes on an unknown/replayed approval.

## Suggested Agent Prompt

```text
Implement SB03 only. Keep MAF approval binding enabled, retain stable persisted
request and call IDs, atomically persist the exact serialized 1.15 MAF session
with the complete application pending snapshot, and remove random IDs. Prove
serialize/scrub/restart/native-bind/at-most-once behavior for function and MCP
calls. Drain or reissue pre-1.15 or incompatible pending approvals. Do not build
a private-JSON classifier, per-ID migration path, fingerprint layer, or
reconstructed compatibility bridge. Stop unless A2 passes.
```
