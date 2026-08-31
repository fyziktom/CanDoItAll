# Validation command index

Repository cwd: CanDoItAll root. Run window: 2026-08-30 local, continuing into 2026-08-31 UTC. SDK10.0.303; Release; /m:1; outputs artifacts/premerge. PostgreSQL fixtures were explicitly directed to the repository local server and created/dropped UUID disposable databases. Credential values are deliberately omitted.

## Frozen build

Exact replayable recipe: bundle://scripts/Build-FrozenGate.ps1. Actual commands and individual exit codes: bundle://reviews/sb09-builds.log. Nine direct production builds plus product/Stable restore/build all exited 0, zero warnings/errors. During the frozen run, Get-FileHash -Algorithm SHA256 compared all nine runtime-build.json assemblies with their recorded values; all match (sb09-binary-identity.log).

## Final owning tests

Integration project: tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj.
Filter: FullyQualifiedName~SharedProviderPremergeRelayTests|FullyQualifiedName~SharedProviderStreamingIntegrationTests|FullyQualifiedName~SharedProviderOpenAiCompatibilityIntegrationTests|FullyQualifiedName~SharedProviderRuntimeProjectionIntegrationTests|FullyQualifiedName~ProviderHistoryCaptureIntegrationTests|FullyQualifiedName~ProviderHistoryPersistenceIntegrationTests|FullyQualifiedName~ProviderHistorySourceProjectionIntegrationTests|FullyQualifiedName~SharedProviderCatalogApiIntegrationTests|FullyQualifiedName~SharedProviderOpenApiSchemaTests|FullyQualifiedName~MigrationBootstrapIntegrationTests|FullyQualifiedName~SharedProviderPersistenceIntegrationTests.
Options: -c Release --artifacts-path ./artifacts/premerge --no-build --no-restore /m:1. Discovery used --list-tests; execution used --logger "trx;LogFileName=sb01-08-final.trx" and bundle reviews/test-results as results directory. Expected/actual179, exit0. Discovery and raw log: sb01-08-final-discovery.log, sb01-08-final.log.

Additional Integration filter: FullyQualifiedName~SharedProviderPremergeUpgradeTests|FullyQualifiedName~ReviewedHeadToRepairs_PreservesSharingHistoryAndTransfer|FullyQualifiedName~ProviderHistoryRuntimeIntegrationTests|FullyQualifiedName~CatalogCacheAllocationsAndCrossScopeRevocation. Expected/actual6; exit0; sb05-08-final-discovery.log and sb05-08-final.trx. This reuses current compiled code and proves both migration lanes.

UI project: tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj.
Filter: FullyQualifiedName~ProviderHistoryPremergeUiTests. Same Release/artifacts/no-build/no-restore flags; logger trx;LogFileName=sb09-ui-complete.trx. Expected/actual1, exit0. CANDOITALL_TEST_CONFIGURATION=Release, ArtifactsPath set to artifacts/premerge, UseArtifactsOutput=true; externally attached BaseURL removed. Final test-only rebuild used /p:BuildProjectReferences=false after the full owning graph had passed and production code remained unchanged. Prior UI iterations do not represent application defect baselines.

Earlier owning unit case sets and exact FQNs are in sb01-04-unit-owning.trx (145) and sb05-unit.trx (110), with their corresponding discovery logs. Stable runs these current cases again under the recorded frozen recipe; no independent count is invented for overlaps.

## Frozen Stable

Project: tests/Solutions/CanDoItAll.Tests.Stable.slnx.
Command: dotnet test tests/Solutions/CanDoItAll.Tests.Stable.slnx -c Release --artifacts-path ./artifacts/premerge --no-build --no-restore --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true" /m:1 --logger "trx;LogFilePrefix=sb09-stable" --results-directory codex/bundles/providers-shared-premerge-review/reviews/test-results.
Discovery used identical options with --list-tests in place of the logger/result options. Initial listing9,369 display entries; original per-assembly listing counts remain in sb09-stable-counts.json. Actual one-run result: exit0,9,424/9,424 passed, no failures/skips. Seven deferred MemberData methods expand the listing by55 rows; source inspection and scripts/Reconcile-StableDiscovery.py account for every difference. Per-assembly expected expanded rows, source hashes and exact TRX identities: sb09-stable-results.json. Reconciliation log: sb09-stable-reconciliation.log.

## Contract, docs, schema, skill

- python reviews/validate-schema-conformance.py reviews/test-results/sb01-08-final.trx, with isolated jsonschema4.25.1: exit0, 28/28; sb06-final-conformance.log.
- tools/Validation/Test-Documentation.ps1: exit0,197; sb07-final-documentation.log.
- SharedInfo tools/validation/Test-SharedInfo.ps1: exit1, one stale-export workflow route mismatch; sb08-sharedinfo-draft.log.
- Current skill-creator/scripts/quick_validate.py on four changed source skills: exit0 each; sb08-skills.log.
- SharedInfo installer -PackageName _candoitall-api-shared,candoitall-api-shared-providers,candoitall-api-agents,candoitall-api-llm-chats,candoitall-api-workflows -Force -WhatIf: preview only, exit0; sb08-install-preview.log.
- dotnet ef migrations has-pending-model-changes --project src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --context AppDbContext --configuration Release --no-build: exit0, no changes; sb08-pending-model.log. ArtifactsPath/UseArtifactsOutput select isolated build.
- Same EF project/startup/context/config flags with migrations script --idempotent, then with from=20260822013043_AddWorkflowNativeCheckpointRequestUniqueness and from=20260830104752_AddProviderHistoryExternalReference: exit0; sb08-sql-all.log, sb08-sql-development.log, sb08-sql-reviewed-head.log. Output filenames/hashes in artifacts.json. Tool10.0.3 reports its older version relative to runtime10.0.4; no failure and no tool upgrade.

The portable raw logs/TRX provide real results; this index does not turn unexecuted export/installation/host requirements into passed commands.
