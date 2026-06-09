# SB011 Project-Structure Process Start Proof

## Result
Passed.

## Scope
- Exercised `POST /api/project-structure/projects/{projectId}/nodes/{nodeId}/process/start`.
- Added a focused integration test:
  - `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureAgentApiIntegrationTests.cs`
- No production code was changed for SB011.

## Assertions Proven
- The project-structure API route delegates to `ProjectStructureAgentService.StartProcessNodeAsync`.
- The service creates a project-scoped process launch plan instead of bypassing Process Core.
- The selected project-structure work node is serialized into `ProcessProjectStructureContext`.
- The returned route points to `/projects/{projectId}/processes?processId=...&launchPlanId=...`.
- `/api/processes/launch-plans/{launchPlanId}` readback preserves the same `ProjectId` and parsed selected-node context.

## Validation
- Command:
  `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~ProjectStructureAgentApi_start_process_node_SB011_INV_001_creates_project_scoped_launch_plan_with_bridge_context --logger "trx;LogFileName=SB011-project-structure-process-start.trx" --results-directory codex\bundles\process-runtime-live-e2e-openai-hardening-v1\proof\SB011\test-results`
- Result: passed 1 test, 0 failed, 0 skipped.
- Transcript: `bundle://proof/SB011/transcripts/project-structure-process-start-integration.txt`
- TRX: `bundle://proof/SB011/test-results/SB011-project-structure-process-start.trx`

## Negative Scans
- Source assertions: `bundle://proof/SB011/transcripts/project-structure-process-start-source-assertions.txt`
- Anti-stub/runtime-host drift: `bundle://proof/SB011/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No transient bundle paths: `bundle://proof/SB011/transcripts/no-transient-bundle-path-scan.txt`
- No unexpected UI/media source drift: `bundle://proof/SB011/transcripts/no-unexpected-ui-media-drift-scan.txt`
- Prepared-stage bundle validator after SB011: `bundle://proof/SB011/transcripts/prepared-validator-after-sb011.txt`

## Browser Validation
Not required for SB011. The subbundle objective names the HTTP API endpoint and bridge context, and the focused integration test validates the returned browser route without changing browser-visible UI.
