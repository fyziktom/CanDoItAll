# 01 Skill Semantic Proof Gates Installation

## Status

- `Ready`

## Objective

- Upgrade the bundle execution and validation skills so deep implementation work cannot pass with shallow scaffolding, then install/reload those skills before any cognitive-memory code changes continue.

## Success Criteria

- Updated execution, bundle-validator, subbundle-validator, and preparation skill documents require semantic proof gates for critical subbundles.
- New reference material defines shallow-pass traps, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- The updated skills are installed into the active Codex skill location used by the implementation agent, or the execution report records the exact active skill-root blocker.
- The implementation agent reopens and cites the updated skills before starting SB02.

## Covered Inputs

- User concern that Codex often simplifies, skips, or treats weak gates as success.
- Observed previous execution report marked all subbundles completed while source behavior remained shallow.
- Existing skills contain good intent but not enough mandatory semantic proof enforcement.

## Prerequisites

- none; this is the first gate and must run before feature-code changes.

## Exact Source References

- /mnt/data/review_current/CanDoItAll-cognitive-memory/codex/skills/bundles/candoitall-bundle-execution/SKILL.md
- /mnt/data/review_current/CanDoItAll-cognitive-memory/codex/skills/bundles/candoitall-bundle-validator/SKILL.md
- /mnt/data/review_current/CanDoItAll-cognitive-memory/codex/skills/bundles/candoitall-subbundle-validator/SKILL.md
- /mnt/data/review_current/CanDoItAll-cognitive-memory/codex/skills/bundles/candoitall-bundle-preparation/SKILL.md
- /mnt/data/review_current/CanDoItAll-cognitive-memory/codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py

## Deliverables

- Updated `candoitall-bundle-execution` skill with a mandatory Semantic Adequacy Gate for critical work.
- Updated `candoitall-bundle-validator` and `candoitall-subbundle-validator` skills to fail shallow proof.
- New or updated reference file such as `references/semantic-adequacy-proof.md` under bundle skills.
- Installation/reload proof for the active skill set used by Codex.
- Execution report row that quotes the updated skill requirement and confirms it was read before continuing.

## Dependency Impact

- Blocks all later subbundles because later work must execute under stricter proof rules.
- If this subbundle is weak, Codex can again close later subbundles with structure-only evidence.

## Validation Depth

- Process-critical closure.
- No cognitive-memory source edits are allowed in this subbundle except tests/fixtures proving the skill update if needed.

## Implementation Steps

1. Open the existing execution/validator/preparation skills and identify where they permit weak proof.
2. Add the semantic proof contract from `templates/semantic-proof-gate-template.md` to the relevant skill references.
3. Update the execution skill Required Flow so every critical subbundle must document shallow-pass trap, adversarial negative test, semantic positive test, anti-stub audit, and raw-note closure.
4. Update validator skills so a gate fails when semantic proof is absent or when tests assert template markers instead of behavior.
5. Install or synchronize the updated skill files into the active Codex skill root if the environment has one; otherwise record the exact reason and require the repo-local skill path to be used.
6. Reopen the updated skill files and record a short citation/excerpt in the execution report before proceeding.

## Scope Exceptions

- Do not implement cognitive-memory behavior in this subbundle.
- Do not replace full skill installation with a prose promise.

## Do Not Do

- Do not remove useful existing skill guidance; harden it.
- Do not mark this complete without proving the implementation agent actually loaded or reopened the changed skills.
- Do not use structural bundle validation as proof that this subbundle succeeded.

## Acceptance Checklist

- Updated skill files contain explicit Semantic Adequacy Gate language.
- Updated skills require adversarial tests for critical subbundles.
- Execution report records active skill installation/reload proof.
- Execution report says cognitive-memory code changes are still untouched before this gate closes.

## Proof Required

- Git diff of changed skill/reference files.
- Command or file-copy log showing skill installation/synchronization, if active skill root exists.
- Execution report excerpt showing the updated skill requirement was reopened and acknowledged.
- Prepared-stage bundle validation after skill changes if this bundle contract is edited.

## Browser Validation Logging

- N/A; skill/process only.
- No browser screenshots required.

## Progression Gate

- SB02 may start only after updated skills are installed/reopened and the execution report cites them.
- No cognitive-memory feature files may be edited before this gate passes.

## Suggested Agent Prompt

```text
Implement only the execution-skill hardening. Update and install the skills, reopen them, record proof, and stop before touching cognitive-memory feature code.
```
