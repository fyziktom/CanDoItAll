# VEXFLOW_MAPPING.md — What to copy conceptually (not literally)

This repo does not depend on VexFlow at runtime, but VexFlow provides a proven mental model.

## 1) Voice correctness
VexFlow `Voice` is sequential-by-ticks and (by default) strict:
- tickables are appended in order
- the sum of durations must match the time signature
- overlaps are structurally impossible (unless you intentionally use multiple voices)

**Implication for Zyphonote:**
Within a `(Part, Staff, Voice)` you should maintain an invariant:
- events are ordered by Start
- no overlaps
- optional: contiguous coverage when auto-rest fill is enabled

## 2) TickContext spacing (the key fix)
VexFlow's `Formatter` builds a list of `TickContext`s:
- each TickContext corresponds to a time position (a “slot”)
- each slot holds the tickables that start at that tick across voices
- each tickable reports metrics: glyph width, left/right modifiers, etc.
- the formatter computes minimum spacing and then **justifies** to stave width

Key sources in VexFlow:
- `src/formatter.ts`
  - creates tick contexts, assigns X positions
  - `preCalculateMinTotalWidth` + `Format`/`justifyToWidth`
- `src/tickcontext.ts`
  - collects per-slot metrics (noteheads, accidentals, dots)
- `src/system.ts`
  - formats multiple staves together and can auto-size width

**Implication for Zyphonote:**
Replace proportional spacing with:
1) build a `Slot[]` per measure (unique start times)
2) compute per-slot minWidth = max(width needed across staves/voices at that slot)
3) compute gaps and distribute extra space (justify)
4) assign each event.X from its slot.X

## 3) Multi-staff / parts
VexFlow `System` formats voices across multiple staves, aligning X positions.
This is the expected behavior for “voicing” (stacked parts/instruments).

**Implication for Zyphonote:**
For stacked parts:
- all parts in a system must share the same slot X mapping per measure
- vertical positioning differs per part/staff
- staff names are drawn at the left of each system

## 4) Lyrics
VexFlow uses `TextNote` (and sometimes Annotations) aligned to TickContexts.
There is no “magic lyric engine” — you provide text positioned at the right tick.

**Implication for Zyphonote:**
Lyrics should be time-aligned (tick-aligned) and rendered as text under the staff.
Optional: support hyphens/extenders.

## 5) What not to copy
- VexFlow's iterative formatter tuning is powerful but complex.
- Zyphonote can start with a simpler deterministic spacing algorithm as long as:
  - slot min-width constraints are respected
  - collisions are avoided
  - system justification is implemented.
