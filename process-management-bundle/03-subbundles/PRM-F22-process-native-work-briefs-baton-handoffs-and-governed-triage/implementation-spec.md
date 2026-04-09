# Implementation spec — PRM-F22

## Core implementation moves

- Introduce work-brief and triage domain models in Processes.
- Extend runtime services so step activation and handoff create durable work brief snapshots.
- Ensure triage decisions are queryable and replayable instead of hiding in ephemeral runtime prompts.
- Add process/project context links so the baton packet carries the right business context without duplicating project hierarchy.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces or projection services over broad cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench or Canvas surfaces are involved.

## Data and service notes

- Feature id: `PRM-F22`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F04, PRM-F05, PRM-F07, PRM-F16, PRM-F17

## Acceptance criteria

- Each executable step can materialize a normalized work brief from process, step, template, customer, and governance context.
- Baton handoffs are persisted as first-class runtime artifacts with source role, target role, brief snapshot, and completion context.
- Triage or dispatcher behavior is modeled as a process role, step, or governed routing decision record rather than hidden out-of-band agent topology.
- Direct production agent-to-agent wiring outside the process requires an explicit override path with journal evidence.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
