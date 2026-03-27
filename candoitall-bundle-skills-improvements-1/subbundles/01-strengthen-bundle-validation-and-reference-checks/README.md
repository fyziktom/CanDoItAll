# Strengthen bundle validation and reference checks

## Status

- `Completed`

## Objective

- Strengthen the preparation contract so a bundle cannot be marked ready while its exact source references are broken or its feedback execution report lacks the sections needed for note-by-note closure.

## Covered Inputs

- `F001`
- `F002`
- `R001`
- `R002`

## Exact Source References

- `C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\SKILL.md`
- `C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py`

## Deliverables

- validator support for checking absolute existing paths under `## Exact Source References`
- validator support for checking feedback execution-report scaffolding
- preparation-skill instructions aligned with the stricter validator rules

## Implementation Steps

1. Extend `validate_bundle.py` with narrowly scoped markdown parsing for subbundle `## Exact Source References`.
2. Add feedback-profile validation for required execution-report headings.
3. Update the preparation skill to describe the new checks and the expectation that bundles are fixed before being marked ready.

## Scope Exceptions

- none

## Do Not Do

- do not reject bundles for arbitrary prose formatting outside the named contract sections
- do not add execution-only artifact checks to preparation validation

## Acceptance Checklist

- a broken exact source reference causes validator failure
- a feedback bundle execution report missing `## Status` or `## Raw Note Closure` causes validator failure
- `canvas-feedback-bundle-6` still validates after the validator changes

## Proof Required

- `python "C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py" "C:\repositories\CanDoItAll\candoitall-bundle-skills-improvements-1" --profile feedback`
- `python "C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py" "C:\repositories\CanDoItAll\canvas-feedback-bundle-6" --profile feedback`

## Suggested Agent Prompt

```text
Implement subbundle 01 only.

Strengthen bundle preparation validation without overfitting it to this repository. Exact source references must be absolute and real, feedback execution reports must already have the structure needed for raw-note closure, and existing good bundles must still validate.
```
