# Codex Notation Pack v2 (Prompt-Chained, Test-Gated)

This pack is meant to be fed into Codex (or another code-generating model) to implement missing music-notation features in a Blazor + Canvas editor (architecture similar to `zyphonote-main`).

The v2 pack fixes a common failure mode: **Codex finishes Milestone A and stops**.
To prevent that, v2 provides **multiple prompts**, a **runbook**, and **persistent in-repo progress files**.

## How to use

1) Start Codex with `prompts/00_START_PROMPT.md`.
2) Follow `prompts/INDEX.md` (one milestone per prompt).
3) After each milestone:
   - run `dotnet test`
   - run Playwright E2E tests
4) Do not advance if tests fail.

## Contents

- `analysis/`
  - VexFlow feature inventory
  - full music notation/editor checklist
  - zyphonote gap analysis
  - slur/tie algorithm mapping
  - **v2**: why the previous run skipped items (`05_Codex_Result_Gap_Analysis.md`)

- `design/`
  - canvas-first HUD + radial menu UX
  - keyboard shortcut map
  - key/time signature + transposition design

- `implementation/`
  - step-by-step blueprint
  - JS interop contract ideas

- `runbook/`
  - **Codex Runbook**: the “keep Codex running” mechanism (status file + test gates)

- `state/`
  - templates for `codex/STATUS.md` and related progress files

- `prompts/`
  - multi-prompt chain (one milestone per run)

- `tests/`
  - fixture suggestions + Playwright plan

- `assets/svg/`
  - SVG glyph sets (VexFlow-derived + SMuFL dump)

- `scripts/`
  - deterministic generators to rebuild SVG sets

## Source inputs used

- VexFlow source snapshot (from the provided zip)
- Zyphonote source snapshot (from the provided zip)

Generated: 2026-02-27 (UTC)
