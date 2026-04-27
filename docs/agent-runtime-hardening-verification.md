# Agent runtime hardening verification

Captured: 2026-04-27T09:14:00-04:00

Working directory: `C:\repositories\CanDoItAll`

## Environment

`dotnet --info`

- SDK: 10.0.203
- Host: 10.0.7
- MSBuild: 18.3.3
- OS: Windows 10.0.26200, win-x64
- `global.json`: `C:\repositories\CanDoItAll\global.json`

## Build

`dotnet build CanDoItAll.slnx --configuration Release --no-restore`

Result: passed with 0 errors and 64 warnings.

Observed existing warning groups:

- NU1904 for `Microsoft.AspNetCore.DataProtection` 10.0.6 critical advisory `GHSA-9mv3-2cwr-p262`.
- NU1902 for `OpenTelemetry.Api` 1.13.1 moderate advisory `GHSA-g94r-2vxg-569j`.
- NU1510 prune warnings for `CanDoItAll.Mcp.DotNetWatch`.
- Existing analyzer warnings in component/integration tests and nullable warnings in process persistence/provisioning code.

## Focused Unit Tests

`dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~AgentFinalizerPolicyTests|FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~ProviderFeatureMatrixTests|FullyQualifiedName~AgentRuntimeHardeningStaticRegressionTests|FullyQualifiedName~AgentOutputContractTests"`

Result: passed. Discovered and executed 56 matching tests: 56 passed, 0 failed, 0 skipped.

Covered test classes:

- `AgentFinalizerPolicyTests`
- `AgentToolInvocationPolicyTests`
- `ProviderFeatureMatrixTests`
- `AgentRuntimeHardeningStaticRegressionTests`
- `AgentOutputContractTests`

## Focused Integration Tests

`dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --filter "FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests|FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests|FullyQualifiedName~MafAgentRuntimeTests"`

Result: passed. Discovered and executed 35 matching tests: 35 passed, 0 failed, 0 skipped.

Covered test classes:

- `AgentFrameworkExecutionRunTrackingIntegrationTests`
- `ProcessMockAgentRuntimeIntegrationTests`
- `MafAgentRuntimeTests`

## Additional Unit Guardrail

`dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --no-build`

First result: 216 passed, 1 failed. The failed test was `LocalWorkspaceProcessHostTests.ExecuteAsync_returns_after_parent_exit_when_descendant_keeps_redirected_pipe_open`, which exceeded its six-second timing assertion under load.

`dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~LocalWorkspaceProcessHostTests.ExecuteAsync_returns_after_parent_exit_when_descendant_keeps_redirected_pipe_open"`

Result: passed, 1/1.

`dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --no-build`

Final result: passed. Discovered and executed 217 tests: 217 passed, 0 failed, 0 skipped.

## Implementation-Time Failures Fixed

The first Release build failed with `CS1501` in `ProcessRunAutomationDispatchService.ToolValidation.cs` after the process outcome parsing signature changed. The caller was corrected to use a candidate-aware overload that preserves branch-outcome id resolution.

An initial focused integration run failed in `Process_mock_calculator_process_completes_end_to_end_through_durable_outbox_dispatch` because the stale parse path validated the branch key without resolving it to a selected branch outcome id. The candidate-aware parse path fixed this, and the final focused integration command above passed.

An initial unit assertion for extraction repair expected a semantic `reason_required` validation error for a missing required DTO member. The serializer correctly reports that shape as `agent.output.malformed_json` before semantic validation can run. The test now asserts the actual contract boundary: extraction succeeds, does not invent `reason`, and validation still fails.

## Repo-Wide Test Status

A repo-wide `dotnet test CanDoItAll.slnx --configuration Release --no-build` run was not executed in this pass. The validated scope for this bundle is the full Release build plus the focused hardening unit and integration filters above.

## Closure Notes

The bundle-surface proof is green. Remaining advisories and analyzer warnings are pre-existing repository issues and were not changed by this bundle.
