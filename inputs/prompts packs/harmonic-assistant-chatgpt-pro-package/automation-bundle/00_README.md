# Codex Automation Bundle — Realtime Harmonic Assistant Upgrade

This bundle is designed to be handed to **Codex** to implement a major upgrade of the Realtime Harmonic Assistant:
- a **canvas-first, “WOW”** harmonic path visualizer (single horizontal timeline with branching),
- a **MIDI note scoring** layer for more robust chord detection (arpeggios + melody noise),
- and **significantly improved route planning** (better musical coherence, multi-dimensional harmonic space cues).

## How to use with Codex
1. Open the repository root in Codex (the full repo, not only this bundle).
2. Run prompts in `/03_CODEX_PROMPTS` **in numeric order**.
3. After each prompt:
   - build/test as instructed,
   - fix issues before moving forward,
   - commit incrementally.

## Bundle structure
- `/01_CONTEXT/` — architecture summary, constraints, glossary
- `/02_DESIGN/` — detailed spec + SVG diagrams (Codex uses these to avoid visual mistakes)
- `/03_CODEX_PROMPTS/` — sequential prompts with acceptance criteria + self-checks
- `/04_TESTS/` — test prompts + scenario data
- `/05_VALIDATION/` — QA and performance checklist
- `/06_PATCH_GUIDES/` — optional helper code templates/snippets

## Non-negotiables from product requirements
- Use **HTML5 Canvas** for the graph rendering (controls may also be drawn in canvas).
- The graph must be **one horizontal flow** (no wrapping / multi-row stacking).
- **Text size** must be adjustable via a **canvas UI control**.
- Harmony must map to **colors** and to a **vertical “mood axis”**:
  - red hues: sharper / more vivid harmonies,
  - blue & green: calmer harmonies,
  - dark shades: darker harmonies.
- Branches from the current chord go **rightward**:
  - brighter/happier routes go **up**,
  - darker routes go **down**.
- Long sessions must remain readable:
  - configurable history window,
  - auto-zoom-out when content doesn't fit,
  - (optional) compression/LOD.

All code comments must be in **English**.

Generated: 2026-02-26

## Progress Checklist
- [x] 01_baseline-and-branch.md
- [x] 02_add-canvas-snapshot-v2.md
- [x] 03_harmony-visual-mapping.md
- [x] 04_build-snapshot-v2-and-history-setting.md
- [x] 05_canvas-renderer-v2.md
- [x] 06_canvas-resize-and-polish.md
- [x] 07_note-score-tracker.md
- [x] 08_scored-chord-detection.md
- [x] 09_scale-context-inference.md
- [x] 10_route-planning-improvements.md
- [x] 11_observability-and-debug.md
- [x] 12_final-checklist.md

### Progress Notes
1. Prompt 01 complete.
2. Baseline tests:
   - RealtimeChordDetectionTests: passed (3/3)
   - RealtimeHarmonicAssistantTests: passed (3/3)
   - Playwright test project executed; all tests currently skipped by test attributes.
3. Working branch created: `feature/harmonic-assistant-wow-canvas-and-scoring`.
4. `02_DESIGN/spec.md` is not present in bundle; all numbered design docs and SVG files were used as the authoritative specification.
5. Prompt 02 complete.
6. Added semantic canvas snapshot DTO v2 + interop `RenderV2Async` + JS payload-shape compatibility shim.
7. Validation after prompt 02:
   - `dotnet build Zyphonote.slnx`: passed
   - `RealtimeHarmonicAssistantTests`: passed (3/3)
8. Prompt 03 complete.
9. Added shared mapping helper: `HarmonyVisualMapping` with:
   - `HarmonyColorMappingMode`
   - `HarmonyVisualMetrics`
   - deterministic Darkness/Energy/WorldY/ColorHex mapping
   - manual HSL->RGB hex conversion.
