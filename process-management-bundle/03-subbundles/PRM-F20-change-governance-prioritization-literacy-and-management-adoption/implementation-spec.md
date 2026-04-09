# Implementation spec — PRM-F20

## Core implementation moves

- Add change-request workflow models with impact analysis and governance approvals.
- Add communication and acknowledgement tracking for major process changes.
- Introduce portfolio prioritization and role-specific process-literacy surfaces.

## Detailed expectations

1. Keep comments in source code in English.
2. Preserve SQLite compatibility and keep PostgreSQL migration parity where storage is touched.
3. Respect Workbench projection-only guardrails whenever Workbench surfaces are involved.
4. Reuse existing CanDoItAll seams before introducing new shared abstractions.

## Data and service notes

- Feature id: `PRM-F20`
- Canonical owner: `CanDoItAll.Modules.Processes` with CRM-HR or Security bridges where needed.
- Cross-module touchpoints: CanDoItAll.Modules.Activity, CanDoItAll.Modules.Processes

## Acceptance criteria

- Change proposals capture reason, impacted processes and roles, expected outcomes, risk, and rollout plan.
- Publish, retire, and critical-change operations can require governance approval based on criticality and impact.
- Affected owners, stewards, approvers, and participants receive communication and acknowledgement tasks when governed versions change.
- The process portfolio can classify criticality and prioritization tiers so not every process is modeled to the same depth.
- UI surfaces provide role-based guidance and glossary/help so middle management and operators can understand the process model.

## Suggested implementation order inside this feature

1. Add domain models and persistence mapping first.
2. Add services and validation rules second.
3. Add UI/editor/runtime integration third.
4. Add tests and end-to-end proof last.