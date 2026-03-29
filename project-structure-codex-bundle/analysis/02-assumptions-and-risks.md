# Assumptions And Risks

## Working Assumptions
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib` remains the canonical active canvas runtime path.
- `ProjectStructurePage` is still the primary runtime surface under refactor.
- Shared canvas changes must be checked against PromptFactory and Sandbox-visible flows.
- Playwright MCP and managed watch remain available for browser proof throughout execution.

## Critical Path Risks
- Shared JS or floating-window changes may regress PromptFactory or Sandbox while improving ProjectStructure.
- The bundle task list is broad enough that stale assumptions can accumulate unless each phase is closed before moving on.
- Later renderer work can invalidate earlier proof if interaction ownership or persistence behavior is still weak.

## Validation Risks
- UI changes without large-screen screenshots and open-overlay proof will produce false confidence.
- Persistence and render-cost claims are weak unless backed by counters, logs, or targeted assertions.
- Existing tests may cover only happy paths for some risky behaviors such as border adoption and overlay wheel ownership.

## Reopen Triggers
- A later subbundle reveals a defect in an earlier critical foundation.
- Shared-canvas browser tests fail after a local ProjectStructure improvement.
- Playwright shows clipping, layering, or event leakage in an overlay state that a test did not explicitly assert.
- A task appears implemented in code but cannot be proven with current runtime behavior.
