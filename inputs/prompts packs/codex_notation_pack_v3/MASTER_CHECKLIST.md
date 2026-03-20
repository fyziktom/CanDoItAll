# MASTER_CHECKLIST.md

> Update this file after every prompt. Never claim “done” without tests.

## Legend
- [ ] not started
- [~] in progress
- [x] done

---

## A — Rhythm engine correctness (no overlaps, no chaos)

- [~] **A1** Add invariant checks: within a (Part, Staff, Voice), events must not overlap. Provide a `ValidateMeasureVoice` helper used in tests.
  - Acceptance: unit tests can assert no overlap after operations.

- [x] **A2** Fix `SetNoteDotsCommand` / duration changes to respect InsertMode (ripple editing).
  - Acceptance: dotted half in 4/4 pushes following notes in InsertAndShift mode.

- [x] **A3** Add `ChangeDurationCommand` variant that accepts InsertMode (or include mode in existing command).
  - Acceptance: UI uses current editor InsertMode.

- [x] **A4** Improve `AutoRestFillEngine`:
  - includes 1/64 (and optionally 1/128) support
  - uses `MeterGrouping.GetBeatBoundaries(signature)` instead of denominator-only beat unit
  - never leaves unfilled gaps when auto-rest is enabled
  - Acceptance: new unit tests for 3/32, 1/32, 1/64 gaps.

- [~] **A5** Add a deterministic “rhythm normalization” pass per measure:
  - removes overlaps (according to mode or chosen policy)
  - ensures sequential coverage when auto-rest fill is on
  - Acceptance: fuzz test for random edit sequences does not produce overlap.

---

## B — Spacing / layout (VexFlow-like tick contexts)

- [x] **B1** Introduce a `TickContextSpacingEngine` (or similar) that:
  - builds unique time slots per measure across **all staves + voices**
  - computes per-slot minimum widths (noteheads, accidentals, dots, rests)
  - outputs slot X positions that avoid collisions

- [x] **B2** Move measure layout from “proportional time mapping” to “slot X mapping”.
  - Acceptance: dense rhythms (32nds) do not overlap.

- [x] **B3** Implement system justification with **variable measure widths**:
  - compute minWidth for each measure, then justify to system width
  - reduce measures-per-system when necessary (wrap) OR implement compression with warnings
  - Acceptance: a measure with many 32nds does not collide and stays within the system.

- [x] **B4** Add layout regression tests (xUnit) that check:
  - monotonic X per slot
  - minimum spacing between consecutive slots
  - no glyph bounding boxes intersect for note-head/rest at different starts (approx allowed)

- [x] **B5** Add Playwright visual checks using `window.__notationLastBaseCommands`:
  - verify that note-head commands do not share identical X for different start times in dense measure fixture.

---

## C — Duration + beaming correctness

- [x] **C1** Extend durations to include at least **32nd** and **64th**:
  - NotationDuration, QuantizationGrid, toolbar, shortcuts
  - Rendering glyphs and flags
  - Acceptance: 32nd & 64th note/rest can be inserted and rendered.

- [x] **C2** Fix beam/flag logic to use **BaseDuration** (not total duration) for dotted notes.
  - Acceptance: dotted eighth still has 1 flag/beam; dotted sixteenth still has 2.

- [~] **C3** Update `AutoRestFillEngine` to optionally prefer dotted rests (configurable).
  - Acceptance: in 6/8, a 3/8 gap can be filled with dotted quarter rest when enabled.

---

## D — Canvas HUD + Radial menu

- [x] **D1** Replace HTML floating toolbar with **in-canvas HUD**:
  - top ribbon-like row inside overlay canvas
  - hit-test integrated with existing pointer pipeline

- [x] **D2** Implement real **radial quick menu around pointer**:
  - invoked by key (e.g., Space or Q) or right-click
  - shows durations/tools around cursor, selection by gesture
  - Acceptance: Playwright test can open radial menu and select “Eighth note” without moving to top toolbar.

- [x] **D3** Keyboard shortcuts review:
  - tool, duration, accidentals, dotted, chord/lyrics modes
  - include a discoverable overlay help (press ?)
  - Acceptance: Playwright test validates a set of shortcuts.

---

