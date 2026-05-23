# Assumptions And Risks

## Assumptions

- Missing artifact recovery should happen only after an execution declares completion and projection still leaves required artifact expectations unsatisfied.
- The process manager may be resolved from the run manager id/name, manager-like agent options, or manager-like agent definitions, matching the existing manager chat resolution model.
- If no manager can be resolved, the runtime should block with an explicit reason and journal entry rather than silently rerun the step executor.

## Critical Path Risks

- Manager recovery can introduce another long-running agent call. The implementation must cap this path to one targeted recovery pass per completed execution outcome.
- The manager may write artifacts with paths that do not match expectation titles or governed artifact paths. Existing projection validation must remain authoritative.
- Live runs with already-pending tool approvals may still need operator action; the code change prevents the repeated bad pattern for subsequent dispatch/recovery cycles.

## Validation Risks

- A static directive test alone would not prove runtime routing. Add at least one focused routing/helper assertion or integration test that exercises manager resolution behavior.
- Live process state can change while validation runs; use saved run evidence for analysis and automated tests for proof.

## Reopen Triggers

- Missing artifact recovery still invokes the original step executor when a manager can be resolved.
- The dispatcher records `Completed` while required artifact expectation ids remain missing.
- Manager recovery failure produces no actionable blocked reason or no journal evidence.
