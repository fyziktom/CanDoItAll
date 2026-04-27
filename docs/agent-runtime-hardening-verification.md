# Agent runtime hardening verification

Captured: 2026-04-27T07:31:21-04:00

## Environment

`dotnet --info`

- SDK: 10.0.203
- Host: 10.0.7
- MSBuild: 18.3.3
- OS: Windows 10.0.26200, win-x64
- `global.json`: `C:\repositories\CanDoItAll\global.json`

## Restore

`dotnet restore CanDoItAll.slnx`

Result: passed. All projects were up to date for restore.

Observed existing warnings:

- NU1904 for `Microsoft.AspNetCore.DataProtection` 10.0.6 critical advisory `GHSA-9mv3-2cwr-p262`
- NU1902 for `OpenTelemetry.Api` 1.13.1 moderate advisory `GHSA-g94r-2vxg-569j`
- NU1510 prune warnings for `CanDoItAll.Mcp.DotNetWatch`

## Build

`dotnet build CanDoItAll.slnx --configuration Release --no-restore`

Result: passed with 0 errors and 64 warnings. Warnings are the existing NuGet advisories/prune warnings plus existing analyzer warnings.

## Bundle-Surface Tests

`dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --no-build`

Result: passed, 203/203.

`dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~AgentFinalizerPolicyTests|FullyQualifiedName~AgentOutputContractTests|FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~ProviderFeatureMatrixTests|FullyQualifiedName~AgentRuntimeHardeningStaticRegressionTests"`

Result: passed, 42/42.

`dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --filter "FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests"`

Result: passed, 8/8.

`dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests"`

Result: passed, 7/7.

`dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "FullyQualifiedName~AgentFinalizerPolicyTests|FullyQualifiedName~AgentOutputContractTests|FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~ProviderFeatureMatrixTests|FullyQualifiedName~AgentRuntimeHardeningStaticRegressionTests|FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests|FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests"`

Result: passed. Matching projects reported 42 unit tests and 15 integration tests passing; other test assemblies reported no matching tests.

## Repo-Wide Test Status

`dotnet test CanDoItAll.slnx --configuration Release --no-build`

Result: timed out after 10 minutes during repo-wide execution. Test-spawned DotNetWatch processes from that command were stopped.

`dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-build`

Result: completed with 421 passing and 30 failing tests. The two process mock runtime failures observed before the finalizer fix are resolved.

Remaining failure groups are outside the MAF post-audit bundle surface:

- ProjectStructure API/MCP host tests fail with `Replacing IHostApplicationLifetime is not supported.`
- PromptFactory and ProjectWorkbench tests cannot locate `output/prompt-library/manifest.json`.
- Migration bootstrap has an existing SQLite table collision for `Automation_DeadLetters`.
- ManagedFiles storage tests fail in storage/runtime-switch paths.
- Processes MCP/seed baseline tests fail on seeded-definition/artifact expectations.

## Closure Notes

The bundle-surface proof is green. Repo-wide acceptance remains blocked by unrelated existing integration/environment failures and dependency advisories listed above.
