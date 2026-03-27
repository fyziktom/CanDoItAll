
# File references

## Existing files to inspect first

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js`
- `tests/CanDoItAll.Tests.Components/PromptFactoryCatalogToolboxTests.cs`
- `tests/CanDoItAll.Tests.Components/PromptFactoryPageTests.cs`
- `tests/CanDoItAll.Tests.Integration/PromptFactoryServiceIntegrationTests.cs`

## Likely new files or folders

- `tests/CanDoItAll.Tests.Playwright/PromptFactoryDuplicateInsertRegressionTests.cs`

## Reuse guidance

- Prefer modifying existing modules and shared components before creating new parallel systems.
- Keep new files cohesive and small; do not scatter item logic across unrelated modules without a reason.
- When a file from another item is reused, preserve its shared nature and avoid item-specific hacks.
