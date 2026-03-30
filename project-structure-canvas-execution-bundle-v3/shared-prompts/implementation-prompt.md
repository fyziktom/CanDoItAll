# Implementation Prompt

Implement only against the current repository state.

- Keep the active scene canvas-based and do not reintroduce DOM or SVG scene layers.
- Preserve Blazor overlays, toolbox dialogs, and accessibility mirror behavior.
- Prefer the smallest correct change and regenerate the public CanvasLib asset from source when runtime JS changes.
- Revalidate with asset verification, component tests, Playwright tests, and bundle status updates before declaring closure.
