---
name: candoitall-subbundle-validator
description: Validate a CanDoItAll subbundle before implementation starts and after proof is captured. Use when Codex must confirm prerequisites, dependency impact, and progression-gate quality so downstream work does not proceed on weak foundations.
---

# CanDoItAll Subbundle Validator

Use this skill before and after each subbundle. It exists to stop dependency mistakes early, when they are still cheap to fix.

## Required Flow

1. Read the root `README.md`, `plan/01-phase-plan.md`, the selected subbundle README, and the relevant traceability rows.
2. Run the entry gate before editing:
   - confirm the current subbundle still owns the intended inputs
   - confirm every listed prerequisite is complete and still trusted
   - confirm the exact source references still match the repo
   - confirm any critical foundation it depends on has strong enough proof for downstream work
3. If the entry gate fails, stop. Repair the bundle or reopen the prerequisite phase before implementing.
4. After implementation, run the closure gate:
   - acceptance checklist and proof required are complete
   - tests, builds, Playwright proof, screenshots, and host proof ran when required
   - screenshot review questions were actually answered, not only captured
   - `## Browser Validation Analytics` and `## Subbundle Gate Results` were updated while the proof was fresh
5. If the subbundle is a critical foundation, run one dependent-flow smoke or dependent-surface validation before allowing the next subbundle to start.
6. If later work exposes a defect in the current subbundle, reopen it immediately and rerun the closure gate after repair.

## Rules

- Do not start work because the next file looks easy when the prerequisite proof is weak.
- Do not pass the closure gate when browser proof is missing or visually wrong.
- Do not let a later subbundle bury evidence that an earlier foundation was incomplete.
- Treat `Progression Gate` as a real stop sign, not as bundle decoration.

## References

- Read [references/prerequisite-and-closure-gates.md](references/prerequisite-and-closure-gates.md) for the phase checklist.
- Use `candoitall-watch-playwright-loop` when the proof depends on fast nearby browser validation.
- Use `candoitall-bundle-validator` for bundle-level readiness and final closure.

## Exit Condition

The subbundle passes only when its prerequisites, proof, and downstream progression decision are explicit enough that the next phase can proceed without borrowing trust from wishful thinking.
