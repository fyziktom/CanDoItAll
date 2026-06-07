# process-core-evidence-descriptors-driver-contract-roadmap-v1

Prepared: 2026-06-07

## Profile
Initiative / architecture hardening bundle.

## Objective
Continue from the completed narrow Process Core seed and pure-rule expansion. Stabilize Core further with execution/finalizer evidence descriptors, adapter confinement, diagnostic result models, and a driver-contract roadmap that remains non-production.

## Current Review
The latest branch state is accepted in scope, but the next work should:
- Close or explicitly classify the current 3 build warnings.
- Add only deterministic Core evidence/read-model descriptors.
- Keep all runtime, persistence, workspace/storage, finalizer and AgentFramework behavior module-local.
- Keep domain driver work proposal-only until permission/audit/sandbox/runtime ownership is proven.

## Subbundle count
42 subbundles across 14 phases.

## Critical gates
Every phase ends with a gate subbundle:
SB003, SB006, SB009, SB012, SB015, SB018, SB021, SB024, SB027, SB030, SB033, SB036, SB039, SB042.

## Hard constraints
See `requirements/02-hard-constraints.md`.

## Implementation start
Start with `plan/01-phase-plan.md`, then execute `subbundles/SB001-*`.
