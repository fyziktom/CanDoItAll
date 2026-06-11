# SB02: UI/project/project-structure launch proof

## Status
Prepared.

## Objective
Prove the user-facing process launch flow from project/project-structure context on a large desktop viewport.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/README.md
- repo://src/CanDoItAll.Modules.Processes/Pages or equivalent process route/component surface
- repo://src/CanDoItAll.Modules.Projects or Workbench project-structure surfaces
- repo://tests/CanDoItAll.Tests.Playwright
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs

## Deliverables
- Add or repair a Playwright large-screen flow that opens a project/project-structure context, selects a representative process template, creates a launch plan, selects process-mock agents, approves, executes, and navigates to run detail.
- Prefer using existing UI routes; only change UI if the current route cannot expose the expected user flow.
- Capture screenshots for template selection, launch plan, assignment/approval, run detail, and project-structure output/readback.

## Do Not Do
- Do not replace this with API-only proof.
- Do not spend time on small/medium/mobile layouts.
- Do not bypass launch plan approval if the user flow normally needs it.

## Acceptance Checklist
- Large desktop viewport 1900x1200 or equivalent.
- The test starts from user-visible project/project-structure context.
- The run id is visible or navigable.
- Project-structure output/run node is visible or API-backed if the UI already supports it.

## Proof Required
- Playwright transcript.
- Screenshot set.
- API/run readback cross-check.

## Browser Validation Logging
Required. Record route, viewport, actions, assertions, screenshot paths and result.

## Progression Gate
SB03 may proceed after UI launch proves the normal user entry path exists or after a concrete UI blocker is recorded.
