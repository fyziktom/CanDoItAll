# CODEX_WORKFLOW.md — Mandatory Workflow (Do Not Skip)

This file is **process law** for Codex when working on this repository.
Large editor changes tend to lose context; this workflow forces progress tracking.

## Prime directives
1) **Never** implement features without first reading:
   - `MASTER_CHECKLIST.md`
   - the current prompt file in `PROMPTS/`
2) Work in **small, verifiable increments**:
   - implement only what the prompt requests
   - add tests in the same step
3) After every step:
   - run the requested tests
   - update `MASTER_CHECKLIST.md` (mark done, add notes, list test runs)
4) If something is ambiguous:
   - choose the simplest consistent behavior
   - document it in `docs/DECISIONS.md`
   - add tests that lock in the behavior

## Output contract (every prompt)
At the end of each prompt, output:
- ✅ Completed checklist IDs
- 🧪 Tests executed (exact commands)
- 📁 Files changed (grouped by area)
- ⚠️ Known limitations / follow-ups (must map back to checklist IDs)
- 🛑 STOP and wait for the next prompt

## Do-not-skip guardrails
- If you encounter a large refactor, **split it** into:
  1) introducing new types (compiles, tests still pass)
  2) wiring behavior behind feature flags
  3) flipping defaults after tests are added
- Prefer additive changes first. Avoid rewriting everything at once.

## Test requirements
- For pure logic (reflow/spacing): xUnit tests in `tests/MusicTheory.Tests`.
- For user flows (toolbar/HUD, lyrics entry): Playwright tests in `tests/App.Web.PlaywrightTests`.
- Keep tests deterministic. Use fixture scores where needed.

## Naming conventions
- New logic engines: `*Engine.cs` (pure, testable).
- Layout: `Layout/*` and must avoid UI dependencies.
- Rendering: produce `RenderCommand`s with stable `CssClass` labels (tests rely on these).

## Performance constraints
- The canvas overlay is redrawn frequently. Avoid allocations in pointer-move hot paths.
- Prefer caching / precomputed arrays in layout.

## STOP rule
Each prompt is a gate. When done, stop and wait for the next prompt.
