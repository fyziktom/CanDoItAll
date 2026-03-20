# 10 — Improve route planning (style weights + constraints + tonal distance + scale context)

Goal: significantly improve planning quality in `RealtimeHarmonicAssistantEngine` by incorporating:
- style pack device weights and constraints,
- multi-dimensional harmonic space cues (circle-of-fifths distance),
- mood-axis continuity (same mapping as canvas),
- inferred scale context (from scored notes).

## Files to modify
- `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs`
- `src/MusicTheory.Core/Generation/HarmonicStylePack.cs` (if helper metadata is needed)
- `src/MusicTheory.Core/Generation/HarmonicStylePackLibrary.cs` (optional: add helper lookups)
- Add helpers:
  - `src/MusicTheory.Core/Generation/Realtime/HarmonicDistance.cs` (circle-of-fifths functions)
- Tests:
  - `tests/MusicTheory.Tests/RealtimeHarmonicAssistantTests.cs`
  - add new `tests/MusicTheory.Tests/RealtimeHarmonicAssistantContextTests.cs`

## 1) Extend engine input context
Ensure `RealtimeChordDetectionResult` exposes inferred scale context (from Prompt 09).
In `Update(detection, settings, ...)`:
- extract:
  - current best chord candidate
  - inferred scale context candidates (top 1..3)
- create a small internal `PerformanceContext`:
  - `PrimaryScaleCandidate` (root + mode + pitch classes)
  - `IsBluesContext` (true if primary scale is BluesMinor or BluesMajor)
  - `PitchClassScoreProfile` (optional)

## 2) History retention respects settings
Implement configurable history retention using `settings.HistorySteps`:
- replace `.TakeLast(31)` with `.TakeLast(settings.HistorySteps - 1)` (clamp safely)
This is required for “history step-count must be configurable”.

## 3) Apply style pack device weights + constraints
When generating transitions:
- annotate each `TransitionCandidate` with:
  - `DeviceName` (string? null for diatonic)
  - `IsChromatic` (bool)
  - `IsSecondaryDominant` (bool)
  - `IsSubstitution` (bool)

Scoring:
- baseScore * deviceWeight (default 1.0)
Constraints (rolling window over last 8 history events):
- compute counters of device types used recently
- if candidate violates constraint:
  - apply penalty (e.g., score *= 0.25) or skip candidate

## 4) Add tonal distance term (circle of fifths)
Implement helper:
- `MinCircleOfFifthsSteps(int pcA, int pcB) => 0..6`
Add scoring:
- bonus for fifth/fourth motion:
  - if steps == 1: +0.10
- penalty for distant jumps:
  - `-0.03 * max(0, steps - 2)`

## 5) Add mood-axis continuity term
Use `HarmonyVisualMapping.ComputeWorldY(chord)`:
- penalty:
  - `-0.06 * abs(worldY(to) - worldY(from))`
This encourages coherent movement (still allows jumps when style says so).

## 6) Add scale context compatibility
If a `PrimaryScaleCandidate` exists:
- compute coverage of candidate chord tones within the scale
- add:
  - `scaleBonus = (coverage - 0.5) * 0.18` clamped
If blues context:
- prefer dominant 7, minor 7, and IV/V motion grammar:
  - add device candidates or adjust diatonic target-degree ordering accordingly

## 7) Update tests
Add unit tests verifying:
- Determinism remains (same input -> same top path)
- Style weights affect ranking (switch style pack and expect reasons include the weighted device and/or ordering changes)
- Blues context:
  - feed detection result with inferred blues scale context
  - expect suggestions include at least one dominant 7 path consistent with I7/IV7/V7 vocabulary within top N

Keep tests resilient (avoid exact numeric score asserts; assert relative ordering and presence).

## Acceptance criteria
- Planning changes are measurable in tests and visible in UI.
- No major performance regression (beam search remains bounded).
- History retention setting works.

## Self-check
- `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~RealtimeHarmonicAssistant"`
