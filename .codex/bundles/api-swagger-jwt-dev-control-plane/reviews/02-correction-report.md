# Correction Report

## Trigger

The user accepted the direction but rejected the bundle as incomplete on 2026-05-05.

## Required Corrections

- Remove `Development` from API code names, route operation names, Settings labels, and configuration names introduced by this bundle where reasonable.
- Expand the API command surface for project-structure node editing and execution: node type changes, reconnect/reparent, dependency add/remove, marker/priority/progress changes, process-node runs, subtree movement, and asset attachment access.
- Expand process and agent APIs with focused command/filter endpoints so callers do not need to fetch or send whole objects for common work.
- Keep endpoint handlers thin and reuse existing UI/MCP/agent services or shared helpers instead of duplicating logic.

## Repair Decision

The bundle was reopened. New subbundles 05-08 cover naming compaction, project-structure command expansion, process/agent command expansion, and reclosure proof.

## Completion Proof

- Introduced API source no longer contains the rejected old API names or routes.
- Project-structure focused routes were added for node type/status/progress/marker/priority, reparenting, link/dependency mutation, process-node execution, subtree transfer, and asset create/content retrieval.
- Process focused routes were added for run-step reads, step artifacts, single artifacts, manager directives, direct messages, transitions, reruns, assignment resolution, and artifact recording.
- Agent focused routes were added for agent-scoped execution start/list/detail and run artifacts, log, metrics, approvals, checkpoints, and tool receipts.
- `requirements/user-stories.xlsx` was regenerated with user-story and API-command coverage.
- `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -v:minimal` passed.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ApiIntegrationTests|FullyQualifiedName~ProjectStructureAgentApiIntegrationTests" -v:minimal` passed with 9 tests.
- Completed-stage bundle validator passed.
