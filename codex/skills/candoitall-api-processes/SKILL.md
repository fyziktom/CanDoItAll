---
name: candoitall-api-processes
description: Use when managing CanDoItAll process definitions, templates, launch plans, runs, steps, assignments, artifacts, direct messages, and analytics through the HTTP API instead of the removed Processes MCP server.
---

# CanDoItAll Processes API

Use this skill when a task needs process authoring or runtime control through the CanDoItAll web API.

## Access

- Start the CanDoItAll web app and inspect Swagger/OpenAPI at `/swagger`.
- Check `/api/access/status` before assuming bearer tokens are required.
- If JWT is active, send `Authorization: Bearer <token>`.
- Do not reinstall or use `candoitall_processes`; that MCP server has been removed.

## Definition And Template Work

- Definitions: `GET /api/processes/definitions`, `GET /api/processes/definitions/{definitionId}`, `POST /api/processes/definitions`, `POST /api/processes/definitions/{definitionId}/publish`, `DELETE /api/processes/definitions/{definitionId}`.
- Import/export: `GET /api/processes/definitions/{definitionId}/export`, `POST /api/processes/definitions/import`.
- Templates: `GET /api/processes/templates`, `GET /api/processes/templates/{processKey}`, `GET /api/processes/templates/{processKey}/detail`, `GET /api/processes/templates/{processKey}/envelope`, `GET /api/processes/templates/{processKey}/mermaid`, `POST /api/processes/templates/{processKey}/import`.
- Baseline scenarios: `GET /api/processes/templates/baseline-scenarios`.

## Runtime Work

- Runs: `GET /api/processes/runs`, `GET /api/processes/runs/{runId}`, `POST /api/processes/runs/start`, `POST /api/processes/runs/stop`.
- Steps: `GET /api/processes/runs/{runId}/steps`, `GET /api/processes/runs/{runId}/steps/{stepRunId}`, `POST /api/processes/runs/{runId}/steps/{stepRunId}/transition`, `POST /api/processes/runs/{runId}/steps/{stepRunId}/rerun-agent`.
- Artifacts and assignments: use run-scoped and step-scoped artifact/assignment routes so context stays small.
- Manager control: `POST /api/processes/runs/{runId}/manager-directives` and `POST /api/processes/runs/{runId}/direct-messages`.
- Launch and HR matching: `/api/processes/launch-plans`, `/hr-match`, `/submit-approval`, `/approval-decisions`, `/provision`, `/execute`, and `/candidate-selections`.

## Filtering Rules

- Use `definitionId`, `projectId`, `status`, `operatingMode`, `search`, and `take` on run lists.
- Use `stepRunId`, `stepDefinitionId`, `artifactId`, `artifactExpectationId`, `artifactKind`, `roleRequirementId`, `partyId`, `agentId`, `executionState`, `search`, `take`, and include flags on run detail routes.
- For artifact review, prefer `/runs/{runId}/steps/{stepRunId}/artifacts` over full run detail.

## Validation

- After starting or transitioning a run, read back the run and specific step.
- After recording artifacts or assignments, query the step-scoped route.
- For templates, use `/detail` when compatibility notes or sidecar files matter.
