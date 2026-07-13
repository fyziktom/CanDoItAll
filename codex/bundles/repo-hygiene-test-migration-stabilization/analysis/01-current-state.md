# Current State

## Test Platform

- SDK: `.NET 10.0.204`.
- `global.json`: SDK `10.0.200`, `rollForward: latestPatch`.
- Unit test project: `tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`.
- Test framework/platform: xUnit via `Microsoft.NET.Test.Sdk`, VSTest-style `--filter`.

## Current Failing Test Probe

Targeted probe:

```powershell
dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~RepositoryTransientArtifactHygieneTests|FullyQualifiedName~RepositoryNamingHygieneTests|FullyQualifiedName~WorkspaceRuntimeProcessToolsTests|FullyQualifiedName~ProjectStructureRuntimeLauncherTests|FullyQualifiedName~ProcessRuntimeIntegrationAdapterTests|FullyQualifiedName~ProcessDefinitionCatalogProjectionTests|FullyQualifiedName~AppDbContextRuntimeSwitchTests"
```

Result: 10 failed, 152 passed, 162 total. Transcript: `evidence/targeted-failing-tests.txt`.

## Failure Clusters

1. Repository transient artifact hygiene:
   - `RepositoryTransientArtifactHygieneTests.RepositoryTransientArtifactHygiene_rejects_tracked_codex_work_package_outputs`.
   - Fails because tracked files under `codex/bundles/skill-tool-mcp-isolation-template-migration/...` violate the current guard.
   - Root question: were those files intentionally committed as durable docs, or should they be removed from tracked source / exported elsewhere?

2. Repository test naming hygiene:
   - `RepositoryNamingHygieneTests.RepositoryNamingHygiene_active_tests_do_not_contain_work_package_identifiers`.
   - Fails on `SB11`, `SB30`, `SB09`, and `sb33` identifiers in integration and memory tests.
   - Root question: rename tests/literals to behavior language, or narrow the scanner with explicit context rules if an identifier is legitimate domain data.

3. Runtime launch path drift:
   - Three `ProjectStructureRuntimeLauncherTests` expect `src\CanDoItAll.Web\CanDoItAll.Web.csproj`.
   - Production resolves `src\App\CanDoItAll.Web\CanDoItAll.Web.csproj`.
   - Likely obsolete assertions after the `src/App` layout move.

4. Watch restore stale-reference test:
   - `WorkspaceRuntimeProcessToolsTests.BuildWatchArgumentList_omits_no_restore_when_a_referenced_project_assets_file_is_stale`.
   - The test fixture uses `..\CanDoItAll.Infrastructure\...` from `src\App\CanDoItAll.Web`, but creates the referenced project under `src\Foundation\...`.
   - Likely obsolete fixture path, but execution must verify production logic with a realistic project-reference path before changing the test.

5. Process-template prose assertion drift:
   - `ProcessDefinitionCatalogProjectionTests.Dotnet_feature_code_change_keeps_browser_proof_out_of_atomic_targeted_validation_step`.
   - Expected phrase: `same canonical solution no longer reproduces the defect`.
   - Current `feature-repair.md`/definition text has been rewritten around concrete validation repair, same-target blockers, and runtime/browser ownership.
   - Root question: preserve the behavior invariant with a less brittle assertion, or restore missing template language if the invariant was accidentally removed.

6. Process branch-signal recovery:
   - Three `ProcessRuntimeIntegrationAdapterTests` fail because `ManagerSignals` is empty when completed output text declares a branch outcome.
   - Parser source still contains heading-plus-next-line and declared-outcome matching logic in `AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`.
   - Root question: newer managed artifact / receipt validation may prevent the branch signal from being emitted; fix the production contract or update obsolete test setup only after semantic proof.

7. Database migration/isolation:
   - Historical full suite showed `PendingModelChangesWarning` from `AppDbContextRuntimeSwitchTests`.
   - Isolated rerun passes: `evidence/database-runtime-switch-test.txt`.
   - EF pending-model check passes: `evidence/ef-pending-model-check.txt`.
   - Root question: broad-suite order/static state contamination, likely around `AppDbContextModelRegistry`, not a currently missing migration.

## 5032 Runtime State

- No final `5032` runtime proof has been captured for this bundle yet.
- SB05 owns rebuilding, starting, and smoke-testing `http://localhost:5032` after hygiene repairs.
