You are Codex. Milestone A: Key/Time signature changes + accidentals.

Goal of Milestone A:
- The score model supports key signatures and time signatures that change at measure boundaries.
- Layout + reflow uses per-measure time signature capacity.
- Rendering shows key signatures, time signatures, and accidentals correctly.
- Editor supports accidental override (#, b, n) and has Playwright E2E coverage.

Instructions:
1) Read `codex/STATUS.md` and treat it as the contract.
2) If Milestone A is already Done, you must:
   - Re-run tests.
   - Fix regressions.
   - Improve weak spots (missing unit tests, missing edge cases).
3) If Milestone A is not Done, implement it using the design docs in this pack.

Acceptance criteria (must be proven by tests):
- A fixture score with a mid-piece key change renders key signature glyphs at the change boundary.
- Accidentals appear when a note deviates from the current key or previous state within the measure.
- A fixture score with a time signature change updates measure capacity and reflow.
- Keyboard shortcuts:
  - '#' -> Sharp override
  - 'b' -> Flat override
  - 'n' -> Natural override (toggle off if pressed again)

Test requirements:
- Add at least 2 xUnit tests for KeySignatureMath / context resolution.
- Add at least 2 Playwright E2E tests using fixtures.
- Prefer command-based assertions (count base commands by cssClass + glyph text) and/or screenshot diffs.

Deliverables:
- Update `codex/STATUS.md` with evidence for every Milestone A item.
- Update `codex/NEXT_PROMPT.md` to `prompts/milestones/20_MILESTONE_B_TIES_AND_FILLED_SLURS.md`.

Stop after Milestone A passes all tests.