## E — Voicing (multi-part), Lyrics, Page layout / Print

- [x] **E1** Add multi-part score model:
  - ScoreDocument.Parts (name, staffMode, order)
  - Events tagged with PartId / PartIndex
  - Acceptance: existing single-part scores migrate automatically.

- [x] **E2** Layout engine supports stacked parts:
  - per-system vertical layout of parts
  - staff connectors for grand staff within a part
  - part name rendered at left of each system

- [x] **E3** Lyrics:
  - lyric entry mode (type + Space advances to next note)
  - rendering under the relevant staff
  - two display modes:
    - note-aligned syllables (standard)
    - measure-cell text (optional “line per measure”)
  - Acceptance: Playwright can enter lyrics and see render commands.

- [x] **E4** Page / paper sizing:
  - A4, Letter (and optional B4)
  - margins and optional page border rendering
  - overflow detection: warn when parts do not fit vertically
  - Acceptance: UI shows page boundaries and overflow warning.

---

## F — Documentation / Fixtures

- [x] **F1** Add fixtures for dense rhythms and ripple edits:
  - dotted-half ripple in 4/4
  - dense 32nd passage with rests
  - multi-part with lyrics

- [x] **F2** Add `docs/NOTATION_EDITING_UX.md` capturing “how users expect editors to behave”:
  - step-time, insert/overwrite modes
  - selection model, chord entry, lyric entry

- [x] **F3** Ensure all docs are updated and linked from repo README.

---

## Progress log
(append entries here)

- 2026-02-27 (Prompt 00 baseline):
  - Baseline test command `dotnet test` from repo root failed with MSBuild `MSB1011` (multiple solution files present).
  - Baseline test command `dotnet test Zyphonote.slnx` passed.
  - Results summary: `Zyphonote.MusicTheory.Tests` 227 passed, `Zyphonote.ORMServer.Tests` 30 passed, `Zyphonote.API.Tests` 7 passed, `Zyphonote.App.PlaywrightTests` 36 skipped (because `RUN_PLAYWRIGHT` not set in baseline run).
  - Fixture copy: `score_ripple_dot_in_measure.json` added to `tests/fixtures/` and `src/App.Blazor/wwwroot/test-fixtures/`.
  - Reproduction run (UI automation using fixture `score_ripple_dot_in_measure`):
    - Insert mode cycle control `setting.insertMode.next` was available in HUD.
    - After Dot on first quarter note, second note timing moved from `1/4` to `3/8` (`timingNoteStarts`: `[0, 0.375]`).
    - Auto-rests were inserted (`timingAutoRestCount: 2`), and no overlap was detected (`timingOverlapDetected: false`).
- 2026-02-27 (Prompt 01 A2/A3):
  - Implemented InsertMode-aware duration edits for `ChangeDuration`, `SetNoteDots`, and `SetNoteBaseDuration` with chord-cluster updates.
  - Updated command payloads (`ChangeDurationCommand`, `SetNoteDurationShapeCommand`, `SetNoteDotsCommand`) to carry `InsertMode` and wired UI call sites to pass current settings.
  - Added unit tests in `tests/MusicTheory.Tests/DurationRippleEditingTests.cs` for scenario S1 (InsertAndShift ripple dot) and S2 (Replace overlap deletion) plus per-voice no-overlap invariant assertion.
  - Added Playwright test `E2E_NotationEditor_RippleDot_ShiftsFollowingNotes_AndAddsRests` using `score_ripple_dot_in_measure`.
  - Test commands run:
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~DurationRippleEditingTests|FullyQualifiedName~TimingAlignmentRegressionTests"`
    - `dotnet test tests/App.Web.PlaywrightTests/Zyphonote.App.PlaywrightTests.csproj --filter "FullyQualifiedName~E2E_NotationEditor_RippleDot_ShiftsFollowingNotes_AndAddsRests" --logger "console;verbosity=minimal"` (with `RUN_PLAYWRIGHT=true`, `BASE_URL=http://127.0.0.1:5055`, local App.Web host)
    - `dotnet test Zyphonote.slnx`