10. Added tests: `HarmonyVisualMappingTests` (3 passing).
11. Prompt 04 complete.
12. Added `HistorySteps` to `AssistantSettings` (default 32; clamped in engine to 8..256).
13. Updated engine history retention to respect `settings.HistorySteps`.
14. Added `History steps` UI slider on `/harmony`.
15. Added semantic `BuildCanvasSnapshotV2(...)` in `Harmony.razor`:
    - history/current/future nodes with `XIndex`, `WorldY`, `Color`
    - edges with probability
    - layout/render hints.
16. Added feature-flag render path:
    - `useCanvasV2 = false` keeps v1 renderer active until prompt 05.
17. Validation after prompt 04:
    - `dotnet build Zyphonote.slnx`: passed
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj`: passed (164/164)
18. Prompt 05 complete.
19. Rewrote `src/App.Web/wwwroot/harmonicAssistantCanvas.js` for v2 rendering:
    - single horizontal xIndex timeline (no wrapping)
    - mood-axis vertical projection centered on current chord
    - branch lane offsets (up/down grouping)
    - zoom-to-fit (`minZoom = 0.35`)
    - cubic curved connectors with color gradients
    - larger nodes, current-node emphasis, caption + tooltip support
    - in-canvas text controls (`A-`, `A+`) with pointer/touch handling.
20. Switched `/harmony` page to v2 rendering path (`useCanvasV2 = true`).
21. Validation after prompt 05:
    - `dotnet build Zyphonote.slnx`: passed
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj`: passed (164/164)
22. Prompt 06 complete.
23. Added ResizeObserver wiring for harmonic canvas:
    - Blazor interop: `ObserveResizeAsync(...)`
    - JS module: `observeResize(id, element)` with observer lifecycle tied to renderer disposal.
24. Verified DPR resize path remains crisp:
    - `canvas.width/height = logical * dpr`
    - `context.setTransform(dpr, 0, 0, dpr, 0, 0)`.
25. Added minor UX polish for canvas interaction:
    - `.harmony-canvas { touch-action: none; cursor: default; }`
    - pointer cursor on interactive text controls and hoverable nodes.
26. Validation after prompt 06:
    - `dotnet build Zyphonote.slnx`: passed
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeChordDetectionTests|FullyQualifiedName~RealtimeHarmonicAssistantTests|FullyQualifiedName~HarmonyVisualMappingTests"`: passed (9/9)
27. Prompt 07 complete.
28. Added floating note-scoring subsystem:
    - `RealtimeNoteScoreOptions` (window/decay/boost/threshold tunables)
    - `RealtimeNoteScoreSnapshot` + `RealtimePitchClassScore`
    - `RealtimeNoteScoreTracker` with lazy exponential decay, hold/sustain accumulation, pruning, and bass pitch-class estimation.
29. Integrated tracker into `RealtimeChordDetectionSessionService`:
    - MIDI events now update both window detector and score tracker
    - manual note mode resets/applies both
    - evaluation now captures `scoreSnapshot` each cycle (detection wiring deferred to prompt 08).
30. Added tracker tests:
    - `RealtimeNoteScoreTrackerTests.Snapshot_ArpeggioSequenceRetainsPitchClassProfile`
    - `RealtimeNoteScoreTrackerTests.Snapshot_SustainHeldNotesAccumulateLessThanPressedNotes`.
31. Validation after prompt 07:
    - `dotnet build Zyphonote.slnx`: passed
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeNoteScoreTrackerTests|FullyQualifiedName~RealtimeChordDetectionTests"`: passed (5/5)
32. Prompt 08 complete.
33. Upgraded `RealtimeChordDetectionService`:
    - added overload `Detect(snapshot, scores, options)`
    - preserved legacy overload by forwarding with `scores = null`
    - added scored pitch-class loop (`StartPitchClassCount` .. `MaxPitchClassCount`)
    - added confidence/acceptance gating (`MinDetectionConfidence`, `MinMatchedPitchClasses`)
    - filtered observed notes to selected pitch classes and preserved bass hint when needed.
