# SB12: 12-documentation-and-skills-refresh

## Goal

Refresh documentation and Codex skills.

## Required work

- Expand `src/CanDoItAll.Modules.Processes/README.md` from module stub into real architecture doc.
- Update `Templates/Processes/README.md` with post-live-run guidance.
- Update `codex/skills/candoitall-api-processes/SKILL.md` with artifact statuses, manager chat, output grounding, run folder projection, live-run troubleshooting, and examples.
- Add/refresh docs for MAF 1.6 adopted/deferred features.
- Add a docs drift checker or source assertion list.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB12` are updated and the next dependent workstream can rely on it.
