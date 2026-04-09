# Verification plan — PRM-F18

## Expected verification outcomes

- Steps can define mandatory inputs, completeness checks, and structured rejection/rework reasons before execution continues.
- The model distinguishes normal path, approved variant, and exception path metadata with escalation or override requirements.
- Decision rights are explicit: who can decide, under what threshold or rule, with what evidence, and through which override route.
- Controls can be tagged as mandatory, conditional, or optional based on risk tier so low-risk work is not over-approved.
- Runtime journals capture exception reasons, overrides, and input-quality failures separately from generic failure states.

## Automated tests

- Unit tests for new invariants and validation rules
- Integration tests for persistence and cross-module seams
- Component tests for editor or viewer surfaces where applicable
- Playwright coverage for the main happy path if new end-user flow is introduced

## Manual verification checklist

1. Configure an input-quality rule and confirm bad inputs block progress with a structured reason.
2. Run an exception path and verify the journal captures exception and override details.
3. Test a low-risk step and confirm extra approvals are not forced without policy.

## Regression concerns to watch

- Blanket approvals added instead of risk-tiered controls
- Decision rights hidden in free text