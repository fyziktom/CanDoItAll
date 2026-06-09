# SB011 Proof Manifest

## Status
Passed.

## Changed Files
| Path | SHA256 | Notes |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureAgentApiIntegrationTests.cs` | `7E4A87E8E6AAD52BAB57AEFCD785D4C97C0B609A674F0B73EE1E4806328314F5` | Adds focused endpoint proof for project-structure process start context. |

## Commands
| Command | Result | Transcript |
| --- | --- | --- |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~ProjectStructureAgentApi_start_process_node_SB011_INV_001_creates_project_scoped_launch_plan_with_bridge_context --logger "trx;LogFileName=SB011-project-structure-process-start.trx" --results-directory codex\bundles\process-runtime-live-e2e-openai-hardening-v1\proof\SB011\test-results` | Passed 1 test | `bundle://proof/SB011/transcripts/project-structure-process-start-integration.txt` |

## Proof Artifacts
| Artifact | SHA256 | Bytes |
| --- | --- | ---: |
| `bundle://proof/SB011/project-structure-process-start-proof.md` | `7AA8BD770B727B0514F96860B97EAEDC002C79989989073A2E40C169CFBD8513` | 2317 |
| `bundle://proof/SB011/transcripts/project-structure-process-start-integration.txt` | `FC2E8E5EC18364D7456B76ED7B515918D911EF5A4A4DDCA50B3EA8724AD93157` | 9204 |
| `bundle://proof/SB011/test-results/SB011-project-structure-process-start.trx` | `8E7C6AB944F068A8A2A0E6A8CBA989B7424AA4FC98501E2726F13A31C8246E56` | 181205 |
| `bundle://proof/SB011/transcripts/project-structure-process-start-source-assertions.txt` | `9949391896148B98CB5D18231070BE25FD24C23091B654F1EE4D118F26BA0E74` | 3276 |
| `bundle://proof/SB011/transcripts/anti-stub-and-runtime-host-drift-scan.txt` | `66E4FD746B5B2FB6EAC081504E23D691E0F1DE036B7DF45072B8115D7709DDC5` | 340 |
| `bundle://proof/SB011/transcripts/no-transient-bundle-path-scan.txt` | `EF06C43B37AA46DA9A028A4E77EE4ECD8363D0B9D9B56DC6EFFA84014C56AD64` | 298 |
| `bundle://proof/SB011/transcripts/no-unexpected-ui-media-drift-scan.txt` | `627467DB689C5A9D496BC6E13CA24EBF3BFA76AB7A2A84CF394DE6E6CDB49EB8` | 1635 |
| `bundle://proof/SB011/transcripts/prepared-validator-after-sb011.txt` | `ABE977F36135D7C5B6086B2C468B2F8C8BB0B1A21494A91E59F706E073015672` | 125 |

## Source Assertions
- `repo://src/CanDoItAll.Web/ProjectStructureAgentApi.cs` maps `POST /api/project-structure/projects/{projectId}/nodes/{nodeId}/process/start`.
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentService.cs` delegates to `ProjectStructureProcessNodeService`.
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessNodeService.cs` creates `ProcessLaunchCreateRequest` with `ProjectStructureContext = startContext` and returns the project process workspace route with `launchPlanId`.
- `repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.Planning.cs` serializes context into `TriggerReason`.
- `repo://src/CanDoItAll.Modules.Processes/ProjectStructure/ProcessProjectStructureContext.cs` provides target-node resolution used by the bridge.

## Production Behavior Artifact Matrix
| Behavior | Producer | Consumer | Lifecycle | Evidence |
| --- | --- | --- | --- | --- |
| Project-structure process start endpoint creates launch plan | `ProjectStructureAgentApi` and `ProjectStructureProcessNodeService` | `ProcessesService.CreateLaunchPlanAsync` and process launch-plan API | API starts from selected project node, creates launch plan, returns project process workspace route | `project-structure-process-start-integration.txt`, `project-structure-process-start-source-assertions.txt` |
| Bridge context survives persistence | `ProcessProjectStructureContextFormatter.AppendToTriggerReason` | `ProcessProjectStructureContextFormatter.TryParse` and downstream process grounding | Selected work node is serialized into launch-plan trigger reason and parsed from process API readback | `project-structure-process-start-integration.txt` |
