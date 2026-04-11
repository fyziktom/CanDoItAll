# Tests and regression net

**Key:** `06-tests-and-regression-net`

## Purpose
Strengthen unit tests and validation scripts so future process-module changes surface pack regressions immediately.

## Dependencies
05-runtime-projection-and-import-parity

## Deliverables
- Updated xUnit tests
- Validation script
- Regression expectations for current architecture

## Mandatory progression gate
Test inventory must cover loader, projection, exporter, catalog, and current baseline expectations.

## Strict execution rule
Do not continue to downstream work if this subbundle or the related architecture review identifies architectural drift, missing evidence, or an invalid simplification. Create a corrective subbundle, complete it, validate it, and only then continue.

## Expected validation
- Update the workbook tabs that this subbundle changes.
- Update JSON sidecars and markdown sidecars together.
- Re-run `tools/validate_process_template_pack.py`.
- Update the traceability matrix and validation report if scope changes.

## Corrective path on failure
Create a test-gap corrective subbundle and rerun gate B.

## Suggested reviewer mix
- Senior C# architect
- Senior QA reviewer
- Process/governance owner for the affected area
