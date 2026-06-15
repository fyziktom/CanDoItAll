# SB05 Driver Abstractions, Capability Catalog, And Strategy Binding Contracts

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Create the generic driver extension model, capability catalog, strategy factory contracts, driver facets, branch family providers, recovery/resupply providers, and strategy result envelopes before Builder and Runtime consume them.

## Why This Bundle Exists

The current driver layer is mostly verification-oriented. The new runtime needs drivers as extension packages without leaking domain details into core/runtime.

## Covered Inputs

- REQ-006 through REQ-009.
- v3 corrected dependency order.

## Context Reset: Read These First

- SB03 execution report.
- `architecture/06-driver-strategy-and-manager-model.md`
- `architecture/11-project-boundary-and-dependency-map.md`
- `architecture/16-execution-adapters-and-integration-boundaries.md`

## Exact Source References

- `repo://codex/bundles/process-module-architecture-v3/architecture/06-driver-strategy-and-manager-model.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/11-project-boundary-and-dependency-map.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/16-execution-adapters-and-integration-boundaries.md`
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/CanDoItAll.Processes.Drivers.VerificationGateway`

## Source Evidence To Use

- `repo://src/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/CanDoItAll.Processes.Drivers.VerificationGateway`
- `repo://src/CanDoItAll.Processes.Drivers.*`
- SB01 driver archive inventory.

## Prerequisites

- SB03 complete.
- Core contracts available.

## In Scope

- Driver descriptors/packages.
- Driver catalog.
- Capability request/match/conflict model.
- Driver dependency/precedence/version rules.
- Strategy factory contracts.
- Strategy binding snapshot contracts.
- Branch family providers.
- Recovery/resupply providers.
- Manager facet providers.
- Template fragment providers.
- Strategy result envelopes.

## Out Of Scope

- No concrete driver implementation beyond test fakes.
- No runtime scheduling.
- No adapter implementation.
- No UI.

## Target Projects / Files

- `src/CanDoItAll.Processes.Drivers.Abstractions`
- tests for driver abstraction contracts.

## Deliverables

- Driver abstraction package.
- Capability catalog contracts.
- Strategy factory and result envelope contracts.
- Contract tests and negative fixtures.

## Expected Deliverables

- Builder can bind strategies through driver catalog contracts.
- Runtime can invoke selected strategy interfaces without referencing concrete drivers.
- Domain capability values remain opaque.

## Dependency Impact

- SB06 depends on driver catalog and binding contracts.
- SB07 depends on strategy result envelopes.
- SB11 depends on driver contracts for concrete driver slice.

## Validation Depth

- Validate with driver contract tests, capability matching tests, conflict/dependency tests, strategy factory tests, domain opacity scans, and dependency scans.

## Architecture Invariants That Must Hold

- Core/runtime do not change for a concrete driver.
- Driver-specific diagnostics become facets, not core state.
- Strategy implementations do not mutate runtime state.

## Implementation Steps

1. Define driver descriptor and package models.
2. Define capability match/conflict/dependency rules.
3. Define strategy factory contracts.
4. Define branch/recovery/manager/template contribution contracts.
5. Define result envelopes and diagnostics.
6. Add contract tests.

## Refactoring Review Checkpoint

- Keep driver contracts separate from implementations.
- Keep capability matching deterministic and testable.
- Ensure examples do not become core vocabulary.

## Required Tests / Proof

- Driver dependency ordering tests.
- Capability conflict tests.
- Strategy factory contract tests.
- Domain opacity tests.
- Negative tests for duplicate exclusive capabilities.

## Search Proof

- Search core/runtime/builder contracts for concrete driver names.
- Search driver abstractions for UI/persistence references.

## Stop And Report Conditions

- Stop if generic runtime must change for a concrete driver.
- Stop if driver abstractions require domain-specific enum values.
- Stop if strategy contracts require direct runtime mutation.

## Do Not Do

- Do not keep driver layer verification-only.
- Do not put domain values into core.
- Do not reference UI or persistence from driver abstractions.

## Acceptance Checklist

- [ ] Driver descriptors exist.
- [ ] Capability matching exists.
- [ ] Strategy factories exist.
- [ ] Result envelopes exist.
- [ ] Contract tests pass.

## Proof Required

- Test output.
- Domain opacity scan.
- Dependency scan.

## Browser Validation Logging

- Browser validation is not required because no UI behavior is implemented.

## Progression Gate

- SB06 may start after driver abstraction contracts and tests pass.

## Suggested Agent Prompt

Execute SB05 from `codex/bundles/process-module-architecture-v3/subbundles/05-driver-abstractions-capability-strategy-binding`. Build driver and strategy contracts only; do not implement concrete domain drivers.

## Handoff Notes For Next Bundle

Record driver catalog API, strategy binding contracts, capability matching behavior, and known open driver questions for SB06.
