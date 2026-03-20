You are Codex. Milestone D: user editing of key/time signature changes at measure boundaries.

Current state assumption:
- The model + renderer can already handle key/time signature changes (Milestone A).
- The missing part is **UX + editing operations** to insert/remove changes.

Goal:
- Users can change key signature and time signature at any measure boundary, including mid-score changes and reverting back later.
- Changes are persisted in score JSON.

UX requirements:
1) Add HUD actions (canvas-first) for:
   - Set Key Signature at selected measure.
   - Set Time Signature at selected measure.
2) Provide a fast picker UI:
   - Key signature: circle-of-fifths list from -7..+7, and enharmonic preference.
   - Time signature: common presets (2/4, 3/4, 4/4, 6/8, 12/8) + custom numerator/denominator.
3) Measure boundary selection:
   - The target measure index should be the currently selected measure (or the measure under pointer).

Implementation requirements:
- Add editing operations in core commands layer (so UI is thin):
  - `SetKeySignatureChange(measureIndex, keySig)`
  - `RemoveKeySignatureChange(measureIndex)`
  - `SetTimeSignatureChange(measureIndex, timeSig)`
  - `RemoveTimeSignatureChange(measureIndex)`
- Ensure reflow/layout updates and playback plan rebuild is triggered.

Tests:
- Add Playwright E2E tests:
  - Start with a blank/simple score.
  - Insert a key signature change at measure 2 and verify rendered key signature glyph count increases.
  - Insert a time signature change and verify measure capacity changes (e.g., inserting notes fills differently) OR verify time signature glyphs in render commands.

Deliverables:
- `codex/STATUS.md` updated with evidence for Milestone D.
- `codex/NEXT_PROMPT.md` set to `prompts/milestones/50_MILESTONE_E_TRANSPOSITION.md`.
