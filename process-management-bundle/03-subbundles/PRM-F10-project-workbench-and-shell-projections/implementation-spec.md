# Implementation spec — PRM-F10

## Core implementation moves

- Add navigation affordances where project users already open structure and calendar.
- If Workbench projection is added, keep it explicitly read/projection oriented.
- Only add new ProjectObjectType values if the projection use case truly needs typed objects.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces over direct cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F10`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F01, PRM-F02, PRM-F07, PRM-F09

## Acceptance criteria

- Project surfaces expose a clear entry point into processes.
- If Workbench projection is enabled, it shows references and summaries rather than acting as the canonical store.
- Process-related project object types and routes remain explicit and typed.
- Shared-project processes are navigated through project ownership rather than duplicated shadow copies.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