34. Extended `RealtimeChordDetectionOptions` with scored-detection tunables:
    - `MinDetectionConfidence`, `MinMatchedPitchClasses`
    - `StartPitchClassCount`, `MaxPitchClassCount`
    - `MinPitchClassScoreToInclude`.
35. Wired session detection call to pass score snapshots:
    - `RealtimeChordDetectionSessionService.EvaluateNowAsync` now calls `Detect(snapshot, scoreSnapshot, options)`.
36. Added scored-detection tests in `RealtimeChordDetectionTests`:
    - arpeggio C7 scenario remains detectable
    - melody overlay scenario keeps C7 identity stable.
37. Validation after prompt 08:
    - `dotnet build Zyphonote.slnx`: passed
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeChordDetectionTests"`: passed (5/5)
38. Prompt 09 complete.
39. Extended `ModeType` with additional scale vocab:
    - `MajorPentatonic`
    - `MinorPentatonic`
    - `BluesMinor`
    - `BluesMajor`.
40. Expanded `TonalScaleLibrary`:
    - added definitions + mode priorities for pentatonic and blues modes
    - added deterministic inference API:
      `GetCandidateScalesForPitchClassScores(pitchClassScores, impliedRootPitchClass, maxResults)`.
41. Exposed inferred scale context in detection results:
    - `RealtimeChordDetectionResult` now includes `InferredScaleContext`
    - `RealtimeChordDetectionService.Detect(...)` now computes inferred context from scored pitch classes (or active notes fallback) while preserving `CompatibleScales` on each chord candidate.
42. Updated app/test construction paths for `RealtimeChordDetectionResult` to supply inferred context.
43. Added test:
    - `TonalScaleLibraryContextInferenceTests.PitchClassScores_BluesProfileRanksCMinorBluesNearTop`.
44. Validation after prompt 09:
    - `dotnet build Zyphonote.slnx`: passed
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~TonalScaleLibraryContextInferenceTests"`: passed (1/1)
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeChordDetectionTests|FullyQualifiedName~RealtimeHarmonicAssistantTests"`: passed (8/8)
45. Prompt 10 complete.
46. Added tonal-distance helper:
    - `src/MusicTheory.Core/Generation/Realtime/HarmonicDistance.cs`
    - `MinCircleOfFifthsSteps(int pcA, int pcB)` for circle-of-fifths distance scoring.
47. Upgraded `RealtimeHarmonicAssistantEngine` planning with a contextual scoring model:
    - built internal `PerformanceContext` from detection inferred scales
    - integrated style-pack device weights (`DeviceWeights`) into transition scoring
    - added rolling constraint penalties using style constraints:
      `MaxChromaticBarsPer8`, `MaxSecondaryDominantsPer8`, `MaxSubstitutionsPerCadence`
    - added tonal coherence term via circle-of-fifths distance
    - added mood-axis continuity penalty via `HarmonyVisualMapping.ComputeWorldY`
    - added scale-context coverage bonus and blues-context dominant grammar bias.
48. Transition generation metadata now includes:
    - `DeviceName`
    - `IsChromatic`
    - `IsSecondaryDominant`
    - `IsSubstitution`.
49. Added blues-aware transitions (`I7/IV7/V7`) when inferred context is blues.
50. Added context-focused planning tests:
    - `RealtimeHarmonicAssistantContextTests.BluesContext_PromotesDominantGrammarInTopSuggestions`
    - `RealtimeHarmonicAssistantContextTests.StyleWeights_ChangeTopPathScoringAndReasons`.
