# Implementation spec — PRM-F14

## Core implementation moves

- Consume telemetry and conformance inputs rather than inventing disconnected KPI placeholders.
- Create improvement-request and training-marker models that stay separate from live run state.
- Route candidates to process owner / governance review instead of auto-mutating processes.

## Detailed expectations

1. Keep comments in source code in English.
2. Preserve SQLite compatibility and keep PostgreSQL migration parity where storage is touched.
3. Respect Workbench projection-only guardrails whenever Workbench surfaces are involved.
4. Reuse existing CanDoItAll seams before introducing new shared abstractions.

## Data and service notes

- Feature id: `PRM-F14`
- Canonical owner: `CanDoItAll.Modules.Processes`.
- Cross-module touchpoints: CanDoItAll.Modules.Processes, tests/CanDoItAll.Tests.Integration/ProcessInsightsIntegrationTests.cs

## Acceptance criteria

- The module can turn outcome telemetry and conformance signals into process-level improvement candidates.
- Improvement requests are separated from live execution state and can be routed to owner/governance review.
- Training-opportunity markers can be generated without contaminating normal execution queries.
- The design remains compatible with a later intelligence-lake layer.

## Suggested implementation order inside this feature

1. Add domain models and persistence mapping first.
2. Add services and validation rules second.
3. Add UI/editor/runtime integration third.
4. Add tests and end-to-end proof last.