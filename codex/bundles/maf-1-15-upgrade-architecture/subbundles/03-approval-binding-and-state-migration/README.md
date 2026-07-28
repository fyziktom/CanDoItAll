# SB03 — Approval Binding and State Migration

## Status

- `Ready after SB02`

## Objective

Integrate MAF 1.15 approval-response binding with CanDoItAll's persistent approval/session model, make decisions request-specific, handle 1.13 legacy state safely, and prove exact-once execution.

## Success Criteria

- Effective binding middleware is proven for every provider path.
- Native 1.15 approval survives serialization, scrubbing, process restart, and exact continuation.
- Decisions target explicit approval IDs.
- Missing/random request IDs cannot become approvable.
- Forged, substituted, replayed, duplicate, stale, and cross-session approvals execute nothing.
- Legacy 1.13 pending approvals are reissued or handled by a tightly controlled temporary bridge.
- Persistent authority, cache optimization, fingerprint, expiry, and transactional consumption are documented.
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

- request-specific approval decision contract;
- stable request ID validation;
- versioned compatibility metadata;
- approval fingerprint/nonce/expiry as needed;
- native 1.15 continuation;
- legacy reissue flow;
- optional temporary bridge behind a flag only if necessary;
- security and restart test suite;
- telemetry;
- `proof/SB03/a2-decision.md`.

## Implementation Steps

1. Prove the effective 1.15 middleware order for each provider.
2. Add explicit tests that binding is active with the restored `AgentSession`.
3. Add compatibility metadata outside opaque MAF JSON.
4. Change continuation input to explicit approval decisions.
5. Remove random approval ID generation and fail closed.
6. Define persistent record as authority and in-process request cache as optimization.
7. Add canonical request fingerprint and transactional exact-once consumption.
8. Serialize a native pending request, scrub, restart, restore, and approve.
9. Implement legacy classifier.
10. Implement preferred reissue path.
11. Add a temporary trusted bridge only if business requirements demand it.
12. Test function and MCP approvals.
13. Run full attack/replay/mixed decision matrix.
14. Keep old mixed-call bypass disabled through A2.
15. Add diagnostics and redacted telemetry.
16. Complete independent security review.

## Do Not Do

- do not disable MAF binding;
- do not trust client-supplied tool name/arguments;
- do not apply one boolean to unbounded pending requests;
- do not mutate opaque MAF JSON to fabricate state;
- do not silently discard a legacy approval;
- do not enable the new mixed-call bypass before parity/security proof;
- do not weaken CanDoItAll mutation policy.

## Acceptance Checklist

- [ ] binding active on every provider
- [ ] native restart continuation exact
- [ ] per-ID decisions
- [ ] no random fallback
- [ ] fingerprint and exact-once
- [ ] legacy reissue tested
- [ ] bridge absent or tightly controlled
- [ ] scrubber preserves binding state
- [ ] function and MCP tests
- [ ] attack matrix passes
- [ ] A2 independent review GO

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
Implement SB03 only. Keep MAF approval binding enabled, make approvals request-specific and exact-once, remove random IDs, version and classify persisted state, support safe 1.13 reissue or a narrowly controlled trusted bridge, prove restart and attack resistance for function and MCP calls, and stop unless A2 passes.
```
