# Implementation spec — PRM-F15

## Core implementation moves

- Add indexes on ProjectId, DefinitionId, VersionId, RunId, state, and timestamps.
- Keep migration projects aligned from the start.
- Document retention or archive strategy for high-volume event rows.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces over direct cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F15`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F01, PRM-F02, PRM-F07, PRM-F08

## Acceptance criteria

- Process tables live in the main app database with consistent naming and indexing conventions.
- SQLite remains supported for local users without extra setup.
- PostgreSQL migrations exist and stay in lockstep with SQLite.
- The journal and runtime tables have a defined retention/extraction seam for future scale.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
