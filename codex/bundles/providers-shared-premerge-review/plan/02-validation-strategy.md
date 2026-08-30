# Validation Strategy

Follow product `docs/testing.md`: Release, `/m:1`, local sibling source dependency mode. Record exact Components/FileTools commits and SDK before final proof. Preparation did not run product builds/tests.

## Focused selection

| Unit | Existing owning selectors | Expected discovery / new cases |
| --- | --- | --- |
| SB01 | Integration: SharedProviderOpenAiCompatibilityIntegrationTests; SharedProviderStreamingIntegrationTests. Unit: SharedProviderRelayPolicyTests. | Existing classes nonzero; add named error:null, oversized 429/504, SDK failed/incomplete/timeout/disconnect cases. |
| SB02 | Unit: SharedProviderSourceUriPolicyTests. Integration: SharedProviderRuntimeProjectionIntegrationTests. | Existing classes nonzero; add loopback IPv4/IPv6/localhost end-to-end selection and non-loopback rejection. |
| SB03 | Unit: ProviderHistoryLifecycleTests, ProviderHistoryCaptureTests. Integration: ProviderHistoryCaptureIntegrationTests. | Existing classes nonzero; add quoted credential keys, explicit timeout vs caller cancellation, preserved terminal usage. |
| SB04 | Integration: ProviderHistoryPersistenceIntegrationTests, ProviderHistorySourceProjectionIntegrationTests. | Existing classes nonzero; add orphan cleanup, retained retry input, bounded concurrent cleanup and quota cases. |
| SB05 | Unit: SharedProviderPublicationAndCatalogTests, SharedProviderRelayPolicyTests. Integration: SharedProviderOpenAiCompatibilityIntegrationTests. | Existing classes nonzero; preserve QueryCacheUsesPersistedStampAcrossInstancesAndRechecksCurrentEligibility and parameter matrix. |
| SB06 | Integration: SharedProviderCatalogApiIntegrationTests, SharedProviderOpenAiCompatibilityIntegrationTests. | Existing OpenApi methods plus new scalar/enum/property/required/unknown-field conformance cases. Planned old SharedProviderOpenApiIntegrationTests is absent; never claim its old count. |
| SB07 | tools/Validation/Test-Documentation.ps1 | N/A automated test; zero missing README/link/metadata findings. |
| SB08 | SharedInfo validators and skill validator; Integration: MigrationBootstrapIntegrationTests, SharedProviderPersistenceIntegrationTests, ProviderHistoryPersistenceIntegrationTests | Exact final route sets/counts/hashes; add exact-development existing-data upgrade/backfill case and reviewed-head populated sharing/history repair-or-preservation case; verify seven branch migrations plus repair migrations. |
| SB09 | Frozen stable gate plus focused ProviderHistoryUiAcceptanceTests and required legacy hosted contract | Discover all selected cases before execution; exact current host/source identities, no excluded lane reported as passed. |

These are existing class/topic selectors, not claimed executed counts. At unit entry enumerate exact FQNs and data rows, declare expected cases (including added regression names), then compare runner discovery. A zero/missing/changed count fails the command; do not silently broaden to everything. Use CodeAnalytics impacted-tests query with actual changed ranges and the separate test solution paths before widening selection.

Example from repository root (replace filter with this table's selected owning class):
```powershell
dotnet build ./src/Integration/CanDoItAll.SharedProviders.Http/CanDoItAll.SharedProviders.Http.csproj --configuration Release /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Integration.slnx --configuration Release --list-tests --filter "FullyQualifiedName~SharedProviderStreamingIntegrationTests" /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Integration.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~SharedProviderStreamingIntegrationTests" /m:1
```

Build owning test assembly before --no-build. Capture exit code, expected/actual discovery, configuration, dependency mode and source hashes. No provider credential is needed for fake-upstream tests. Environment-dependent fixtures remain separate lanes.

## Performance evidence

Use 1/32/256-message normalization; 1/50/200-publication repeated routing with changed publication/profile/secret rows from another scope; 1 MiB and bounded 64 MiB response payloads; concurrent requests and bounded retention batches. Record bytes allocated, peak working set where relevant, elapsed distribution, DB query count and EXPLAIN ANALYZE BUFFERS for changed SQL. No fixed speedup promise. Default policy batch=500 every20s gives an outbox upper bound of 25 items/s before overhead; source maintenance separately caps each source at 100 per pass (5 items/s). These are capacity hypotheses: assess backlog at declared arrival rates; change scheduling only if measured requirements justify it.

## Invalidation and broad gate

Each subbundle lists its keys. Source/test/config/SDK/dependency-mode drift invalidates matching proof. Documentation-only edits do not rerun the product suite.

Named broad trigger: this pre-merge branch adds shared persistence migrations and cross-cutting MAF/Composition/HTTP contracts across independent consumers. At checkpoint CP-MERGE-FROZEN, after focused proof passes, run the product and Stable solution Release restore/build/test recipe from docs/testing.md once, preserving its exclusion filter:
`Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true`.

The stable pass does not close browser, Docker, live/paid, scale or original SB07 three-application requirements. Keep those separately identified and respect existing authority. Never rerun broad proof merely after checksum/docs changes.

## Browser and host proof

Use existing app surfaces at 1920x1080: provider History and global Request history initially lazy; explicit search/paging; detail overlay; Light/Detailed settings. Preserve existing table as primary surface, existing tabs/forms/dialog sizing, internal list/dialog scroll owners and action visibility. Capture and inspect normal and open-overlay states. No layout/CSS work is planned; if repair requires it, load Components skill and amend scope before edits.

Use isolated PostgreSQL for migration/cleanup, never live user data. Lane A starts at development migration 20260822013043_AddWorkflowNativeCheckpointRequestUniqueness: preserve existing profiles/canonical agent/Simple Chat/workflow data, then validate new schema/backfill. Lane B starts at reviewed head with populated sharing/history/detail/ownership/quota data, applies repair migrations if present, and verifies preservation/transfer. Do not invent feature tables in the older baseline. Product SQL generation does not apply migrations. Original SB07 topology/budget decision is in the handoff plan; no Docker work was run in preparation.
