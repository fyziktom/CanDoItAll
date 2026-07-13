# 03-tests-and-runtime-proof

## Status

- `Complete`

## Objective

Run focused validation, record proof, and close architecture findings.

## Covered Inputs

- Verify the new filesystem tool family is safe, discoverable, and working.

## Prerequisites

- SB01 and SB02 completed.

## Exact Source References

- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`
- `bundle://reviews/01-execution-report.md`
- `bundle://reviews/csharp-architecture-gate.md`

## Deliverables

- Passing focused tests.
- Affected project build output.
- Updated execution report and architecture gate.

## Dependency Impact

- Final closure.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Run focused unit tests for filesystem plugin, policy, templates, and composition.
2. Build affected MAF/Core/Web projects as needed.
3. Run architecture review gate.
4. Update bundle proof and closure rows.

## Scope Exceptions

Live browser validation is not required for this backend tool change.

## Do Not Do

- Do not close with build-only proof.
- Do not ignore failed template seed tests.

## Acceptance Checklist

- [x] Focused tests pass.
- [x] Builds pass or known unrelated warning is documented.
- [x] Architecture gate passes.
- [x] Raw notes are closed.

## Proof Required

- Command transcript summaries in execution report.

## Browser Validation Logging

- N/A.

## Progression Gate

- Passed for the filesystem architecture slice. Full unit-suite unrelated failures are documented in `reviews/01-execution-report.md`.

## Suggested Agent Prompt

```text
Run SB03 validation. If a test fails, repair the implementation and rerun.
```

## C# Architecture Impact

Closure review for extracted filesystem boundary.

## Boundary Ownership

Verify no behavior drifted back into `WorkspaceRuntimePlugin`.

## Dependency Direction

Verify no new cycles/references.

## Pattern Decision

Confirm plugin/catalog composition remains justified.

## Testability Contract

Direct tests must instantiate extracted behavior.

## Partial Class Policy

No new partial class allowed.

## Architecture Proof Required

Final architecture gate table.
