# Implementation spec — PRM-F21

## Core implementation moves

- Add structured conformance observations and deviation clustering.
- Compare observed execution with canonical process paths.
- Enforce access control and privacy-safe handling for sensitive review notes.

## Detailed expectations

1. Keep comments in source code in English.
2. Preserve SQLite compatibility and keep PostgreSQL migration parity where storage is touched.
3. Respect Workbench projection-only guardrails whenever Workbench surfaces are involved.
4. Reuse existing CanDoItAll seams before introducing new shared abstractions.

## Data and service notes

- Feature id: `PRM-F21`
- Canonical owner: `CanDoItAll.Modules.Processes` with CRM-HR or Security bridges where needed.
- Cross-module touchpoints: CanDoItAll.Modules.Processes, CanDoItAll.Modules.Security

## Acceptance criteria

- Reviewers can record conformance observations against runs or process versions with structured deviation reasons.
- The system can cluster repeated unofficial loops, extra handoffs, and bypass patterns from journals for owner review.
- Observation notes support restricted visibility and privacy-safe governance handling; there is no unmanaged rumor registry.
- Process owners can convert deviation clusters into approved variants, fixes, or policy-breach investigations.
- Conformance reporting can show paper-versus-reality deltas by step, interface, owner, customer segment, or project.

## Suggested implementation order inside this feature

1. Add domain models and persistence mapping first.
2. Add services and validation rules second.
3. Add UI/editor/runtime integration third.
4. Add tests and end-to-end proof last.