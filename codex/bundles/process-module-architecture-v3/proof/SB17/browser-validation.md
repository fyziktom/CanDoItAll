# SB17 Browser Validation

## Route And Viewport

- Route: Process shell route exercised by `ProcessShellSmokeTests`.
- Browser: Playwright Chromium through the existing test harness.
- Viewport: 1600x1000 desktop-sized proof from the smoke test harness.

## Actions

1. Load the Process shell and global definition catalog.
2. Open a definition editor state that includes the role editor and definition canvas.
3. Wait for `processes-definition-canvas`.
4. Select `processes-canvas-node-step-decision-intake`.
5. Assert the selection panel contains `Capture architecture decision demand`.
6. Click toolbox action `processes-canvas-toolbox-process-step-implementation`.
7. Assert the command receipt contains `accepted`.
8. Click `processes-canvas-recompose`.
9. Assert the command receipt contains `recomposed`.
10. Capture `processes-definition-canvas.png`.

## Assertions And Screenshots

- Playwright transcript: bundle://proof/SB17/test-playwright-process-shell-sb17.txt
- Canvas screenshot: bundle://proof/SB17/browser/processes-definition-canvas.png
- Supporting shell screenshots:
  - bundle://proof/SB17/browser/processes-project-shell.png
  - bundle://proof/SB17/browser/processes-global-definition-catalog.png
  - bundle://proof/SB17/browser/processes-definition-role-editor.png

## Console And Network Summary

The focused Playwright test completed successfully with 1/1 tests passing. No Blazor error UI or failed assertion interrupted the canvas selection, toolbox action, or recomposition flow.
