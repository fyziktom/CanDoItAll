# Current State

## Source Inventory Snapshot

- CodeAnalytics snapshot `snap-20260530233954-854cccd0` inspected 10 source projects and 728 documents with no blocking errors.
- Generated route inventory found 311 total `/api` routes including `/api/access` and Cognitive Memory v1 aliases.
- Focused control-plane count is 271 routes after excluding `/api/access` and Cognitive Memory v1 aliases.
- Surface counts from source:
  - `agents`: 57 routes.
  - `workflows`: 37 routes.
  - `processes`: 58 routes.
  - `project-structure`: 51 routes.
  - `cognitive-memory`: 38 legacy routes and 38 v1 alias routes.
  - `plugins`: 20 routes.
  - `projects`: 10 routes.
  - `access`: 2 routes.
- The route workbook is `bundle://inventories/api-docs-skills-gap-map.xlsx`; the rendered summary proof is `bundle://inventories/api-docs-skills-gap-map-summary.png`.

## API Contract And Test State

- `tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs` includes a focused OpenAPI route test but does not assert Cognitive Memory `/contract`, `/projections/rebuild`, `/automation/run`, `/retention/cleanup`, or the v1 aliases.
- Cognitive Memory has an internal contract route and route template list in `src/CanDoItAll.Web/Api/CognitiveMemoryApi.ContractEndpoints.cs`.
- The code exposes both `/api/cognitive-memory` and `/api/cognitive-memory/v1`; the docs and skills do not consistently treat v1 as the preferred base for new integrations.

## Docs State

- `docs/api-control-plane.md` lists Project Structure, Processes, and Agents skills in the development workflow but omits Workflows and Cognitive Memory.
- `docs/cognitive-memory/operations/api.md` states 35 routes per surface while source exposes 38.
- `docs/process-agent-operator-runbook.md` is too light for current run/detail DTOs, recovery options, freshness/profile semantics, and projection lineage.
- `docs/agent-runtime-hardening-verification.md` is a dated proof record from 2026-04-27 and needs clear historical framing if left in place.

## Skills State

- Repo skill copies and active local skill copies currently hash-match before edits.
- `candoitall-api-agents` is too high-level for current teams, providers, capabilities, execution-run filters, approvals, metrics, and runtime snapshot routes.
- `candoitall-api-workflows` mostly tracks routes but lacks precise DTO fields for source process linkage, paging, event pages, and artifact content.
- `candoitall-api-processes` is rich but needs an exact 58-route appendix and current DTO field map.
- `candoitall-api-project-structure` is under-specified for the 51-route API surface.
- `candoitall-api-cognitive-memory` omits or under-specifies v1/contract/database-transfer routes and advanced DTO groups.

## Agent Tool State

- `MafAgentRuntime.ProcessTools.cs` exposes 23 process tools versus 58 HTTP process routes.
- `MafAgentRuntime.ProjectStructureTools.cs` exposes 28 project-structure tools versus 51 HTTP routes.
- Missing tool areas include process launch plans, escalations, operator approvals, manager directives, direct messages, template profile routes, scoped artifacts/assignments, node metadata/status/progress/markers/priority, process/workflow node operations, asset content, and lease renew.

## Provider And DTO State

- Provider models now include private provider pricing, tags, native hosted tools, local MCP, image generation, structured output, vision, compaction, and model parameter policies including reasoning effort.
- Key DTOs needing docs/skill coverage are embedded in `AgentsApi.cs`, `WorkflowsApi.cs`, and `ProcessesApi.cs` for those surfaces, plus `CognitiveMemoryApiDtos.cs` for Cognitive Memory. Important types include `AgentExecutionRunStartApiRequest`, `AgentExecutionRunApiQuery`, `WorkflowRunStartApiRequest`, `WorkflowRunListApiQuery`, `WorkflowEventListApiQuery`, `ProcessRunListApiQuery`, `ProcessRunDetailApiQuery`, `ProcessArtifactRecordApiRequest`, and Cognitive Memory settings/projection/automation/retention request DTOs.
