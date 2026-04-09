# Implementation spec — PRM-F11

## Core implementation moves

- Use IActivityStream and IAutomationSignalSource-style seams where possible.
- Represent validation/test references as typed links rather than embedded foreign payload blobs.
- Keep hook writes resilient so process completion is not fragile when a downstream integration is temporarily unavailable.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces over direct cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F11`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F04, PRM-F06, PRM-F07, PRM-F08

## Acceptance criteria

- Runs can emit activity entries and automation signals without tight module coupling.
- Validation and TestLab references can be attached to steps and gates.
- Overdue steps and blocked approvals become visible in automation/operations surfaces.
- The hook design does not require the intelligence lake to exist first.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
