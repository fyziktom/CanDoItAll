# Runtime projection and import parity

**Key:** `05-runtime-projection-and-import-parity`

## Purpose
Project current-module import envelopes from the pack and verify parity for dependencies, artifact inputs, and decision roles.

## Dependencies
04-mermaid-and-sidecar-driver

## Deliverables
- Current-module import envelopes
- Compatibility reports
- Projection parity tests

## Mandatory progression gate
Projection compatibility reports must show no blocking issues.

## Strict execution rule
Do not continue to downstream work if this subbundle or the related architecture review identifies architectural drift, missing evidence, or an invalid simplification. Create a corrective subbundle, complete it, validate it, and only then continue.

## Expected validation
- Update the workbook tabs that this subbundle changes.
- Update JSON sidecars and markdown sidecars together.
- Re-run `tools/validate_process_template_pack.py`.
- Update the traceability matrix and validation report if scope changes.

## Corrective path on failure
Create a projection corrective subbundle before continuing.

## Suggested reviewer mix
- Senior C# architect
- Senior QA reviewer
- Process/governance owner for the affected area
