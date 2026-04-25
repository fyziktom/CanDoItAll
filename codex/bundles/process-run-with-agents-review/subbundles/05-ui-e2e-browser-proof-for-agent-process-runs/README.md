# 05 UI E2E Browser Proof For Agent Process Runs

## Status

- `Implemented and validated`

## Objective

Prove through a browser that a user can launch, observe, inspect, and recover an agent-backed process run from Process Workspace.

## Covered Inputs

- REQ-001: Launch from UI.
- REQ-002: Observe run state from UI.
- REQ-005: Inspect artifacts from UI.
- REQ-006: Missing artifact path from UI.
- REQ-008: Manual rerun path from UI.
- REQ-010: Browser E2E proof.
- REQ-012: Existing component patterns are preserved.

## Prerequisites

- Subbundle 01 is complete.
- Subbundle 02 is complete.
- Subbundle 03 is complete.
- Subbundle 04 is complete.
- Deterministic process mock agents can be enabled in test configuration.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProcessesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProjectProcessesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsLaunchSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsActiveSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsExecutionSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsArtifactsSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentRuntime.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PlaywrightAppFixture.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`

## Deliverables

- Playwright E2E test or test suite that seeds/enables process mock agents, creates or imports the deterministic calculator process, executes through the UI, and observes completion.
- Browser assertions for launch plan creation, approval, execute-ready launch, active run, execution attempt details, artifact ledger, branch outcomes, and completed run.
- Negative browser scenario for at least one recovery path: missing artifact, failed/crashed agent, or dead-lettered dispatch.
- Screenshots or trace artifacts for desktop and at least one narrow/mobile viewport where practical.
- Documentation of how to run the E2E proof locally.

## Dependency Impact

- This is the final closure proof for the review bundle improvements.
- Failure here reopens the relevant implementation subbundle rather than accepting backend-only proof.

## Validation Depth

- Full browser validation with real app host.
- Backend deterministic mock path should be reused for stable setup.
- UI assertions must inspect visible state, not only database records.

## Implementation Steps

1. Add deterministic browser setup for process mock agents and calculator process.
2. Navigate to `/projects/{projectId}/processes` or `/processes`.
3. Create a launch plan through UI, approve it, provision if needed, and execute it.
4. Observe active run progress until settled.
5. Inspect Execution and Evidence tabs for attempts, branch outcomes, artifacts, and health state.
6. Run a negative/recovery scenario and prove the operator can see and act on it.
7. Capture screenshots/traces and update execution report.

## Do Not Do

- Do not replace browser proof with backend service calls only.
- Do not assert hidden database state as the main E2E proof.
- Do not depend on real external LLM providers for deterministic E2E.
- Do not skip mobile/narrow layout checks if new UI surfaces are dense.

## Acceptance Checklist

- UI can launch the deterministic process.
- UI shows active execution and selected run details while work is in progress.
- UI shows final branch outcomes and artifacts.
- UI shows missing/recovery/dead-letter state for the negative scenario.
- Browser proof artifacts are captured and referenced in the execution report.

## Proof Required

- Playwright test result.
- Browser screenshots or traces.
- Focused backend tests still green.
- Execution report updated with routes, viewports, screenshots, and results.

## Closure Proof

- Added Playwright coverage for a seeded agent-backed recovery run at `/processes?processId={id}&runId={id}`.
- Browser assertions cover selected run health, missing artifact evidence, dead-lettered automation, manual rerun, and `agent-step-rerun` outbox visibility.
- Passed `dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~Processes_agent_recovery_run_surfaces_missing_artifact_deadletter_and_manual_rerun" --logger "console;verbosity=normal"` with 1 test.
- Captured `reviews/artifacts/sb12-agent-recovery-artifact-ledger.png`, `reviews/artifacts/sb12-agent-recovery-rerun-outbox.png`, and `reviews/artifacts/sb12-agent-recovery-metadata.md`.

## Browser Validation Logging

- Required.
- Routes: `/processes` and/or `/projects/{projectId}/processes`.
- Viewports: desktop and one narrow/mobile viewport for dense status surfaces.
- Evidence: screenshots/traces showing launch, active execution, execution details, evidence/artifact ledger, and recovery state.

## Progression Gate

- The review bundle implementation can close only after this browser proof passes.

## Suggested Agent Prompt

```text
Implement subbundle 05 only after subbundles 01-04 are complete. Add Playwright browser proof for launching, observing, inspecting, and recovering a deterministic agent-backed process run from Process Workspace. Use mock agents, not real provider calls, for deterministic E2E.
```
