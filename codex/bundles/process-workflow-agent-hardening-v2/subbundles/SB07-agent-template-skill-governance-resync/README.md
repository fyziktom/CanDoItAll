# SB07 Agent/template/skill governance resync

## Status

Completed.
Critical foundation: **Yes**

## Objective

Align agents, process templates, API skills, and active Codex skill root with the stricter operation contracts, tool registry, provider usage, and proof-quality requirements.

## Covered Inputs

R12; bundle skill requirements and user instruction to include agents/skills/tools.

## Prerequisites

SB01-SB06 implementation decisions stable.

## Exact Source References

- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/software-delivery/definition.md`
- `repo://Templates/Agents/teams/dotnet-delivery/members/blazor-application-developer/instructions.md`
- `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-application-developer/instructions.md`
- `repo://Templates/Agents/teams/delivery-platform/members/delivery-manager/instructions.md`
- `repo://codex/skills/candoitall-api-processes/SKILL.md`
- `repo://codex/skills/candoitall-api-agents/SKILL.md`
- `repo://codex/skills/bundles/candoitall-bundle-validator/SKILL.md`

## Deliverables

- Updated software delivery process template with explicit operation contracts and proof expectations.
- Updated agent instructions requiring real tool use/proof capture where applicable, not prose-only delivery.
- Updated API skills with strict proof and contract language.
- Active skill-root synchronization proof with repo and active SHA-256 hashes.
- No scenario-key hardcoding in production templates or skills.

## Dependency Impact

This subbundle affects downstream proof and must be treated as a dependency exactly as modeled in `bundle://plan/01-phase-plan.md`. If this subbundle fails, all downstream subbundles that depend on its runtime behavior or proof contract must be reopened.

## Validation Depth

Critical subbundle validation requires semantic adequacy proof: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, changed-file hashes, and command/browser transcripts where applicable.

## Implementation Steps

1. Update templates after SB01-SB06 APIs are stable.
2. Make agent instructions reference operation contracts and current-run proof binding.
3. Update bundle validator/execution skills only if SB05 changes proof contract.
4. Run skill/template drift scans.
5. Capture active skill-root synchronization proof before downstream closure.

## Scope Exceptions

None planned. If implementation discovers a legacy compatibility exception, record it in this file and in `traceability/` before continuing.

## Do Not Do

Do not patch prompts before runtime contracts are stable. Do not hide missing runtime behavior in stronger wording only.

## Acceptance Checklist

- [x] Source references were reopened before editing.
- [x] Implementation is the smallest correct change set for this subbundle.
- [x] Failing-first proof was captured for behavior-changing critical work.
- [x] Passing proof was captured after implementation.
- [x] Anti-stub audit was run.
- [x] Raw notes owned by this subbundle were closed or explicitly blocked.
- [x] Downstream dependency impact was reviewed before moving on.

## Proof Required

Template lint, scenario-key scan, skill hash sync, active/repo hash comparison, one process run using updated instructions.

## Browser Validation Logging

N/A unless template editor UI is changed.

## Progression Gate

SB09 must verify active skill sync and no stale prompt copies before final closure.

## Suggested Agent Prompt

You are implementing `SB07 Agent/template/skill governance resync` in `fyziktom/CanDoItAll` on branch `development`. Read this subbundle README, the root README, `plan/01-phase-plan.md`, `traceability/`, and all exact source references before editing. Implement only this subbundle. Do not close it without the required semantic proof, transcripts, changed-file hashes, anti-stub audit, and raw-note closure update.
