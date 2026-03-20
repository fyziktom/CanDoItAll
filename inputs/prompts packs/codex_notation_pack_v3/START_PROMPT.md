You are Codex. You will implement missing/incorrect music-notation editor functionality in this repository.

You MUST follow the workflow and prompts in the provided pack.

1) Open and read `CODEX_WORKFLOW.md`. Treat it as mandatory process.
2) Open `MASTER_CHECKLIST.md`. This is the source of truth for requirements.
3) Execute prompts in order from `PROMPTS/README.md`, starting with `PROMPTS/00_START.md`.
   - Do NOT skip prompts.
   - Do NOT merge prompts.
   - After each prompt: run tests, update checklist, STOP.

Primary focus for the first milestones:
- Fix ripple editing for duration changes (dots) so no overlaps occur.
- Improve AutoRestFill completeness and beat grouping.
- Implement TickContext-like spacing to eliminate collisions for 1/32+ rhythms.
Only after these are correct, proceed to:
- 32nd/64th duration support
- In-canvas HUD + radial menu
- Multi-part voicing + lyrics + page layout.

If you are unsure, prefer small incremental changes with tests.

Begin with `PROMPTS/00_START.md`.
