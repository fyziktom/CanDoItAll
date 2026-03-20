# Recording + Import/Export (v2.1)

Goal:
- Record chord events (detected or manually entered), plus optional MIDI note-score snapshots.
- Export as JSON, import back, replay on canvas.

## Data model
`HarmonicSessionRecording`:
- `version`
- `createdUtc`
- `settingsSnapshot` (mood, modules enabled, thresholds)
- `events[]`:
  - `timestampMs`
  - `chordLabel`
  - `confidence` (optional)
  - `rootPc` / `quality` normalized (optional)
  - `noteScores[]` (optional, compressed)
  - `inferredScale` (optional)

## UI
- Canvas widget “Recording”
  - Start / Stop
  - Save (download JSON)
  - Load (file input)
  - Clear
  - Replay (play/pause, speed)
- Keep heavy logic in C#; JS only displays status and sends button events.

Acceptance:
- User can record a 5-minute session, export JSON, import it later and see the same chord history on canvas.
