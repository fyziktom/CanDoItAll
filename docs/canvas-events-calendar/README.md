# Events Calendar Canvas

This folder documents the events calendar implementation currently living in:

- `C:\repositories\zyphonote-web\src\account-events.php`
- `C:\repositories\zyphonote-web\src\assets\js\zy-account-events-page.js`
- `C:\repositories\zyphonote-web\src\assets\js\zy-canvas-calendar.js`
- `C:\repositories\zyphonote-web\src\assets\js\zy-canvas-primitives.js`
- `C:\repositories\zyphonote-web\src\api\account-events-calendar.php`

The important architectural conclusion is that the calendar is a mostly self-contained widget. Unlike the playlist builder canvas, the calendar engine builds almost the entire UI inside one host element:

1. The PHP page injects boot data and one host div.
2. A page adapter wires the engine to the PHP API.
3. The calendar engine builds the toolbar, canvas stage, list view, side panel, and modal editor.
4. Shared primitives provide the resize-aware canvas surface, date math, hit testing, mini months, and timed-grid drawing.

This matters for the Blazor port. The easiest first version is a wrapper around the full JS calendar widget. Later, after parity is stable, the surrounding DOM UI can be split into Blazor-owned pieces if that is still desirable.

## Key conclusions

- `zy-canvas-calendar.js` is already a reusable calendar engine, not just page-specific glue.
- The component is canvas-first, but not canvas-only. It also owns DOM-based toolbar, list, side panel, and editor surfaces.
- Styling is injected by the calendar script itself, not mainly by `input.css`.
- The calendar supports day, week, month, year, and list views on the same event model.
- The engine already supports CRUD, drag, resize, timezone switching, playlist linking, and export callback hooks.
- There is no pan or zoom model. Interaction is view-based and region-based.

## Documentation map

- [Architecture: file map and data contract](architecture/file-map-and-data-contract.md)
- [Engine: account events page adapter](engine/account-events-page-adapter.md)
- [Engine: canvas calendar engine](engine/canvas-calendar-engine.md)
- [Engine: primitives and styles](engine/primitives-and-styles.md)
- [Behavior: layout, rendering, and interactions](behavior/layout-rendering-and-interactions.md)
- [Guidelines: Codex rebuild checklist](guidelines/codex-rebuild-checklist.md)
- [Rebuild: Blazor JS interop component plan](rebuild/blazor-jsinterop-component-plan.md)

## Fast rules for Codex

- Treat `zy-canvas-calendar.js` as the reusable core.
- Treat `zy-account-events-page.js` as the PHP page adapter, not the component itself.
- Treat `zy-canvas-primitives.js` as a required dependency of the calendar engine.
- Do not assume CSS lives outside the engine. The current component injects its own style block.
- Preserve the current canvas rendering and hit-testing in JavaScript for the first Blazor version.
- Do not redesign this into a generic scheduler before reproducing the current behavior.
