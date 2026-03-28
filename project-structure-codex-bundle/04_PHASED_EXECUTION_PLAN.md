# Phased execution plan

## Why the order matters

This program should not be executed as a random list of refactors.

If Codex starts with a big renderer rewrite or page split before stabilizing input ownership and persistence chatter, it will increase regression risk and make performance evidence harder to trust.

The required order is:

1. stabilize user interaction,
2. remove pathological server and DB chatter,
3. reduce expensive reload paths,
4. make the current renderer retained and culled,
5. modularize and harden validation,
6. only then consider a true-canvas spike.

## Phase summary

### P0 — Stabilize and de-risk
Focus:
- overlay guards,
- commit-only persistence,
- batched move persistence,
- no full reloads for simple changes,
- runtime cleanup,
- instrumentation and browser gates.

### P1 — Remove the largest renderer bottlenecks
Focus:
- retained DOM/SVG scene,
- viewport culling,
- dirty-region drag loop,
- lazy/isolated overlay rendering.

### P2 — Make the platform maintainable
Focus:
- structured plain-JS modularization,
- dedicated browser regression suite.

### P3 — Strategic optional work
Focus:
- true-canvas renderer benchmark spike,
- duplicate-library cleanup only after the main path is stable.

## Ordered task list

### P0-01 — Overlay input isolation and wheel ownership
- **Goal:** Make floating windows, toolbox content, dialogs, popovers, and support overlays fully own their pointer, wheel, focus, and context-menu interactions.
- **Depends on:** None
- **Primary files:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js, src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor, src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor, tests/CanDoItAll.Tests.Components/CanvasWorkbenchTests.cs, tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs
- **Exit signal:** Wheel inside toolbox no longer changes scene zoom.; Clicking a toolbox accordion header never starts canvas selection or pan.; Right-click inside toolbox/floating-window content never opens the scene context menu.; Existing node/canvas context menu behavior still works on the scene.

### P0-02 — Commit-only floating-window persistence
- **Goal:** Keep floating-window drag/resize local in JS and persist geometry only on commit or idle.
- **Depends on:** P0-01
- **Primary files:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvas-floating-window.js, src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor, src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor, src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor, tests/CanDoItAll.Tests.Components/CanvasFloatingWindowTests.cs, tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs, tests/CanDoItAll.Tests.Playwright/PromptLibraryVerificationTests.cs
- **Exit signal:** Zero `SaveViewStateAsync` calls while actively dragging or resizing a floating window.; Exactly one persisted state update after drag/resize commit.; PromptFactory floating toolbox still drags and restores correctly.

### P0-03 — Commit-only canvas state persistence and UI-state ownership cleanup
- **Goal:** Keep pan/zoom/live viewport state in JS during interaction and persist only the final idle/commit snapshot.
- **Depends on:** P0-01
- **Primary files:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js, src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor, src/CanDoItAll.Components.CanvasLib/Canvas/CanvasWorkbenchContracts.cs, src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor, tests/CanDoItAll.Tests.Components/CanvasWorkbenchTests.cs
- **Exit signal:** No `SaveViewStateAsync` during active pan/zoom.; No `RefreshCanvasSurface()` triggered by pure viewport movement.; ProjectStructure drag no longer persists both domain X/Y and long-lived UI manual positions.

### P0-04 — Batch node-move persistence
- **Goal:** Persist multi-node drag as a single mutation and a single save transaction.
- **Depends on:** P0-03
- **Primary files:** src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor, src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs, tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs
- **Exit signal:** Multi-node drag produces one service call and one DB save transaction.; Drag commit keeps selected nodes selected.; Moved-node border adoption still behaves correctly.

### P0-05 — Avoid full surface reloads after simple mutations
- **Goal:** Stop calling the heavyweight structure reload path for every non-structural change.
- **Depends on:** P0-04
- **Primary files:** src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor, src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs, src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs, tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs
- **Exit signal:** Status/progress/marker/priority changes no longer force full structure reloads.; Inline note edit no longer needs a full reload when only the note node changed.; Create/delete/link flows still end in consistent graph state.

### P0-06 — Runtime surface cleanup and support/demo separation
- **Goal:** Slim the runtime page and clearly separate production authoring UI from support/demo cards.
- **Depends on:** None
- **Primary files:** src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor, src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css, tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs
- **Exit signal:** ProjectStructure runtime page no longer renders always-on demo cards.; User-facing runtime behavior is clearer and lighter.; Any moved support functionality remains reachable where intended.

