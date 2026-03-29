# Performance hotspots and budgets

## Prioritized hotspots

| ID | Hotspot | Severity | Evidence | Impact | Target tasks |
| --- | --- | --- | --- | --- | --- |
| H01 | Runtime workbench scene is still DOM/SVG, not real canvas | critical | CanvasWorkbench.razor:121-126 hosts a div; canvasWorkbenchInterop.js:6018-6121 builds div/svg layers; real canvas appears only in export path at canvasWorkbenchInterop.js:6254-6290. | High DOM count, layout/reflow cost, SVG path churn, limited dense-scene headroom. | T10;T11;T12;T13;T14;T15 |
| H02 | Move flow still triggers full ReloadSurfaceAsync | critical | ProjectStructurePage.razor:952-965 batches persistence correctly but still calls ReloadSurfaceAsync after move. | Dense graphs pay unnecessary full graph reload and rebuild cost after every drag commit. | T03;T15 |
| H03 | View-state persistence remains on hot path | critical | ProjectStructurePage.razor:968-995, 1316-1330, 1426-1439 eagerly save state; PromptFactoryPage.razor:2867-2898 does the same. | InteractiveServer chatter, extra DB writes, surface refresh churn, latency spikes. | T02;T16 |
| H04 | Overlay input isolation is incomplete | high | canvasWorkbenchInterop.js:3038-3041 overlay selector does not cover floating windows/toolbox; wheel handler at 5880-5883 always preventDefault + zoom. | Broken toolbox expand/collapse and accidental canvas zoom while using overlays. | T00;T01;T04;T05 |
| H05 | Toolbox behavior and layout are incomplete | high | ProjectStructurePage.razor:160-170 renders two-line rows with no tooltip; ProjectStructurePage.ToolWindows.cs:83-91 cannot toggle closed-on-second-click; existing Playwright coverage misses expand/collapse. | Broken product behavior and poor usability on a frequently used authoring surface. | T00;T04;T05 |
| H06 | canvasWorkbenchInterop.js is a 6648-line monolith | high | Single file owns normalization, renderers, input, context menu, export, and runtime bootstrap. | Difficult maintenance, review risk, helper duplication, hard-to-localize regressions. | T06;T07;T08 |
| H07 | Global canvas-workbench.css is a 4324-line monolith | high | Shared scene styles, toolbox, floating windows, diagnostics, and component-specific rules live together. | High styling drift risk and difficult ownership boundaries. | T07;T08 |
| H08 | ProjectStructurePage markup remains extremely large | high | ProjectStructurePage.razor is 1808 lines even after some partial-class extraction; CSS is another 823 lines. | Difficult to reason about feature boundaries and page-specific regressions. | T04;T05;T08;T15 |
| H09 | App asset includes are duplicated and manual | medium | Web App.razor:25-70 and Sandbox App.razor:17-56 each hard-code long CanvasLib script lists. | Drift risk, missing script regressions, higher maintenance cost. | T06;T07;T17 |
| H10 | Preview-boundary components are mixed conceptually with runtime renderer code | medium | Tiny boundary JS shims exist in CanvasLib and are loaded globally; PromptFactory support lane uses several preview components while runtime workbench uses different code paths. | Confusion about what is actually shipping runtime behavior vs preview/demo evidence. | T06;T07;T09;T16 |
| H11 | PromptFactory is a second shared-canvas consumer with its own hot-path persistence issues | high | PromptFactoryPage.razor:2867-2898 eagerly persists UI state and refreshes the surface on shared callbacks. | Shared-canvas refactors can regress PromptFactory or leave its performance path behind. | T02;T16 |
| H12 | Legacy real-canvas ProjectStructureCanvas exists but is disconnected from the current runtime | medium | ProjectStructureCanvas.razor and workbenchInterop.js use real canvas but ProjectStructurePage now uses CanvasWorkbench instead. | Useful reference implementation, but also architectural confusion if left undocumented. | T09;T10;T17 |
| H13 | CanvasBenchmark exists but is not yet the authoritative migration evidence harness | medium | CanvasBenchmark.razor already compares retained preview and true-canvas prototype, but no rollout gate consumes it. | Renderer decisions risk being intuition-driven rather than measured. | T00;T10;T15;T17 |
| H14 | Export pipeline still assumes DOM clone instead of renderer-owned composition | medium | CanvasWorkbench.CaptureImageAsync delegates to exportImageData; current export path clones workbench DOM and paints through foreignObject/canvas. | The export path will break or become misleading once runtime scene layers move to real canvas unless rewritten. | T14;T15 |

## Minimum budgets and hard rules

| Metric / rule | Target | Notes |
| --- | --- | --- |
| Persistence writes during active pointermove / wheel | 0 | Must be locally buffered until commit/idle |
| ReloadSurfaceAsync after ordinary multi-node move | 0 | Use local patch path |
| Stage DOM element count in real-canvas runtime | Materially lower than baseline | Canvas stage should not scale linearly with node count in the same way |
| Renderer proof | Canvas layers visibly present in runtime stage | Do not claim canvas migration unless the stage is actually canvas-based |
| Toolbox wheel isolation | 0 unintended zoom changes | Measured in browser and/or diagnostic metrics |
| Generated source fragment size | Within split budgets | See file split plan |

## Metrics Codex should expose or log

At minimum, make the runtime expose or record:
- renderer kind (`dom-svg`, `hybrid-canvas`, `canvas`),
- stage DOM node count,
- visible node count,
- visible link count,
- redraw count,
- average render time,
- state publish request count,
- state publish commit count,
- persistence commit count,
- zoom event count,
- overlay-isolated wheel count,
- export duration.

## Benchmark strategy

Use the existing sandbox harness:
- `CanvasBenchmark.razor`
- `canvasBenchmarkPage.js`

Do not invent a separate benchmark page unless the existing one cannot support a required proof.

## Evidence rule

A performance claim is not accepted unless there is at least one of:
- browser metric output,
- benchmark result,
- DOM count reduction proof,
- screenshot-based proof of the new stage structure.

## Product-safety reminder

Never trade away feature parity silently for a performance win.
If a canvas optimization breaks:
- quick action dialog,
- note editing,
- export,
- tooltip behavior,
- selection parity,
the change is incomplete.
