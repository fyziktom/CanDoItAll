# 02 Validator And Proof Depth Auditor

## Status

- `Completed`

## Execution Proof

- Entry gate: passed. SB01 was completed, active skills were reopened, and cognitive-memory feature code remained blocked.
- Source-reference repair: stale `/mnt/data/review_current/CanDoItAll-cognitive-memory` exact source references in subbundle READMEs were synchronized to `C:/repositories/CanDoItAll` so validation checks the current repo instead of failing on stale paths.
- Validator implementation: `codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` now runs completed-stage proof-depth checks for critical SBxx subbundles.
- Fixtures:
  - `codex/skills/bundles/candoitall-bundle-preparation/tests/fixtures/proof-depth-shallow`
  - `codex/skills/bundles/candoitall-bundle-preparation/tests/fixtures/proof-depth-complete`
- Repo-local proof:
  - `python -m py_compile .../validate_bundle.py` passed.
  - shallow fixture command exited `1` and reported missing SB01 semantic adequacy evidence.
  - complete fixture command exited `0`.
  - this bundle passed `--stage prepared --profile initiative`.
- Active skill proof:
  - synchronized validator, fixtures, and final-closure skill wording into `C:\Users\lucys\.codex\skills`.
  - active shallow fixture command exited `1`.
  - active complete fixture command exited `0`.
- Cognitive-memory source files were not edited in SB02.

## Objective

- Extend automation so a completed bundle cannot pass by filling required tables while missing semantic proof, adversarial tests, or raw-note behavioral closure.

## Success Criteria

- Completed-stage validation includes semantic proof checks for critical subbundles.
- A shallow completed-bundle fixture fails the proof-depth auditor.
- A complete fixture with semantic proof passes the proof-depth auditor.
- The auditor is invoked in final closure instructions for this bundle.

## Covered Inputs

- Existing `validate_bundle.py` validates structure, headings, table rows, and statuses but cannot inspect semantic proof quality.
- Previous execution report claimed completion even though source review found single-key clustering, template dreaming, and shallow recall synthesis.

## Prerequisites

- SB01 completed and updated skills installed/reopened.

## Exact Source References

- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-validator/SKILL.md
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-subbundle-validator/SKILL.md
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-execution/SKILL.md

## Deliverables

- Enhanced `validate_bundle.py` or a new companion proof-depth script.
- Fixture bundles under an appropriate tests/fixtures or tools/fixtures path that demonstrate shallow failure and complete success.
- Updated skill references that require running the proof-depth auditor before final closure.
- Execution report rows proving the shallow fixture fails.

## Dependency Impact

- Blocks cognitive-memory implementation because later completion must be checked by the stronger auditor.
- Protects against reports that say `Completed` without real behavior evidence.

## Validation Depth

- Process-critical closure with negative fixture proof.
- Automation proof plus manual audit proof.

## Implementation Steps

1. Inspect the current validation script and identify completed-stage blind spots.
2. Add checks for semantic proof sections or equivalent execution-report rows for critical subbundles.
3. Add checks that critical subbundles list at least one adversarial negative test and one shallow-pass trap.
4. Add checks that raw-note closure rows are not marked solved without proof paths or commands.
5. Add shallow and complete validator fixtures.
6. Run the auditor against both fixtures and this prepared bundle.

## Scope Exceptions

- Do not make the validator so rigid that normal UI-only or process-only subbundles must invent irrelevant backend tests; allow explicit N/A only with reason.
- Do not delay cognitive-memory bug fixes by requiring perfect generalized static analysis; the auditor should catch the known process failure class.

## Do Not Do

- Do not only update prose skills; add executable validation or a concrete proof-depth checklist that fails missing evidence.
- Do not let `Passed build` count as semantic proof by itself.
- Do not allow browser smoke to replace backend semantic tests.

## Acceptance Checklist

- Shallow completed fixture fails.
- Complete fixture passes.
- Final closure instructions mention both structural validator and proof-depth auditor.
- Execution report includes command outputs for both fixtures.

## Proof Required

- Validator/auditor command outputs.
- Diff of validator and fixture files.
- Execution report semantic proof gate row.
- Prepared-stage validation after validator updates.

## Browser Validation Logging

- N/A; validator/process only.
- No browser screenshots required.

## Progression Gate

- SB03 may start only after the proof-depth auditor is active and documented.
- Cognitive-memory code changes remain blocked until this gate passes.

## Suggested Agent Prompt

```text
Implement only the validator/proof-depth hardening. Add a failing shallow fixture and a passing complete fixture. Record command proof before moving to regression corpus work.
```
