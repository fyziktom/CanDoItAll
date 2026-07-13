# SB08 Acceptance Criteria Matrix

## Status

- `Completed`

## Objective

Convert project-structure acceptance requirements into explicit criteria that QA, repair, and recheck steps must prove, preventing complex products such as Tetris from passing with only a shell UI and generic runtime proof.

## Covered Inputs

- GPTPro acceptance matrix analysis.
- `project-structure-acceptance-gap` evidence.
- Requirement R08.

## Prerequisites

- SB07 template prompts and route metadata are available.
- SB06 artifact inventory has selected the artifact owner for acceptance criteria.
- Project-structure launch variable and artifact flows have been reviewed.

## Exact Source References

- `bundle://codex-tasks/08-acceptance-criteria-matrix.md`
- `bundle://evidence/project-structure-acceptance-gap.md`
- `bundle://inventories/03-artifact-template-inventory.md`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureChecklistService.cs`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/software-delivery/artifacts/migration-rehearsal-pack.json`

## Deliverables

- Acceptance criteria matrix artifact or launch variable contract.
- Criteria id model for feature requirements, UI behavior, runtime proof, and negative cases.
- QA prompt changes requiring criterion-by-criterion proof.
- Repair prompt changes carrying failed criteria and runtime gate findings forward.
- Tests proving Calculator-like projects remain lightweight and Tetris-like projects require gameplay criteria.

## Dependency Impact

- SB07 prompts must cite this matrix where product acceptance is non-trivial.
- SB10 operator diagnostics should surface failed criteria ids.
- SB11 final regression must include criteria matrix negative and positive scenarios.

## Validation Depth

- Critical product-quality guard.
- Requires semantic tests, template tests, and one project-structure fixture with complex criteria.

## Implementation Steps

1. Define where acceptance criteria matrix data is produced: launch variable, managed artifact, or both.
2. Model criteria with stable ids, description, owner step, proof type, and negative acceptance case.
3. Ensure criteria survive assignment launch, QA, repair, and recheck.
4. Update templates and prompts to require proof per criterion for non-trivial products.
5. Add tests for a simple Calculator-like flow where build/test proof is sufficient.
6. Add tests for a Tetris-like flow where shell UI plus browser receipt is insufficient.
7. Add repair branch test proving failed criteria are available to the repair step.

## C# Architecture Impact

This phase adds product acceptance structure without putting product-specific rules into generic runtime gates.

## Boundary Ownership

- Workbench/project-structure code owns product criteria extraction.
- Process templates own criteria expectations.
- Runtime owns persistence and routing of failed criteria as generic findings.

## Dependency Direction

- Workbench can enrich launch variables consumed by process runtime.
- Generic runtime must treat criteria as structured metadata, not Tetris-specific behavior.

## Pattern Decision

- Strongly typed criteria matrix and failed-criteria findings.
- Rejected: prompt-only natural-language criteria with no typed ids.

## Testability Contract

- Criteria generation is unit-testable without MAF.
- Routing tests can construct criteria failures without launching a real browser.
- Browser validation remains an integration proof, not the only acceptance signal.

## Partial Class Policy

- Do not add more partial files to hide project-structure acceptance behavior.
- If an existing partial is touched, the closure gate must justify why no extraction is required.

## Architecture Proof Required

- Source assertion that criteria ids are strongly typed or constant-backed.
- Tests proving criteria propagation across launch, QA, repair, and recheck.
- Negative proof that status-only acceptance fails.

## Do Not Do

- Do not use magic strings for criteria ids.
- Do not make every small project carry heavyweight gameplay criteria.
- Do not accept screenshots as product proof unless they are tied to criteria ids.

## Acceptance Checklist

- Complex project criteria can be represented and persisted.
- QA cannot accept a complex product without criterion-level proof.
- Repair receives failed criteria and runtime findings.
- Simple projects remain practical and do not need irrelevant criteria.

## Proof Required

- `bundle://proof/SB08/manifest.md` after execution.
- Criteria matrix failing-first transcript.
- Passing propagation and route transcripts.
- Source assertions for criteria ids and artifact ownership.
- Anti-stub audit proving criteria are consumed, not just generated.

## Browser Validation Logging

- Browser validation is required for at least one complex product fixture during execution.
- Record criteria ids, viewport, screenshot path, Playwright evidence, and result in `reviews/01-execution-report.md`.

## Progression Gate

- SB11 final regression is blocked until complex and simple criteria scenarios pass.

## Suggested Agent Prompt

Implement SB08 by adding a typed acceptance criteria matrix that flows from project structure into QA, repair, and recheck. Prove that shell UI plus generic runtime receipts cannot satisfy complex product requirements.
