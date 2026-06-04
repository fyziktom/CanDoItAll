# Normalized Requirements

| ID | Requirement | Success criteria | Owner |
| --- | --- | --- | --- |
| RQ-001 | Preserve a source-of-truth API, DTO, docs, skills, and tool parity inventory. | `inventories/api-docs-skills-gap-map.xlsx` contains source route counts, endpoint inventory, gap map, DTO map, docs/skills status, tool parity, plan, and validation sheets; regenerated output matches source. | SB01 |
| RQ-002 | Repair HTTP API contract and focused test coverage for current route exposure. | Focused OpenAPI/API contract tests assert current high-risk routes, especially Cognitive Memory contract/operations/v1 aliases; any source/OpenAPI mismatch is fixed or documented. | SB02 |
| RQ-003 | Decide and repair agent runtime tool parity for process and project-structure operations. | Missing tools are implemented with strongly typed requests, policy constants, approval behavior, and tests, or explicitly marked as HTTP-only fallback in docs/skills. | SB03 |
| RQ-004 | Refresh docs to match current APIs, provider capabilities, DTOs, and historical proof status. | `docs/api-control-plane.md`, Cognitive Memory API docs, process operator runbook, provider-related docs, and historical proof docs reflect current source and route counts. | SB04 |
| RQ-005 | Refresh repo-managed API skills and active local skill copies. | Agents, Workflows, Processes, Project Structure, and Cognitive Memory API skills contain route/DTO/examples matching source; active skill root hashes match repo copies after edits. | SB05 |
| RQ-006 | Add drift guardrails for route/docs/skills/API coverage. | A focused script or test fails when high-priority source route changes are not reflected in OpenAPI/docs/skills coverage expectations. | SB06 |
| RQ-007 | Close the initiative with durable proof and no weak handoff state. | Execution report contains commands, proof paths, gate results, raw note closure, residual risks; completed-stage validator passes. | SB07 |

## Non-Goals

- Do not refactor unrelated API services or UI components while doing docs/skills parity work.
- Do not create broad abstractions solely for documentation generation unless SB06 proves they reduce drift meaningfully.
- Do not treat exact route text coverage as proof of good docs; it is only a guardrail signal.
