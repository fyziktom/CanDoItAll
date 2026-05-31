# Agent runtime hardening verification

> Historical proof record. This file captures the April 27, 2026 runtime-hardening verification run and is not the current API/docs/skills parity source of truth. For current parity repair planning and closure evidence, use `codex/bundles/api-docs-skills-parity-v1/`.

Captured: 2026-04-27T10:52:21-04:00

Working directory: `C:\repositories\CanDoItAll`

## Environment

`dotnet --info`

- SDK: 10.0.203
- Host: 10.0.7
- MSBuild: 18.3.3
- OS: Windows 10.0.26200, win-x64
- `global.json`: `C:\repositories\CanDoItAll\global.json`

## Mandatory Commands

`dotnet restore CanDoItAll.slnx`

Result: passed. Restore completed with existing warnings: NU1510 prune suggestions, NU1904 for `Microsoft.AspNetCore.DataProtection` 10.0.6, and NU1902 for `OpenTelemetry.Api` 1.13.1.

`dotnet build CanDoItAll.slnx --configuration Release --no-restore`

Result: passed. Build completed with 0 errors and 56 warnings. Warning groups were existing package advisories/prune warnings and existing analyzer/nullable warnings outside this bundle.

`dotnet test CanDoItAll.slnx --configuration Release --no-build`

Result: failed outside the round2 MAF/provider/finalizer surface. The run executed for 22m 51s and exposed existing broad-suite failures in these categories:

- Component/project-structure canvas assertions such as `ProjectStructureActionCatalogAdapterTests.Process_definition_nodes_expose_execute_process_without_add_process`, `ProcessCanvasSurfaceFactoryTests.Definition_surface_projects_step_participant_ports_artifact_ports_and_explicit_links`, and `CanvasWorkbenchTests.Workbench_renders_toolbar_hint_and_help_overlay`.
- ProjectStructure MCP/API integration tests failing while constructing the test host with `Replacing IHostApplicationLifetime is not supported`.
- Playwright browser suites failing in bulk after startup/browser prerequisites were not satisfied for the full solution run.
- DotNetWatch integration tests failing in the live wrapper/server validation matrix.
- A timing-sensitive unit failure in `LocalWorkspaceProcessHostTests.ExecuteAsync_returns_after_parent_exit_when_descendant_keeps_redirected_pipe_open`.

No failure in the full-solution run referenced the round2 finalizer, tool-policy, provider-capability, or structured-output tests added for this bundle.

## Focused Proof

Focused unit test classes referenced by this verification record:

- `AgentFinalizerPolicyTests`
- `AgentToolInvocationPolicyTests`
- `ProviderFeatureMatrixTests`
- `AgentRuntimeHardeningStaticRegressionTests`
- `AgentOutputContractTests`

`dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --no-restore`

Result: passed. 221 tests passed, 0 failed, 0 skipped.

`dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --configuration Release --no-restore --filter SettingsPageProvidersTests`

Result: passed. 2 tests passed, including the Ollama provider UI/save path assertion that persisted structured output remains false.

`dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-restore --filter "FullyQualifiedName~WorkspaceProviderCapabilityIntegrationTests|FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests"`

Result: passed. 11 tests passed. This covered provider capability persistence and required-finalizer sequencing failure after a post-finalizer validation tool.

`dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --filter MafAgentRuntimeTests`

Result: passed. 20 tests passed. This covered finalizer tool attachment and JSON-only instruction wording.

`dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --no-restore --filter ProviderFeatureMatrixTests`

Result: passed. 6 tests passed, including the managed OpenAI Responses structured-output source guard.

`git diff --check`

Result: passed with line-ending normalization warnings only.

`git grep -n "RunAsync<" -- src tests docs`

Result: no matches. Typed-output `RunAsync<T>` is not currently used; `docs/maf-runtime-stabilization.md` records the current decision to keep dynamic response-format validation/finalizer flow.

## Closure Notes

Round2 implementation proof is green on the targeted unit, component, and integration coverage that exercises the requested behavior. The mandatory full-solution test command is not green because of unrelated broad-suite failures already outside this bundle's runtime/provider/finalizer scope.

## Round 3 Recovery Addendum

Captured: 2026-04-27.

Round 3 added typed recovery/rework state, proof fingerprinting, retry ledger/backoff, process mutation approval governance, provider approval matrix proof, domain recovery guidance providers, and secret scanning.

Additional focused commands run:

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~AgentFinalizerPolicyTests|FullyQualifiedName~ProviderFeatureMatrixTests|FullyQualifiedName~AgentRuntimeHardeningStaticRegressionTests|FullyQualifiedName~SecretScanningTests"`: passed, 68/68.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AgentRecoveryModelsTests|FullyQualifiedName~MafAgentRuntimeTests"`: passed, 37/37.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"`: passed, 132/132.
- `git grep -l "sk-[A-Za-z0-9_-]\{20,\}" -- . ":!**/bin/**" ":!**/obj/**" ":!**/.git/**"`: no tracked-file matches.

Full solution validation was rerun:

- `dotnet --info`: SDK 10.0.203, host 10.0.7, MSBuild 18.3.3.
- `dotnet restore CanDoItAll.slnx`: passed with existing NU1510, NU1902, and NU1904 warnings.
- `dotnet build CanDoItAll.slnx --configuration Release --no-restore`: passed with 0 errors and 56 warnings.
- `dotnet test CanDoItAll.slnx --configuration Release --no-build`: failed on existing broad-suite failures outside the round 3 recovery/governance surface. The round 3 targeted fixtures above passed after the final changes.

Security note: the repository no longer contains the exposed key in app configuration or tracked source scans, but the exposed credential still must be rotated or revoked outside the repository.
