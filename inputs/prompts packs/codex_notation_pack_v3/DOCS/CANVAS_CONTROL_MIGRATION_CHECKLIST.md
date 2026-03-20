# CANVAS_CONTROL_MIGRATION_CHECKLIST.md

## Status
- [x] C01 Home tab contains `Undo` and `Redo`.
- [x] C02 Added `Tools` ribbon tab with `Clear Sheet` (`app.edit.clearSheet`).
- [x] C03 Playback tab contains `Initialize MIDI`, `WebAudio`, and `MIDI` mode controls.
- [x] C04 Playback tab contains recording session controls: `Start Record`, `Stop Record`.
- [x] C05 Playback tab contains recording setup controls: insert measure, start grid, duration grid, min duration, staff split, sustain pedal.
- [x] C06 Migrated controls are hidden from legacy Blazor UI.
- [x] C07 Diagnostics overlay default is off.
- [x] C08 Top score-start vertical spacing increased by ~20% from computed canvas top margin.

## Iteration Log

### Iteration 1
- Implemented HUD ribbon expansion:
  - Added `tab.tools` and `app.edit.clearSheet`.
  - Added playback MIDI controls and full recording controls/actions in canvas ribbon.
  - Added recording/midi state parameters through `NotationEditor.razor -> NotationEditorShell.razor -> NotationEditorCanvas.razor` and exposed these in `window.__notationEditorStateSnapshot`.
  - Set diagnostics default to off in host/shell/canvas defaults.
  - Increased `ResolveHudTopPadding()` output by 20%.
- Hid duplicated legacy controls after canvas migration:
  - Removed top-card Undo/Redo/Clear Sheet buttons.
  - Removed legacy MIDI Initialize and Playback Output mode controls.
  - Removed legacy Recording tab.
- Updated Playwright coverage:
  - Extended migrated toolset action assertions.
  - Added tests for tools clear-sheet, playback MIDI mode toggles, playback recording controls, diagnostics default off.
  - Raised ribbon/staff spacing assertion from 20px to 24px.

## Test Runs
1. `dotnet test tests/App.Web.PlaywrightTests/Zyphonote.App.PlaywrightTests.csproj --filter "FullyQualifiedName~E2E_NotationEditor_CanvasHud_ExposesMigratedToolsetAcrossRibbonTabs|FullyQualifiedName~E2E_NotationEditor_CanvasHud_ToolsClearSheet_ClearsInsertedNotes|FullyQualifiedName~E2E_NotationEditor_CanvasHud_PlaybackMidiModeButtons_UpdateSnapshotState|FullyQualifiedName~E2E_NotationEditor_CanvasHud_PlaybackRecordingButtons_UpdateSnapshotState|FullyQualifiedName~E2E_NotationEditor_CanvasHud_Diagnostics_DefaultsOff|FullyQualifiedName~E2E_NotationEditor_CanvasHud_RendersRibbonAboveStaffWithoutOverlap" --logger "console;verbosity=minimal"`
   - Result: Passed command execution; 6 tests skipped because Playwright gating env (`RUN_PLAYWRIGHT`) was not enabled.
2. `dotnet test Zyphonote.slnx --nologo`
   - Result: Passed.
   - Summary: `MusicTheory.Tests` 242 passed, `ORMServer.Tests` 30 passed, `API.Tests` 7 passed, `App.PlaywrightTests` 46 skipped (Playwright gating env not enabled).
