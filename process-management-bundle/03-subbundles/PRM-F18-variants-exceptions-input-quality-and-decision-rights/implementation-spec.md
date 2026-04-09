# Implementation spec — PRM-F18

## Core implementation moves

- Model input-quality rules, exception playbooks, approved variants, and decision-right rules canonically.
- Integrate with runtime/journal flows so exceptions and overrides are explicit.
- Add risk-tiered control metadata to avoid blanket approval bureaucracy.

## Detailed expectations

1. Keep comments in source code in English.
2. Preserve SQLite compatibility and keep PostgreSQL migration parity where storage is touched.
3. Respect Workbench projection-only guardrails whenever Workbench surfaces are involved.
4. Reuse existing CanDoItAll seams before introducing new shared abstractions.

## Data and service notes

- Feature id: `PRM-F18`
- Canonical owner: `CanDoItAll.Modules.Processes`.
- Cross-module touchpoints: CanDoItAll.Modules.Processes

## Acceptance criteria

- Steps can define mandatory inputs, completeness checks, and structured rejection/rework reasons before execution continues.
- The model distinguishes normal path, approved variant, and exception path metadata with escalation or override requirements.
- Decision rights are explicit: who can decide, under what threshold or rule, with what evidence, and through which override route.
- Controls can be tagged as mandatory, conditional, or optional based on risk tier so low-risk work is not over-approved.
- Runtime journals capture exception reasons, overrides, and input-quality failures separately from generic failure states.

## Suggested implementation order inside this feature

1. Add domain models and persistence mapping first.
2. Add services and validation rules second.
3. Add UI/editor/runtime integration third.
4. Add tests and end-to-end proof last.