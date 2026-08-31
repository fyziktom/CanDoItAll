# Focused Test Selection And Invalidation

No tests/discovery/builds ran during preparation. Counts below are **source inventory at8a8dc2da0**, not runtime results. All tests use xUnit2/VSTest/net10.0. Future execution lists the exact filter first, reconciles the inventory plus newly added cases, then runs the identical selector and verifies nonzero executed TRX counts. Early-return platform cases are not affirmative proof.

## Projects And Commands

- U: `tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- I: `tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`
- C: `tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`

From repository root, substitute the selected project and OR-combined fully qualified class prefixes from the tables:

```powershell
dotnet test <project> --configuration Release --list-tests --filter '<selector>'
dotnet test <project> --configuration Release --filter '<selector>' --logger 'trx;LogFileName=selected.trx' --results-directory <owned-proof-results>
```

Use isolated artifact/output paths when needed to avoid colliding with5032/watch. Build affected projects once for each frozen source/configuration/dependency state, then use --no-build only while those binaries remain current. Record exact command, working directory, start time, exit code, discovery and execution output. Existing shell examples are recipes, not already-run proof.

## SB01 Filesystem Security And Downstream Commit

| Project | FullyQualifiedName prefix | Existing cases |
|---|---|---:|
| U | CanDoItAll.Tests.Unit.Storage.PhysicalFileSystemPathPolicyTests | 10 |
| U | CanDoItAll.Tests.Unit.Storage.DurableFileWriterTests | 9 |
| I | CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspaceStoreLockIntegrationTests | 16 |
| I | CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspaceExistingRunUpdateRecoveryIntegrationTests | 15 |

Execution reconciliation: SB01 added eight cases in DurableFileWriterTests, yielding27unit cases on both Windows and Linux. Existing31integration cases remain. Actual discovery/TRX and capability proof are recorded in proof/SB01. Selection reason: operation freshness, atomic durable replacement and a real dependent workspace transaction. Add explicit root/case-change, unknown→known, ancestor-swap, flush-count/probe-count and cross-process tests in these owning classes or declare the new exact class in this plan before running it. Existing child crash host: `tests/Support/CanDoItAll.DurableFileWriter.TestHost`.

Mandatory capability evidence: actual symlink/root/ancestor security assertions run on a capable platform (Windows privilege or Linux); Unix private-mode assertions execute on Linux. Silent early returns do not count; record executed scenario/platform. No production-data mutation.

## SB02 Validated Provider Projections

| Project | FullyQualifiedName prefix | Existing cases |
|---|---|---:|
| U | CanDoItAll.Tests.Unit.AgentFramework.ProviderRuntimeProfileSnapshotServiceTests | 8 |
| U | CanDoItAll.Tests.Unit.SharedProviderRuntimeProfileMaterializerTests | 21 |
| U | CanDoItAll.Tests.Unit.AgentFramework.ProviderCatalogProjectionFailureTests | 12 |
| U | CanDoItAll.Tests.Unit.AgentFramework.AgentExecutionPreparationServiceTests | 9 |
| U | CanDoItAll.Tests.Unit.AgentFramework.AgentProviderCredentialDispatchScopeTests | 10 |
| U | CanDoItAll.Tests.Unit.ProviderManagementBoundaryTests | 1 |
| U | CanDoItAll.Tests.Unit.SharedProviderArchitectureCharacterizationTests | 12 |
| I | CanDoItAll.Tests.Integration.SharedProviders.SharedProviderRuntimeProjectionIntegrationTests | 21 |
| I | CanDoItAll.Tests.Integration.AgentFramework.ProviderInitializationIntegrationTests | 2 |

73unit +23integration source cases. Characterizations include `Warm_reads_probe_revisions_without_full_profile_reload`, `GetProviderAsync_does_not_resurrect_stale_catalog_entry_after_committed_delete`, `Warm_acquisition_revalidates_canonical_provider_and_reuses_blueprint`, `Next_dispatch_reads_the_secret_again` and `Persisted_shared_graph_projects_through_materializer_mapper_snapshot_and_catalog`.

Execution selection declared before SB02 characterization: **76 unit and 35 integration cases** (73 existing unit + 3 direct validation/allocation cases; 23 existing integration + 12 added in the same integration class). New cases are `Warm_single_and_set_lookups_reject_corruption_without_token_changes` (8 inline corruption values; each covers both lookup modes), `Concrete_revision_probes_preserve_full_load_revisions_with_bounded_queries`, `Revision_set_preserves_invalid_unrelated_source_value_conversion_failure`, and `Revision_probes_preserve_local_mapping_failure_without_token_changes`. The concrete-query characterization initially records three reads; require a failing-first two-read expectation on the unchanged selected-provider implementation before accepting the optimization. New unit cases in the existing materializer class are `Validate_retains_canonical_shape_for_operationally_disabled_graphs`, `Validate_rejects_missing_and_malformed_graphs_without_effective_profiles`, and `Validate_avoids_effective_model_copy_allocations`. `Duplicate_imports_are_rejected_before_invalid_source_materialization` adds real relational cardinality/failure-precedence coverage by temporarily dropping and restoring only the uniquely leased fixture database index, with owned duplicate cleanup and exact index-definition restoration in `finally`. No live/application schema migration is involved. No new class selector is needed.

Add concrete-loader unchanged-token malformed snapshot/duplicated metadata/forged cache negatives for **both** single and set cached lookup. Existing recording-loader mocks cannot prove the database loader avoids calling its own full-load path. New relational tests must inspect actual query/materialization behavior and full-load parity, not only EF InMemory or mocked counters.

Integration safety: explicit designated test PostgreSQL server via `CANDOITALL_TESTS_POSTGRES_CONNECTION`; preserve unique leased database creation/disposal in `CanDoItAllTestEnvironment`. Never supply live5032/5214 profiles/database via sharedActiveProfile. If no test server is configured, stop before the fixture auto-probes5432/starts compose PostgreSQL; do not mutate live infrastructure.

## SB03 Recovery And Projection

| Project | FullyQualifiedName prefix | Existing cases |
|---|---|---:|
| U | CanDoItAll.Tests.Unit.AgentFramework.FileSandboxWorkspaceChatProjectionStoreTests | 20 |
| I | CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspaceStoreLockIntegrationTests | 16 |
| I | CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspaceExistingRunUpdateRecoveryIntegrationTests | 15 |
| I | CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspaceChatRunCommitRecoveryIntegrationTests | 12 |
| I | CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspaceGenericNewRunCommitRecoveryIntegrationTests | 6 |
| I | CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspaceUsageProjectionIntegrationTests | 15 |
| I | CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspaceAdmissionReadScalingIntegrationTests | 6 |

70integration source cases plus20unit. Selection reason: trusted immediate path versus untrusted recovery, lock/cancellation boundaries, real projections and read scaling. Reuse SB01 proof only if all source/test/dependency/build invalidation keys match; otherwise rerun the affected selector.

SB03 execution adds **19 integration cases** under FullyQualifiedName~CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspacePreparedCommitReadIntegrationTests: two real append-progress read-count cases (one and32 existing logs), six matching-target canonicalization cases (Run and ChatIndex, each canonical/compact/unknown-property JSON), ten noncooperating missing/foreign payload cases across all five supported payloads, and one recovered-journal read-count/idempotence case. These run separately from the existing70; total89unique storage integration cases plus20projection unit cases. New cases were declared to the coordinating reviewer before discovery; old-code characterization yielded the expected two read-count failures and17passes before production persistence edits.

The original70case baseline retains one deadline failure under concurrent SB02 build/test contention. The exact unchanged CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspaceUsageProjectionIntegrationTests.File_source_checkpoints_large_manifest_within_host_deadline_and_resumes_after_restart then passed alone with its existing two-second per-pass budget and no competing agent build/test/CodeAnalytics work. Do not erase this failure or increase its timeout. Run candidate as69cases excluding that exact FQN, then the exact one-case deadline selector during an explicitly coordinated quiet interval. This remains70unique existing cases, not71. Preserve original failure and isolated pass in proof/SB03; no application host or default PostgreSQL instance is involved.

## Combined Failure/Activity Gate

- U: `FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentChatExecutionActivityOrchestratorTests` (5cases), including context-capture terminal failure and approval continuation bound to same run.
- U: `FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentExecutionCancellationRegistryTests` (2cases).
- U: exact existing `Stop_rejects_a_running_chat` method in FloatingAgentChatArchitectureTests; resolve its fully qualified namespace during list-tests and record the single-case selector.
- C: `FullyQualifiedName~CanDoItAll.Tests.Components.AgentFramework.AgentExecutionActivityStatusTests` (9cases:4Fact+5InlineData), including Failed/Cancelled typed-state rendering.
- Added exact Integration selector: `FullyQualifiedName=CanDoItAll.Tests.Integration.AgentFramework.AgentFrameworkExecutionRunTrackingIntegrationTests.SendMessageAsync_runtime_failure_persists_failed_log_and_activity_after_store_reopen` (2Theorycases). These exercise the real terminalizer, durable reopened store and activity replay with injected startup/provider-origin runtime errors; they are not HTTP-adapter failure tests. See proof/SB03/combined-failure-test-plan.md. Preserve isolated actual provider-failure and startup-exception cases when the existing orchestration fixture does not demonstrate the full terminal persistence/log path. Required named scenario: startup exception reaches caller/activity and persisted Failed log, never an unobserved task or stale success.
- Real Playwright MCP matrix on5032and5214 is additional mandatory proof; no automated Playwright fixture substitutes for these exact live hosts.

## Discovery, Invalidation And Broad Gate

Each source count must be reconciled with actual runtime discovery before passing. New tests are added to the planned selection and expected count before the execution command. Zero/unexpected discovery or skipped required scenario = failed validation, not success.

Invalidation keys: production/test hashes, public contract shape, filesystem lifetime/freshness/flush/lock behavior, provider graph/revision/DB generation semantics, projection/journal schema/order, build SDK/package/dependency mode and host/provider/fixture configuration. Use CodeAnalytics impacted-tests on actual changed ranges only during execution; include all U/I/C workspaces and promote conditional scopes when containment assumptions break.

Default broad gate: **Not required**. A public filesystem/provider contract, shared serialization/journal schema, DI/project-reference or build/test infrastructure change is a named expansion trigger: reopen scope and select the newly impacted consumer suites. Only after that review may an unfiltered affected project/solution gate run once at the `Frozen Integration` checkpoint. Task size or Governed tier alone does not authorize broad tests.
