# 06_MIDI_CHORD_DETECTION.md

## Goal
Implement a realtime MIDI chord detector that:
- listens to MIDI input,
- when the user plays >2 notes, shows **multiple chord interpretations** (e.g., Am7 vs C6),
- for each candidate shows:
  - chord name
  - interval structure
  - inversion / bass note
  - missing notes, duplicated notes, and contradictory notes
- renders a notation preview of the chord (using our notation system) and allows playback,
- links candidates to compatible scales/modes and visualizes scale notes.

## Existing code you MUST reuse
### MIDI input
- `src/MusicNotation.Editor/Services/IMidiService.cs`
- `src/MusicNotation.Editor/Services/MidiService.cs`
- Web MIDI JS interop: `src/MusicNotation.Editor/wwwroot/midiInterop.js`
- Active notes tracking: `src/MusicTheory.Core/Midi/MidiActiveNotesTracker.cs`

### Chord recognition
- `src/MusicTheory.Core/Recognition/ChordRecognitionEngine.cs`
- `src/MusicTheory.Core/Recognition/ChordRecognitionMatch.cs`
- Chord voicing / inversion analysis:
  - `src/MusicTheory.Core/Theory/ChordVoicingAnalyzer.cs`
- Pitch models:
  - `src/MusicTheory.Core/Models/NotePitch.cs`

### Notation preview
- Score model + helpers:
  - `src/MusicTheory.Core/NotationEditor/Model/ScoreDocument.cs`
  - `src/MusicTheory.Core/NotationEditor/Model/ScoreFactory.cs` (use patterns here)
  - `src/MusicTheory.Core/NotationEditor/Formats/NotationJsonFormatService.cs`

## New components/services to implement
### A) Core algorithm (testable)
Add to `src/MusicTheory.Core/Recognition/`:
- `RealtimeChordWindowDetector`:
  - input: stream of note-on/note-off events + sustain changes + timestamps
  - output: “chord snapshot” (set of active notes, bass note, duplicated pitch classes, note ages)
  - chord window heuristic:
    - a chord is considered “stable” after no new note-ons for `DebounceMs` (e.g., 80–150ms)
    - if notes are spread out (arpeggio), allow `MaxChordWindowMs` (e.g., 300ms)
    - reset window if time since last note-on > `SilenceResetMs` (e.g., 700ms) and no held notes
- `RealtimeChordDetectionService`:
  - uses `ChordRecognitionEngine`
  - computes candidate ranking score:
    - prefer more matched notes
    - penalize contradictions and missing
    - penalize overly complex chords in “beginner” mode
  - enrich each candidate with:
    - inversion via `ChordVoicingAnalyzer`
    - list missing/duplicate pitch classes based on actual MIDI notes
    - compatible scales/modes (see section D)

Add unit tests in `tests/MusicTheory.Tests`:
- chord window groups near-simultaneous notes into one chord
- sustain pedal does not permanently “poison” recognition (older sustained notes are down-weighted)
- ranking returns expected top chord for known inputs

### B) UI
Create new page:
- `src/App.Blazor/Pages/MidiChordDetector.razor` (route: `/midi-chords` OR `/harmony/chords`)
UI requirements:
- MIDI device selector/status (reuse IMidiService devices list)
- “Active notes” list
- Candidate chords list (top 8):
  - shows name + inversion
  - expandable details (intervals, missing/extra)
  - button: “Preview in notation” (renders small staff)
  - button: “Send to editor” (opens editor with chord inserted or appended)
- Scale/mode suggestions:
  - show 3–6 matching scales with confidence
  - show scale notes visually (chips) + highlight which are currently pressed

Add stable selectors for Playwright:
- `data-testid="midi-status"`
- `data-testid="midi-chord-candidate-{index}"`

### C) Notation preview implementation
- Build a temporary `ScoreDocument` with 1 measure:
  - chord notes stacked (treble staff) + optional bass note in bass staff
  - chord symbol set to candidate name
- Render using an existing component:
  - If easiest, reuse `StaffSvg` patterns from `src/App.Blazor/Pages/ChordExplorer.razor`
  - Or embed a simplified read-only NotationEditor component.

### D) Scales/modes mapping
Currently scale support is limited:
- `src/MusicTheory.Core/Theory/ScaleLibrary.cs`
- `src/MusicTheory.Core/Theory/TonalScaleLibrary.cs`

Extend or add:
- `ModeType` expansions (dorian, phrygian, lydian, mixolydian, locrian, harmonic minor, melodic minor)
- A function:
  - `GetCandidateScalesForChord(chord, contextKey?)` returning ranked options.

### E) Performance and realtime constraints
- UI update should stay responsive with < 50ms processing per update.
- Use debouncing to avoid recomputing on every MIDI message.
- Use `CancellationToken` / coalescing updates to avoid backlog.

## Edge cases to handle (MUST)
- Duplicate notes across octaves should not break recognition; duplicates should be reported.
- Sustain pedal:
  - held notes vs sustained notes should be distinguishable for heuristics.
  - when pedal is released, chord updates immediately.
- Chords with missing root (e.g., shell voicings): still suggest likely chords with missing root flagged.
- Very dense clusters: cap candidate list and show a “too many notes” warning.
- No MIDI support in browser: show a helpful message (Web MIDI secure context requirement).

## Acceptance criteria (must be measurable)
- With 3+ simultaneous notes, candidates appear within 150ms after last note-on.
- Candidate list includes at least 5 interpretations when musically plausible.
- Inversion detection matches bass note correctly for triads and seventh chords.
- Notation preview renders for each candidate and can be played back (even simple).
- Works offline.

## Verification steps
- Manual:
  - Connect a MIDI keyboard; play C–E–G, then C–E–G–A, then A–C–E–G.
  - Verify candidates include both C6 and Am7 for C–E–G–A.
- Tests:
  - `dotnet test tests/MusicTheory.Tests`
