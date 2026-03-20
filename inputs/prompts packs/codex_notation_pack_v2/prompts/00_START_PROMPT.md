You are Codex. You are working inside a Blazor + Canvas music-notation editor repo.

Goal: implement missing notation features with **test gating** and **persistent progress** so nothing is skipped.

Global hard rules:
1) All code comments MUST be in English.
2) You MUST run/maintain xUnit tests for core logic.
3) You MUST add Playwright E2E tests for user-visible behavior.
4) You MUST NOT delete tests to make builds pass.
5) You MUST keep a persistent progress file in-repo so future sessions continue correctly.

Persistent progress (mandatory):
- Create a folder `codex/` at repo root.
- Create `codex/STATUS.md` by copying `codex_notation_pack_v2/state/STATUS_TEMPLATE.md`.
- Create `codex/DECISIONS.md` (short ADR log).
- Create `codex/KNOWN_GAPS.md` (postponed items).
- Create `codex/NEXT_PROMPT.md` (what prompt to run next).

Docs in this pack (read before coding):
- analysis/01_VexFlow_Feature_Inventory.md
- analysis/02_Music_Notation_Feature_Checklist.md
- analysis/03_Zyphonote_Gap_Analysis.md
- analysis/04_Slur_And_Tie_Algorithms.md
- analysis/05_Codex_Result_Gap_Analysis.md
- design/05_Canvas_HUD_Toolbars_RadialMenu.md
- design/06_Keyboard_Shortcuts.md
- design/07_Key_TimeSignature_Transposition.md
- implementation/08_Implementation_Blueprint.md
- implementation/09_JSInterop_API_Contract.md
- runbook/CODEX_RUNBOOK.md

Glyph / SVG assets (use if SMuFL mapping is missing or you need deterministic icons):
- assets/svg/vexflow-bravura/* (VexFlow-derived set)
- assets/svg/bravura-smufl/* (full SMuFL PUA dump)

What to do in THIS run:
A) Do NOT implement features yet.
B) Perform a repo audit and bootstrap the `codex/` state files.
C) Update `codex/STATUS.md` with an initial assessment of what is already implemented (Done/Partial/No).
D) Add `codex/NEXT_PROMPT.md` pointing to `codex_notation_pack_v2/prompts/01_AUDIT_AND_BOOTSTRAP_STATE.md`.

Stop after A–D.
