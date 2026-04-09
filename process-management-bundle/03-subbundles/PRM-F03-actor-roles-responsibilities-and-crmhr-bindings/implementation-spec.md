# Implementation spec — PRM-F03

## Core implementation moves

- Create role and binding entities with snapshots plus durable CRM-HR references.
- Expose query helpers that make actor bindings easy to use in designers and runtime screens.
- Prefer bridge interfaces if CRM-HR shaping needs to evolve.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces over direct cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F03`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F01, PRM-F02

## Acceptance criteria

- A process step can reference responsible, consulted, approver, and observer roles.
- Roles can bind to CRM-HR parties and AI-agent profiles without duplicating durable identity.
- Actor rebinding preserves auditability of earlier runs.
- Future AI execution metadata can be attached without introducing a runtime dependency on AgentFramework.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
