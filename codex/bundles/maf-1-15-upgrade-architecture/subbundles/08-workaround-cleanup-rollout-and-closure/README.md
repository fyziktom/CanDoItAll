# SB08 — Workaround Cleanup, Rollout, and Closure

## Status

- `Complete for development compatibility scope; production A4 remains open`

## Objective

Remove only proven-obsolete workarounds, complete the authorized development
regression and real-provider validation, document state migration and rollback,
and record production canary/rollback/A4 as a separate open continuation.

## Success Criteria

- Workaround register has an implemented decision and proof for every item.
- No cleanup weakens finalizer, file/tool policy, approval binding, or runtime isolation.
- Full restore/build/test passes or inherited exceptions are explicitly approved.
- Real provider, approval restart, handoff, governed process, and A2A validations pass.
- The local 5032 development canary is healthy.
- Legacy approval backlog is zero or has a controlled dated plan.
- No legacy approval reconstruction bridge exists.
- Optional approval-not-required bypass is either separately enabled with proof or remains explicitly deferred.
- Production canary, rollback rehearsal, and A4 remain explicitly unasserted
  until a separately authorized general-rollout continuation.

## Covered Requirements

- R01-R22

## Prerequisites

- A3 GO;
- SB06 file/capability security complete;
- SB07 A2A/optional inventory complete.

A production-like state copy is a prerequisite only for the future production
rollout continuation.

## Exact Source References

- all files changed in SB02-SB07
- workaround register
- telemetry configuration
- deployment/state migration documentation
- full solution and test projects
- SharedInfo/skills/docs locations discovered by Codex

## Deliverables

- final workaround decisions;
- cleaned targeted warnings/dead hooks;
- full validation logs;
- real-provider proof;
- local development canary proof;
- migration runbook;
- final execution report;
- `proof/SB08/final-validation.md`;
- explicit production canary/rollback/A4 continuation;
- list of deferred follow-ups.

## Implementation Steps

1. Review every workaround decision against proof.
2. Remove only code whose replacement is proven on the full runtime path.
3. Preserve defense-in-depth checks even when MAF now supplies a lower-level guard.
4. Remove dead options/hooks and narrow suppressions.
5. Run package alignment, targeted, integration, concurrency, full solution, and security tests.
6. Run real provider validations.
7. Validate legacy approval drain/reissue behavior with deterministic fixtures
   and prove that no reconstruction bridge can execute it.
8. Run the local 5032 development canary and inspect health/telemetry.
9. Document production state-store rollback without claiming a rehearsal.
10. Enable 1.15 approval-not-required bypass only through its own reviewed gate; otherwise leave parity setting explicit.
11. Update developer/runtime/migration documentation and skills.
12. Complete requirement traceability and independent review.
13. Record deferred optional feature bundles.
14. Leave production A4 open for the separately authorized rollout continuation.

## Do Not Do

- do not call the upgrade complete with untested persisted approvals;
- do not delete finalizer or workspace policy as “framework duplication” without proof;
- do not introduce a legacy approval reconstruction bridge;
- do not hide failures behind blanket catches or warning suppressions;
- do not enable optional features merely because tests are green;
- do not claim a production rollback rehearsal without evidence.

## Acceptance Checklist

- [x] workaround register closed for compatibility scope
- [x] no security boundary weakened
- [x] full deterministic migration test matrix
- [x] real provider validation
- [x] native function-approval restart validation
- [x] dedicated hosted-MCP approval restart validation
- [x] handoff validation
- [x] governed process-step validation without process E2E
- [x] A2A compatibility validation; inbound hosting remains inactive
- [x] local development canary
- [ ] production canary rehearsal
- [ ] production rollback rehearsal
- [x] migration runbook
- [x] traceability complete
- [x] independent source architecture review
- [ ] A4 production general-rollout GO

The unchecked items remain explicit. A4 is the machine-defined production
general-rollout gate and is not used as a synonym for local development
validation.

## Proof Tier

- `Governed`

## Proof Required

- Materialize every evidence path listed under `Deliverables`; do not leave proof only in chat or terminal scrollback.
- Record exact commands, exit codes, repository SHA, relevant environment details, and timestamps.
- Preserve failing-first evidence before the passing result whenever behavior changes.
- Hash persisted-state fixtures and redact secrets or sensitive payloads.
- Link the final proof from `reviews/01-execution-report.md`.

## Progression Gate

Development compatibility closure requires `proof/SB08/final-validation.md`.
General rollout remains blocked until a future
`proof/SB08/a4-decision.md` records `GO`.

## Reopen Triggers

- production canary differs from rehearsal;
- legacy approval remains unclassified;
- package graph changes;
- MAF patch release is adopted;
- security or state regression appears.

## Suggested Agent Prompt

```text
Implement SB08 for development compatibility only. Close the workaround
register with evidence, preserve defense-in-depth, run deterministic and real
provider validations, validate the local 5032 canary, document state migration
and rollback, and complete traceability. Keep production canary, rollback
rehearsal, and A4 explicitly open for a separately authorized rollout.
```