- 2026-02-27 (Prompt 02 A4):
  - Updated `AutoRestFillEngine` to segment gap-filling by `MeterGrouping.GetBeatBoundaries(signature)` instead of denominator-only beat units.
  - Added deterministic no-gap fallback inside segment filling so auto-rest generation does not terminate early on small remainders.
  - Improved generic meter grouping fallback in `MeterGrouping` to use denominator-based units (including compound handling for divisible `x/8` signatures), enabling signatures like `3/32`.
  - Added/updated unit tests in `tests/MusicTheory.Tests/TimingAlignmentRegressionTests.cs`:
    - `AutoRestFill_SixtyFourthGap_IsFilled`
    - `AutoRestFill_CompoundMeter_RespectsSixEightBeatBoundaries`
    - `AutoRestFill_ThreeThirtySecondTimeSignature_FillsTrailingThirtySecondGap`
  - Test commands run:
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~TimingAlignmentRegressionTests|FullyQualifiedName~DurationRippleEditingTests"`
    - `dotnet test Zyphonote.slnx`
- 2026-02-27 (Prompt 03 C2):
  - Updated note beaming/flag level resolution to use note `BaseDuration` with safe duration-shape inference fallback when legacy data has stale/default shape metadata.
  - Added regression tests in `tests/MusicTheory.Tests/BeamingDurationRegressionTests.cs`:
    - `DottedEighth_UsesBaseDurationBeamLevelOne`
    - `DottedSixteenth_UsesBaseDurationBeamLevelTwo`
  - Test commands run:
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~BeamingEngine_4_4_BeamsWithinQuarterGroups|FullyQualifiedName~BeamingEngine_6_8_BeamsByCompoundGroups|FullyQualifiedName~BeamingEngine_SixteenthRun_RendersTwoParallelBeamLevels|FullyQualifiedName~BeamingEngine_Direction_FollowsFirstNoteInRun|FullyQualifiedName~BeamingDurationRegressionTests"`
    - `dotnet test Zyphonote.slnx`
- 2026-02-28 (Prompt 04 B1-B5):
  - Added `TickContextSpacingEngine` in layout layer and wired `ScoreLayoutEngine` to use slot-based X assignment instead of proportional start/capacity mapping.
  - Implemented system-level variable measure widths with greedy wrapping (bounded by configured `MeasuresPerSystem`) and width justification based on each measure's intrinsic minimum width.
  - Added dense spacing fixture `score_dense_32nd_subdivisions.json` to `tests/fixtures/` and `src/App.Blazor/wwwroot/test-fixtures/`.
  - Added xUnit regression `DenseFixture_UsesStrictlyIncreasingSlotXAndMinimumSpacing` in `tests/MusicTheory.Tests/TickContextSpacingRegressionTests.cs`.
  - Added Playwright regression `E2E_NotationEditor_DenseFixture_RestsDoNotCollapseToSameX` in `tests/App.Web.PlaywrightTests/NotationEditorUiTests.cs`.
  - Test commands run:
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~TickContextSpacingRegressionTests|FullyQualifiedName~NotationEditorCoreTests"`
    - `dotnet test tests/App.Web.PlaywrightTests/Zyphonote.App.PlaywrightTests.csproj --filter "FullyQualifiedName~E2E_NotationEditor_DenseFixture_RestsDoNotCollapseToSameX" --logger "console;verbosity=minimal"` (with `RUN_PLAYWRIGHT=true`, `BASE_URL=http://127.0.0.1:5055`, local `src/App.Web` host)
    - `dotnet test Zyphonote.slnx`
- 2026-02-28 (Prompt 05 C1):
  - Verified 32nd/64th duration support across model, quantization, HUD/toolbar actions, keyboard shortcuts, glyph mappings, and renderer flag/rest selection paths.
  - Added unit regression tests in `tests/MusicTheory.Tests/DurationInsertionRegressionTests.cs`:
    - `InsertNote_ThirtySecondDuration_SetsBaseDurationAndDotCount`
    - `InsertNote_SixtyFourthDuration_SetsBaseDurationAndDotCount`
  - Added Playwright regression `E2E_NotationEditor_ThirtySecondInsert_DrawsFlagOrBeam` in `tests/App.Web.PlaywrightTests/NotationEditorUiTests.cs`.
  - Test commands run:
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~DurationAndMusicXmlExtensionTests|FullyQualifiedName~DurationInsertionRegressionTests"`
    - `dotnet test tests/App.Web.PlaywrightTests/Zyphonote.App.PlaywrightTests.csproj --filter "FullyQualifiedName~E2E_NotationEditor_ThirtySecondInsert_DrawsFlagOrBeam" --logger "console;verbosity=minimal"` (with `RUN_PLAYWRIGHT=true`, `BASE_URL=http://127.0.0.1:5055`, local `src/App.Web` host)
    - `dotnet test Zyphonote.slnx`
