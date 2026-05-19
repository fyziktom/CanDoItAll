# 07 Maintainability File Splits

## Status

- Status: `Completed`

## Objective

Refactor oversized cognitive-memory files into smaller logical units after behavior tests exist, while preserving API, persistence, and UI behavior.

## Covered Inputs

- Refactor map from subbundle 01.
- Behavioral tests from subbundles 04, 05, and 06.
- Current large files in recall, advanced services, consolidation, settings, review UI, and Blazor page.

## Prerequisites

- Subbundle 01 must produce a final refactor map.
- Behavior tests for affected areas must already pass.
- No unrelated user changes may be overwritten.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs

## Deliverables

- Smaller files split by behavior.
- Shared helpers only where they reduce duplication or make tests clearer.
- No public API route drift.
- Build and targeted tests passing.
- UI/browser proof if Blazor page changes.

## Dependency Impact

- Can run after core behavior is covered.
- Feeds closure subbundle and long-term maintainability.
- Must avoid broad renames that make review harder without reducing complexity.

## Validation Depth

- Build.
- Targeted unit/integration/component tests for touched areas.
- Browser/Playwright proof for UI changes.
- Route contract smoke if API mapper is split.

## Implementation Steps

1. Confirm tests pass before refactor.
2. Split one responsibility at a time.
3. Keep public types and route contracts stable unless explicitly changed.
4. Run tests after each meaningful split.
5. Update inventories and workbook.

## Do Not Do

- Do not perform mechanical churn across unrelated files.
- Do not invent trivial interfaces.
- Do not change behavior while claiming a pure refactor.
- Do not ignore UI browser proof if Razor files change.

## Acceptance Checklist

- Largest touched files are smaller and behaviorally coherent.
- Tests pass.
- API routes remain stable.
- UI remains usable if touched.
- Refactor notes explain the new boundaries.

## Proof Required

- Before/after file inventory.
- Build/test output.
- Browser evidence if UI changed.
- Execution report update.

## Execution Proof

- Split new stable responsibilities out of oversized services without broad UI churn: external text extraction, staged-source manifest validation, consolidation fact extraction, and typed model execution profiles now live in dedicated files.
- Kept larger Blazor/recall/advanced-service splits as documented future refactor targets because this pass needed behavior proof first and no UI routes were changed.
- No browser validation was required because no Blazor UI behavior or markup was modified.
- Final unit, integration, component, and solution build proof covers the touched boundaries.

## Browser Validation Logging

- Browser validation is required for any UI page/component change.
- Record route, viewport, Playwright evidence, screenshot paths, and result.

## Progression Gate

- Proceed to closure only after refactor tests and browser proof pass.

## Suggested Agent Prompt

Refactor only the oversized files covered by tests. Keep route, persistence, and UI behavior stable. Capture before/after inventory and validation evidence.
