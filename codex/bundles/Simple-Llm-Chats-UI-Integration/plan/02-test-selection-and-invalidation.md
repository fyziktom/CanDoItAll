# Focused Test Selection And Invalidation

## Per-Subbundle Loop

1. Compute the actual diff against the subbundle start commit.
2. Put only changed production/test paths and exact one-based changed ranges into `changes`.
3. Put inspected but unchanged files into `contextOnlyPaths`.
4. Call `code_analytics_impacted_tests_get` with every relevant lane workspace and `behaviorIntent=Unknown`.
5. Verify workspace health, source discovery, test discovery, resolved symbols, confidence, and fallback reason.
6. Run every required selector and verify non-zero expected discovery.
7. Promote conditional selectors only when a stated trigger occurs.
8. Re-run impacted analysis after the final diff; do not rely on the pre-edit answer.

## Intent Rule

`BehaviorPreservingImplementation` may be asserted only after the conservative `Unknown` result and only when observable Razor output, callbacks, accessibility, CSS/scroll behavior, public contract, and runtime behavior are intentionally unchanged.

## Broad Gate

- SB01-SB11: no unfiltered Stable solution.
- SB12: one unfiltered `tests/Solutions/CanDoItAll.Tests.Stable.slnx` run at the frozen candidate commit.
- A failed targeted owner test promotes related conditional consumers before any broad gate.

## Browser Scope

Use named Playwright scenarios only. Target 1600x1000 or maximized desktop. Capture and inspect normal state plus relevant open dialogs/dropdowns/floating windows. Do not run small/mobile passes.
