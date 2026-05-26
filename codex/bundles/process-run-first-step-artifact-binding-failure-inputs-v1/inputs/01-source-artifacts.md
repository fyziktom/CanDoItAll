# Source Artifacts

All files in this table were captured from the running local app at `http://localhost:5032` on 2026-05-26.

| ID | Path | Source API | Notes |
| --- | --- | --- | --- |
| API-000 | `inputs/api-evidence/00-access-status.json` | `GET /api/access/status` | API enabled, OpenAPI enabled, authorization disabled. |
| API-001 | `inputs/api-evidence/01-dev-runtime.json` | `GET /_dev/runtime` | Runtime profile context. |
| API-002 | `inputs/api-evidence/02-dev-database-selection.json` | `GET /_dev/database/selection` | Active database selection. |
| API-003 | `inputs/api-evidence/03-openapi-full.json` | `GET /swagger/v1/swagger.json` | Full OpenAPI snapshot. |
| API-004 | `inputs/api-evidence/04-openapi-relevant-path-index.json` | Derived from OpenAPI | Relevant process, agent, project-structure, storage, and dev routes. |
| API-010 | `inputs/api-evidence/10-runs-recent.json` | `GET /api/processes/runs?take=50` | Recent run list used to identify the failed run. |
| API-011 | `inputs/api-evidence/11-run-detail-full.json` | `GET /api/processes/runs/{runId}` | Full process run detail with include flags and `take=500`. |
| API-012 | `inputs/api-evidence/12-run-steps.json` | `GET /api/processes/runs/{runId}/steps` | All step runs. |
| API-013 | `inputs/api-evidence/13-step-00-detail.json` | `GET /api/processes/runs/{runId}/steps/{stepRunId}` | Focused failed step detail. |
| API-014 | `inputs/api-evidence/14-run-artifacts.json` | `GET /api/processes/runs/{runId}/artifacts` | Run artifacts. |
| API-015 | `inputs/api-evidence/15-step-00-artifacts.json` | `GET /api/processes/runs/{runId}/steps/{stepRunId}/artifacts` | Failed step artifacts. |
| API-016 | `inputs/api-evidence/16-artifact-delivery-contract-detail.json` | `GET /api/processes/runs/{runId}/artifacts/{artifactId}` | Delivery contract artifact record. |
| API-017 | `inputs/api-evidence/17-run-assignments.json` | `GET /api/processes/runs/{runId}/assignments` | Runtime assignments. |
| API-018 | `inputs/api-evidence/18-step-00-assignments.json` | `GET /api/processes/runs/{runId}/steps/{stepRunId}/assignments` | Empty array for failed step scoped assignment query. |
| API-019 | `inputs/api-evidence/19-run-escalations.json` | `GET /api/processes/runs/{runId}/escalations` | Failed-step escalation. |
| API-020 | `inputs/api-evidence/20-process-definition-detail.json` | `GET /api/processes/definitions/{definitionId}` | Published process definition detail and lint result. |
| API-021 | `inputs/api-evidence/21-process-definition-export.json` | `GET /api/processes/definitions/{definitionId}/export` | Definition export snapshot. |
| API-022 | `inputs/api-evidence/22-process-launch-plans-definition-project.json` | `GET /api/processes/launch-plans?...` | Launch-plan context for the definition and project. |
| API-023 | `inputs/api-evidence/23-process-templates-list.json` | `GET /api/processes/templates` | Template catalog. |
| API-024 | `inputs/api-evidence/24-template-blazor-app-delivery-detail.json` | `GET /api/processes/templates/blazor-app-delivery/detail` | Template detail for comparison with persisted definition. |
| API-025 | `inputs/api-evidence/25-template-blazor-app-delivery-mermaid.mmd` | `GET /api/processes/templates/blazor-app-delivery/mermaid` | Template graph. |
| API-026 | `inputs/api-evidence/26-process-analytics.json` | `GET /api/processes/analytics` | Process analytics snapshot. |
| API-030 | `inputs/api-evidence/30-agent-execution-runs-for-process.json` | `GET /api/agents/execution-runs?processRunId={runId}` | Agent execution runs tied to this process run. |
| API-031A | `inputs/api-evidence/31-agent-execution-run-91e6a078-ac63-43e6-9901-6f8364539c42-detail.json` | `GET /api/agents/execution-runs/{executionRunId}` | Step 0 agent execution detail. |
| API-031B | `inputs/api-evidence/31-agent-execution-run-d38da822-a980-44ce-952b-6e86c0b74bbb-detail.json` | `GET /api/agents/execution-runs/{executionRunId}` | Later manager-chat execution detail. |
| API-032A-B | `inputs/api-evidence/32-agent-execution-run-*-artifacts.json` | `GET /api/agents/execution-runs/{executionRunId}/artifacts` | Empty arrays for both captured execution runs. |
| API-033A-B | `inputs/api-evidence/33-agent-execution-run-*-checkpoints.json` | `GET /api/agents/execution-runs/{executionRunId}/checkpoints` | Step run has none; manager-chat run has one checkpoint. |
| API-034A-B | `inputs/api-evidence/34-agent-execution-run-*-tool-receipts.json` | `GET /api/agents/execution-runs/{executionRunId}/tool-receipts` | Step run has one workspace write receipt; manager-chat run has none. |
| API-035A-B | `inputs/api-evidence/35-agent-execution-run-*-log.json` | `GET /api/agents/{agentId}/execution-runs/{executionRunId}/log` | Execution logs. |
| API-036A-B | `inputs/api-evidence/36-agent-execution-run-*-metrics.json` | `GET /api/agents/{agentId}/execution-runs/{executionRunId}/metrics` | Execution metrics. |
| API-037A-B | `inputs/api-evidence/37-agent-execution-run-*-approvals.json` | `GET /api/agents/{agentId}/execution-runs/{executionRunId}/approvals` | Step run has none; manager-chat run has one pending approval. |
| API-040 | `inputs/api-evidence/40-project-structure-projects.json` | `GET /api/project-structure/projects` | Project list. |
| API-041 | `inputs/api-evidence/41-project-structure-hierarchy.json` | `GET /api/project-structure/projects/{projectId}/hierarchy` | Project hierarchy. |
| API-042 | `inputs/api-evidence/42-project-structure-read-selected-nodes.json` | `POST /api/project-structure/projects/{projectId}/structure/read` | Nodes referenced by the step output. |
| API-043 | `inputs/api-evidence/43-project-structure-read-full-project.json` | `POST /api/project-structure/projects/{projectId}/structure/read` | Full project read, capped at `take=500`. |