51. Validation after prompt 10:
    - `dotnet build Zyphonote.slnx`: passed
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeHarmonicAssistant"`: passed (5/5)
52. Prompt 11 complete.
53. Added lightweight structured debug logging with throttling:
    - `RealtimeChordDetectionSessionService` now logs evaluation latency, top detected chord, top inferred scale context, and pitch-class count at debug level.
    - `HarmonicAssistantSessionService` now logs engine update latency, top detection, top inferred scale, and top suggestion probability at debug level.
    - both services guard logging with `logger.IsEnabled(LogLevel.Debug)` and suppress repeated signatures for ~2 seconds.
54. Added optional debug panel on `/harmony` behind `Show debug` toggle:
    - top detection candidates
    - top pitch-class score profile
    - inferred scale context candidates
    - top suggestion paths with consolidated reasons.
55. Validation after prompt 11:
    - `dotnet build Zyphonote.slnx`: passed
    - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeChordDetectionTests|FullyQualifiedName~RealtimeHarmonicAssistant"`: passed (10/10)
56. Prompt 12 complete.
57. Added final deep docs for implementation and tuning:
    - `docs/harmonic-assistant/wow-canvas-and-scoring-design.md`
    - `docs/harmonic-assistant/harmony-razor-deep-analysis.md`
    - `docs/harmonic-assistant/chatgpt-pro-improvement-bundle.md`
    - updated `docs/harmonic-assistant/README.md` index and verification snapshot.
58. Full test run:
    - `dotnet test Zyphonote.slnx`: passed
    - totals:
      - MusicTheory.Tests: 171 passed
      - OMR.Service.Tests: 30 passed
      - API.Tests: 7 passed
      - App.Web.PlaywrightTests: 10 skipped (existing skip attributes).
59. Manual QA and performance checklist status:
    - verified by implementation/tests: canvas single-horizontal flow, no wrapping logic, text-size controls, auto-zoom, scored detection loop bounds, note tracker bounds, beam-search bounds, debug log throttling.
    - requires local workstation manual verification: interactive browser resize/rotation behavior and hardware MIDI scenarios from `qa-checklist.md` (cannot be executed in this environment due process policy and hardware constraints).

## Final Summary
### What changed
- Canvas:
  - semantic snapshot v2 + responsive JS layout
  - mood-axis vertical mapping, branch lanes, centerline, tinting, curved edges
  - text-size controls and resize observer support
- Detection:
  - realtime floating note scoring tracker
  - scored pitch-class recognition loop with confidence gating
  - scale-context inference from score profiles (including pentatonic/blues)
- Planning:
  - inferred context used in beam scoring
  - style device weights + constraints integrated
  - circle-of-fifths distance + mood continuity terms
  - blues grammar emphasis when inferred context is blues
- Observability:
  - debug logging with throttling
  - optional `/harmony` debug panel with core internals
- Tests/docs:
  - new unit tests for tracker, scored detection, context inference, planning context
  - full documentation refresh for ChatGPT Pro-assisted iteration

### How to test
1. Build:
   - `dotnet build Zyphonote.slnx`
2. Full tests:
   - `dotnet test Zyphonote.slnx`
3. Targeted harmony tests:
   - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeChordDetectionTests|FullyQualifiedName~RealtimeHarmonicAssistant|FullyQualifiedName~TonalScaleLibraryContextInferenceTests|FullyQualifiedName~RealtimeNoteScoreTrackerTests"`
4. Manual QA:
   - execute `harmonic-assistant-codex-automation-bundle/05_VALIDATION/qa-checklist.md`
   - use `Show debug` toggle on `/harmony`

### Known limitations
- Full manual QA with live browser interaction and MIDI hardware remains environment-dependent.
- Playwright harmony E2E tests are still skipped in current repo test configuration.
- Canvas render-time metric is not yet surfaced in UI.

### Next steps
1. Run full manual QA on target devices (desktop resize + mobile rotation + MIDI keyboard scenarios).
2. Add non-skipped `/harmony` Playwright coverage for debug panel and canvas controls.
3. Add optional render-time telemetry in debug panel for tighter performance tuning.
