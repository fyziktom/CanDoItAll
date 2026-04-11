# Schema and pack refresh

**Key:** `01-schema-and-pack-refresh`

## Purpose
Refresh the template-pack schema so it models role usages, dependencies, artifact inputs, branch coordinates, and shared/local resources explicitly.

## Dependencies
None

## Deliverables
- Updated definition schema and manifest
- Rebuilt process-template pack folders
- Updated workbook tabs for dependencies and artifact inputs

## Mandatory progression gate
Must pass architecture review gate A before the next subbundle may begin.

## Strict execution rule
Do not continue to downstream work if this subbundle or the related architecture review identifies architectural drift, missing evidence, or an invalid simplification. Create a corrective subbundle, complete it, validate it, and only then continue.

## Expected validation
- Update the workbook tabs that this subbundle changes.
- Update JSON sidecars and markdown sidecars together.
- Re-run `tools/validate_process_template_pack.py`.
- Update the traceability matrix and validation report if scope changes.

## Corrective path on failure
Create a corrective subbundle focused on schema mismatches, then rerun gate A.

## Suggested reviewer mix
- Senior C# architect
- Senior QA reviewer
- Process/governance owner for the affected area
