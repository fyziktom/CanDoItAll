# SB02 - Artifact-backed validator and fake-proof fixtures

## Status

- Status: `Completed`

## Objective

Extend bundle validation so a completed bundle with plausible semantic prose but missing artifacts fails.

## Covered Inputs

- Current `validate_bundle.py` checks labels and weak values, not transcripts or source/test artifact reality.
- Previous report passed by self-reporting semantic evidence.
- Need a validator that proves the process hardening is executable.

## Prerequisites

- SB01 completed and active skills verified.

## Exact Source References

- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-execution/references/semantic-adequacy-proof.md
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-validator/SKILL.md
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-subbundle-validator/SKILL.md
- C:/repositories/CanDoItAll/codex/bundles/cognitive-memory-execution-depth-professor-learning-followup/reviews/01-execution-report.md

## Deliverables

- Add artifact-backed completed-stage validation for critical subbundle proof manifests.
- Add negative fixtures: prose-only fake proof, missing transcript, fake test name, missing changed-file hash, missing failing-first transcript.
- Add positive fixture with real local artifacts and command transcripts.
- Add cross-platform path handling so Windows and POSIX absolute paths are handled intentionally, not accidentally.

## Dependency Impact

- Blocks SB03-SB10.
- SB03 must use the new proof manifest schema.
- Future completed-stage validation must fail if proof artifacts are missing.

## Validation Depth

- Run validator against every fake-proof fixture and prove failure.
- Run validator against the positive fixture and prove pass.
- Run `python -m py_compile` for validator script.
- Include command transcripts in `proof/SB02/`.

## Implementation Steps

- Design the proof manifest schema or parser.
- Update `validate_bundle.py` to read manifests for completed critical subbundles.
- Reject completed critical subbundles without manifest paths in execution report or subbundle README.
- Verify transcript files exist and contain command line, exit code, and cited test names.
- Verify failing-first transcript contains a failing exit code for required negative tests.
- Verify passing transcript contains passing exit code after implementation.
- Add fixture tests or script-level smoke commands.

## Do Not Do

- Do not only require more labels in the execution report.
- Do not accept command names without transcript files.
- Do not mark the previous bundle as valid just because it has semantic evidence blocks.

## Acceptance Checklist

- A prose-only fake completed bundle with all semantic labels fails.
- A bundle with fake test names and no transcript fails.
- A bundle with no failing-first evidence for critical feature work fails.
- A valid fixture with artifacts passes.
- Actual current bundle can be revalidated or honestly listed as partial/failing if its proof artifacts are insufficient.

## Proof Required

- `proof/SB02/manifest.md` with validator script hash and fixture paths.
- Transcript: `python -m py_compile ...validate_bundle.py`.
- Transcript: fake-proof fixtures failing for the expected reasons.
- Transcript: positive fixture passing.

## Browser Validation Logging

- N/A - validator/process work only.

## Progression Gate

- SB03 cannot start until the fake-proof fixtures fail and the positive fixture passes.
- If the validator can still be satisfied by invented prose, reopen SB02.

## Suggested Agent Prompt

Implement SB02. Make completed-stage validation artifact-backed. Prove that plausible prose-only semantic evidence fails.


Remember: a subbundle is not complete because the report says it is complete. It is complete only when source code, tests, proof manifest, transcripts, and validator/red-team gates agree.
