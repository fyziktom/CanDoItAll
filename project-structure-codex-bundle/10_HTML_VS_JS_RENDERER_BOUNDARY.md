# HTML vs JS-renderer boundary

## Important correction

The right goal is **not** “put everything in canvas.”

That would create new problems:
- weaker accessibility,
- harder text input behavior,
- more custom hit testing,
- more custom focus and keyboard handling,
- more difficult debugging.

## Keep these HTML/Blazor

These should stay as overlays or normal DOM UI:

- toolbox,
- selection window,
- health window,
- quick action dialog,
- project hierarchy dialogs,
- summary modal,
- transcript provider confirmation,
- mermaid viewer,
- media and document previews,
- uploads and form fields,
- settings/help overlays.

## Move more responsibility to JavaScript where it matters

What JavaScript should own much more aggressively:

- scene object element maps,
- node/link/frame patching,
- viewport culling,
- pointer ownership,
- overlay-vs-scene event guards,
- drag/pan/zoom loop,
- transient geometry and dirty-region updates,
- instrumentation counters.

## Product-specific nuance

The user concern that “many things are still HTML with CSS” is valid only when those things are incorrectly placed in the hot path or when their input leaks into the scene.

In this workbench:
- HTML overlays are **appropriate**,
- but they must be completely isolated from the scene host,
- and their movement/state must not continuously force expensive page persistence.

## What should become lighter or move out of runtime

Even when UI stays HTML, it does not all need to stay on the runtime page at all times.

Good candidates to move behind debug/support gating:
- CanvasBoundaryCard demo sections,
- explanatory support-only content,
- any large always-on support card that is not required for normal authoring.

## Final architecture rule

- **Scene density and interaction mechanics:** JS-owned.
- **Rich overlay UI:** HTML/Blazor-owned.
- **Domain truth and persistence:** C#-owned.

That is the boundary this bundle optimizes for.
