
# Specification

## Item identity

- **Item ID:** I11
- **Title:** Python environment nodes
- **Origin:** docx
- **Dependencies:** I01, I10

## Objective

Add lightweight environment nodes for Python toolchains so scripts and workflows can point at concrete runtimes.

## Normalized scope

Implement Python environment nodes with provider selection such as python or conda, plus identity metadata like environment name.

### In scope

- Python environment node creation and editing.
- Provider selection and environment identity fields.
- Visual association with related scripts or tasks.

### Out of scope

- Complete environment provisioning or package management automation.

## Key implementation decisions

- Keep environment nodes small and composable.
- Use explicit provider and name metadata rather than loose free-form labels.
- Design Python environment nodes so they can later feed script execution and validation flows.

## Implementation tasks

- Add environment node family and python-specific metadata.
- Expose provider selection and name fields in the editor.
- Ensure nodes can be connected to scripts or tasks meaningfully.

## Risks to control

- Environment nodes become generic unlabeled boxes if subtype visuals are weak.

## Covered original notes

- N079 — Environments
- N080 — Python Environment
- N081 — Provider (python, conda)
- N082 — Name, etc.
