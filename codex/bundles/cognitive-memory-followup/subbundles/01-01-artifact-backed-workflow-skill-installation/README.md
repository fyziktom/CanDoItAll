# SB01 - Artifact-backed workflow skill installation

## Status

- Status: `Completed`

## Objective

Upgrade the bundle workflow/execution/validator skills so critical work cannot continue from prose-only semantic evidence.

## Covered Inputs

- User concern: Codex often simplifies, skips, or passes weak gates.
- Current workflow skill has good prose but no artifact-backed blocking manifest.
- Current semantic proof reference lists labels but does not require transcripts/hashes/red-team artifacts.

## Prerequisites

- None. This is the first hard gate.

## Exact Source References

- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-workflow/SKILL.md
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-execution/SKILL.md
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-execution/references/semantic-adequacy-proof.md
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-validator/SKILL.md
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-subbundle-validator/SKILL.md
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-preparation/SKILL.md

## Deliverables

- Update workflow, execution, bundle-validator, subbundle-validator, and preparation skills to require proof manifests for critical subbundles.
- Add a new skill reference document for artifact-backed proof manifests and red-team closure.
- Add an installation/reload manifest showing repo skill paths and active Codex skill-root paths with hashes.
- Document that dependent subbundles are blocked until prerequisite proof manifests validate.

## Dependency Impact

- Blocks SB02-SB10.
- SB02 must implement executable validation for the rules introduced here.
- No cognitive-memory production files may be modified in this subbundle.

## Validation Depth

- Text diff proof is not enough; include active installed skill hash proof.
- Add a fake-proof prevention rule directly to the workflow skill, not only to execution skill.
- Require explicit stop-and-repair behavior when proof manifests are missing.

## Implementation Steps

- Reopen every current skill file listed in Exact Source References.
- Add artifact-backed proof manifest requirements to the workflow skill outcome contract and gate discipline.
- Add execution-skill instructions to create `proof/SBxx/manifest.*` before closure.
- Add validator-skill instructions that report prose-only proof as failure.
- Install or synchronize modified skills into the active Codex skill root used by the agent.
- Record before/after hashes and active reload checks in `proof/SB01/manifest.md`.

## Do Not Do

- Do not only add more motivational prose.
- Do not start cognitive-memory feature changes in SB01.
- Do not claim installation without hash/path evidence from the active skill root.

## Acceptance Checklist

- Workflow skill explicitly requires artifact-backed proof before critical subbundle closure.
- Execution skill requires failing-first and passing transcripts for critical behavior changes.
- Validator skills require manifest validation and red-team closure for critical subbundles.
- Installation manifest proves active skill root contains the new rules.

## Proof Required

- `proof/SB01/manifest.md` with changed skill file hashes.
- Transcript showing active skill root verification.
- No production cognitive-memory file changed in SB01.

## Browser Validation Logging

- N/A - process/skill work only.

## Progression Gate

- SB02 cannot start until active installed skills are verified by hash and reopened by Codex.
- If active skill root cannot be verified, mark SB01 Blocked and do not continue.

## Suggested Agent Prompt

Implement SB01. Modify and install the bundle workflow skill family so proof manifests are mandatory and downstream work is blocked until artifact-backed proof validates.


Remember: a subbundle is not complete because the report says it is complete. It is complete only when source code, tests, proof manifest, transcripts, and validator/red-team gates agree.
