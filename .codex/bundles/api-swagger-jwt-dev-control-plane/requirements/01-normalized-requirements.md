# Normalized Requirements

| Id | Requirement | Source | Validation |
| --- | --- | --- | --- |
| R-001 | The web host exposes OpenAPI/Swagger metadata for a documented API surface. | N001 | Build plus HTTP/openapi smoke. |
| R-002 | JWT bearer authorization is optional from `appsettings.json`, disabled by default, and applied to API groups only when enabled. | N001, N008 | Options unit tests and integration tests for enabled/disabled modes. |
| R-003 | Enabling JWT without a sufficiently strong signing key fails predictably during startup/configuration. | N008 | Unit test for validation failure. |
| R-004 | Settings UI has an API access section showing whether JWT is active and generating bearer tokens only when active. | N008 | Component/UI proof and token issuer test. |
| R-005 | Project endpoints reuse `ProjectsService` for list, get editor, save, delete, and hierarchy operations. | N001, N002 | Integration/API tests and source review. |
| R-006 | Project-structure endpoints continue to reuse `ProjectStructureAgentService`, leases, analytics, import, checklist, dependency, asset, node mutation, subtree transfer, and process-node launch logic. | N001, N002, N006 | Existing ProjectStructureAgentApi tests plus focused command integration test and auth/OpenAPI exposure. |
| R-007 | Process endpoints reuse `ProcessesService` for definitions, publication, import/export, runs, transitions, reruns, assignments, artifacts, direct messaging, manager directives, launch plans, HR matching, approval, provisioning, execution, and path-scoped runtime commands. | N001, N002, N005, N006 | Integration tests around process list/detail/filter, focused artifact routes, and source review. |
| R-008 | Process run detail supports filters and focused step/artifact routes for step runs, artifacts, assignments, work briefs, decisions, conformance observations, and execution runs so clients can avoid full context overload. | N007 | Filtered detail and step-scoped artifact tests. |
| R-009 | Agent endpoints reuse `IAgentFrameworkWorkspaceService` for catalog, editor, save/delete/clone, chat sessions, execution runs, execution artifacts, agent-scoped logs, metrics, approvals, checkpoints, tool receipts, and chat/probe flows. | N001, N002 | OpenAPI route proof, build, and source review. |
| R-010 | The bundle includes an xlsx user-story coverage workbook used during implementation review. | N004 | Workbook exists and maps stories to requirements/subbundles/status. |
| R-011 | Architecture review is performed after the API surface and again before closure, with repair subbundles added if the shared-service direction drifts. | N009 | Execution report architecture review rows. |
