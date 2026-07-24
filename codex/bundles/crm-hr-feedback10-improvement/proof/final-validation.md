# Final Validation

Run date: `2026-07-24`

## Release Solution Build

```powershell
dotnet build CanDoItAll.slnx --configuration Release --no-restore -nologo -v:minimal /m:1
```

- Exit code: `0`
- Result: `0 errors`, `165 warnings`
- Elapsed: `31.39s`
- Explicit risk: the warning set includes the existing high-severity `NU1903` advisory for `System.Security.Cryptography.Xml` `10.0.7`. This is repository dependency debt and is not described as a clean-warning build.

## Feature UI Selection

```powershell
dotnet test tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --configuration Release --no-build -nologo --filter "(FullyQualifiedName~PagedRecordBrowserTests|FullyQualifiedName~CrmHrCatalogDialogTests|FullyQualifiedName~CrmHrDirectoryPageFreshnessTests|FullyQualifiedName~CrmHrNavigationTests|FullyQualifiedName~CrmHrWorkspaceFreshnessTests)"
```

- Exit code: `0`
- Result: `37 passed`, `0 failed`, `0 skipped`
- Elapsed: `1m50s`
- Includes the delayed recruiting-context render-race regression.

## Recruiting Race Regression Alone

```powershell
dotnet test tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --configuration Release --no-build -nologo --filter "FullyQualifiedName~Recruiting_query_selection_publishes_context_only_after_workspace_load_completes"
```

- Exit code: `0`
- Result: `1 passed`, `0 failed`, `0 skipped`
- Elapsed: `17s`

## Focused CRM-HR API

```powershell
dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --no-build -nologo --filter "FullyQualifiedName~CrmHrApiIntegrationTests"
```

- Exit code: `0`
- Result: `2 passed`, `0 failed`, `0 skipped`
- Elapsed: `30s`

The selection contains the real-host linked scenario and the invalid-reference/query validation negative.

## Broader CRM-HR Integration Regression

```powershell
dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --no-build -nologo --filter "(FullyQualifiedName~CrmHrCrossModuleIntegrationTests|FullyQualifiedName~CrmHrAuditTrailIntegrationTests|FullyQualifiedName~CrmHrSourceSnapshotPagingIntegrationTests|FullyQualifiedName~CrmHrSchemaIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests|FullyQualifiedName~PartyMergeIntegrationTests|FullyQualifiedName~PartyDirectoryIntegrityIntegrationTests|FullyQualifiedName~RecruitmentLifecycleIntegrationTests|FullyQualifiedName~WorkforceProfileIntegrationTests)"
```

- Exit code: `0`
- Result: `35 passed`, `0 failed`, `0 skipped`
- Elapsed: `7m38s`

## Skill Synchronization

- Repo-owned `candoitall-api-crmhr`: `Skill is valid!`
- Installed `candoitall-api-crmhr`: `Skill is valid!`
- `SKILL.md`, `references/api-contract.md`, and `agents/openai.yaml` had identical corresponding SHA-256 hashes in repo and installed roots.

## Architecture, Performance, And Hygiene

- No project-reference change or dependency cycle was introduced.
- Web API files are thin transport/DTO adapters over canonical CRM-HR services and contain no direct `DbContext` or domain persistence.
- No critical performance anti-pattern was found in the follow-up path. Existing measured follow-ups remain documented in the architecture gate.
- `git diff --check` exited `0`.

## Non-Blocking Repository Baseline

The broader all-unit baseline is not claimed green. It was diagnostically stopped after unrelated existing failures in workflow snapshot tests, seed-version/hygiene gates, stale in-memory CRM-HR fixtures, and the repository secret scan. These failures are outside the changed follow-up paths; focused affected component/API/integration suites above are the closure evidence.

## Decision

`Pass` for the affected CRM-HR follow-up and bundle closure. The `NU1903` dependency advisory and unrelated all-unit baseline remain explicit residual risks.
