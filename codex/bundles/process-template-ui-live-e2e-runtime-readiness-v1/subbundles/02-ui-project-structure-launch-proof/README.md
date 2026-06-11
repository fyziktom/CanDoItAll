# SB02: UI/project/project-structure launch proof

## Status
- Status: Completed

## Objective
Prove the user-facing process launch flow from project/project-structure context on a large desktop viewport.

## Covered Inputs
- Raw request: determine whether process execution works again like before from the user/operator perspective.
- REQ-002: prove representative template launch from large-screen UI/project/project-structure flow.

## Prerequisites
- SB01 closure gate passed or recorded a blocker that does not invalidate UI proof.
- Existing Playwright host can run the web app and seed project/project-structure context.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/README.md
- repo://src/CanDoItAll.Modules.Processes/Pages
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor
- repo://src/CanDoItAll.Modules.Workbench/Pages
- repo://src/CanDoItAll.Modules.Projects
- repo://tests/CanDoItAll.Tests.Playwright
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs

## Deliverables
- Add or repair a Playwright large-screen flow that opens a project/project-structure context, selects a representative process template, creates a launch plan, selects process-mock agents, approves, executes, and navigates to run detail.
- Prefer using existing UI routes; only change UI if the current route cannot expose the expected user flow.
- Capture screenshots for template selection, launch plan, assignment/approval, run detail, and project-structure output/readback.

## Dependency Impact
- SB03-SB08 may not claim user-facing launch readiness unless SB02 proves the normal UI entry path or records a concrete UI blocker.
- SB06 browser readback proof should reuse the run detail surface identified here when available.

## Validation Depth
- Use Playwright against a 1900x1200 or equivalent large desktop viewport.
- Capture route, actions, assertions, screenshots, API/run readback, and visual review answers.
- Include semantic adequacy proof, manifest, screenshot artifacts, source assertions, and anti-stub audit under `proof/SB02/`.

## Implementation Steps
- Locate the project/project-structure route that exposes process launch.
- Seed or navigate to a representative project context through existing test support.
- Select a representative template, create and approve the launch plan, execute with process-mock agents, and navigate to run detail.
- Capture screenshots and API readback for template selection, approval, execution, run detail, and project-structure output.
- Repair UI only when the existing route cannot expose the expected flow.

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
- Completed proof manifest: `bundle://proof/SB02/manifest.md`
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Passing Playwright transcript: `bundle://proof/SB02/transcripts/focused-playwright.txt`
- Failing-first source assertion: `bundle://proof/SB02/transcripts/failing-first-source-assertion.txt`
- Screenshot set: `bundle://proof/SB02/screenshots/`

## Browser Validation Logging
- Required: record route, viewport, Playwright actions, assertions, screenshot paths, API cross-check, visual review answers, and result in `reviews/01-execution-report.md`.

## Progression Gate
- SB03 may proceed only after UI launch proves the normal user entry path exists or after a concrete UI blocker is recorded.
- Reopen SB02 if later automation proof cannot be connected back to the user-visible launch flow.

## Suggested Agent Prompt
- Implement SB02 with real Playwright browser proof from project/project-structure context at a large desktop viewport. Capture screenshots, route actions, run detail readback, and proof artifacts under `proof/SB02/`; do not substitute API-only evidence.
