# SB16: 16-generic-process-and-workflow-regression

## Goal

Protect non-software process behavior and workflows.

## Required work

- Run non-software template tests: customer onboarding, business plan, incident response, architecture decision, agent training/improvement process if present.
- Run workflow-backed process step mapping tests.
- Run subprocess mapping tests.
- Ensure MAF upgrade did not make Processes depend on software-specific assumptions.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB16` are updated and the next subbundle can safely depend on it.
