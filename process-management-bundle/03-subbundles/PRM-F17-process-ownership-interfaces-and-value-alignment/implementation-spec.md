# Implementation spec — PRM-F17

## Core implementation moves

- Add governance-profile entities for owner, customer, criticality, and strategy links.
- Add interface-contract entities and validation rules.
- Enforce publish guardrails for governed processes that lack required ownership metadata.

## Detailed expectations

1. Keep comments in source code in English.
2. Preserve SQLite compatibility and keep PostgreSQL migration parity where storage is touched.
3. Respect Workbench projection-only guardrails whenever Workbench surfaces are involved.
4. Reuse existing CanDoItAll seams before introducing new shared abstractions.

## Data and service notes

- Feature id: `PRM-F17`
- Canonical owner: `CanDoItAll.Modules.Processes` with CRM-HR or Security bridges where needed.
- Cross-module touchpoints: CanDoItAll.Modules.Processes

## Acceptance criteria

- A process definition cannot be published without a process owner, primary customer, criticality tier, and value statement.
- Process definitions can declare sponsor, stewarding managers, strategic objective links, and upstream/downstream interface contracts.
- Interface contracts capture sender, receiver, required inputs/outputs, definition of done, and handoff expectation metadata.
- Actor assignments remain separate from org hierarchy; the model does not force the process graph to mirror reporting lines.
- Shared-project or shared-library processes preserve explicit ownership instead of being duplicated as shadow copies.

## Suggested implementation order inside this feature

1. Add domain models and persistence mapping first.
2. Add services and validation rules second.
3. Add UI/editor/runtime integration third.
4. Add tests and end-to-end proof last.