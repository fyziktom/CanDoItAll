# Implementation spec — PRM-F13

## Core implementation moves

- Define bridge contracts and null/manual implementations inside Processes.
- Map actor execution mode and approval posture conceptually to the overlay repo's rights model.
- Keep this seam thin enough that a later adapter project can implement it.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces over direct cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F13`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F03, PRM-F05, PRM-F06, PRM-F07

## Acceptance criteria

- The process runtime can distinguish manual, AI, and hybrid executor modes.
- The process module compiles and works without referencing AgentFramework projects.
- A bridge contract exists for future AI execution and handoff orchestration adapters.
- CRM-HR remains the durable owner of AI agent identity and staffing.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
