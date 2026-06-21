# SB16 Browser Validation

## Playwright Smoke Proof

- Route: `/processes`
- Scenario: searched for `architecture`, selected `architecture-decision-governance`, edited and saved role details, applied the `process-role.solution-architect` template action, captured global and project route screenshots.
- Transcript: `test-playwright-process-shell-sb16.txt`
- Screenshots:
  - `browser/processes-definition-role-editor.png`
  - `browser/processes-global-definition-catalog.png`
  - `browser/processes-project-shell.png`

## In-App Browser Proof

- App started through dotnetwatch and verified at the local Process route.
- Verified selected definition: `Architecture decision governance and ADR stewardship`.
- Verified role editor present: true.
- Verified step binding panel present: true.
- Verified role command receipt after Save/Apply: `Accepted: Role 'Solution architectBrowser proof architecture steward SB16' customized from Blank role.`
- Verified Blazor error UI visible: false.
- State artifact: `browser/browser-proof.json`.

## Notes

- The in-app Browser CDP screenshot command timed out twice (`Page.captureScreenshot`) after the state proof had already passed. Visual screenshot evidence is therefore supplied by the passing Playwright smoke test artifacts.
