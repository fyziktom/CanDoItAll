# Implementation spec — PRM-F02

## Core implementation moves

- Create normalized entities for definitions, versions, nodes, and transitions.
- Add validation rules for node kind combinations, start/end constraints, and version publication invariants.
- Provide application services for create, clone, publish, archive, and list.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces over direct cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F02`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F01

## Acceptance criteria

- Process definitions support draft, published, and archived versions.
- Published versions are immutable and draft edits produce a new working version.
- Definitions can be scoped as workspace templates or project-owned processes.
- The canonical graph is stored outside Workbench metadata.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
