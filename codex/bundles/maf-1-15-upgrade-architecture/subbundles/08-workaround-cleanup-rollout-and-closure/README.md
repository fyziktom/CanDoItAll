# SB08 — Workaround Cleanup, Rollout, and Closure

## Status

- `Ready after SB06 and SB07`

## Objective

Remove only proven-obsolete workarounds, complete full regression and real validation, rehearse canary/rollback, document state migration, and close every requirement.

## Success Criteria

- Workaround register has an implemented decision and proof for every item.
- No cleanup weakens finalizer, file/tool policy, approval binding, or runtime isolation.
- Full restore/build/test passes or inherited exceptions are explicitly approved.
- Real provider, approval restart, handoff, governed process, and A2A validations pass.
- Canary and rollback are rehearsed against copied/sanitized state.
- Legacy approval backlog is zero or has a controlled dated plan.
- Temporary bridge is disabled or has measurable expiry/removal.
- Optional approval-not-required bypass is either separately enabled with proof or remains explicitly deferred.
- A4 closure passes.

## Covered Requirements

- R01-R22

## Prerequisites

- A3 GO;
- SB06 file/capability security complete;
- SB07 A2A/optional inventory complete;
- production-like state copy for rehearsal.

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
- canary/rollback rehearsal;
- migration runbook;
- final execution report;
- `proof/SB08/a4-decision.md`;
- list of deferred follow-ups.

## Implementation Steps

1. Review every workaround decision against proof.
2. Remove only code whose replacement is proven on the full runtime path.
3. Preserve defense-in-depth checks even when MAF now supplies a lower-level guard.
4. Remove dead options/hooks and narrow suppressions.
5. Run package alignment, targeted, integration, concurrency, full solution, and security tests.
6. Run real provider validations.
7. Rehearse legacy approval reissue/bridge on copied state.
8. Rehearse canary feature flags and metrics.
9. Rehearse rollback including state-store restoration.
10. Enable 1.15 approval-not-required bypass only through its own reviewed gate; otherwise leave parity setting explicit.
11. Update developer/runtime/migration documentation and skills.
12. Complete requirement traceability and independent review.
13. Record deferred optional feature bundles.
14. Pass A4.

## Do Not Do

- do not call the upgrade complete with untested persisted approvals;
- do not delete finalizer or workspace policy as “framework duplication” without proof;
- do not leave a permanent legacy bridge silently enabled;
- do not hide failures behind blanket catches or warning suppressions;
- do not enable optional features merely because tests are green;
- do not omit rollback evidence.

## Acceptance Checklist

- [ ] workaround register closed
- [ ] no security boundary weakened
- [ ] full deterministic tests
- [ ] real provider validation
- [ ] approval restart validation
- [ ] handoff validation
- [ ] governed process validation
- [ ] A2A validation
- [ ] canary rehearsal
- [ ] rollback rehearsal
- [ ] migration runbook
- [ ] traceability complete
- [ ] independent review
- [ ] A4 GO

## Proof Tier

- `Governed`

## Proof Required

- Materialize every evidence path listed under `Deliverables`; do not leave proof only in chat or terminal scrollback.
- Record exact commands, exit codes, repository SHA, relevant environment details, and timestamps.
- Preserve failing-first evidence before the passing result whenever behavior changes.
- Hash persisted-state fixtures and redact secrets or sensitive payloads.
- Link the final proof from `reviews/01-execution-report.md`.

## Progression Gate

Final closure only when `proof/SB08/a4-decision.md` records `GO`.

## Reopen Triggers

- production canary differs from rehearsal;
- legacy approval remains unclassified;
- package graph changes;
- MAF patch release is adopted;
- security or state regression appears.

## Suggested Agent Prompt

```text
Implement SB08 only. Close the workaround register with evidence, preserve defense-in-depth, run full and real validations, rehearse state migration/canary/rollback, document deferred optional work, complete traceability, and do not declare success unless A4 honestly passes.
```