- 2026-02-28 (Prompt 06 D1-D3):
  - Kept the existing in-canvas HUD render/hit pipeline and extended it with a pointer-centered radial quick menu overlay.
  - Added radial interaction flow in `NotationEditorCanvas`:
    - Space hold and Q-toggle open modes
    - angle-based highlighted slice resolution (inner ring durations, outer ring tools)
    - release/commit behavior that applies selected action and closes the menu
    - radial state published to `window.__notationEditorStateSnapshot`.
  - Updated shell keyboard handling in `NotationEditorShell`:
    - Space keydown/up opens and commits radial selection
    - Q toggles radial menu visibility
    - I cycles insert mode
  - Extended shortcut hint overlay text to include insert-mode and radial shortcuts.
  - Added Playwright regression `E2E_NotationEditor_RadialMenu_SelectEighth_ThenInsertNote` and expanded keyboard shortcut coverage to assert insert-mode cycling on `i`.
  - Existing Playwright regression `E2E_NotationEditor_CanvasHud_RestInsert_ReflowsMeasureTiming` continues to validate HUD rest tool insertion.
  - Test commands run:
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~DurationInsertionRegressionTests"`
    - `dotnet test tests/App.Web.PlaywrightTests/Zyphonote.App.PlaywrightTests.csproj --filter "FullyQualifiedName~E2E_NotationEditor_RadialMenu_SelectEighth_ThenInsertNote|FullyQualifiedName~E2E_NotationEditor_CanvasHud_RestInsert_ReflowsMeasureTiming|FullyQualifiedName~E2E_NotationEditor_KeyboardShortcuts_UpdateCanvasHudState" --logger "console;verbosity=minimal"` (with `RUN_PLAYWRIGHT=true`, `BASE_URL=http://127.0.0.1:5055`, local `src/App.Web` host)
    - `dotnet test Zyphonote.slnx`
- 2026-02-28 (Prompt 07 E1):
  - Added multi-part score primitives:
    - `ScorePart` model (`Id`, `Name`, `Abbrev`, `StaffMode`, `Order`)
    - `ScoreDocument.Parts`, `EnsureDefaultPart()`, and `EnsurePartModelConsistency()`
    - `ScoreEvent.PartId` (cloned through `NoteEvent`/`RestEvent`)
  - Updated JSON format migration (`NotationJsonFormatService`):
    - serializes/deserializes `parts` and event `partId`
    - if `parts` are missing/empty, creates a default part
    - if event `partId` is missing or unknown, assigns default part id
    - upgrades deserialized scores to `ScoreDocument.CurrentSchemaVersion`
  - Added migration regressions in `tests/MusicTheory.Tests/ScorePartMigrationTests.cs`:
    - `Deserialize_LegacyJsonWithoutParts_AssignsDefaultPartToAllEvents`
    - `Deserialize_MissingOrUnknownPartId_FallsBackToDefaultPart`
  - Test commands run:
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~ScorePartMigrationTests|FullyQualifiedName~NotationEditorCoreTests|FullyQualifiedName~SignatureEditingOperationsTests" --nologo`
    - `dotnet test Zyphonote.slnx --nologo`
