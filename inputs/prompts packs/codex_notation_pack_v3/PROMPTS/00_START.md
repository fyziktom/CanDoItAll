# PROMPT 00 — Bootstrapping and non-skipping contract

You are Codex working inside a Blazor + Canvas music-notation editor repo.

Before editing any code:
1) Read these files fully:
   - `CODEX_WORKFLOW.md`
   - `MASTER_CHECKLIST.md`
   - `AUDIT_CURRENT_STATE.md`
   - `DESIGN/REFLOW_AND_SPACING.md`

2) Run the existing test suite to establish baseline.
   - `dotnet test`

3) Reproduce the current bug quickly:
   - Load fixture `score_ripple_dot_in_measure.json` (copy from this pack into `tests/fixtures/` if it does not exist yet).
   - In the UI:
     - ensure InsertMode is InsertAndShift
     - click the first quarter note
     - choose Dot tool and click that first note
   - Observe whether the second note shifts and whether an auto-rest is inserted.
   - If you cannot reproduce visually, inspect `window.__notationLastBaseCommands` in Playwright.

4) Update `MASTER_CHECKLIST.md` Progress log with:
   - baseline test results
   - reproduction notes for the bug

Now proceed to `PROMPTS/01_RIPPLE_DURATION_CHANGE.md`.

STOP after completing steps 1-4. Do not implement fixes yet.
