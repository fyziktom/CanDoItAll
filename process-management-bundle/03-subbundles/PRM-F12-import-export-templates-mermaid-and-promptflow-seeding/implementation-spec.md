# Implementation spec — PRM-F12

## Core implementation moves

- Reuse Workbench Mermaid heuristics as a reference, but implement a dedicated process import/export service.
- Treat Mermaid imports as draft/bootstrap only with warnings.
- Define a lossless JSON package format for execution-grade portability.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer narrow bridge interfaces over direct cross-module coupling.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F12`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F02, PRM-F09

## Acceptance criteria

- Mermaid mindmap and flowchart can be imported into a draft process with explicit limitations recorded.
- Published processes can be exported as Mermaid and JSON packages.
- Starter templates can reference prompt-flow patterns without making Prompt Factory the canonical process store.
- Import warnings are explicit whenever semantics do not round-trip perfectly.

## Suggested implementation order inside this feature

1. Add domain/contracts/entities first.
2. Add EF configuration and persistence tests.
3. Add application services and validation rules.
4. Add UI surfaces/adapters next.
5. Add end-to-end and regression coverage last.
