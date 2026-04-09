# Implementation spec — PRM-F05

## Core implementation moves

- Model transitions and dedicated handoff records separately.
- Store reason text, default-path semantics, and retry/rework metadata.
- Add graph validation for orphan edges, unreachable ends, and invalid loops.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces over direct cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F05`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F02, PRM-F03, PRM-F04

## Acceptance criteria

- Decision paths can carry condition text, default-path markers, and branch priority.
- Handoffs record source actor, target actor, payload summary, and completion reason.
- The engine rejects invalid graphs such as unreachable end states or orphaned transitions.
- Sequential specialized handoffs are first-class even before AgentFramework runtime integration.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
