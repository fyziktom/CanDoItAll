# Normalized Requirements

| ID | Requirement | Source Evidence | Owning Subbundle |
|---|---|---|---|
| RH-001 | Preserve repository hygiene by resolving tracked transient `codex/bundles/...` artifacts without weakening the guard broadly. | `RepositoryTransientArtifactHygieneTests` failure in `evidence/targeted-failing-tests.txt`. | SB01 |
| RH-002 | Replace work-package IDs in active tests with behavior-language names or justify narrow scanner exceptions with proof. | `RepositoryNamingHygieneTests` failure lists `SB11`, `SB30`, `SB09`, `sb33`. | SB01 |
| RH-003 | Update runtime launch path expectations or path resolution so tests and production agree on the current `src/App/CanDoItAll.Web` layout. | Three `ProjectStructureRuntimeLauncherTests` failures. | SB02 |
| RH-004 | Restore trustworthy watch restore-skip semantics for stale referenced projects using realistic `ProjectReference` paths. | `WorkspaceRuntimeProcessToolsTests.BuildWatchArgumentList_omits_no_restore_when_a_referenced_project_assets_file_is_stale`. | SB02 |
| RH-005 | Repair process-template invariant tests so they assert behavior, not stale prose, while preserving validation/repair routing semantics. | `ProcessDefinitionCatalogProjectionTests` missing expected phrase. | SB03 |
| RH-006 | Restore branch-outcome manager signal recovery from completed process output text/artifacts. | Three `ProcessRuntimeIntegrationAdapterTests` manager-signal failures. | SB03 |
| RH-007 | Stabilize database runtime-switch/migration tests by separating pending model changes from test-order/static-state contamination. | Historical full-suite `PendingModelChangesWarning`; isolated DB test and EF pending check now pass. | SB04 |
| RH-008 | Prove no pending PostgreSQL model changes before adding or skipping migrations. | `evidence/ef-pending-model-check.txt`. | SB04 |
| RH-009 | Rebuild and start the app on `http://localhost:5032`, then prove a fresh app responds for manual testing. | User request. | SB05 |
| RH-010 | Produce a closure report that distinguishes fixed failures from any remaining unrelated suite failures. | User request and prior full-suite noise. | SB05 |
