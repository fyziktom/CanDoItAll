# SB04: Project-structure multi-team UI E2E

## Objective
Strengthen user-facing launch proof for canonical multi-team/software-delivery flow.

## Exact source references
- repo://tests/CanDoItAll.Tests.Playwright
- repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateCatalogInventory.cs
- repo://Templates/Processes/processes/software-delivery/definition.json
- repo://src/CanDoItAll.Modules.Workbench

## Implementation steps
1. Extend Playwright proof to launch canonical `software-delivery` from project/project-structure context.
2. Verify assignment review covers multi-role governance roles.
3. Execute launch plan.
4. Verify redirect to project-scoped run detail.
5. Verify run steps show implementation, peer-review, QA, release approval/writeback path.
6. Verify project-structure context survives serialization/readback.

## Acceptance checklist
- Large desktop only.
- No mobile/small/medium optimization work.
- No duplicate `multi-team-development` alias unless explicitly approved.
- UI proof includes screenshots or Playwright trace paths.
