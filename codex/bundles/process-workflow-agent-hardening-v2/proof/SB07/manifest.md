# SB07 Proof Manifest

## Scope

Subbundle: `SB07 Agent/template/skill governance resync`.

This pass aligns process templates, repo skills, and active skill-root copies with strict operation contracts, production-path proof rules, and provider usage requirements.

## Source Changes

- `repo://Templates/Processes/processes/software-delivery/definition.json`
  - Adds `ContractMode: Strict` to the governed software-delivery template.
- `repo://Templates/Processes/processes/software-delivery/definition.md`
  - Documents strict contract mode for the permission model.
- `repo://codex/skills/candoitall-api-processes/SKILL.md`
  - Adds production-path proof rules for real process E2E and rejects suppressed automation/manual transition proof.
- `repo://codex/skills/candoitall-api-agents/SKILL.md`
  - Adds process proof verification rules for execution runs, tool receipts, and provider usage observations.
- `repo://codex/skills/bundles/candoitall-bundle-validator/SKILL.md`
- `repo://codex/skills/bundles/candoitall-bundle-execution/SKILL.md`
  - Adds production-path E2E proof guardrails.

Active skill root copies were synchronized to:

- `C:\Users\lucys\.codex\skills\candoitall-api-processes\SKILL.md`
- `C:\Users\lucys\.codex\skills\candoitall-api-agents\SKILL.md`
- `C:\Users\lucys\.codex\skills\candoitall-bundle-validator\SKILL.md`
- `C:\Users\lucys\.codex\skills\candoitall-bundle-execution\SKILL.md`

Changed file hashes:

- `bundle://proof/SB07/changed-file-hashes.txt`

## Passing Proof

- `bundle://proof/SB07/transcripts/template-contract-and-scenario-scan.txt`
  - Validates `ContractMode: Strict`, `OperatingMode: GovernedLive`, 20 step operation contracts, mutable `quality-repair`, runtime proof stages `qa-validation,qa-recheck`, and no SB04 scenario keys in production templates, agents, repo skills, or seed assets.
- `bundle://proof/SB07/active-skill-sync-hashes.json`
  - Confirms active skill-root hashes match repo skill hashes for the four synchronized skills.
- `bundle://proof/SB04/scenarios/recipe-pantry-planner/process-run-detail.json`
  - Current real process run after the Blazor/dotnet delivery instruction hardening. QA accepted and repair branch skipped.

## Anti-Stub Audit

- `bundle://proof/SB07/anti-stub-audit.txt`
  - Scanned updated templates/skills for TODO, NotImplemented, and stub-only markers.
  - Result: pass.

## Raw Note Closure

SB07 closes the raw-note slice for agent/template/skill governance resync. Active skill-root files match repo copies and future agents are instructed not to accept fixture-only proof for critical production E2E.

## Downstream Impact

SB09 must verify active skill sync, scenario-key absence, and no stale proof wording before final closure.
