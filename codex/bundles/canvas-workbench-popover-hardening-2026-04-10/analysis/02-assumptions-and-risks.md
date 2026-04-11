# Assumptions And Risks

## Assumptions

- The reported crash is produced by the shared `CanvasWorkbench` runtime files loaded from `CanDoItAll.Components.CanvasLib`, not by a consumer page redefining `showPopover`.
- The workbench route and sandbox route share the same runtime bundle closely enough that `/groups/canvas` is a valid first browser-proof surface.
- Annotation hover is the popover trigger path behind the reported stack because `syncSceneHoverState` only calls the popover path for `type === "annotation"`.

## Critical Path Risks

- If subbundle 01 fixes only the missing binding but leaves stale `hoveredAnnotationKey` behavior intact, subbundle 02 and later browser proof can report false stability while still suppressing popovers after click or refresh.
- If the fix changes popover semantics for DOM-rendered annotation badges, downstream consumers can regress even when the canvas route looks fixed.
- If canvas popover show and hide paths stay inconsistent, later proof on one node layout will not be trustworthy for other annotation-bearing node shapes.

## Validation Risks

- Real workbench validation depends on a reachable seeded project route. If that route is unavailable, the bundle must record the gap explicitly instead of pretending sandbox-only proof fully closes the workbench complaint.
- Browser caches or stale static assets can hide the true JS result. The proof loop must force a rebuilt runtime and fresh page load.
- There is no direct JS unit-test harness for these runtime chunks in the current bundle path, so browser proof and targeted .NET validation matter more than usual.

## Reopen Triggers

- Reopen subbundle 01 if any browser or console proof still shows an uncaught exception from annotation hover or click.
- Reopen subbundle 02 if the popover fails to reopen after clicking the same annotation, after refresh, or after a node-selection rerender.
- Reopen any completed phase if real workbench proof diverges from sandbox proof in the same shared runtime path.
