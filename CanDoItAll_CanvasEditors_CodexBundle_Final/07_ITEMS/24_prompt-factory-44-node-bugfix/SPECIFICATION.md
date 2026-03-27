
# Specification

## Item identity

- **Item ID:** I24
- **Title:** Prompt Factory intermittent 44-node insertion bugfix
- **Origin:** docx
- **Dependencies:** I21

## Objective

Root-cause and fix the intermittent bug where a single component insertion sometimes attempts to add dozens of nodes.

## Normalized scope

Instrument, reproduce, and eliminate the intermittent duplicate-add behavior in Prompt Factory, with a regression harness that proves the fix.

### In scope

- Reproduction strategy and diagnostics.
- Prompt Factory add-component pipeline hardening.
- Regression tests and evidence.

### Out of scope

- General Prompt Factory UX redesign unrelated to the duplicate-add bug.

## Key implementation decisions

- Do not close this item without root-cause evidence; symptom-only patches are not enough.
- Assume the fault may live in event dispatch, repeated submissions, or interop duplication rather than only in the final add method.
- Guard the action pipeline against duplicate submissions where appropriate.

## Implementation tasks

- Add instrumentation or logging around component-add dispatch.
- Create a reliable reproduction or a bounded stress harness.
- Fix the root cause and add defensive deduplication where justified.
- Add regression tests that would fail on the old behavior.
- Document the root cause in the validation evidence.

## Risks to control

- Intermittent bugs often appear fixed until event duplication under load is re-tested.

## Covered original notes

- N152 — Bugs:
- N153 — Adding of any component wants to add 44 nodes (happens just sometimes, like 4/5 situations).
