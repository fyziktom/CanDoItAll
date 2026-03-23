# Shared Component Map

## Current surfaces to shared framework components

| Current surface / concern | Shared framework target | Notes |
| --- | --- | --- |
| CanvasWorkbench shell | CanvasWorkbenchShell + CanvasSceneHost | Keep current shell, decompose internals. |
| CanvasWorkbench stage layout | CanvasWorkbenchStageShell | Already strong; keep as shared stage frame. |
| Project Structure graph projection | ProjectStructureGraphAdapter | Remove from page. |
| Prompt Factory graph projection | PromptFactorySessionGraphAdapter | Remove from page. |
| Project Structure create catalog | ProjectStructureActionCatalogAdapter + CreateActionPalette | Keep domain catalog separate from shared UI. |
| Prompt Factory create catalog | PromptFactoryCatalogToolbox + CreateActionPalette | Keep domain catalog separate from shared UI. |
| Project Structure selection border | GroupFrameOverlay / domain metadata | Stop generating frame state purely in the page. |
| Prompt Factory undo/redo | CommandHistoryStore + PromptFactoryUndoRedoAdapter | Promote to shared infra. |
| Project Calendar wrapper | CanvasCalendarHost + ProjectCalendarAdapter | Replace legacy wrapper. |
| Context menus and quick create | ContextMenuHost + CreateActionPalette | Avoid page-level menu duplication. |

## Shared ownership rule

If a behavior appears in more than one canvas or is obviously needed by the next planned feature, it belongs in the shared framework unless it is truly a domain policy.
