# Assumptions and risks

## Working Assumptions

- The local branch is `processes-hardening` and starts from the reviewed `phase6` commit plus the prepared bundle commit.
- Existing phase6 runtime work is real but may have public/API/tool/documentation drift.
- Focused tests listed in `plan/01-phase-plan.md` are the minimum closure proof.

## Critical Path Risks

- API/tool schema updates can miss import/export or template paths unless SB01 inventory is complete.
- Health/read-model fields can be populated by tests but stranded from production producers; critical proof must cite producer and consumer paths.
- Documentation-only closure is unsafe for runtime governance fields; docs must match source and tests.

## Validation Risks

- Integration tests may be expensive; targeted filters must still exercise production emitters, not fixture-only seeding.
- UI observability in SB15 requires browser or deterministic component proof if a rendered surface changes.
- PostgreSQL-only validation must remain explicit because older bundles and test artifacts contain SQLite references.

## Reopen Triggers

- Reopen SB01-SB04 if later phases find a public schema/tool model missing an owned field.
- Reopen SB07 if SB13 proves script policy still trusts aliases outside the grounded ledger.
- Reopen SB09-SB12 if manual/API transitions and automation finalizer validation disagree.
- Reopen SB15 if final red-team proof cannot observe typed block/recovery state through API or UI.
