# Implementation spec — PRM-F24

## Core implementation moves

- Introduce a dedicated overlay projection service that composes definition, run, and journal state.
- Extend the process canvas adapter with overlay badges and selection navigation.
- Keep all state mutation in runtime services; overlays remain read-only projections.
- Add end-to-end tests that verify projection boundaries and live supervisory visibility.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces or projection services over broad cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench or Canvas surfaces are involved.

## Data and service notes

- Feature id: `PRM-F24`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F07, PRM-F08, PRM-F09, PRM-F22

## Acceptance criteria

- A live run can be viewed on the authored process canvas with active, waiting, blocked, and completed step overlays.
- Canvas overlays show current assignee or executor, wait reason, approval state, and last baton movement where relevant.
- Timeline and canvas views link to the same underlying run and journal without duplicate state mutation paths.
- Runtime overlay projection is explicitly separated from canonical definition data and mutable runtime state.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
