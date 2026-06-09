# Source Artifacts To Recheck During Implementation

The implementation agent must re-read the current `maf-processes-refactor` branch before editing. Do not trust this bundle as a substitute for live source inspection.

## Current bundle / proof artifacts
- `repo://codex/bundles/process-runtime-restoration-ui-e2e-driver-integration-v1/reviews/01-execution-report.md`
- `repo://codex/bundles/process-runtime-restoration-ui-e2e-driver-integration-v1/proof/SB046/transcripts`
- `repo://codex/bundles/process-runtime-restoration-ui-e2e-driver-integration-v1/proof/SB048`
- `repo://codex/bundles/process-runtime-restoration-ui-e2e-driver-integration-v1/proof/SB054`

## Stable source surfaces
- `repo://src/CanDoItAll.Modules.Processes/README.md`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.TriggerStart.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.*.cs`
- `repo://src/CanDoItAll.Modules.Processes/ProcessAgentRuntimeToolProvider.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components`
- `repo://src/CanDoItAll.Modules.Processes/Api`
- `repo://src/CanDoItAll.Modules.Projects`
- `repo://src/CanDoItAll.Modules.Workbench`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf`
- `repo://src/CanDoItAll.Composition`
- `repo://src/CanDoItAll.Web`

## Tests to inspect and update
- `repo://tests/CanDoItAll.Tests.Integration/ApplicationStartupIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Playwright`
- `repo://tests/CanDoItAll.Tests.Integration`
- `repo://tests/CanDoItAll.Tests.Unit`

## Important rule
Long-lived source and tests must not depend on `codex/bundles/<bundle-name>/...` paths. Use stable test data, direct source scans, stable docs, or generated snapshots under `tests/**/TestData`.
