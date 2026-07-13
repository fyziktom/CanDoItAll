# SB06 Template And Artifact Inventory Closure

## Status

- `Completed`

## Objective

Close the user's concern that the demonstrated blocked Tetris process is only one example by auditing every process template, branch-like validation flow, and artifact template surface before migration begins.

## Covered Inputs

- User request to analyze all similar process templates and artifact templates.
- GPTPro template coverage notes.
- Requirement R07.

## Prerequisites

- SB02 target contract is understood so the audit can classify branch-aware receipt needs correctly.
- Current process and artifact inventories are available.
- No production template edits are made in this phase except inventory corrections.

## Exact Source References

- `bundle://inventories/01-process-template-inventory.md`
- `bundle://inventories/03-artifact-template-inventory.md`
- `bundle://codex-tasks/07-template-and-prompt-hardening.md`
- `repo://Templates/Processes/processes`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/blazor-app-delivery/definition.json`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`

## Deliverables

- Complete audit table for every `Templates/Processes/processes/*/definition.json`.
- Explicit action for each template: migrate, exempt, or no-op with reason.
- Artifact-template audit for acceptance criteria, runtime gate findings, repair briefs, and recheck inputs.
- List of exact template files that SB07 and SB08 must edit.
- Exemption tests or inspection notes for templates that look branch-like but do not need runtime route metadata.

## Dependency Impact

- SB07 depends on the final migration list.
- SB08 depends on the artifact owner chosen for acceptance criteria matrix data.
- SB11 depends on this phase to prove no process family was skipped.

## Validation Depth

- Broad inspection phase.
- Requires machine-generated template scan plus manual architecture classification.

## Implementation Steps

1. Scan all `definition.json` files for `Branches`, `RequiredReceipts`, accepted/repair/recheck/escalation branch ids, browser runtime receipt names, and completion issue route metadata.
2. Scan all process artifact directories for criteria, QA note, repair note, runtime evidence, and recheck carriers.
3. Compare scan output with `inventories/01-process-template-inventory.md` and `inventories/03-artifact-template-inventory.md`.
4. Update inventories with every template found, including templates that are exempt.
5. Mark each impacted process as `migrate`, `exempt`, or `no-op`.
6. Record the reason and downstream subbundle owner for every non-no-op process.
7. Add a failing-first inventory assertion test or script transcript that fails when a new branch-like template has no migration/exemption decision.

## C# Architecture Impact

This phase prevents a narrow Tetris-only fix by defining the full migration surface.

## Boundary Ownership

- Template metadata remains under `Templates/Processes`.
- Generic runtime code must consume metadata, not know template names.
- Workbench-specific artifact carriers can own project-structure criteria details.

## Dependency Direction

- Template inventory may reference process runtime concepts.
- Runtime services must not reference a concrete template inventory file at execution time.

## Pattern Decision

- Use explicit migration/exemption records.
- Rejected: discover and patch only the blocked software-delivery template.

## Testability Contract

- The inventory script or assertion must be repeatable from the repo root.
- The assertion must identify the exact template missing a decision.

## Partial Class Policy

- No production partial-class change is allowed in this phase.

## Architecture Proof Required

- Inventory diff showing every template was considered.
- Source assertion that no runtime service gained template-name branching during the audit.

## Do Not Do

- Do not treat absence of `RequiredReceipts` as proof that a template is safe.
- Do not edit business artifact templates unless they carry acceptance criteria, runtime findings, repair inputs, or branch outcomes.
- Do not hardcode the Tetris process id or blocked 5032 instance data into templates.

## Acceptance Checklist

- Every process definition has an explicit audit row.
- Every artifact template family has an audit row or a clear exclusion reason.
- `software-delivery`, Blazor delivery variants, `dotnet-development-slice`, `dotnet-solution-setup`, and runtime writeback templates are classified.
- The migration list for SB07 and artifact list for SB08 are unambiguous.

## Proof Required

- `bundle://proof/SB06/manifest.md` after execution.
- Template scan transcript.
- Inventory assertion transcript.
- Updated inventory files with portable `repo://` references.
- Anti-stub audit proving the inventory is generated or source-backed.

## Browser Validation Logging

- N/A for SB06.

## Progression Gate

- SB07 must not start until every branch-like process template has a migration or exemption decision.

## Suggested Agent Prompt

Implement SB06 by completing the process and artifact template audit. Do not change runtime behavior; produce the migration/exemption inventory that SB07, SB08, and SB11 can enforce.
