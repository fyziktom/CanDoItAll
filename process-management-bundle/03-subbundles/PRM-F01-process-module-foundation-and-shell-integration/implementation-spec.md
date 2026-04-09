# Implementation spec — PRM-F01

## Core implementation moves

- Mirror the current module registration pattern used by Projects, Factory, Workbench, and CRM-HR.
- Create a small landing surface first so shell navigation and routing can stabilize before deeper domain work lands.
- Add migration-project placeholders or first real migrations immediately so the module is not developed purely in memory.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces over direct cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F01`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: module foundation only

## Acceptance criteria

- A top-level /processes route exists and renders without breaking current shell navigation.
- A project-scoped route exists for process work and can be opened from project UX.
- The module is registered through the same composition pattern as existing CanDoItAll modules.
- SQLite and PostgreSQL migrations compile with the new module present.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
