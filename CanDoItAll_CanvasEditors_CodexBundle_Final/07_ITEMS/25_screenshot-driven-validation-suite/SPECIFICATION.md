
# Specification

## Item identity

- **Item ID:** I25
- **Title:** Screenshot-driven validation suite and evidence protocol
- **Origin:** conversation
- **Dependencies:** I03, I07, I08, I12, I14, I15, I16, I17, I18, I19, I20, I21, I22, I23, I24

## Objective

Make screenshot-based validation a hard release gate for canvas-editor changes so visual regressions are not hand-waved away.

## Normalized scope

Add a dedicated screenshot validation protocol, naming convention, artifact checklist, and Playwright-first evidence strategy for all UI-changing items.

### In scope

- Artifact naming convention for screenshots.
- Validation checklist and semantic screenshot review template.
- Playwright coverage expansion where it pays off most.

### Out of scope

- Replacing functional tests with screenshots alone.

## Key implementation decisions

- Any item that changes the canvas or toolbox UI must produce screenshots and a short semantic analysis, not only passing tests.
- Prefer automated Playwright captures where practical, then supplement with manual evidence if the scenario is hard to script.
- A task is not done if screenshot evidence is missing or obviously does not show the claimed behavior.

## Implementation tasks

- Define artifact names and storage layout per item.
- Require screenshot capture and short semantic analysis for all UI items.
- Expand Playwright coverage where the current suite is too thin for the requested canvas changes.

## Risks to control

- Visual regressions will slip through if screenshot capture exists without semantic review.

## Covered original notes

- No direct DOCX note mapping. This item exists because the user explicitly required cross-cutting validation or shared architecture.
