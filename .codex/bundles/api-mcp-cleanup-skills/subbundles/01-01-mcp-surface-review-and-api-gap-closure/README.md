# 01 MCP Surface Review And API Gap Closure

## Status

- `Completed`

## Objective

Preserve ProjectStructure and Processes MCP operating guidance and close API parity gaps before deleting the MCP projects.

## Covered Inputs

- Original request item 1.
- R-001 and R-002.

## Prerequisites

- Prepared bundle exists.

## Exact Source References

- C:\repositories\CanDoItAll\.codex\bundles\api-mcp-cleanup-skills\analysis\01-current-state.md
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs

## Removed Source Inputs Reviewed Before Deletion

- `src/CanDoItAll.Mcp.ProjectStructure/ProjectStructureTools.cs`
- `src/CanDoItAll.Mcp.Processes/ProcessesTools.cs`

## Deliverables

- API parity notes retained in bundle.
- Process template baseline scenario endpoint.
- Process template detail endpoint with compatibility/supporting file payload.
- Project-structure API route no longer advertised as MCP-facing.

## Dependency Impact

- Skills depend on the final route names and preserved guidance.
- Removal subbundle depends on parity review completion.

## Validation Depth

- Source review and targeted integration/API test or build proof.

## Implementation Steps

1. Update `ProcessesApi` with missing template endpoints using existing template services.
2. Adjust project-structure API route naming and tests without duplicating service logic.
3. Record any uncovered MCP-only behavior as a blocker before deletion.

## Do Not Do

- Do not recreate MCP coordinators inside API handlers.
- Do not remove domain services.

## Acceptance Checklist

- Baseline scenario list is API-accessible.
- Template detail includes compatibility notes and supporting files.
- Bundle records preserved MCP guidance.

## Proof Required

- Source search or targeted tests confirm new endpoints/routes.
- Execution report records route and API parity proof.

## Closure Proof

- Added `GET /api/processes/templates/baseline-scenarios`.
- Added `GET /api/processes/templates/{processKey}/detail`.
- Renamed project-structure API route from `/api/project-structure-mcp` to `/api/project-structure`.
- Updated API integration expectations; focused integration tests passed in `op_0cbc98281de54034b3969c41253b7196`.

## Browser Validation Logging

- No browser proof required unless Swagger UI is launched for visual route inspection.

## Progression Gate

- Downstream deletion can proceed only after MCP parity gaps are closed or explicitly blocked.

## Suggested Agent Prompt

Use existing process template services to add missing API endpoints and update project-structure route naming. Keep the diff small and update tests that assert old route names.
