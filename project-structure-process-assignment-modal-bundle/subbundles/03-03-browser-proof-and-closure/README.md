# 03 - Browser Proof And Closure

## Status

- Status: `Completed`
- Closed: `2026-05-09`
- Proof: `reviews/browser-01-start-dialog.png`, `reviews/browser-02-assignment-modal.png`, `reviews/browser-03-agent-picker.png`, `reviews/browser-04-assignment-modal-narrow.png`, and completed-stage bundle validation.

## Objective

Run final validation, capture real browser screenshots, compare the result to the supplied design, close raw notes, and complete bundle validators.

## Covered Inputs

- IN-005

## Prerequisites

- Subbundle 01 closure gate passed.
- Subbundle 02 closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\project-structure-process-assignment-modal-bundle\inputs\95662112-3264-419a-af0a-db487b3ff7da.png`
- `C:\repositories\CanDoItAll\project-structure-process-assignment-modal-bundle\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`

## Deliverables

- Targeted tests and build proof.
- Browser screenshots for full-screen assignment modal at large and narrower viewports.
- Browser screenshot for the manual agent picker open from the assignment flow.
- Raw-note closure table updated to `Solved`, `Partially solved`, or `Not solved`.
- Final validators pass.

## Dependency Impact

- Final gate. If any screenshot contradicts the design or manual picker proof fails, reopen subbundle 01 or 02.

## Validation Depth

- Targeted component/integration tests.
- Browser proof through Playwright MCP or CLI.
- Visual review against the screenshot design.

## Implementation Steps

1. Run targeted tests.
2. Start the app with the repo's preferred dev-server/watch path.
3. Navigate to a seeded project structure route and open the process assignment modal.
4. Capture desktop and narrower screenshots.
5. Open manual picker from an assignment action and capture screenshot.
6. Update execution report, root README validation summary, subbundle statuses, and raw-note closure.
7. Run completed-stage validator.

## Scope Exceptions

- If a full end-to-end route cannot be seeded within the session, record the blocker and provide the strongest available component/browser proof. This is not acceptable if the modal cannot be visually inspected in a real browser.

## Do Not Do

- Do not close the bundle on tests alone.
- Do not mark screenshot validation complete without visually reviewing the images.

## Acceptance Checklist

- Screenshots visibly correspond to the attached design.
- No modal clipping, overlap, or unreadable text at validated viewports.
- Manual picker open-state proof exists.
- Execution report and raw-note closure are complete.

## Proof Required

- Test command output.
- Screenshot paths.
- Browser analytics rows.
- Prepared and completed validator output.

## Browser Validation Logging

- Record route, viewport, actions, assertions, screenshot paths, and result for each screenshot pass.

## Progression Gate

- Pass only if raw notes are closed with evidence and final validators pass.

## Suggested Agent Prompt

Execute subbundle 03. Treat screenshot review as a gate, not a courtesy. Reopen earlier subbundles if real browser proof is weak.
