# Implementation spec — PRM-F04

## Core implementation moves

- Use typed contract rows for inputs, outputs, and evidence requirements.
- Support both human-readable guidance and machine-checkable requirement flags.
- Keep cross-module artifact references typed rather than opaque strings where possible.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces over direct cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F04`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F02, PRM-F03

## Acceptance criteria

- Each step can declare entry criteria, exit criteria, expected artifacts, and evidence requirements.
- Steps can declare reusable input and output contracts with type, cardinality, and notes.
- Reviewers can see required evidence before completion is allowed.
- Contract data is queryable separately from the diagram layout.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
