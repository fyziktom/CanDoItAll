# PROMPT 04 — Implement TickContext spacing + system justification (B1-B3)

Goal: Dense rhythms (including 1/32 rests from dotted notes) must not collide visually.

Read:
- `DESIGN/REFLOW_AND_SPACING.md` (Part 3)
- `REFERENCE/VEXFLOW_MAPPING.md`

Tasks:
1) Implement a new spacing engine in C# (layout layer):
   - Build unique slot starts per measure (across staff+voice).
   - Compute per-slot min widths using:
     - notehead/rest widths
     - dots
     - accidentals (use existing accidental placements)
   - Assign slot X positions with minimum gaps and justification to measure width.

2) Change `ScoreLayoutEngine`:
   - Replace proportional X mapping with slot-based mapping.
   - Keep barline alignment.

3) Implement variable measure widths per system:
   - Compute minWidth for each measure.
   - Pack measures into systems based on available width (wrap when too dense).
   - Justify within system to fill width.

4) Tests:
   - Unit test: load `score_dense_32nd_subdivisions.json`, run layout, assert:
     - note-head and rest event X is strictly increasing by Start (for voice 0).
     - adjacent slot X deltas >= a minimum threshold (e.g., > 2px or based on glyph width).
   - Playwright: load dense fixture, assert no identical X for consecutive `rest` commands.

5) Run `dotnet test`.

Update checklist:
- Mark **B1**, **B2**, **B3**, **B4**, **B5** done (as applicable).
- Add progress log entry.

STOP.
