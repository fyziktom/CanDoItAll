# I24 Root Cause Evidence

## Root cause

The duplicate-node bug was caused by the same create intent being accepted more than once inside a very small time window.

- The canvas runtime could dispatch the same create payload twice when a toolbox action, preview interaction, or repeated activation path landed on the same component add action in quick succession.
- The Prompt Factory page previously trusted every incoming `OnCreateAction` payload and created a node each time.
- Result: a single user intent could materialize two identical nodes, which is exactly what showed up in the 44-node bug report.

## Fix applied

The fix is intentionally layered.

- JS-side short window guard:
  [`C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`](C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js)
  now tracks `lastCreateSignature` and `lastCreateRequestedAt` and ignores duplicate create submissions inside a `450ms` window.
- Server-side/page-side guard:
  [`C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\CanvasAdapters\PromptFactoryCreateActionDeduplicator.cs`](C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\CanvasAdapters\PromptFactoryCreateActionDeduplicator.cs)
  builds a stable signature from action id, source, parent, placement, subtype, title, subtitle, notes, uploaded file name, and input values.
- Prompt Factory integration:
  [`C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.Catalog.cs`](C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.Catalog.cs)
  now short-circuits duplicate requests before node creation.

## Why this is the correct fix

- The JS guard removes the accidental double-dispatch path at the source.
- The C# guard keeps the page deterministic even if a duplicate request still slips through from another activation path.
- The signature includes the real creation payload, so distinct user intents are still allowed.

## Validation evidence

- Exhaustive Prompt Library verification passed after the fix:
  `dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj -m:1 --no-build --filter "FullyQualifiedName~PromptLibraryVerificationTests" /nodeReuse:false /p:UseSharedCompilation=false`
- The dedicated artifact capture test passed and intentionally dispatched the same component-add action twice:
  `dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj -m:1 --no-build --filter "FullyQualifiedName~Prompt_factory_artifacts_capture_toolbox_preview_and_single_add_flow" /nodeReuse:false /p:UseSharedCompilation=false`
- That test proves the behavior directly:
  duplicate dispatch does not create duplicate nodes.

## Artifact proof

- [`C:\repositories\CanDoItAll\artifacts\screenshots\i24\01-primary-state.png`](C:\repositories\CanDoItAll\artifacts\screenshots\i24\01-primary-state.png)
- [`C:\repositories\CanDoItAll\artifacts\screenshots\i24\02-secondary-state.png`](C:\repositories\CanDoItAll\artifacts\screenshots\i24\02-secondary-state.png)
- [`C:\repositories\CanDoItAll\artifacts\screenshots\i24\03-interaction-result.png`](C:\repositories\CanDoItAll\artifacts\screenshots\i24\03-interaction-result.png)

