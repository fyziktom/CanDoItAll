# SB04: Project-structure multi-team UI E2E

## Status
- Status: `Completed`

## Objective
Strengthen user-facing launch proof for canonical multi-team/software-delivery flow.

## Covered Inputs
- REQ-004: Re-run and harden project/project-structure UI launch for canonical multi-team/software-delivery path.

## Prerequisites
- SB03 must be completed or honestly classified so run-detail/readback expectations are clear.
- Large desktop Playwright infrastructure must be available.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Playwright
- repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateCatalogInventory.cs
- repo://Templates/Processes/processes/software-delivery/definition.json
- repo://src/CanDoItAll.Modules.Workbench

## Deliverables
- Playwright proof launching canonical `software-delivery` from project/project-structure context.
- Assignment review proof for multi-role governance roles.
- Run detail proof showing implementation, peer-review, QA, release approval/writeback path.
- Serialization/readback proof preserving project-structure context.

## Dependency Impact
- SB05 and SB07 rely on the project-scoped launch path being stable.
- SB08 release decision must cite the UI proof outcome.

## Validation Depth
- Large desktop Playwright actions and screenshot/trace proof.
- Backend assertions for project-scoped run detail and context serialization.
- Negative proof that no duplicate `multi-team-development` alias was introduced.

## Implementation Steps
1. Extend Playwright proof to launch canonical `software-delivery` from project/project-structure context.
2. Verify assignment review covers multi-role governance roles.
3. Execute launch plan.
4. Verify redirect to project-scoped run detail.
5. Verify run steps show implementation, peer-review, QA, release approval/writeback path.
6. Verify project-structure context survives serialization/readback.

## Do Not Do
- Do not add small/medium/mobile optimization work.
- Do not add a duplicate `multi-team-development` alias unless explicitly approved.

## Acceptance Checklist
- Large desktop proof exists.
- No mobile/small/medium optimization work was added.
- No duplicate `multi-team-development` alias exists unless explicitly approved.
- UI proof includes screenshots or Playwright trace paths.

## Proof Required
- `bundle://proof/SB04/manifest.md`
- `bundle://proof/SB04/semantic-invariants.md`
- Playwright transcript and screenshot path for the large desktop flow.
- Source scan transcript for alias drift and project-structure context serialization.

## Browser Validation Logging
- Record project/project-structure route, `1900x1200` viewport, Playwright MCP evidence, screenshot path, visual review result, and pass/fail in the execution report.

## Progression Gate
- SB05 may start only after canonical project-structure launch proof passes or is explicitly blocked without being counted as release UI proof.

## Suggested Agent Prompt
Implement only the project/project-structure multi-team UI proof or stabilization for SB04, record artifact-backed Playwright evidence, then run the closure gate before SB05.
