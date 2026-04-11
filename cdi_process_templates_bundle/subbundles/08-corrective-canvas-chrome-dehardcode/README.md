# Corrective subbundle — canvas chrome de-hardcode

**Key:** `08-corrective-canvas-chrome-dehardcode`

## Purpose
Address the remaining hardcoded authoring chrome in ProcessCanvasSurfaceFactory so quick-create and group actions become file-driven.

## Dependencies
07-architecture-review-gate-a

## Deliverables
- Patch plan and sample patch
- Chrome-actions catalog sidecar
- Acceptance criteria for UI de-hardcoding

## Mandatory progression gate
Either complete the de-hardcode patch or explicitly accept the debt in the final QA memo.

## Strict execution rule
Do not continue to downstream work if this subbundle or the related architecture review identifies architectural drift, missing evidence, or an invalid simplification. Create a corrective subbundle, complete it, validate it, and only then continue.

## Expected validation
- Update the workbook tabs that this subbundle changes.
- Update JSON sidecars and markdown sidecars together.
- Re-run `tools/validate_process_template_pack.py`.
- Update the traceability matrix and validation report if scope changes.

## Corrective path on failure
Escalate as architectural debt that must stay visible in the final bundle.

## Suggested reviewer mix
- Senior C# architect
- Senior QA reviewer
- Process/governance owner for the affected area

## Specific architectural concern
The current module still hardcodes definition-canvas quick-create and group-context action lists in `ProcessCanvasSurfaceFactory.BuildDefinitionChrome()`. This makes authoring chrome less adaptable than the rest of the file-driven pack.

## Required patch inputs
- `repo-overlay/output/process-template-pack/toolbox/chrome-actions.json`
- `repo-overlay/patches/ProcessCanvasSurfaceFactory.current-architecture.patch`
- Current repo `ProcessCanvasSurfaceFactory.cs`

## Acceptance criteria
- Definition quick-create actions are loaded from a sidecar catalog rather than a hardcoded list.
- Definition group-context actions are loaded from a sidecar catalog rather than a hardcoded list.
- If the sidecar is missing, the module must fail loudly or fall back through an explicit governed policy rather than silently drifting.
