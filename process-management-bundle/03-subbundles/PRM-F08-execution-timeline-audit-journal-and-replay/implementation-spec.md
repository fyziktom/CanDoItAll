# Implementation spec — PRM-F08

## Core implementation moves

- Create append-only event rows with actor, reason, state, and timing data.
- Publish high-level events through IActivityStream.
- Provide replay/query helpers for timelines and debugging.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces over direct cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F08`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F07

## Acceptance criteria

- Every run change emits a durable process event with actor and reason metadata.
- High-level process events appear on the shared activity stream.
- A replay API can reconstruct step order and handoff decisions from journaled events.
- Journal writes are separated from mutable current-state rows.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
