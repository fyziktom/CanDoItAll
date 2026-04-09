# Implementation spec — PRM-F06

## Core implementation moves

- Add approval and escalation entities / services.
- Model explicit separation-of-duties and self-approval conflict checks.
- Keep policy metadata separate from mutable runtime state.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer explicit policy records over hidden booleans.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F06`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F03, PRM-F04, PRM-F05, PRM-F07

## Acceptance criteria

- A process run can pause awaiting approval and resume with an auditable decision.
- Escalation routes can target a human party or supervisory role.
- Policy metadata is explicit and not hidden inside runtime-only configuration.
- Approval policies can prevent self-approval or conflicting role combinations unless an explicit override path is configured.
- The model can later map to agent external-call approvals and collaboration rights.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI / runtime integration next.
5. Add end-to-end and regression coverage last.
