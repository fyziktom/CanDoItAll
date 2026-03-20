# Realtime Harmonic Assistant: Deep Documentation Pack

## Purpose
This folder documents how the Realtime Harmonic Assistant currently works, why it behaves that way, where its constraints are, and how to improve it safely.

This is written as a direct handoff for engineering work and for ChatGPT Pro-assisted refactoring/design.

## Scope
The analysis is centered on:
- Harmony page orchestration (`Harmony.razor`)
- Session orchestration layer
- Realtime chord detection pipeline
- Harmonic engine internals (hypothesis update, transitions, beam search)
- Canvas rendering pipeline (C# interop + JS renderer)
- Tests and product requirements that define expected behavior

## Document Index
- `realtime-harmonic-assistant-deep-dive.md`
  End-to-end architecture and behavior, from UI events to generated suggestions.
- `harmony-razor-deep-analysis.md`
  Deep breakdown of `Harmony.razor` orchestration, lifecycle, and snapshot construction.
- `wow-canvas-and-scoring-design.md`
  Final implementation design for canvas v2, MIDI scoring, context inference, and route planning.
- `algorithm-and-scoring-notes.md`
  Exact scoring logic, formulas, thresholds, and search behavior.
- `harmonic-assistant-source-map.md`
  Curated file map of all code and docs directly related to the assistant.
- `chatgpt-pro-improvement-bundle.md`
  Practical high-impact guide for using ChatGPT Pro to tune and extend this subsystem.
- `improvement-roadmap-for-chatgpt-pro.md`
  Prioritized improvement backlog with concrete implementation guidance.
- `test-and-observability-plan.md`
  Current test coverage, important gaps, and recommended instrumentation.
- `chatgpt-pro-workflow.md`
  Practical workflow + prompts for using ChatGPT Pro to improve this subsystem.
- `baseline-validation.md`
  Test command/results snapshot used as the current baseline.

## Baseline Verification Performed
Focused unit tests executed:
- `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeHarmonicAssistantTests"`
- `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeChordDetectionTests|FullyQualifiedName~RealtimeHarmonicAssistant|FullyQualifiedName~TonalScaleLibraryContextInferenceTests|FullyQualifiedName~RealtimeNoteScoreTrackerTests"`
- Result: 16 passed, 0 failed.

## Important Current Risks (Quick View)
- Lock key toggle currently does not lock to any key unless `LockedKeyPitchClass` is set, but UI does not expose a key picker.
- Harmony page does not provide MIDI input selection/connection controls.
- Hardware MIDI QA still requires manual workstation testing.
- Canvas render-time metric is not yet surfaced in UI.

## Snapshot
Analysis generated against repository state at:
- Date: 2026-02-26
- Workspace: `c:\repositories\zyphonote`
