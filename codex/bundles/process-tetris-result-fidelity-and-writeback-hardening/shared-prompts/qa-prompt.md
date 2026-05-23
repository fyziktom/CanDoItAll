# QA Prompt

Use this prompt to validate a subbundle or final bundle closure.

```text
Validate the current subbundle against bundle://requirements/01-normalized-requirements.md and its README.

For process-runtime work:
- Verify tests cover both positive and negative governed paths.
- Reject prompt-only proof when runtime behavior can be tested.
- Confirm no hidden fallback marks failed writeback as complete.

For Tetris/browser work:
- Run Playwright against the actual delivered app route.
- Capture route, viewport, screenshot, snapshot, and console.
- Verify status leaves Loading.
- Trigger keyboard controls and assert visible game state changes.
- Verify high score writes to and can be read from localStorage.
- Reject proof that only clicks New game or counts board cells.
- Verify the app is static-hostable/no-backend when the contract requires it.

Record results in reviews/01-execution-report.md and cite artifact paths.
```
