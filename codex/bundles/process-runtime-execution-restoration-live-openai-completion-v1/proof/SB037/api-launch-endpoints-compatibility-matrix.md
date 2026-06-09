# SB037 API Launch Endpoints Compatibility Matrix

## Status
Completed.

## Objective
Prove launch API compatibility across direct process runs, launch-plan execution, and project-structure process start routes.

## Compatibility Matrix
| Launch surface | Source/API | Proof test | Expected result |
| --- | --- | --- | --- |
| Direct service run start | `ProcessesService.StartRunAsync(ProcessRunStartRequest)` | `StartRunAsync_SB018_INV_001_persists_project_context_runtime_rows_and_dispatch_outbox` | Persists project context, run/step/work-brief rows, journal, and start/dispatch outbox records |
| Launch-plan execution | `ProcessesService.ExecuteLaunchPlanAsync(ProcessLaunchExecutionRequest)` -> `StartRunAsync` with `LaunchPlanId` | `StartRunAsync_SB018_INV_002_rejects_invalid_not_ready_and_duplicate_start_attempts` | Rejects not-ready plans, executes approved/provisioned plan once, rejects duplicate execution |
| Project-structure launch plan | `/api/project-structure/projects/{projectId}/nodes/{nodeId}/process/start` with `IncludeLaunchPlan=true`, `Execute=false` | `ProjectStructureAgentApi_start_process_node_SB011_INV_001_creates_project_scoped_launch_plan_with_bridge_context` | Returns `launch-plan-ready`, launch route with `launchPlanId`, persisted project/node bridge context |
| Project-structure execute | `/api/project-structure/projects/{projectId}/nodes/{nodeId}/process/start` with `IncludeLaunchPlan=true`, `Execute=true` | `ProjectStructureAgentApi_execute_process_node_SB012_INV_001_preserves_run_context_and_projects_output_folder` | Returns `run-started`, route with `runId`, persisted project/node run context, output folder projection |
| Process workspace launch selection | `ProcessWorkspace.Launch` query/selection path | Source assertions against `LaunchPlanIdQuery`, `CreateLaunchPlanAsync`, and `ExecuteLaunchPlanAsync` | Workspace resolves selected launch plan by query/id and calls typed service methods |

## Validation
- Focused transcript: `bundle://proof/SB039/transcripts/launch-api-compatibility-tests.txt`
- TRX: `bundle://proof/SB039/SB039-launch-api-compatibility.trx`
- Source assertions: `bundle://proof/SB039/transcripts/source-assertions.txt`

## Closure
SB037 is closed by the passing Gate M integration slice and source assertions. No browser proof is required because no browser-visible behavior changed in this subbundle.
