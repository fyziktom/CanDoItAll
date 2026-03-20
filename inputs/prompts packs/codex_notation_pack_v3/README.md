# Codex Notation Pack v3 — Reflow/Spacing + Voicing + Canvas HUD

This pack is designed to keep Codex **on track** while implementing large, multi-step changes in the Zyphonote notation editor.
It is structured as:

- A **master checklist** with IDs and acceptance criteria
- A **runbook** that Codex must follow on every step (to avoid skipped requirements)
- A sequence of **prompts** (run one-by-one)
- **Scenarios** that Codex must validate (unit tests + Playwright)
- Reference notes mapping Zyphonote's current code to **VexFlow** concepts (Formatter / TickContext / System)
- Optional **reference implementations** (skeletons) that Codex can adapt

> IMPORTANT: Run prompts in order: `PROMPTS/00_START.md`, then `PROMPTS/01_...`, etc.
> Each prompt ends with a **STOP** instruction. Do not skip steps.

## Repository targets (current codebase)
This pack assumes the repository has:
- C# score model + commands: `src/MusicTheory.Core/NotationEditor/*`
- Blazor editor UI: `src/MusicNotation.Editor/*`
- JS canvas renderer: `src/MusicNotation.Editor/wwwroot/notationEditorCanvas.js`
- Tests:
  - xUnit: `tests/MusicTheory.Tests/*`
  - Playwright: `tests/App.Web.PlaywrightTests/*`

## The big goals
1) Fix **rhythmic reflow** and **auto-rest fill** so edits never create overlaps or chaotic micro-gaps.
2) Replace naive proportional spacing with a **TickContext-style spacing** model (VexFlow-like), so dense rhythms (e.g. 32nds) do not collide.
3) Implement **multi-part / voicing** (stacked staves with names and clef choices), plus **lyrics** and **print/page layout**.
4) Implement **in-canvas HUD** (toolbars + a true radial quick menu around the pointer), with full keyboard shortcut coverage.
5) Cover everything with **unit tests + Playwright**.

## Where to start
Open `PROMPTS/00_START.md` and paste it into Codex.
