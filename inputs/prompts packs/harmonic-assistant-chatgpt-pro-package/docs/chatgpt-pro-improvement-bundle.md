# ChatGPT Pro Improvement Bundle Guide

## Goal
This guide is for using ChatGPT Pro as a high-leverage collaborator for the Harmonic Assistant.
It describes the artifacts, workflows, and high-impact improvement loops.

## What to Feed ChatGPT Pro
1. Core architecture docs:
   - `docs/harmonic-assistant/harmonic-assistant-source-map.md`
   - `docs/harmonic-assistant/realtime-harmonic-assistant-deep-dive.md`
   - `docs/harmonic-assistant/harmony-razor-deep-analysis.md`
   - `docs/harmonic-assistant/wow-canvas-and-scoring-design.md`
2. Quality and validation docs:
   - `docs/harmonic-assistant/test-and-observability-plan.md`
   - `harmonic-assistant-codex-automation-bundle/05_VALIDATION/qa-checklist.md`
   - `harmonic-assistant-codex-automation-bundle/05_VALIDATION/performance-checklist.md`
3. Most relevant source files:
   - `src/App.Blazor/Pages/Harmony.razor`
   - `src/App.Web/wwwroot/harmonicAssistantCanvas.js`
   - `src/MusicTheory.Core/Recognition/RealtimeNoteScoreTracker.cs`
   - `src/MusicTheory.Core/Recognition/RealtimeChordDetectionService.cs`
   - `src/MusicTheory.Core/Theory/TonalScaleLibrary.cs`
   - `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs`

## Recommended ChatGPT Pro Prompts
### 1) Tuning pass (safe)
"Given these files, propose conservative parameter changes that improve C7 arpeggio stability without reducing detection responsiveness."

### 2) Failure analysis
"Given this debug panel output and MIDI trace, explain why the top detected chord is wrong and propose a bounded fix with tests."

### 3) Planning quality
"Given style pack X and inferred context Y, explain why path ranking is unintuitive and propose a deterministic scoring adjustment."

### 4) Performance review
"Audit canvas and scoring loops for avoidable allocations and recommend low-risk refactors."

## High-Impact Improvement Areas
### A) Scored detection confidence
- Add confidence to detection result model and debug panel directly.
- Calibrate acceptance thresholds by context density.
- Add scenario-based benchmark corpus for precision/recall drift checks.

### B) Scale context robustness
- Add weighted root-candidate priors from history.
- Add optional hysteresis to avoid context flicker between nearby modes.
- Add explicit dominant-blues hybrid handling for mixed major/minor thirds.

### C) Route planning realism
- Replace coarse counters with true rolling window state in path nodes.
- Add cadence-phase awareness (`approach`, `arrival`, `release`) to scoring.
- Add section-form priors for verse/chorus contrast.

### D) Canvas usability
- Persist text scale, zoom preference, and debug visibility.
- Add optional reduced-motion mode for accessibility.
- Add explicit collision-avoidance metrics for label readability.

## Guardrails for AI-Generated Changes
1. Keep beam search and recognition loops bounded.
2. Preserve single horizontal timeline invariant in canvas.
3. Keep settings externally adjustable; do not hardcode "magic" behavior.
4. Require tests for every algorithmic change.
5. Preserve deterministic ordering for ties.

## Regression Test Focus
- `RealtimeChordDetectionTests`
- `RealtimeNoteScoreTrackerTests`
- `TonalScaleLibraryContextInferenceTests`
- `RealtimeHarmonicAssistantTests`
- `RealtimeHarmonicAssistantContextTests`

## Tuning Workflow
1. Capture debug panel snapshot during a problematic phrase.
2. Ask ChatGPT Pro for root-cause hypotheses and a ranked fix list.
3. Implement smallest bounded fix.
4. Run targeted tests + full suite.
5. Re-evaluate with same debug scenario and compare.

## Known Gaps to Prioritize
- Automated end-to-end tests for `/harmony` interactions are still limited.
- Canvas render-time telemetry is not yet shown in panel.
- Hardware MIDI manual QA still required for final confidence before release.
