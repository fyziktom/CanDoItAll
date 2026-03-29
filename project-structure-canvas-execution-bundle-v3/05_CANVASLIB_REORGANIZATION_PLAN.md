# CanvasLib reorganization plan

## Why this matters

`CanvasLib` is now large enough that folder structure itself affects productivity and correctness.

At the moment, the library mixes:
- active runtime workbench code,
- preview/boundary components,
- shared helpers,
- calendar code,
- generated public assets,
- legacy compatibility context.

That makes it harder to answer simple questions such as:
- what is the real runtime renderer?
- what is only a preview component?
- what code is safe to change for ProjectStructure without surprising PromptFactory?
- what should be loaded globally by the app shell?

## Target structure

```text
src/CanDoItAll.Components.CanvasLib/
  Canvas/
    Shared/
      Interop/
      Runtime/
      Theme/
    Workbench/
      Contracts/
      State/
      Runtime/
      Render/
      Interaction/
      Accessibility/
      Preview/           # only if runtime-facing preview helpers remain
    Calendar/
      Contracts/
      Runtime/
      Render/
      Interaction/
  Components/
    Shared/
      Accessibility/
      FloatingWindow/
      Assets/
    Workbench/
      CanvasWorkbench/
      Internal/
    Calendar/
    Preview/
      Boundary/
  wwwroot/
    js/
      canvasWorkbenchInterop.js      # generated public bundle
      canvas-floating-window.js      # generated public bundle
      canvaslib.preview.js           # optional generated preview bundle
    js-src/
      shared/
      floating-window/
      workbench/
        shared/
        state/
        render/
        interaction/
        overlays/
        export/
        runtime/
      preview/
      calendar/
    css-src/
      workbench/
      floating-window/
      preview/
  docs/
    canvaslib/
      README.md
      folder-structure.md
tools/
  canvaslib/
    build-assets.cjs
    verify-assets.cjs
```

## Reorganization rules

### 1) Preserve namespaces where useful
The physical folder move should not force avoidable public namespace churn.  
Keep `CanDoItAll.Components.CanvasLib` as the public namespace unless there is a clear benefit to a nested namespace change.

### 2) Separate runtime from preview
Preview/boundary components used by PromptFactory or Sandbox should be moved under a clearly named preview folder.  
Do not leave them next to the runtime workbench components as if they were the primary renderer.

### 3) Keep public asset URLs stable during the transition
Consumers should continue to resolve the same `_content/...` public assets while the implementation moves behind generated bundles or compatibility shims.

### 4) Keep ComponentKit out of the active refactor path
`CanDoItAll.ComponentKit` is already documented as legacy/compatibility-only.  
Do not refactor both trees in parallel unless a real consumer requires it.

## Reorganization matrix

| Current concern | Problem | Target area | Notes |
| --- | --- | --- | --- |
| Runtime workbench code mixed with preview helpers | Conceptual and maintenance confusion | `Canvas/Workbench/**` and `Components/Workbench/**` | Runtime renderer code should be the canonical path. |
| Shared helpers mixed with workbench specifics | Hard to reuse and hard to navigate | `Canvas/Shared/**` | Move interop bridge, scene host, theme tokens, shared runtime helpers here. |
| Preview-boundary components live next to runtime components | Makes the library look like it has many runtime primitives that it does not actually use | `Components/Preview/Boundary/**` | Keep these for PromptFactory support lane and tests. |
| Calendar and workbench share top-level folders without enough separation | Different product areas drift together | `Canvas/Calendar/**` and `Components/Calendar/**` | Calendar stays separate. |
| Public JS bundle is hand-edited monolith | Unsafe and hard to review | `wwwroot/js-src/**` + generated public output | Generated output remains compatible. |
| Public CSS bundle is hand-edited monolith | Unsafe and hard to review | `wwwroot/css-src/**` + generated public output | Generated output remains compatible. |
| App entrypoints duplicate script lists | Drift risk | `Components/Shared/Assets/**` | Add centralized include components/helpers. |

## Recommended component grouping

### Shared runtime
- `CanvasFloatingWindow`
- `AccessibilityMirrorLayer`
- asset include helpers
- shared theme/runtime helpers

### Workbench runtime
- `CanvasWorkbench`
- workbench internal overlays and stage helpers
- canvas renderer runtime code
- state and interaction helpers specific to workbench scenes

### Preview boundary
- `NodeCardComposer`
- `ConnectorPathPrimitive`
- `GroupFrameOverlay`
- `DiagnosticsOverlay`
- `MinimapOverview`
- related preview cards used by PromptFactory/Sandbox

### Calendar
Keep calendar code structurally separate from workbench code.

## Output of this task

A successful reorganization should make it immediately obvious:
- which files are runtime scene code,
- which files are preview-only,
- which files are shared utilities,
- which files are generated public outputs.
