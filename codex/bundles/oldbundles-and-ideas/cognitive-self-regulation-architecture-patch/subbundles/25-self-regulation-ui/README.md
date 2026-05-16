# 25 Self-Regulation UI

## Status

Architecture patch subbundle. This is not an implementation bundle.

## Objective

Define operator-visible surfaces for posture, confidence, warnings, humility triggers, calibration health, and professor review.

## Deliverables

- Architecture documentation updates.
- C# contract updates where applicable.
- Requirements and acceptance criteria updates.
- Validation/test plan updates.
- Traceability updates.

## Do Not Do

- Do not implement runtime code yet.
- Do not mutate canonical memory directly.
- Do not bypass source, access, redaction, review, or mutation authority.
- Do not use display confidence as a decision model.

## Acceptance Checklist

- Deliverables are added to the main architecture bundle.
- Dependencies and progression gates are documented.
- Non-happy paths and safety cases are covered.
- Integration with score geometry, probing, attention, and metamemory is explicit.
