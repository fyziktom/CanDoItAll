# Current State

## Repo Findings

- `repo://src/CanDoItAll.Modules.Workbench/Workbench/ProjectWorkbenchModels.cs` configures `ProjectObjectRecord.Notes` with `HasColumnType("TEXT")`, so the long-note bug is unlikely to be a database max-length problem.
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor` handles inline note edits through `HandleNodeEditedAsync`, deriving the title with `ProjectStructureNodeHelpers.BuildSimpleNoteTitle(request.Notes)` and storing `request.Notes` as the note body.
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor` handles creation through `ResolveCreatedTitle`. Current behavior returns `request.Title.Trim()` before checking the quick-note case. The CanvasLib quick note composer sends the full note text as both `title` and `notes`, so quick-note creation can persist a full multiline note as `Title` instead of a derived title.
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureNodeHelpers.cs` currently truncates simple note titles to 64 characters. That is acceptable for persisted title storage because `ProjectObjectRecord.Title` is limited to 200 characters, but it should not control the rendered note body.
- `repo://tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs` already checks inline note edit persistence. It does not deeply cover long quick-note creation with first-line title derivation and full body preservation.
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs` checks a multiline note, but currently asserts the runtime `Title` equals the multiline text. That can pass while the more important persisted note body contract remains weak.

## CanvasLib Findings

- `repo://ExternalPackages/CanDoItAll.Components.CanvasLib.0.1.0.nupkg` contains the consumed CanvasLib runtime assets. The editable source is available locally at `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.CanvasLib` as a local development aid.
- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/04-context-menu-and-composer.js` commits quick notes by reading `state.composer.textInput.value.trim()` on unmodified `Enter`. It sends the same text as both `title` and `notes`.
- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/01-foundation.js` estimates inline-note widths dynamically up to `348px`, with more width for longer text and long tokens.
- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.CanvasLib/wwwroot/css/workbench/scene/04-scene-and-nodes.css` fixes `.cw-node.is-inline-text` to `14.25rem`, so DOM cards do not use the dynamic width the layout engine reserves.
- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/02-layout-and-legacy-render.js` positions nodes but does not set DOM node width from `getNodeSize` for inline notes.

## Screenshot Analysis

- `bundle://inputs/01-canvas-reference.png` shows a large desktop viewport. Visible cards generally have enough horizontal space for more text than the current inline note card CSS allows.
- The visual issue should be validated on the live canvas, because the screenshot does not expose runtime sizing, measured node bounds, or actual long-note text.

## Initial Cause Hypothesis

- The storage issue is probably not database truncation. The more likely causes are quick-note title/body conflation, Enter committing the note before users finish multi-line longer notes, and tests checking runtime title rather than persisted note body.
- The visual truncation issue is likely caused by dynamic JS measurement and fixed CSS width disagreeing.
