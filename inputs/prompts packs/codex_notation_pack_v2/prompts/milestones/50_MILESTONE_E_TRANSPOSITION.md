You are Codex. Milestone E: Transposition.

Goal:
- Provide transposition operations for selected notes / selected measures.
- Respect key signature when spelling notes (avoid ugly accidentals when possible).

Scope (must implement at minimum):
1) Transpose selected notes:
   - Semitone up/down
   - Octave up/down
2) Transpose selection with a target key signature preference:
   - Use current measure's KeySignature preference to spell pitch classes.

Core implementation suggestions:
- Add a service in core: `TranspositionService`.
- API:
  - `TransposeNote(NotePitch pitch, int semitones, KeySignature contextKey)` -> `NotePitch`.
  - `TransposeSelection(ScoreDocument score, IEnumerable<Guid> eventIds, int semitones)`.
- Spelling algorithm (simple but acceptable):
  - Convert to MIDI, add semitones, then map to NoteName using:
    - if key signature has explicit preference (Sharps/Flats), use it.
    - else choose the spelling that matches key signature default accidental for some letter with minimal cost.

UX:
- Add HUD buttons and keyboard shortcuts:
  - `Ctrl+Up/Down` semitone
  - `Ctrl+Shift+Up/Down` octave
  - Provide a small overlay message: “Transposed +1 semitone”.

Tests:
- Unit tests:
  - Transpose C4 +2 -> D4
  - Transpose B3 +1 -> C4 (octave carry)
  - Transpose in flat key: pitch class spelling prefers flats.
- Playwright:
  - Create a score with selected note and invoke transpose shortcut.
  - Assert that the rendered note head moved to correct staff position and/or pitch label in debug HUD.

Deliverables:
- `codex/STATUS.md` updated.
- `codex/NEXT_PROMPT.md` set to `prompts/milestones/60_MILESTONE_F_EXTENDED_NOTATION_PHASE_1.md`.
