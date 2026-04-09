# Implementation spec — PRM-F09

## Core implementation moves

- Build a dedicated process-designer adapter on top of CanvasLib.
- Keep graph semantics canonical outside layout data.
- Add template-aware role editing and staffing-status cues without blocking Wave 1 on deeper Wave 2 handoff chrome.

## Detailed expectations

1. Introduce or modify the listed repo touchpoints in the smallest coherent slice that can compile and test.
2. Keep comments in source code in English.
3. Prefer adapter-level composition before modifying shared CanvasLib primitives.
4. Preserve SQLite compatibility and keep PostgreSQL migration parity when storage is touched.
5. Honor Workbench projection-only guardrails whenever Workbench is part of the feature.

## Data and service notes

- Feature id: `PRM-F09`
- Canonical owner: `CanDoItAll.Modules.Processes`
- Cross-module touchpoints: PRM-F02, PRM-F03, PRM-F16

## Acceptance criteria

- Users can create and edit process nodes and transitions from an interactive canvas surface.
- Diagram layout persists independently from the canonical graph semantics.
- Phase grouping and actor grouping can be represented without forcing Workbench to be the source of truth.
- Wave 1 designer delivery is not blocked by later handoff-label chrome; handoff visuals can deepen in Wave 2.
- The design leaves room for labeled transitions and swimlane extensions where CanvasLib needs them.

## Suggested implementation order inside this feature

1. Add adapter and basic node/edge rendering first.
2. Add layout persistence.
3. Add semantic panels including role/template pickers.
4. Add minimal staffing/policy cues.
5. Add richer handoff chrome later when Wave 2 semantics exist.
