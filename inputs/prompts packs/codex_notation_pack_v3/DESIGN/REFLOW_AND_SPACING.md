# DESIGN — Rhythm Reflow + Spacing (Fixing Overlaps and Collisions)

This document specifies how to fix two classes of problems:

1) **Rhythm correctness**: editing must not create overlaps / impossible timelines.
2) **Spacing correctness**: dense rhythms must not visually collide.

Both fixes are required. Fixing only one will not solve the screenshot bug.

---

## Part 1 — Rhythm correctness (reflow)

### Current behavior (problem)
- The editor stores events with explicit `Start` + `Duration`.
- Insert operations can ripple (`InsertAndShift`), but **duration changes** do not.
- `SetNoteDotsCommand` calls `ScoreEditingOperations.ChangeDuration` which:
  - modifies only the selected note
  - runs `ReflowEngine.NormalizeFrom` (measure overflow handling)
  - does **not** shift later notes inside the same measure
Result: overlaps within a voice.

### Target behavior
When an event's duration changes, treat it like an edit that can consume or create time.

**Definitions**
- Let `cluster` = all events at the same `(Part, Staff, Voice, Start)` (chord stack).
- Let `oldDur` = max duration among cluster events (they should be equal after invariants).
- Let `newDur` = desired duration.
- Let `delta = newDur - oldDur`.

**InsertMode semantics for duration-change**
- Replace:
  - Update cluster duration to `newDur`.
  - Remove any events (same staff+voice) that start inside `[Start, Start+newDur)` (except the cluster).
  - Auto-rest fill fills holes.
- InsertAndShift:
  - Update cluster duration to `newDur`.
  - If `delta > 0`: shift all events with `Start >= Start+oldDur` by `delta`.
  - If `delta < 0`: do not pull events left by default (leave gap, auto-rest fill will fill).
- Split:
  - Update cluster duration to `newDur`.
  - Trim/split any overlapped events, preserving tails after `Start+newDur`.

After this in-measure operation:
- run `ReflowEngine.NormalizeFrom(score, measureIndex)` to push overflow across measures
- run `AutoRestFillEngine.RecomputeAll(score)` (or optimized per-measure recompute)

### Required API changes
- `SetNoteDotsCommand` must carry `InsertMode` (from editor settings)
  - Option A: add `InsertMode` to existing command
  - Option B: create `SetNoteDotsCommandV2` and migrate UI to use it
- `ScoreEditingOperations.ChangeDuration(...)` must accept InsertMode and implement above semantics.

### Invariants to enforce
Add a helper that can be used in tests (and optionally debug asserts):
- for each measure
  - for each `(Part, Staff, Voice)`
    - events are sorted by Start
    - no overlaps: `prev.End <= next.Start`
    - chord stacks allowed: multiple NoteEvents with same Start are OK but must have same Duration

---

## Part 2 — Auto-rest fill improvements

### Current problems
- Candidate durations stop at 1/32, leaving gaps for smaller durations.
- Beat grouping is naive (denominator only), not using compound groupings.

### Target behavior
- Auto-rest fill must never leave gaps when enabled.
- It must fill using the editor’s supported rhythmic vocabulary:
  - base durations up to 1/64 (optionally 1/128)
  - dotted rests optional (config)
- It must respect beat boundaries from `MeterGrouping.GetBeatBoundaries(signature)`.

### Algorithm (deterministic, simple)
For each measure, for each `(Part, Staff, Voice)`:
1) collect user events sorted by Start
2) derive all “occupied” segments `[Start, End)`
3) for each gap `[cursor, nextStart)`:
   - fill by splitting at beat boundaries:
     - while cursor < gapEnd:
       - segmentEnd = min(gapEnd, nextBoundaryAfter(cursor))
       - fill segment `[cursor, segmentEnd)` using greedy durations from largest to smallest
4) also fill tail `[lastEnd, capacity)` similarly
5) remove all previous auto rests before inserting new ones

**Greedy fill inside segment**
- `remaining = segmentEnd - cursor`
- choose the largest duration (including dotted if enabled) that is `<= remaining`
- insert rest, advance cursor
- continue until remaining == 0

---

## Part 3 — Spacing correctness (TickContext-style)

### Current behavior (problem)
`ScoreLayoutEngine` sets:
`x = ContentLeft + ContentWidth * (Start / Capacity)`
This guarantees collisions when Start differences are tiny (32nds, 64ths).

### Target behavior
- Use time slots (TickContexts) so that each slot has a minimum required width.
- Align slots across all staves/voices within a measure (and across parts once implemented).
- Justify measure widths to system width.

### Data structures
Introduce:
- `MeasureSlot`:
  - `Start: Rational`
  - `MinLeft: double` (space needed to the left, e.g. accidentals)
  - `MinRight: double` (dots, flags)
  - `GlyphWidth: double` (notehead/rest baseline)
  - `MinWidth: double = MinLeft + GlyphWidth + MinRight + Padding`
  - `X: double` (computed)
- `MeasureSpacingPlan`:
  - `Slots: MeasureSlot[]` sorted by Start
  - `MinTotalWidth: double`
  - `AssignX(...)` method

### Computing slot min widths
Per slot, consider all events at that start across staff+voice:
- notehead width (from `ScoreLayout.NoteHeadWidth`)
- rest width (from `ScoreLayout.RestWidth`)
- dots:
  - dot count -> dot area width (dotCount * (DotWidth + padding))
- accidentals:
  - use `AccidentalEngine` output to count columns at that slot
  - `minLeft = columns * AccidentalColumnWidth + padding`
- flags/beams:
  - add right padding for flags if not beamed

Take the maximum across all events at that slot.

### Assigning X positions
1) build `Slots` and compute their individual `MinWidth`.
2) compute a base “gap” between slots:
   - `gapMin = layout.MinSlotGapPx` (new constant, e.g. 6)
3) compute `MinTotalWidth = sum(slot.MinWidth) + gapMin*(slots-1)`
4) if measureWidth == 0: return plan only
5) if `MinTotalWidth <= measureWidth`:
   - distribute extra space to gaps proportionally to time delta between slots
6) else:
   - you must not collide; choose strategy:
     - reduce measures per system (wrap) OR
     - increase measureWidth (system overflow / horizontal scroll) OR
     - apply compression with a visible warning
   - For print, prefer wrapping.

### System-level measure widths
Replace constant measure width with:
- compute `minWidth[i]` for each measure in the system
- pack measures into systems based on available width:
  - greedy: keep adding measures until sum(minWidth)+barlineGaps exceeds width
- justify widths within a system:
  - `allocatedWidth[i] = minWidth[i] + leftover * (minWidth[i]/sumMin)`
- then assign measure `ContentLeft/ContentWidth` accordingly.

---

## Testing strategy (must add)
### Unit tests (xUnit)
- Duration change ripple scenarios (A2)
- Auto rest fill covers full measure for gaps requiring 1/64 (A4)
- Slot spacing plan produces non-colliding X (B4)

### Playwright
- Reproduce the dotted-half ripple scenario by clicking:
  - insert two half notes
  - Dot tool on first note
  - assert via `window.__notationLastBaseCommands` that:
    - there is an auto-rest (CssClass contains 'auto-rest')
    - note-head X positions are strictly increasing for the voice events.

---

## Practical implementation notes
- Keep spacing logic in C# (layout engine), not JS, so it’s testable.
- Rendering can remain unchanged; it will naturally use the improved X positions.
- Do not attempt to fully replicate VexFlow's iterative formatter. Start deterministic and add complexity only if needed.
