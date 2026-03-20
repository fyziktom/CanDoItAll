# RHYTHM_SCENARIOS.md — Situations that must be handled

These scenarios are used to create unit tests and Playwright E2E validations.
They focus on the two bug classes:
- rhythmic overlap / missing rests
- visual collisions due to spacing

Notation:
- TS = time signature
- `N(pitch, dur, start)` = note
- `R(dur, start)` = rest

Assume a single voice (Voice=0) unless noted.

---

## S1 — Dot causes ripple shift (4/4)
Initial (TS 4/4):
- N(C4, 1/2, 0)  // half
- N(D4, 1/2, 1/2)

Action:
- Apply dot to the first note (becomes 3/4) in **InsertAndShift** mode.

Expected:
- First note duration: 3/4
- Second note start shifts to 3/4, duration still 1/2
- Reflow pushes overflow 1/4 into next measure OR trims according to policy.
- There must be **no overlap** within voice 0.
- AutoRestFill fills any remaining gaps.

---

## S2 — Duration increase overlaps a later note (Replace)
Initial (4/4):
- N(C4, 1/4, 0)
- N(D4, 1/4, 1/4)
- N(E4, 1/4, 2/4)
- N(F4, 1/4, 3/4)

Action:
- Change duration of N(C4) to 1/2 in Replace mode.

Expected:
- Notes starting inside [0, 1/2) are removed (D4 is deleted).
- Remaining notes keep start times unless overlap rules dictate otherwise.
- Auto rest fill fills holes if created.

---

## S3 — Gap fill requires 1/64
Initial (4/4):
- N(C4, 63/64, 0)  // e.g. base=whole with ties in export or constructed
Action:
- AutoRestFill enabled.

Expected:
- An auto rest of 1/64 at start 63/64 (or equivalent decomposition) is created.
- AutoRestFill must not leave any remainder gap < smallest supported duration.

---

## S4 — Dotted beaming uses BaseDuration
Initial (4/4):
- N(C4, dotted eighth = 3/16, 0) but BaseDuration=Eighth DotCount=1

Expected:
- Beam/flag level is the same as an Eighth note (1), not 0.

---

## S5 — Dense 32nd run does not collide
Initial (4/4):
- 32 notes of 1/32 each across the measure

Expected (layout):
- Note-head X positions strictly increase with start time.
- Adjacent X delta >= a minimum threshold (tunable) so glyphs do not overlap.

---

## S6 — Multiple parts keep measure alignment
Initial:
- Part 1: treble
- Part 2: bass
Both have notes at same starts.

Expected:
- For a given measure slot, X positions match across parts.
- Y positions differ per part.

---

## S7 — Lyrics entry (standard)
Initial: notes at 0, 1/4, 2/4, 3/4

Action:
- Lyrics mode: click first note, type “Hel- lo”

Expected:
- 2 lyric syllables created, attached to first two notes
- a hyphen rendered between them.
