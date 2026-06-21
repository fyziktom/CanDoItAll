# Target Solution

## Intended End State

Project-structure simple notes store their full text in the note body and show a derived, bounded title where a title is needed. The inline note card uses CanvasLib dynamic sizing consistently across layout and DOM rendering, so medium-length notes use available canvas space before wrapping.

## Boundaries

- Workbench owns typed create/edit requests, title derivation, and persistence.
- CanvasLib owns inline-note composer semantics, runtime measurement, DOM sizing, and canvas rendering.
- Tests own regression proof for long note body preservation and screenshot-backed layout behavior.

## Minimal Implementation Shape

- Update Workbench quick-note title normalization so `ProjectObjectType.Note` with `quick-note` create mode derives the title from note body before falling back to `request.Title`.
- Keep `Notes` as the full note text for create and edit.
- Update CanvasLib inline-note commit/rendering to avoid title/body conflation and to apply measured inline-note width to DOM nodes.
- Rebuild/update the consumed local CanvasLib package in `repo://ExternalPackages` and update package references only if required by versioning.
- Extend component tests and Playwright proof to check `Notes`, `InlineText`, runtime title, DOM width, and screenshot readability.

## Allowed Side Effects

- Canvas inline-note cards may become wider for medium/long text, up to the existing measurement cap.
- Existing short notes should retain compact width when text is genuinely short.
- Package version may be bumped to avoid stale NuGet cache ambiguity.
