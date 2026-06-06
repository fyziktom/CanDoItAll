# Assumptions and risks

## Critical Path Risks

- Projection behavior can regress silently if source-family order changes.
- Duplicated candidate mutation logic can create duplicate artifacts or missing `RecordedArtifactExpectationIds`.
- Splitting host methods too aggressively can hide file/EF/storage side effects behind pure-looking names.
- Removing broad host too early can break compatibility wrappers used by tests.
- Driver-readiness vocabulary can accidentally turn into a production driver API if not explicitly constrained.

## Validation Risks

- Build-only proof is not enough.
- Source scans must verify dependency direction, not just file existence.
- Integration tests must include negative cases for duplicate keys, external reference collisions, missing expectation ids, outside-workspace paths and source-family order.
- Existing broad architecture tests may have unrelated historical fixture issues; bundle-specific tests must be precise and portable.

## Reopen Triggers

Reopen the last source-moving subbundle if any of these happen:

- A coordinator still depends on `ProcessRunAutomationDispatchService` directly after the migration phase.
- The broad host remains large without a documented temporary exception.
- A source-family is reordered or skipped.
- Candidate mutation is duplicated or becomes source-family-specific.
- Any Process Core, production driver API, UI file, or prohibited viewport proof appears.
