# SB11 — Final regression and release gate

Proof tier: **Governed**

## Objective

Execute the one broad stable Release gate, classify every result, and produce a final merge decision.

## Scope

- Rebase/re-anchor evidence to current HEAD and inspect complete diff.
- Run validators and focused migration/API tests.
- Run one Restore/Release build/stable filtered solution test gate.
- Run pending-model and documentation validation.
- Confirm CI matrix readiness for Windows/Linux/macOS.
- Complete FINAL-MERGE-DECISION.md.

## Expected change surface

- proof only unless a final-gate bug requires reopening its owning subbundle
- no opportunistic refactor

## Targeted validation

- exact stable gate from docs/testing.md, once
- focused LlmChats API/PostgreSQL test
- migration pending-model check
- documentation and architecture validators

All test commands must comply with `test-budget.json`. Record exact commands and results in the proof
manifest.

## Acceptance criteria

- [ ] Stable Release gate passes or every failure is proven pre-existing and explicitly accepted by operator policy.
- [ ] No new quarantine.
- [ ] No hidden skipped lane.
- [ ] Final review states Ready, Not Ready, or Ready with named residual items.
- [ ] All proof manifests and handoffs are complete.

## Forbidden work

- unfiltered full suite
- Playwright/UI tests
- fixing unrelated failures without reopening scope
- claiming CI passed before it actually runs

## Handoff

Complete `SESSION-HANDOFF.md` and `proof/proof-manifest.json`. Do not unlock the next subbundle until
all acceptance criteria and any checkpoint are satisfied.
