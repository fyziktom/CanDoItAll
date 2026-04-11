# Mermaid and sidecar driver

**Key:** `04-mermaid-and-sidecar-driver`

## Purpose
Ensure every template can export Mermaid flowchart and sequence diagrams plus supporting markdown sidecars.

## Dependencies
03-process-template-enhancement

## Deliverables
- Mermaid flowcharts
- Mermaid sequence diagrams
- Supporting sidecar file inventory

## Mandatory progression gate
Mermaid exporter and supporting file inventory must cover definition docs and resource docs.

## Strict execution rule
Do not continue to downstream work if this subbundle or the related architecture review identifies architectural drift, missing evidence, or an invalid simplification. Create a corrective subbundle, complete it, validate it, and only then continue.

## Expected validation
- Update the workbook tabs that this subbundle changes.
- Update JSON sidecars and markdown sidecars together.
- Re-run `tools/validate_process_template_pack.py`.
- Update the traceability matrix and validation report if scope changes.

## Corrective path on failure
Create an exporter corrective subbundle and rerun architecture review gate B.

## Suggested reviewer mix
- Senior C# architect
- Senior QA reviewer
- Process/governance owner for the affected area
