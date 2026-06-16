# SB18 Browser Validation

## Route And Viewport

- Route: `/processes`, then `/projects/{projectId}/processes?runId=55555555-5555-5555-5555-555555555555`.
- Harness: Playwright through `CanDoItAll.Tests.Playwright`.
- Viewport: 1440x900.

## Actions

1. Opened the global Process shell and selected the Definitions tab.
2. Searched for `architecture` and selected `architecture-decision-governance`.
3. Saved and published the definition editor draft.
4. Selected the canvas decision-intake step, added an implementation step, and recomposed the canvas.
5. Opened the step editor and asserted the selected step text `Capture architecture decision demand`.
6. Set operation target scope to `ExternalArtifactDestination`, checked `WriteExternalArtifactDestination`, and saved.
7. Added a branch outcome, set route target to `PreviousStep`, set loop budget to `2`, and saved.
8. Added an artifact expectation.
9. Changed step kind to `Subprocess`, selected `dotnet-development-slice`, and mapped the subprocess.
10. Exercised the role editor, Feed Defaults, and project-scoped Process route as dependent smoke coverage.

## Assertions

- Step editor rendered selected step content.
- Step save receipt contained `saved`.
- Add-branch receipt contained `added`.
- Branch route with loop budget save receipt contained `saved`.
- Add-artifact receipt contained `added`.
- Subprocess map receipt contained `mapped`.
- No Blazor error UI was visible.
- Browser summary recorded `FailedRequests=0` and `PageErrors=0`; two `/_blazor/disconnect` cleanup posts were recorded as ignored expected Blazor disconnect noise.

## Screenshots

- bundle://proof/SB18/browser/processes-definition-step-editor.png
- bundle://proof/SB18/browser/processes-definition-canvas.png
- bundle://proof/SB18/browser/processes-definition-role-editor.png
- bundle://proof/SB18/browser/processes-global-definition-catalog.png
- bundle://proof/SB18/browser/processes-project-shell.png

## Console And Network Summary

- bundle://proof/SB18/browser/browser-validation-summary.txt

Result: passed through Playwright; no Blazor error UI, page error, unexpected failed request, or assertion failure interrupted the owned flow.
