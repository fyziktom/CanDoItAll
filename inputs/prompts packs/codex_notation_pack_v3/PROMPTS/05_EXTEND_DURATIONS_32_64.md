# PROMPT 05 — Add 32nd and 64th durations (C1) + UI

Goal: Users can insert 32nd/64th notes and rests and they render correctly.

Tasks:
1) Extend duration vocabulary:
   - Update `NotationDuration` enum to include ThirtySecond and SixtyFourth.
   - Update `QuantizationGrid` to include 1/32 and 1/64.
   - Update toolbar UI to show 32/64 buttons.
   - Add keyboard shortcuts for 32/64 (choose keys; document them).

2) Update SMuFL glyph mapping:
   - Add Rest32/Rest64 and Flag32/Flag64 to `GlyphId` and `SmuflBravuraGlyphProvider`.
   - Update `NotationSceneRenderer.ResolveRestGlyph` and `ResolveFlagGlyph`.

3) Update beam engine compatibility:
   - Beam levels can be 3/4 for 32/64.

4) Tests:
   - Unit: inserting 1/32 and 1/64 produces correct baseDuration/dots.
   - Playwright: choose 32nd duration, click to insert, verify at least one `flag`/beam is drawn.

5) Run `dotnet test`.

Update checklist:
- Mark **C1** done.

STOP.
