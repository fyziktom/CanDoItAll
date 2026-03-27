
# File references

## Existing files to inspect first

- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`
- `tests/CanDoItAll.Tests.Playwright/PromptLibraryVerificationTests.cs`
- `playwright-prompt-factory-built-canvas.png`
- `playwright-prompt-flow-context-menu-final.png`

## Likely new files or folders

- `tests/CanDoItAll.Tests.Playwright/CanvasVisualRegressionEvidenceTests.cs`
- `artifacts/screenshots/README.md`

## Reuse guidance

- Prefer modifying existing modules and shared components before creating new parallel systems.
- Keep new files cohesive and small; do not scatter item logic across unrelated modules without a reason.
- When a file from another item is reused, preserve its shared nature and avoid item-specific hacks.
