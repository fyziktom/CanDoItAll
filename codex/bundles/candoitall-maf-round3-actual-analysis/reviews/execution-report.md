# Round 3 Execution Report

Captured: 2026-04-27

## Implementation summary

Round 3 recovery/governance work is implemented:

- removed committed provider key material from app configuration and runtime payload copies;
- added repository secret scanning regression and secure configuration documentation;
- classified process mutation/read tools through a typed policy registry;
- approval-wrapped exposed process mutation tools while preserving explicit internal suppression;
- added typed recovery decisions, rework packets, recovery context, proof fingerprints, proof reuse decisions, and retry ledger/backoff logic;
- persisted typed rework packet and recovery attempt journal events for automated retry and manual rerun paths;
- added recovery worker backoff checks;
- aligned OpenAI/Azure OpenAI Chat Completions approval capability flags with approval-required function wrapping tests;
- moved calculator/Blazor/project retry guidance behind domain recovery guidance providers;
- added behavior-level tests and truthful verification documentation.

## Changed implementation areas

- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `src/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Recovery/AgentRecoveryModels.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.*.cs`
- `src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Rerun.cs`
- `src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs`
- `src/CanDoItAll.Web/appsettings.json`
- `docs/agent-recovery-stabilization.md`
- `docs/secure-configuration.md`

## Tests added or updated

- `tests/CanDoItAll.Tests.Unit/SecretScanningTests.cs`
- `tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`
- `tests/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`
- `tests/CanDoItAll.Tests.Unit/ProviderFeatureMatrixTests.cs`
- `tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs`
- `tests/CanDoItAll.Tests.Integration/AgentRecoveryModelsTests.cs`
- `tests/CanDoItAll.Tests.Integration/MafAgentRuntimeTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Validation results

Passed:

- `python codex\bundles\candoitall-maf-round3-actual-analysis\scripts\validate_bundle.py --stage prepared`
- `dotnet --info`
- `dotnet restore CanDoItAll.slnx`
- `dotnet build CanDoItAll.slnx --configuration Release --no-restore`
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~AgentFinalizerPolicyTests|FullyQualifiedName~ProviderFeatureMatrixTests|FullyQualifiedName~AgentRuntimeHardeningStaticRegressionTests|FullyQualifiedName~SecretScanningTests"`: 68/68 passed.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AgentRecoveryModelsTests|FullyQualifiedName~MafAgentRuntimeTests"`: 37/37 passed.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"`: 132/132 passed.
- `git grep -l "sk-[A-Za-z0-9_-]\{20,\}" -- . ":!**/bin/**" ":!**/obj/**" ":!**/.git/**"`: no tracked-file matches.
- PowerShell repository scan excluding `bin`, `obj`, `.git`, and `node_modules`: no matches.

Failed:

- `dotnet test CanDoItAll.slnx --configuration Release --no-build`.

Observed failure categories in the full solution test command:

- component canvas assertions in existing component tests;
- ProjectStructure MCP/API host construction failures with `Replacing IHostApplicationLifetime is not supported`;
- Playwright suite startup failures because the fixture tried to launch the Debug web app from a Release no-build run;
- storage, prompt factory, project-structure, and process integration failures outside this bundle surface;
- dotnetwatch integration wrapper/server validation failures;
- one timing-sensitive `LocalWorkspaceProcessHostTests` timeout.

No targeted round 3 recovery/governance fixture failed after the final fixes.

## Remaining risks

- The exposed provider key must be rotated or revoked outside the repository. Removing it from source does not invalidate the compromised credential.
- The mandatory full solution test gate remains red due unrelated existing suites. This blocks a claim that the entire repository is green, but does not contradict the focused round 3 proof.
- Existing NuGet audit warnings remain: NU1902 for `OpenTelemetry.Api` and NU1904 for `Microsoft.AspNetCore.DataProtection`; NU1510 prune suggestions also remain.
