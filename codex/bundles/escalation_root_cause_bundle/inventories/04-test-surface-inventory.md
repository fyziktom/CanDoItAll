# Test Surface Inventory

## Unit Test Targets

- `repo://tests/Unit/CanDoItAll.Tests.Unit/DotNetProcessLaunchVariableContributorTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeToolPreflightServiceTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessMafHardeningRegressionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessTemplateRuntimeWritebackTextTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRequiredRuntimeToolNamesTests.cs`

## Integration Test Targets

- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProcessApiIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureWorkflowScenarioHarnessTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs`

## Manual Test Targets

- `bundle://tests/manual-process-validation.md`
- `bundle://tests/regression-test-matrix.md`

## Required Negative Fixtures

- Empty `.slnx` with generated project folder and missing helper receipt.
- Launch variables with unresolved `{CurrentProcessRunId}` in script refs.
- Parent subprocess child with physical output file but no accepted artifact slot.
- Safe/idempotent diagnostic repeated beyond retry budget.
- Template prose containing a hard required receipt without typed metadata.
