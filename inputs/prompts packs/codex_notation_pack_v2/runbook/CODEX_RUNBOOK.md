# Codex Runbook (Multi-Prompt, Test-Gated)

This pack is designed to keep Codex “in the loop” for a large, multi-week refactor.
The key idea is: **Codex must persist progress in-repo** and **must not advance without tests**.

## How to use this pack

1. Copy this pack into the target repo under `codex_notation_pack_v2/` (or keep it external and just paste prompts).
2. Start Codex with `prompts/00_START_PROMPT.md`.
3. Then run prompts **in order** from `prompts/INDEX.md`.
4. After each milestone, run:
   - `dotnet test`
   - Playwright E2E suite
   - (Optional) `dotnet format` / linters

## The “keep Codex running” mechanism

Codex must create & maintain the following files inside the repo:

- `codex/STATUS.md` – authoritative checklist with “Done/Partial/No” and evidence.
- `codex/DECISIONS.md` – short ADR log (why a design choice was made).
- `codex/NEXT_PROMPT.md` – which prompt to run next and why.
- `codex/KNOWN_GAPS.md` – anything postponed (must include owner + concrete follow-up prompt).

**Rule:** Every time Codex finishes a work unit, it must update `codex/STATUS.md` and set `codex/NEXT_PROMPT.md`.

## Test gating (mandatory)

Each milestone prompt requires:

- Creating/expanding **unit tests** (xUnit) for core logic.
- Creating/expanding **Playwright E2E tests** for user-visible behavior.
- Capturing **objective evidence** for each requirement in `codex/STATUS.md`:
  - test names
  - fixture file names
  - screenshots / command counts

If a requirement cannot be implemented in the milestone, Codex must:

1. Add a failing test (or a skipped test with a clear reason),
2. Document the gap in `codex/KNOWN_GAPS.md`,
3. And only then move on.

## Definition of Done (DoD)

A milestone is “Done” only if:

- All acceptance criteria in that milestone are met.
- All tests pass.
- `codex/STATUS.md` shows:
  - requirement ticked
  - evidence links (test names)
  - key files/paths

