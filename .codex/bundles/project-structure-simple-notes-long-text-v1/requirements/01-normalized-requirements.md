# Normalized Requirements

## Requirements

| ID | Requirement | Acceptance evidence |
| --- | --- | --- |
| `R001` | Creating a simple note with long, multiline text must persist the complete normalized note body in `Notes`. | Component test and browser proof check `Notes`/`InlineText` after create and after reload/runtime state refresh. |
| `R002` | Editing an existing simple note with long, multiline text must persist the complete body and derive a bounded display title from the first non-empty line. | Existing edit test extended or supplemented with long-text coverage. |
| `R003` | Quick-note creation must not use the whole long note body as persisted title when the note body is present. | Test asserts title equals the first non-empty line shortened by `BuildSimpleNoteTitle`; notes equal full body. |
| `R004` | Inline note commit behavior must make longer note entry predictable and must not silently lose intended text. | Browser proof enters long text through the inline composer and verifies runtime/persisted state. |
| `R005` | Rendered simple-note cards must use available width more effectively before text wraps or truncates. | Browser screenshot and DOM metrics show long/medium note cards wider than the previous fixed `14.25rem` when content warrants it. |
| `R006` | Note rendering must remain readable and stable: no text overlap with badges, annotations, collapse controls, or nearby nodes. | Screenshot review and Playwright assertions on bounding boxes/no overlap. |
| `R007` | The CanvasLib package consumed by CanDoItAll must be updated consistently if shared runtime assets change. | Package reference/version and local package artifact updated; build/test uses the updated package. |

## Hard Constraints

- Keep the implementation typed across Blazor/Workbench service boundaries. Do not introduce stringly-typed persistence shortcuts.
- Do not add a silent fallback that hides failed note saves. Existing create/edit failures should remain explicit or no-op only where current flow already behaves that way.
- Keep changes scoped to simple note creation/editing/rendering and package consumption.
- Do not alter unrelated dirty work in `C:/repositories/CanDoItAll.Components`.
- Use existing CanvasLib/Workbench patterns rather than custom page-local wrappers.
