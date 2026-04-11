# Baseline scenario realignment

**Key:** `02-baseline-scenario-realignment`

## Purpose
Realign the seeded baseline scenarios to the current five-process repository expectations and current step keys.

## Dependencies
01-schema-and-pack-refresh

## Deliverables
- Five current baseline scenarios
- Updated validation assertions for software, branching, and hotfix expectations

## Mandatory progression gate
Validator must confirm five expected baseline scenarios and exact key process expectations.

## Strict execution rule
Do not continue to downstream work if this subbundle or the related architecture review identifies architectural drift, missing evidence, or an invalid simplification. Create a corrective subbundle, complete it, validate it, and only then continue.

## Expected validation
- Update the workbook tabs that this subbundle changes.
- Update JSON sidecars and markdown sidecars together.
- Re-run `tools/validate_process_template_pack.py`.
- Update the traceability matrix and validation report if scope changes.

## Corrective path on failure
Create a corrective subbundle for baseline drift, then rerun validation.

## Suggested reviewer mix
- Senior C# architect
- Senior QA reviewer
- Process/governance owner for the affected area
