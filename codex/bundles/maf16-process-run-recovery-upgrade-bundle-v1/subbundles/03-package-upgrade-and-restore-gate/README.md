# SB03: 03-package-upgrade-and-restore-gate

## Goal

Perform package upgrade and make restore deterministic.

## Required work

- Update package references or central package management if introduced.
- Do not leave mixed 1.3/1.6 MAF packages unless explicitly required and justified.
- Run restore and capture package downgrade/conflict warnings.
- Fix transitive dependency conflicts, especially `Microsoft.Extensions.AI`, `OpenTelemetry`, OpenAI/Azure OpenAI SDKs, and ModelContextProtocol.
- Do not proceed until restore is clean or all remaining warnings are documented.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB03` are updated and the next subbundle can safely depend on it.