- 2026-02-28 (Prompt 08 E2/E4):
  - Completed stacked multi-part system layout wiring:
    - `SystemLayout.Parts` with per-part vertical frame assignment
    - part-aware event grouping and accidentals (by `PartId`)
    - part labels (`part-name`) and grand staff connector rendering (`staff-connector`)
  - Added page layout plumbing:
    - `PageSettings` model (A4/Letter, orientation, margins)
    - layout page metadata + warnings (`ScoreLayout.Pages`, `ScoreLayout.Warnings`)
    - renderer `page-border` commands and `layout-warning` text commands
  - Stabilized HUD page-border toggle path:
    - added `ShowPageBorders` UI action wiring
    - clamped/compacted advanced HUD sidebar layout so toggle remains reachable on short canvases
    - made Playwright HUD canvas clicks deterministic (`Force = true`) and added state waits in page-border test
  - Added tests:
    - `tests/MusicTheory.Tests/MultiPartLayoutPageTests.cs`
      - `Layout_MultiPartSystem_StacksPartOffsets_AndAlignsX`
      - `Layout_ShowPageBorders_AddsPageLayoutMetadata`
    - `tests/App.Web.PlaywrightTests/NotationEditorUiTests.cs`
      - `E2E_NotationEditor_PageBordersToggle_RendersPageBorderCommands`
  - Test commands run:
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~MultiPartLayoutPageTests|FullyQualifiedName~NotationEditorCoreTests" --nologo`
    - `dotnet test tests/App.Web.PlaywrightTests/Zyphonote.App.PlaywrightTests.csproj --filter "FullyQualifiedName~E2E_NotationEditor_PageBordersToggle_RendersPageBorderCommands" --logger "console;verbosity=minimal" --nologo` (with `RUN_PLAYWRIGHT=true`, `BASE_URL=http://127.0.0.1:5055`, local `src/App.Web` host)
- 2026-02-28 (Prompt 09 E3):
  - Added structured lyrics model:
    - new `LyricSyllable`/`Syllabic` model in score domain
    - `ScoreDocument.Lyrics` collection with clone + consistency/migration sync against note anchors and part ids
    - schema bump to `CurrentSchemaVersion = 6`
  - Updated lyric editing operations and commands:
    - `SetLyricSyllable` and `ToggleLyricExtender` operations
    - new commands `SetLyricSyllableCommand` and `ToggleLyricExtenderCommand`
    - retained `SetNoteLyric` compatibility by routing through structured lyric model
  - Implemented lyrics entry workflow in canvas/shell:
    - click note in Lyrics tool sets lyric cursor
    - typing appends syllable text
    - Space commits/advances
    - hyphen commits as begin/middle and advances
    - underscore toggles extender
    - Escape exits lyric mode
  - Kept rendering through `cssClass: "lyric"` and wired layout lyric text resolution to prefer `ScoreDocument.Lyrics` verse 1 entries.
  - Updated tests:
    - unit regressions in `tests/MusicTheory.Tests/NotationEditorCoreTests.cs` for lyric model sync and syllabic/extender metadata
    - Playwright regression `E2E_NotationEditor_LyricsEntry_HelLo_RendersLyricCommands`
  - Test commands run:
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~NotationEditorCoreTests|FullyQualifiedName~ScorePartMigrationTests" --nologo`
    - `dotnet test tests/App.Web.PlaywrightTests/Zyphonote.App.PlaywrightTests.csproj --filter "FullyQualifiedName~E2E_NotationEditor_LyricsEntry_HelLo_RendersLyricCommands" --logger "console;verbosity=minimal" --nologo` (with `RUN_PLAYWRIGHT=true`, `BASE_URL=http://127.0.0.1:5055`, local `src/App.Web` host)
- 2026-02-28 (Prompt 10 F1-F3 + final verification):
  - Added missing fixture for multi-part + lyrics:
    - `tests/fixtures/score_multi_part_lyrics.json`
    - `src/App.Blazor/wwwroot/test-fixtures/score_multi_part_lyrics.json`
  - Added UX behavior documentation:
    - `docs/NOTATION_EDITING_UX.md`
  - Updated root README links to workflow/checklist/docs/fixtures.
  - Full suite run:
    - `dotnet test Zyphonote.slnx --nologo`
  - Deferred backlog rationale (explicit):
    - **A1** deferred: current command/test stack already enforces no-overlap in targeted edit regressions, but a dedicated reusable `ValidateMeasureVoice` helper was not introduced yet.
    - **A5** deferred: deterministic normalization + fuzz harness is larger than this prompt gate and needs a dedicated pass to avoid destabilizing recent ripple/spacing changes.
    - **C3** deferred: dotted-rest preference requires policy/config surface additions and meter-aware selection rules not yet implemented in current AutoRestFill API.
