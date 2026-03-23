# Playlist Builder Canvas

This folder documents the playlist builder canvas implementation currently living in:

- `C:\repositories\zyphonote-web\src\account-playlists.php`
- `C:\repositories\zyphonote-web\src\assets\js\zy-playlist-builder-page.js`
- `C:\repositories\zyphonote-web\src\assets\js\zy-learning-pack-canvas.js`
- `C:\repositories\zyphonote-web\src\assets\js\zy-canvas-workbench.js`
- `C:\repositories\zyphonote-web\src\input.css`

The important architectural conclusion is that the playlist builder is not a single component. It is a layered feature:

1. A PHP page shell defines the host layout and server data payload.
2. A playlist-specific page controller owns manifest editing, inspector rendering, outline rendering, and score-library actions.
3. A generic canvas engine renders the package, sections, and items and handles selection, zoom, pan, drag, and context actions.
4. A generic workbench helper provides the floating context menu and optional ribbon, toolbar, and dock chrome.

This matters for the Blazor port. The best first version is not a full rewrite. The best first version is a Blazor wrapper around the existing generic canvas engine, with the playlist-specific page logic split into proper .NET models and components.

## Key conclusions

- The canvas engine is already reusable enough to justify a JS interop wrapper.
- The playlist builder page controller is specialized and should not be copied as-is into a shared library.
- The engine is a structured tree and mindmap hybrid, not a free-form graph editor.
- The current layout is fixed to `package -> section -> item`.
- The current playlist builder uses only score items, but the engine already knows about additional item types such as text, checkpoint, and image.

## Documentation map

- [Architecture: file map and data contract](architecture/file-map-and-data-contract.md)
- [Engine: playlist builder page controller](engine/playlist-builder-page-controller.md)
- [Engine: learning pack canvas engine](engine/learning-pack-canvas-engine.md)
- [Engine: workbench and canvas chrome](engine/workbench-and-canvas-chrome.md)
- [Behavior: layout, rendering, and interactions](behavior/layout-rendering-and-interactions.md)
- [Guidelines: Codex rebuild checklist](guidelines/codex-rebuild-checklist.md)
- [Rebuild: Blazor JS interop component plan](rebuild/blazor-jsinterop-component-plan.md)

## Fast rules for Codex

- Treat `zy-learning-pack-canvas.js` as the real reusable core.
- Treat `zy-playlist-builder-page.js` as feature-specific orchestration.
- Do not model this as an arbitrary node graph on the first Blazor implementation.
- Preserve the current interaction model first, then generalize later.
- Keep canvas rendering and hit testing in JavaScript. Use Blazor for state, inspector UI, commands, persistence, and composition.
