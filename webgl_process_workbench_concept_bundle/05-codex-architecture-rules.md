# Codex architecture rules

These are non-negotiable.

## Library boundary

- `CanDoItAll.Components.WebGlLib` must not reference `CanDoItAll.Modules.Processes`.
- Process-template projection belongs outside the universal library.
- Generic scene/event contracts must not hardcode process node kinds into the library core.

## Runtime boundary

- Rendering, hit-testing, drag preview, connection preview, and camera updates must stay in JavaScript.
- Blazor must receive coarse-grained semantic events, not per-frame interaction deltas.
- Do not place the frame loop in C#.

## Scene rules

- Default to a perspective camera for sandbox authoring.
- Keep the default layout deterministic and center-lane driven instead of free-fly.
- Use semantic Z-depth sparingly and intentionally.
- Keep labels in a DOM/HTML overlay or mirror layer.

## Sandbox rules

- Add a new dedicated sandbox project.
- Load real template processes from the existing template pack.
- Keep all edits in-memory and resettable.
- Do not write concept changes into the production Processes UI or persistence layer.

## Automation rules

- Expose a semantic runtime API modeled after the current canvas automation helpers.
- Expose host debug state and DOM mirror anchors.
- Add deterministic screenshot/export support.
- Never rely on screenshot-only validation for move/connect actions.

## Tooling rules

- No CDN dependencies for the concept runtime.
- Keep asset build steps explicit and committed.
- Mirror repository conventions where practical.
