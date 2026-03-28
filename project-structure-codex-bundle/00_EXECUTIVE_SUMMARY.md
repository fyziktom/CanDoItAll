# Executive summary

## Bottom line

Your current performance ceiling is not being set by HTML5 canvas itself.  
The current `ProjectStructurePage` hot path is still a DOM/SVG renderer with Blazor/InteractiveServer state chatter layered on top. That is why larger node counts begin to feel slow sooner than expected.

## What should happen next

Do **not** jump straight to a full true-canvas rewrite.

The highest-ROI path is:

1. stabilize overlay input ownership,
2. stop persisting hot-path state during active interaction,
3. batch node-move persistence,
4. stop forcing full surface reloads after simple mutations,
5. convert the renderer to retained DOM/SVG patching with viewport culling,
6. only then evaluate a real `<canvas>` renderer spike with benchmarks.

## Recommended ownership split

### JavaScript should own
- scene render loop,
- retained node/link/frame element maps,
- pan/zoom,
- drag loop and snap guides,
- viewport culling,
- transient selection/hover details needed only for interaction,
- floating-window drag/resize in progress,
- debug counters for render and patch behavior.

### C# should own
- domain entities and typed node metadata,
- services and DB mutations,
- action catalogs,
- graph adapters,
- structured create/edit/delete commands,
- final committed persisted view state,
- dialogs and rich overlay UI state.

### HTML/Blazor should remain responsible for
- toolbox,
- selection window,
- health window,
- dialogs,
- uploads,
- previews,
- transcript confirmation,
- mermaid viewer,
- summary modal,
- hierarchy dialogs.

Those overlays are not the problem by themselves. The problem is that they are not fully isolated from the scene host today.

## Highest-risk current issues

### 1) Event leakage from overlays into the scene host
The toolbox/floating-window path likely fails because overlay ownership is incomplete, especially for wheel, click, and context menu handling.

### 2) Active interaction is too expensive on InteractiveServer
Window drag, pan, zoom, and scene state changes should not continuously cause server callbacks, rerender work, and DB persistence.

### 3) Node moves are persisted inefficiently
Multi-node drag currently becomes N writes and a heavy reload path.

### 4) Simple mutations trigger heavyweight reload paths
Status/progress/marker/priority and note edits should not always end in full `GetStructureAsync()` + `SyncGraphAsync()` reload behavior.

### 5) The renderer rebuilds too much
The current renderer clears layers instead of patching them.

## Do not lose these features

The refactor must preserve all existing visible behavior, including:
- toolbox search and accordion groups,
- selection window states,
- quick action modal,
- hierarchy dialogs,
- attachment previews,
- summary exports,
- transcript provider confirmation,
- runtime launch actions,
- mermaid viewer,
- image export,
- PromptFactory and Sandbox shared-canvas behavior.

The bundle includes a traceability map and explicit validation gates so Codex does not “optimize away” important features.

## Success definition

This program is successful when:
- the toolbox behaves reliably,
- wheel inside overlays no longer zooms the canvas,
- active drag/pan/window movement stay local,
- node moves batch persist,
- simple mutations avoid full surface reloads,
- the renderer stops doing full layer teardown on common interactions,
- all mapped features and existing tests remain intact,
- browser screenshots and counters prove the improvements.
