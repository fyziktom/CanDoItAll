# Architecture review gate B

## Purpose

Stop after schema FK hardening and dependency uniqueness repair, then verify that the DB now protects the model strongly enough to justify lifecycle work.

## Required deliverables
- A written Gate B memo with explicit pass/fail decision.
- A statement of whether the DB now enforces the Process graph strongly enough.
- A corrective subbundle if FK/invariant hardening still leaks through application-only assumptions.

## Repository touchpoints
- `02-open-findings.md`
- `templates/review-gate-memo-template.md`
- `subbundles/04-process-schema-referential-integrity-hardening/README.md`
- `subbundles/05-null-safe-dependency-uniqueness-and-db-invariants/README.md`

## Validation commands
- `Review the live repository, migrations, and new schema/invariant tests before continuing.`

## Review questions
1. Are the remaining Process graph invariants now backed by the DB rather than only by service code?
2. Did FK and uniqueness hardening preserve differential save behavior without reopening major graph corruption risk?
3. Is it now safe to proceed to lifecycle hardening without a corrective persistence redesign?

## Corrective trigger

If any answer is no, stop and open the DB-integrity corrective playbook before continuing.

## Corrective template

- `subbundles/_corrective-db-integrity-reset`

## Gate notes

This gate should fail if the only way to keep the save path working is to leave the schema materially under-enforced.