### P0-07 — Instrumentation and browser gates foundation
- **Goal:** Add the measurement and screenshot infrastructure required for safe refactoring.
- **Depends on:** None
- **Primary files:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js, tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs, tests/CanDoItAll.Tests.Playwright/PromptLibraryVerificationTests.cs
- **Exit signal:** Codex can prove improvements with counters and screenshots rather than only with anecdotes.; A failing browser gate is considered a failed task.

### P1-01 — Retained DOM/SVG renderer for nodes, links, and frames
- **Goal:** Keep the current renderer model but make it retained and patch-based rather than rebuild-based.
- **Depends on:** P0-03, P0-07
- **Primary files:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js
- **Exit signal:** Normal drag/pan no longer clears and rebuilds node and link layers.; Retained element maps stay consistent after create/delete/link/collapse operations.

### P1-02 — Viewport culling and filtered scene projection
- **Goal:** Render only what matters for the current viewport and interaction context.
- **Depends on:** P1-01
- **Primary files:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js, src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs
- **Exit signal:** Rendered visible node count is materially smaller than total node count on large graphs.; Selection/focus still works when selected nodes move into or out of view.

### P1-03 — Dirty-region drag loop owned by JS
- **Goal:** Keep drag, pan, guides, and affected links entirely in JS with minimal patch scope.
- **Depends on:** P1-01
- **Primary files:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js
- **Exit signal:** Active drag updates only moved nodes, affected links, and active guides.; Guide rendering stays correct while render cost drops materially.

### P1-04 — Selection-panel decomposition and lazy expensive support surfaces
- **Goal:** Reduce the Razor render tree and compute heavy overlay sections only when needed.
- **Depends on:** P0-06
- **Primary files:** src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor, src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.SelectionPanel.cs, src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Workflows.cs, tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs
- **Exit signal:** Selection UI remains feature-complete.; Unrelated viewport changes do not force large overlay recomputation.

### P2-01 — Scene patch protocol and plain-JS modularization
- **Goal:** Make the JS layer maintainable without TypeScript or a new bundler requirement.
- **Depends on:** P1-01, P1-02, P1-03
- **Primary files:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js, src/CanDoItAll.Components.CanvasLib/wwwroot/js/*.js
- **Exit signal:** Public API stays stable or is migrated with tests in the same task.; Hot-path JS is easier to reason about and code ownership is explicit.

### P2-02 — Dedicated screenshot and performance regression suite
- **Goal:** Turn the validated runtime states into a maintainable browser regression suite.
- **Depends on:** P0-07
- **Primary files:** tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs, tests/CanDoItAll.Tests.Playwright/*.cs
- **Exit signal:** Browser regressions are easier to localize.; Codex can rerun a precise subset of Playwright tests after each subbundle.

### P3-01 — Optional true-canvas renderer spike
- **Goal:** Benchmark an actual canvas renderer only after the current architecture is stabilized and measured.
- **Depends on:** P1-01, P1-02, P1-03, P2-01, P2-02
- **Primary files:** src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js, src/CanDoItAll.Components.CanvasLib/**, tests/CanDoItAll.Tests.Playwright/**
- **Exit signal:** A go/no-go decision is backed by measured evidence, not intuition.

### P3-02 — Optional shared-library consolidation
- **Goal:** Retire or intentionally isolate the duplicated canvas component trees after the main fixes are complete.
- **Depends on:** P2-01
- **Primary files:** src/CanDoItAll.ComponentKit/**, src/CanDoItAll.Components.CanvasLib/**
- **Exit signal:** There is one clearly canonical shared canvas implementation path or an explicitly documented reason for temporary duality.


## Exit criteria by phase

### P0 exit criteria
- overlay interactions are reliable,
- no DB writes during active viewport or window movement,
- node moves batch persist,
- simple node-property edits avoid unnecessary full reload,
- runtime page is slimmer and less confusing,
- counters and browser gates exist.

### P1 exit criteria
- hot-path interactions stop clearing and rebuilding whole node/link layers,
- viewport culling is measurable on large graphs,
- selection overlays remain feature-complete,
- large graphs feel materially better than the pre-P1 baseline.

### P2 exit criteria
- the JS layer is maintainable without TypeScript,
- browser regressions are localized and repeatable,
- shared canvas changes are easier to reason about.

### P3 exit criteria
- true-canvas go/no-go is backed by benchmark evidence,
- duplicate library cleanup is either complete or intentionally deferred with justification.

## Hard sequencing rule

Do not start:
- `P1-01` before `P0-03`,
- `P1-02` before `P1-01`,
- `P1-03` before `P1-01`,
- `P2-01` before the retained renderer path is stable,
- `P3-01` before P0 and P1 have evidence,
- `P3-02` before the team agrees which shared library is canonical.
