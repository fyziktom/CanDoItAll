# architecture-api-doc-refresh

## Status

- `Completed`

## Objective

- Correct the current technical documentation around architecture, APIs, and suppressed MCPs.

## Success Criteria

- `docs/architecture-beta.md` no longer contains a Mermaid `architecture-beta` block.
- README/docs index identify the HTTP API and API skills as the current process/project/agent automation path.
- Stale Processes and ProjectStructure MCP setup pages are replaced with retired/suppressed transition guidance.
- A technical API control-plane doc exists and links to source-backed endpoint families.

## Covered Inputs

- `REQ-001`
- `REQ-002`
- `REQ-003`

## Prerequisites

- Prepared bundle validator passes.
- No earlier subbundle prerequisites.

## Exact Source References

- C:/repositories/CanDoItAll/README.md
- C:/repositories/CanDoItAll/docs/README.md
- C:/repositories/CanDoItAll/docs/architecture-beta.md
- C:/repositories/CanDoItAll/docs/processes-mcp-setup.md
- C:/repositories/CanDoItAll/docs/project-structure-mcp-setup.md
- C:/repositories/CanDoItAll/src/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Web/Api/ProcessesApi.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Web/Api/ProjectsApi.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Web/Api/AgentsApi.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Web/ProjectStructureAgentApi.cs

## Deliverables

- Updated root README and docs index.
- Updated `docs/architecture-beta.md`.
- New or updated technical API documentation.
- Retired/suppressed transition pages for old Processes and ProjectStructure MCP setup docs.

## Dependency Impact

- Customer-facing docs depend on this phase for accurate API/MCP/architecture statements.
- Validation depends on this phase to remove stale active setup claims.

## Validation Depth

- Critical documentation foundation.

## Implementation Steps

1. Replace the fragile Architecture Beta diagram with safe Mermaid flowchart syntax.
2. Update architecture wording to match active API and MCP boundaries.
3. Add an API control-plane doc covering access, route families, advanced settings, and developer validation.
4. Replace old Processes and ProjectStructure MCP setup pages with transition guidance.
5. Update README and docs index links.

## Scope Exceptions

- Infographic generation and customer-facing narrative are owned by subbundle 02.

## Do Not Do

- Do not modify runtime code.
- Do not reintroduce removed MCP setup commands.
- Do not claim `CanDoItAll.Economy` is implemented in this repository.

## Acceptance Checklist

- `docs/architecture-beta.md` contains no `architecture-beta` code fence.
- `docs/processes-mcp-setup.md` and `docs/project-structure-mcp-setup.md` are transition docs, not active install docs.
- API docs cite `/api/access/status`, `/api/processes`, `/api/projects`, `/api/agents`, and `/api/project-structure`.
- README/docs index route users to API control-plane docs and current skill pack guidance.

## Proof Required

- File inspection for changed docs.
- Search for removed MCP active setup claims.
- `git diff --check` eventually passes in subbundle 03.

## Browser Validation Logging

- N/A: documentation-only changes with no browser-visible app behavior change.

## Progression Gate

- Downstream work may continue only after the stale MCP claims are corrected and Architecture Beta no longer uses failing Mermaid syntax.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
