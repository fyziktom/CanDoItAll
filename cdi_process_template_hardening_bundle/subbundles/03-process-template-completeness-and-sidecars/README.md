# Process-template completeness and sidecars

## Purpose
Re-check every process definition against the now-current module features and remove any historical simplifications that were only retained because the older module lacked the needed capabilities.

## Depends on
02-template-pack-materialization

## Deliverables
- Detailed process definitions for all bundled templates
- Role, artifact, checklist, validation, prompt, Mermaid, and projection sidecars
- Current-process completeness review evidence

## Repository touchpoints
- `output/process-template-pack/processes/*/definition.json`
- `output/process-template-pack/processes/*/steps/*.md`
- `output/process-template-pack/processes/*/roles/*.json`
- `output/process-template-pack/processes/*/projection/*`

## Validation commands or checks
- `python cdi_process_template_completion_and_architecture_hardening_bundle/tools/validate_process_template_pack.py output/process-template-pack`

## Senior review questions
- Did any process remain flattened only because of older module constraints?
- Does each step now exploit dependencies, artifact inputs, decision roles, and branch outcomes where that makes the process more accurate?
- Are role descriptions detailed enough for staffing, AI orchestration, and audit use?

## Strict corrective rule
Open a process-specific corrective subbundle for each failing template and do not continue until every process passes review.
