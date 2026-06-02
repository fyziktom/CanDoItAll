# SB06 - Agent, Skill, Template Canonicalization And Active Sync

## Status

Completed. Classification: **Critical foundation**.

## Objective

Align agent instructions, process templates, API skills, bundle skills, and runtime prompts with the canonical contracts. Prove active Codex skill-root synchronization before downstream E2E work depends on changed skills.

## Covered Inputs

Covers agent/skill/template drift, removed MCP assumptions, stale evidence warnings, product root discipline, fake shim avoidance, browser proof language, numeric enum/API notes, and active skill sync risk.

## Prerequisites

SB01 completed. SB04 and SB05 findings should be incorporated if they change proof/tool/workflow contracts.

## Exact Source References

- `repo://Templates/Agents/manifest.json`
- `repo://Templates/Agents/teams/dotnet-delivery/members/blazor-application-developer/instructions.md`
- `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-application-developer/instructions.md`
- `repo://Templates/Agents/teams/delivery-platform/members/delivery-manager/instructions.md`
- `repo://Templates/Agents/teams/visual-automation-templates/members/screenshot-review-storage-agent/instructions.md`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/software-delivery/definition.md`
- `repo://codex/skills/candoitall-api-agents/SKILL.md`
- `repo://codex/skills/candoitall-api-processes/SKILL.md`
- `repo://codex/skills/candoitall-api-workflows/SKILL.md`
- `repo://codex/skills/candoitall-api-project-structure/SKILL.md`
- `repo://codex/skills/bundles/*/SKILL.md`

## Deliverables

- Updated template/agent/skill language aligned to canonical contracts.
- Skill/API parity tests updated.
- Template governance tests updated.
- Active skill-root sync proof with repo SHA-256 and active SHA-256 hashes.
- Removed-MCP-assumption scan.
- Proof manifest and semantic invariants for SB06.

## Dependency Impact

SB08 depends on active agent/skill/template behavior. If SB06 is wrong, E2E runs can follow stale instructions even if code is correct.

## Validation Depth

Deep semantic validation. Must include active skill-root hash proof and negative scan for stale/removed MCP assumptions.

## Implementation Steps

1. Inventory all skill/template/agent references to canonical concepts.
2. Update wording to point to canonical contracts and current HTTP API/tool behavior.
3. Preserve generic app-delivery instructions.
4. Remove or qualify stale MCP assumptions.
5. Add/extend skill/API parity tests.
6. Synchronize active Codex skill root if the environment requires copying repo skills.
7. Record repo and active SHA-256 hashes.
8. Run template governance and skill parity tests.

## Scope Exceptions

Do not rewrite all agent personalities. Only update behaviorally relevant contract/proof/tool/cost/workflow language.

## Do Not Do

- Do not rely on repository skill edits without active sync proof.
- Do not add scenario-specific instructions for Tetris or other test apps.
- Do not weaken browser proof or current-run proof language.
- Do not reintroduce removed MCP-only assumptions where HTTP API is canonical.

## Acceptance Checklist

- [x] Agent instructions reference canonical contracts.
- [x] Skills reflect current HTTP API behavior.
- [x] Template governance tests pass.
- [x] Skill/API parity tests pass.
- [x] Active skill root hashes are recorded.
- [x] SB06 proof manifest exists.

## Proof Required


Because this is a critical subbundle, the Semantic Adequacy Gate proof must include:

- `proof/SBxx/manifest.md`
- `proof/SBxx/semantic-invariants.md` or `.json`
- changed-file hashes
- command transcript paths
- source assertions
- shallow-pass trap
- adversarial negative proof
- semantic positive proof
- anti-stub audit
- raw-note literal closure
- dependency smoke proof where stated

Production Behavior Artifact Matrix required for any new/changed production skill synchronization record, template version record, or agent capability proof record.


## Browser Validation Logging

N/A unless skill/template editor UI is changed. If changed, record browser route, viewport, screenshot, and result.

## Progression Gate

SB06 passes only when changed skills/templates are both in repo and active execution environment, proven by hashes.

## Suggested Agent Prompt

Implement SB06 only. Canonicalize agent/template/skill language and prove active skill sync before any E2E scenario uses the changed instructions.
