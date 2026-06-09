# Source Hotspots To Inspect

## Tests to fix

- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
  - remove all direct reads of `codex/bundles/<bundle-name>`
  - replace with source-backed, stable docs, or `tests/TestData/Architecture` fixtures

## Process runtime

- `repo://src/CanDoItAll.Modules.Processes`
- `repo://src/CanDoItAll.Modules.Processes/Automation`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- process UI/components/pages under `CanDoItAll.Modules.Processes`
- project/project-structure UI surfaces under `CanDoItAll.Modules.Projects` if they own process launch affordances
- app composition/DI in `CanDoItAll.Composition` and `CanDoItAll.Web`

## New read-only driver surfaces

- `repo://src/CanDoItAll.Processes.Drivers.*`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationBatchOrchestrator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationPayloadBuilder.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDomainEvidenceReadOnlyAdapters.cs`

## UI proof

- `repo://tests/CanDoItAll.Tests.Playwright`
- any existing Playwright route smoke tests
- existing app startup/test host helpers
