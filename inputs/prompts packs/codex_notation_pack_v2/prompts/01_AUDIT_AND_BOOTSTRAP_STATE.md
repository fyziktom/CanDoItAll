You are Codex continuing work in the repo.

You MUST:
- Read `codex/STATUS.md`.
- Run `dotnet test` and note pass/fail.
- Inspect the notation editor feature set by searching for: KeySignature, TimeSignatureChanges, Accidentals, Slur rendering, Tie rendering, Canvas HUD.

Tasks:
1) Expand `codex/STATUS.md` with concrete evidence:
   - For each item you mark Done/Partial, add file paths and test names (if any).
2) Create `codex/REPO_AUDIT.md` summarizing:
   - What is already implemented.
   - What is missing (top 10 risks).
   - Which milestone to run next.
3) Create/Update `codex/NEXT_PROMPT.md`:
   - If Milestone A is incomplete: set next prompt to `prompts/milestones/10_MILESTONE_A_VERIFY_OR_IMPLEMENT.md`.
   - If Milestone A is complete: set next prompt to `prompts/milestones/20_MILESTONE_B_TIES_AND_FILLED_SLURS.md`.

Important:
- Do NOT implement features in this run.
- Do NOT change code except creating/updating these `codex/*` files.
