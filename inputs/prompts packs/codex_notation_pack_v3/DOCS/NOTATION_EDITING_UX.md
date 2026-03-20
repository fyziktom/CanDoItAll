# NOTATION_EDITING_UX.md — Expectations from common notation editors

This doc is for Codex to match user expectations (Finale, Sibelius, Dorico, MuseScore).

## Entry modes
### Step-time (most common for mouse entry)
- Choose duration (quarter, 8th, …)
- Click staff position => insert note at snapped rhythmic position
- Optional chord entry: hold modifier or toggle “Add to chord” then click additional pitches at the same start time

### Insert vs Overwrite (ripple)
- Insert/Ripple mode:
  - inserting longer rhythm pushes later material to the right
  - can push into later measures, creating new measures
- Overwrite/Replace mode:
  - inserting overwrites any material within its time span
- Split mode:
  - inserting “cuts” existing notes/rests and preserves tails

Users expect **duration changes** (dots, base duration edits) to follow the same mode.

## Selection + editing
- Click notehead selects event
- Shift-click extends selection (optional)
- Delete removes selected event and fills with rests (if enabled)
- Dot tool:
  - clicking a note toggles dots (0 -> 1 -> 2 -> 0)
  - in ripple mode, increasing duration pushes later events

## Rests and filling
- Most editors keep measures rhythmically complete:
  - gaps are filled with rests (often automatically)
- Rest grouping respects beat grouping:
  - 6/8 is grouped as 3/8 + 3/8, not six 1/8 rests

## Lyrics
- Lyrics entry mode:
  - click first note
  - type syllable; Space advances
  - hyphen indicates multi-syllable words
  - underscore indicates melisma extender
- Multiple verses supported by selecting verse number.

## Quick access UI
- Power workflow:
  - keyboard shortcuts for tools/durations
  - radial menu near cursor to reduce travel
- Discoverability:
  - top ribbon remains available
  - help overlay (press ?)

## Print
- Page borders (A4 / Letter) are expected for print view.
- Overflow warnings must be obvious when too many staves do not fit vertically.
