# 11. QA Remediation Summary Round 1

The round-1 QA findings were incorporated into the bundle as follows.

## Closed item 1. Atomicity semantics clarified

Updated in:

- `01-current-state-analysis.md`
- `02-target-operating-model.md`

Resolution:

- bundle 1 atomicity now explicitly means logical active-runtime atomicity for Codex
- stable public port continuity without a relay/proxy is explicitly out of scope

## Closed item 2. Rollback made mandatory and testable

Updated in:

- `03-architecture-redesign.md`
- `04-tool-contract-and-state-model.md`
- `05-implementation-plan.md`
- `08-validation-criteria.md`

Resolution:

- rollback tool, state transitions, and validation gates are now explicit

## Closed item 3. Retry governance tightened

Updated in:

- `04-tool-contract-and-state-model.md`
- `08-validation-criteria.md`

Resolution:

- idempotent vs non-idempotent retry rules are now defined
- idempotency keys are required for non-idempotent operations

## Closed item 4. Resource scope examples added

Updated in:

- `03-architecture-redesign.md`
- `05-implementation-plan.md`
- `06-checklists.md`

Resolution:

- the bundle now names concrete resources and scope rules instead of only saying "replace the global lock"

## Closed item 5. Measurable validation thresholds added

Updated in:

- `08-validation-criteria.md`

Resolution:

- watch fluency, bridge repair, atomic prepare, commit, and rollback now have explicit pass/fail criteria

## Closed item 6. Backward compatibility elevated to a release gate

Updated in:

- `04-tool-contract-and-state-model.md`
- `06-checklists.md`
- `08-validation-criteria.md`

Resolution:

- current tool names and `WatchRun`/`RunOnce` flows are now protected by strict compatibility requirements

## Closed item 7. Candidate endpoint allocation made explicit

Updated in:

- `03-architecture-redesign.md`
- `05-implementation-plan.md`
- `06-checklists.md`
- `07-prompts.md`
- `08-validation-criteria.md`

Resolution:

- candidate endpoint leasing is now a named architectural component with validation gates

## Closed item 8. Self-host validation isolation promoted to a first-class requirement

Updated in:

- `01-current-state-analysis.md`
- `03-architecture-redesign.md`
- `05-implementation-plan.md`
- `06-checklists.md`
- `07-prompts.md`
- `08-validation-criteria.md`

Resolution:

- live-backend-safe build/test validation of `CanDoItAll.Mcp.DotNetWatch` is now an explicit requirement instead of an implied side benefit

## QA status after remediation

Round-1 findings are considered closed.
Proceed to final approval review.
