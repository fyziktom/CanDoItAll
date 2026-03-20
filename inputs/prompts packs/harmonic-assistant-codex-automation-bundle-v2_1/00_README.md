# Harmonic Assistant – Codex Automation Bundle (v2.1)

This bundle upgrades the current Harmonic Assistant to be:
- **More “WOW” and usable** in Canvas (layout that scales with text size and avoids overlaps)
- **More modular** (toggleable assistant modules, each with an in-canvas widget)
- **More musical** (anti-loop planning, optional public-domain progression patterns)
- **More practical** (recording sessions + import/export)

## How to use (human)
1. Give Codex the **START PROMPT** below.
2. Ensure Codex runs prompts in `/03_CODEX_PROMPTS` in numeric order.
3. Verify with `/05_VALIDATION/qa-checklist.md`.

## START PROMPT
START PROMPT (Codex)

You are Codex. Implement the next iteration of the Realtime Harmonic Assistant:
- Canvas-first “killer” visualization with scalable spacing and a canvas widget system
- Move remaining Blazor controls into canvas widgets (only keep minimal Blazor scaffolding)
- Add modular assistant pipeline (toggleable modules with canvas widgets)
- Add anti-looping / novelty constraints in route planning
- Add optional public-domain progression pattern module (matcher + suggestion bias)
- Add recording + import/export of chord sessions
- Keep performance and determinism; add tests

Hard rules:
- Follow /03_CODEX_PROMPTS in numeric order. Do not skip steps.
- Keep all code comments in English.
- Prefer small, compilable checkpoints. After each step run tests.
- Never allow the graph to “wrap” into multiple horizontal rows; only one horizontal timeline with vertical offsets.
- When fontScale changes, also increase X spacing and lane spacing to prevent overlaps.
- Any new “module” MUST be toggleable and have a canvas widget control panel entry.

Start:
1) Read /00_README.md fully.
2) Read /02_DESIGN/* and all SVG diagrams.
3) Execute prompts 01.. in /03_CODEX_PROMPTS.
4) Update the Progress Checklist in /00_README.md after each prompt.

Begin now with /03_CODEX_PROMPTS/01_repo_audit_and_ui_inventory.md


## Progress Checklist
- [x] 01 Repo audit & UI inventory
- [x] 02 Canvas widget framework (UI registry + layout)
- [x] 03 Migrate remaining controls from Blazor to canvas widgets
- [x] 04 Layout scaling fix (spacing + collision guard)
- [x] 05 Modular assistant pipeline (C#) + settings + widget binding
- [x] 06 Anti-looping and novelty scoring in route planning
- [x] 07 Public-domain progression corpus format + loader
- [x] 08 Pattern matcher module + planning bias + UI widget
- [x] 09 Recording + import/export + UI widget
- [x] 10 Tests + performance + observability polish
- [x] 11 Final validation / cleanup

## Notes
- Canvas renderer currently scales node radius with fontScale, but **step spacing does not** (see `computeLayout` usage of `BASE_STEP_PX` and `BASE_LANE_PX`). This causes overlap when text grows; v2.1 fixes this.

## Final Summary

### What changed

- Harmony page is now canvas-first with minimal Blazor host UI (`canvas` + compact status line).
- Added a generic canvas widget system with item kinds:
  - `button`, `toggle`, `slider`, `dropdown`, `label`
- Migrated major controls to canvas widgets:
  - mood, text/graph settings, module controls, MIDI controls, recording controls.
- Added layout scaling and overlap guards:
  - spacing scales with `fontScale`,
  - overlap detection with zoom retry + stretch fallback.
- Added modular planning pipeline:
  - `IHarmonicAssistantModule`,
  - `NoveltyGuardModule`,
  - `PatternMatcherModule` (with compatibility wrapper `PatternCorpusModule`).
- Added public-domain/generic pattern corpus assets and loader:
  - `assets/harmonic-assistant/patterns/*.json`,
  - `PatternCorpusLoader`.
- Added pattern-derived annotations (`patternName`, `patternSource`) into suggestion metadata/tooltips.
- Added `HarmonicSessionRecorder` with:
  - start/stop/capture/export/import/replay.
- Added debug observability widget showing:
  - chord confidence,
  - inferred scale,
  - module contribution summary.

### How to test

- Build:
  - `dotnet build Zyphonote.slnx`
- Full tests:
  - `dotnet test Zyphonote.slnx --no-build`
- Focused harmonic tests:
  - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~HarmonicAssistantModuleTests|FullyQualifiedName~PatternMatcherModuleTests|FullyQualifiedName~PatternMatcherTokenizationTests|FullyQualifiedName~PatternCorpusLoaderTests|FullyQualifiedName~HarmonicSessionRecorderTests"`
- See validation report:
  - `docs/harmonic-assistant/v2_1-final-validation.md`

### Known limitations

- In this execution environment, launching a persistent local web host process for full interactive manual canvas QA was blocked by policy; interactive `/harmony` clicks were validated primarily through code-path checks and automated tests.
- Playwright suites in this repository are currently skipped by configuration (unchanged in this work).

### Next steps

- Add non-skipped Playwright coverage for `/harmony` canvas widgets (click/drag/slider/dropdown flows).
- Add richer widget collapsing/scrolling behavior for very dense widget panels.
- Add corpus-pack selection UI with explicit list rendering instead of cycle button.

## Post-run UX update (2026-02-27)

Additional improvements applied after the main v2.1 prompt sequence:

- Fixed V2 empty-state rendering so MIDI controls are visible before first chord history appears.
- Added accordion widget headers with collapse/expand and auto-collapse-on-play.
- Kept recording usable while minimized by adding compact transport controls.
- Added replay pause/resume/stop controls in the recording widget.
- Fixed phantom history chord insertion when changing settings/corpus by separating reconfigure from update.
- Added graph panning via middle-mouse drag and Alt+left-drag.
- Added regression test for non-appending reconfigure behavior:
  - `Reconfigure_DoesNotAppendHistoryEvents` in `tests/MusicTheory.Tests/RealtimeHarmonicAssistantTests.cs`.
