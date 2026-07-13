# SB07 Template And Prompt Migration

## Status

- `Completed`

## Objective

Migrate all impacted process definitions and step prompts so branch-aware receipt rules, routeable completion issues, runtime gate findings, and repaired QA instructions are available beyond the single blocked Tetris example.

## Covered Inputs

- GPTPro RC1 through RC4.
- User request to cover similar process templates.
- Requirements R01, R02, R03, R04, R05, and R07.

## Prerequisites

- SB04 branch routing and runtime gate finding contracts exist.
- SB06 migration/exemption inventory is complete.
- SB05 provider boundary is understood so template prompts do not reintroduce generic domain leakage.

## Exact Source References

- `bundle://codex-tasks/07-template-and-prompt-hardening.md`
- `bundle://inventories/01-process-template-inventory.md`
- `bundle://inventories/03-artifact-template-inventory.md`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/blazor-app-delivery/definition.json`
- `repo://Templates/Processes/processes/blazor-app-repair-fix/definition.json`
- `repo://Templates/Processes/processes/blazor-backend-feature/definition.json`
- `repo://Templates/Processes/processes/blazor-frontend-feature/definition.json`
- `repo://Templates/Processes/processes/blazor-fullstack-feature/definition.json`
- `repo://Templates/Processes/processes/dotnet-feature-function-implementation/definition.json`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`

## Deliverables

- Structured branch-aware receipt metadata in impacted templates.
- Template-level completion issue route metadata for accepted-branch proof/content failures.
- Prompt wording that separates missing proof from deterministic product defects.
- Runtime gate finding handoff instructions for repair and recheck branches.
- Template load and compatibility tests for migrated templates.

## Dependency Impact

- SB08 depends on prompts that require acceptance criteria ids.
- SB10 depends on runtime gate finding labels being useful to operators.
- SB11 depends on template tests and exemption closure.

## Validation Depth

- Critical behavior migration.
- Requires JSON schema/load tests, route tests, and prompt source assertions.

## Implementation Steps

1. Apply the SB06 migration list template by template.
2. Convert unconditional browser/runtime product receipts into structured rules with explicit branch applicability and purpose.
3. Add route metadata so accepted-branch completion issues route to repair or recheck where the template defines those branches.
4. Preserve backward compatibility for templates that still use string receipt arrays.
5. Update QA and recheck prompts to distinguish missing proof from product defects.
6. Add runtime gate finding output instructions that repair branches can consume.
7. Add template load tests for every migrated definition.
8. Add negative tests proving accepted output with missing proof routes to repair instead of retry/manager where configured.

## C# Architecture Impact

This phase uses metadata-driven behavior instead of adding generic runtime branches for process names.

## Boundary Ownership

- Templates own branch route metadata and process-specific prompt text.
- Runtime services own generic interpretation of metadata.
- Domain providers own .NET/Blazor advice phrasing where runtime recovery advice is needed.

## Dependency Direction

- Templates feed contracts and runtime.
- Runtime cannot depend on a template-specific static helper or Workbench file.

## Pattern Decision

- Metadata-driven routing and branch-aware receipt rules.
- Rejected: hardcoded switch statements for `software-delivery`, `blazor-app-delivery`, or `dotnet-development-slice`.

## Testability Contract

- Each migrated template must have a load test.
- At least one negative and one positive route scenario must cover the incident shape.
- String-only receipt compatibility must remain tested.

## Partial Class Policy

- No new partial classes are justified for template migration.

## Architecture Proof Required

- Source assertion that runtime behavior is template-id agnostic.
- Template load transcript.
- Route negative/positive transcript.

## Do Not Do

- Do not remove runtime/browser proof requirements to make the process pass.
- Do not duplicate the same receipt in product and capability gates without purpose metadata.
- Do not weaken QA prompts into status-only acceptance.

## Acceptance Checklist

- Every SB06 `migrate` template is edited or explicitly deferred with a blocking reason.
- No migrated template relies on unconditional accepted-branch runtime receipts when only repair branch evidence is applicable.
- Missing browser/runtime proof and product defects route differently where the process semantics require it.
- Template tests fail on malformed route metadata.

## Proof Required

- `bundle://proof/SB07/manifest.md` after execution.
- Failing-first incident template test transcript.
- Passing template load and route test transcripts.
- Source assertions for migrated definitions.
- Anti-stub audit for route metadata consumers.

## Browser Validation Logging

- Browser validation is required for migrated Blazor/browser templates during execution.
- Capture route, viewport, evidence artifact, screenshot path, and result in `reviews/01-execution-report.md`.

## Progression Gate

- SB08 and SB11 are blocked until all SB06 migration decisions are closed.

## Suggested Agent Prompt

Implement SB07 by migrating every impacted process template from the SB06 inventory to branch-aware receipts and completion issue route metadata. Preserve compatibility, add tests, and do not add template-name conditionals to generic runtime code.
