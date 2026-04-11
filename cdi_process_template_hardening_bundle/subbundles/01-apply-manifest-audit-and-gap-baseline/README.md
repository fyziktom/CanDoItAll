# Apply-manifest audit and gap baseline

## Purpose
Prove exactly what the previous bundle claimed should exist in the repository, identify what is actually present, and establish a non-negotiable baseline before any new changes continue.

## Depends on
None

## Deliverables
- Machine-readable application audit against the in-repo apply manifest
- Human-readable gap register with missing-file samples and category counts
- Stop/go decision for materialization work

## Repository touchpoints
- `cdi_process_templates_bundle/apply-manifest.json`
- `output/process-template-pack/`
- `tools/audit_process_template_bundle_materialization.py`

## Validation commands or checks
- `python tools/audit_process_template_bundle_materialization.py . cdi_process_templates_bundle/apply-manifest.json`
- `python cdi_process_template_completion_and_architecture_hardening_bundle/tools/audit_bundle_application.py . cdi_process_templates_bundle/apply-manifest.json`

## Senior review questions
- Does the current repository physically contain the full template-pack tree expected by the previous bundle?
- Do the old bundle claims about execution and validation still match repository reality?
- Is any downstream work blocked because the source-of-truth files are absent?

## Strict corrective rule
Create a corrective subbundle immediately. No template, refactor, or QA work may continue until the baseline audit gap is explicitly closed.
