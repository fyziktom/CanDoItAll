# 07 — Implement RealtimeNoteScoreTracker (floating note scoring window)

Goal: create a time-window note scoring system that tracks importance of notes/pitch classes over time:
- arpeggios are recognized as chords
- melody noise is suppressed for chord naming
- low-scored notes remain available for scale/style context

## Files to add
- `src/MusicTheory.Core/Recognition/RealtimeNoteScoreTracker.cs`
- `src/MusicTheory.Core/Recognition/RealtimeNoteScoreOptions.cs`
- `src/MusicTheory.Core/Recognition/RealtimeNoteScoreSnapshot.cs`

## Files to modify
- `src/App.Blazor/Services/RealtimeChordDetectionSessionService.cs` (integrate tracker)
- (optional) `docs/harmonic-assistant/algorithm-and-scoring-notes.md` (update documentation)

## 1) Implement options + snapshot
Follow `/02_DESIGN/03_midi-scoring-and-detection.md`:
Options should include:
- WindowMs, DecayMs
- NoteOnBoost, HoldBoostPerSecond, SustainHeldMultiplier
- MinScoreToKeep, MaxTrackedNotes
- Any additional knobs you deem necessary

Snapshot should provide:
- TimestampMs
- `IReadOnlyList<(int PitchClass, double Score)>` sorted desc (or equivalent record)
- `IReadOnlyDictionary<int,double>` pitch class scores (optional)
- `int? BassPitchClass`
- `bool TooManyNotes` or similar safety signal

## 2) Implement tracker update semantics
- Consume `RealtimeMidiEvent` (same type used by window detector)
- Track per MIDI note:
  - score (double)
  - lastUpdateMs (double)
  - isHeld (bool)
  - isSustained (bool)
- Use exponential decay computed lazily per note on update and on snapshot.
- Holding increases score continuously (per-second boost).
- Sustain-held notes accumulate less than pressed notes.

Prune old/low-score notes:
- keep dictionary small and performant

## 3) Integrate into detection session
In `RealtimeChordDetectionSessionService`:
- Add `private readonly RealtimeNoteScoreTracker scoreTracker = new(new RealtimeNoteScoreOptions(...))`
- In `HandleMidiMessageReceived`:
  - `scoreTracker.Apply(midiEvent)` alongside `detector.Apply(midiEvent)`
- In manual notes mode:
  - reset tracker and apply note-ons similarly
- In `EvaluateNowAsync`:
  - compute `scoreSnapshot = scoreTracker.GetSnapshot(nowMs)`
  - (do not wire into detection yet; next prompt will update detection service)

## Acceptance criteria
- Build passes.
- Tracker produces stable non-empty pitch class scores when notes are played.
- No regressions in existing detection behavior yet.

## Self-check
- Add minimal unit tests (optional here; full tests in later prompts).
- `dotnet build`
