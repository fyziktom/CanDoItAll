# Implementation spec — PRM-F07

## Core implementation moves

- Add run/step/assignment entities and services.
- Keep state transitions deterministic and replay-friendly.
- Add assignment resolution that can use template-derived eligibility, capacity/validation state, and fallback routes.
- Journal meaningful rebind decisions.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer deterministic transitions and explicit concurrency handling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F07`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F03, PRM-F05, PRM-F06, PRM-F16

## Acceptance criteria

- A process run can start from a published definition version and keep that version immutable for the run lifetime.
- Only valid state transitions are allowed for runs and steps.
- Conflicting claims and double completions are rejected deterministically.
- Assignment resolution can consider eligible pools, capacity/validation state, and fallback routes before work is claimed or rebound.
- Manual, human-approved, and AI-backed executors all fit the same state machine.

## Suggested implementation order inside this feature

1. Add run/state entities first.
2. Add persistence and concurrency protections.
3. Add assignment resolution rules and fallback handling.
4. Add UI / journal integration.
5. Add end-to-end and regression coverage last.
