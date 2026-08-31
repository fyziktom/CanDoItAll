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

Selection reason: operation freshness, atomic durable replacement and a real dependent workspace transaction. Add explicit root/case-change, unknown→known, ancestor-swap, flush-count/probe-count and cross-process tests in these owning classes or declare the new exact class in this plan before running it. Existing child crash host: `tests/Support/CanDoItAll.DurableFileWriter.TestHost`.

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

## Combined Failure/Activity Gate

- U: `FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentChatExecutionActivityOrchestratorTests` (5cases), including context-capture terminal failure and approval continuation bound to same run.
- U: `FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentExecutionCancellationRegistryTests` (2cases).
- U: exact existing `Stop_rejects_a_running_chat` method in FloatingAgentChatArchitectureTests; resolve its fully qualified namespace during list-tests and record the single-case selector.
- C: `FullyQualifiedName~CanDoItAll.Tests.Components.AgentFramework.AgentExecutionActivityStatusTests` (9cases:4Fact+5InlineData), including Failed/Cancelled typed-state rendering.
- Preserve/add isolated actual provider-failure and startup-exception cases when the existing orchestration fixture does not demonstrate the full terminal persistence/log path. Required named scenario: startup exception reaches caller/activity and persisted Failed log, never an unobserved task or stale success.
- Real Playwright MCP matrix on5032and5214 is additional mandatory proof; no automated Playwright fixture substitutes for these exact live hosts.

## Discovery, Invalidation And Broad Gate

Each source count must be reconciled with actual runtime discovery before passing. New tests are added to the planned selection and expected count before the execution command. Zero/unexpected discovery or skipped required scenario = failed validation, not success.

Invalidation keys: production/test hashes, public contract shape, filesystem lifetime/freshness/flush/lock behavior, provider graph/revision/DB generation semantics, projection/journal schema/order, build SDK/package/dependency mode and host/provider/fixture configuration. Use CodeAnalytics impacted-tests on actual changed ranges only during execution; include all U/I/C workspaces and promote conditional scopes when containment assumptions break.

Default broad gate: **Not required**. A public filesystem/provider contract, shared serialization/journal schema, DI/project-reference or build/test infrastructure change is a named expansion trigger: reopen scope and select the newly impacted consumer suites. Only after that review may an unfiltered affected project/solution gate run once at the `Frozen Integration` checkpoint. Task size or Governed tier alone does not authorize broad tests.
