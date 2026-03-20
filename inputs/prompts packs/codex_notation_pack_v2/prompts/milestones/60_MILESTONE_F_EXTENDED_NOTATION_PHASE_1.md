You are Codex. Milestone F (Phase 1): Extended notation essentials.

This editor must eventually support the full set of common engraving features.
Phase 1 focuses on the “must have” building blocks that unlock a lot of repertoire.

Phase 1 features (implement in this milestone):
1) Tuplets (triplets at minimum):
   - Model: tuplet grouping for a contiguous time range inside a measure.
   - Rendering: bracket + number (e.g., 3).
   - Editing: a tool or shortcut to apply/remove a triplet over selected notes.
2) Grace notes (acciaccatura at minimum):
   - Model: grace note events attached to a main note.
   - Rendering: small notehead + slash (optional), no rhythmic spacing contribution.
   - Editing: shortcut to toggle selected note as grace.

Hard constraints:
- Keep measure-boundary key/time signatures from previous milestones.
- Do not break existing beaming/reflow.

Tests:
- Add at least 2 unit tests for tuplet duration math.
- Add at least 1 Playwright fixture + E2E test verifying tuplet bracket commands.

Deliverables:
- Update `codex/STATUS.md` and add new lines in the Extended Notation section.
- Set `codex/NEXT_PROMPT.md` to `prompts/checkpoints/90_FINAL_REVIEW_AND_CLEANUP.md` (or create Phase 2 prompt if more work remains).
