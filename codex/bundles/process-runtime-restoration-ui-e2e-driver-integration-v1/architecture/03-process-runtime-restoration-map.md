# Process Runtime Restoration Map

## Surfaces to inventory

- Process templates / template catalog service.
- Project or project-structure UI entry points that allow starting processes.
- Process run creation service/API.
- Dispatch service and its background or manual trigger path.
- MAF workflow/direct-agent executor integration.
- Artifact projection, finalization, process/run/step status updates.
- Existing test/fake provider configuration for deterministic E2E.

## Required scenario matrix

| Scenario | UI/API entry | Runtime proof | Artifact proof |
| --- | --- | --- | --- |
| `.NET app create` | Project context start process | run creates steps, dispatch executes, finalizer closes | generated/updated files or artifact records |
| `.NET app modify` | Existing project structure | run modifies target or produces patch artifact | changed file/artifact evidence |
| Business analysis | Project/process template | run produces analysis artifact | business-analysis output/evidence artifact |

## UI proof rule

Use large desktop viewport only. The test must show:
- route loads,
- project/process entry is visible,
- template is selectable,
- process run is started,
- status/run id is visible or navigable.
