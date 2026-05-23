# SB05: API-Backed Demo Backup And Rerun

## Status

- Status: `Completed`
- Critical foundation: `Yes`

## Scope

- Prepare a clean, rerunnable demo.
- Back up existing project-structure data and assets.
- Seed basic info through APIs only.

## Objective

Make the live demo rerunnable from basic project-structure information without direct DB mutation.

## Covered Inputs

- Follow-up request `03-live-blazor-delivery-request`
- `R010`
- `R011`

## Prerequisites

- Runtime API host running current binaries.
- PostgreSQL development DB reachable.
- SB03/SB04 readiness work complete enough to import/link processes.

## Exact Source References

- `repo://src/CanDoItAll.Web/ProjectStructureAgentApi.cs`
- `repo://src/CanDoItAll.Web/Api/ProjectsApi.cs`
- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.SettingsEndpoints.cs`

## Dependency Impact

- API data only.
- Backup files under the requested output root.

## Validation Depth

- API transcript for runtime status, cognitive-memory settings, project backup, and seed operations.
- Backup manifest with project, node, asset, and output paths.

## Contract

- Start a current app host on PostgreSQL.
- Use API status endpoints to prove PostgreSQL is active.
- Disable cognitive memory through `/api/cognitive-memory/settings`.
- Back up existing project records, selected project-structure subtree, linked process definitions/runs, and assets through APIs.
- Store backup and run outputs under `C:\programovani\dotnet-demo\output`.
- Seed only basic project-structure information required for agents to build the app.
- Link/import process definitions through API endpoints.

## Implementation Steps

- Start or verify a current host.
- Confirm PostgreSQL through API diagnostics.
- Disable cognitive memory with the settings API.
- Back up current project and selected project structure including assets.
- Seed basic information into a clean rerun area through API endpoints.
- Link/import selected process definitions through API endpoints.

## Do Not Do

- Do not mutate project-structure DB tables directly.
- Do not load demo data through tests.
- Do not edit generated app output.

## Acceptance Checklist

- [x] Backup manifest lists API endpoints, project id, node ids, asset ids, and output paths.
- [x] Clean basic-info seed can be rerun from the backup.
- [x] Cognitive memory is disabled.
- [x] PostgreSQL profile is active.

## Proof Required

- `bundle://proof/SB05/manifest.md`
- `bundle://proof/SB05/backups/**`
- `bundle://proof/SB05/transcripts/api-backup-and-seed.txt`

## Browser Validation Logging

- Not applicable. Browser validation happens in SB07.

## Progression Gate

- SB05 passes when the demo can be started from backed-up basic information through API records.

## Suggested Agent Prompt

Use `bundle://shared-prompts/implementation-prompt.md`.
