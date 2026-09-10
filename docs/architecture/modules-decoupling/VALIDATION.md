# Module Decoupling Validation

## Environment and revision

Baseline code: `3da508996e46e68d811f5ffb6926df8b876878cf`, branch `modules-decoupling`, initially clean. Windows 10.0.26200 x64; SDK 10.0.303 selected by the existing 10.0.302/latestPatch policy; runtime 10.0.11. No SDK/package changes. Ignored evidence root: `artifacts/modules-decoupling/20260910-baseline/`.

| Source dependency | HEAD | Initial state |
| --- | --- | --- |
| Components | `1c939033cb427b507086d1f7f2381c43992175ee` | Clean; branch **codex/original-ui-refactoring-release**; matches CI pin |
| FileTools | `7c7453c6583365ae5bd63f8fc6efc4a776e15818` | Clean; detached; differs from CI pin `498b36825bd5a5222429972af120b04becf4b3f6` |
| Mcp | `abeaea463aa906bca2d83a1b608ff432dbc29d8d` | Clean; main |
| SharedInfo | `00e6fe6389eba9a6bc3b2bb78e27fb10b7597292` | Clean; main |
| AgentFramework.Rag | `3cc37118461437f1b798af804b0b047a1558be24` | Clean; main |
| AgentFramework.SemanticCompletion | `fef6ab6ec84e04b4edb3f94ffb9d3ca81e79e32e` | Clean; main |
| CodeAnalysis | `b023500eee239a56358843f1380adb4610f72850` | Clean; main |
| IPFS | `1869a78e45ea6591a7c5acb24ac83f7401685711` | Clean; main |
| Ledger | `317e452d3cce9fd67752b9e41b33fff07486088a` | Clean; main |
| Demos | `974a6dc9fa0bcbcaa57c70f2b6ed7e065dea5465` | Clean; main |

All sibling sources remain read-only. Shared standards inspected: documentation and tooling, with the standards-map invariants; no templates copied.

## Baseline checks

| Check | Reproduction / observation | Result |
| --- | --- | --- |
| Repository | `git status --short`, `git branch --show-current`, `git rev-parse HEAD` | PASS: clean expected branch and actual HEAD recorded |
| Signing reuse | Matching Git GnuPG `gpgconf --launch gpg-agent`; two detached signatures of disposable non-secret input in separate shell invocations, each verified | PASS: expected fingerprint `96E836FAA8854EE98ABC10903C206549E1D7EAD6`; second reused cache without pinentry. No settings changed during the probes. Initial default idle cache later expired; finite task policy and restoration details are in EXECUTION. |
| First checkpoint | `git commit -S`, `git verify-commit HEAD`, `git log -1 --format='%H %G? %GF'` | PASS: `49ef77a3dcc4bc5b4d39126e7770e4706553098d`, status G, expected fingerprint. Local only. |
| Product build | `dotnet build ./CanDoItAll.slnx --configuration Release /m:1` | FAIL: output copy locks held by existing PID 1836; 1000 warnings / 200 errors, elapsed 6:23. Log: `product-build.log`. Isolated output rerun passed below; not classified as a source defect. |
| Isolated product build | `dotnet build ./CanDoItAll.slnx --configuration Release --artifacts-path ./artifacts/modules-decoupling/build /m:1` | PASS: 0 warnings / 0 errors, elapsed 3:20.81. Log: `product-build-isolated.log`. Same source/dependency graph; existing sandbox retains its explicit Parity output. |
| Established host | Read-only `GET http://localhost:5032/_dev/runtime` and `/_dev/database/selection`, PID/path inspection | Ready, PID 1836, main checkout Release executable, predates task. Runtime override profile `e5df9ad6-33db-c697-4a06-78a74976013c`; no pending activation. Build freshness for task not proven. |
| Ollama availability | `GET http://127.0.0.1:11434/api/tags` | PASS for discovery only: `gpt-oss:20b` installed. Real tool invocation NOT_RUN. |
| OpenAI | Configured provider discovery, `GET /_dev/agentframework/credential`, bounded synthetic `POST /api/agents/providers/{id}/test-chat` | Connectivity PASS on baseline host: provider `c1c103db-707e-3f52-8809-8d804fc171d1`, model `gpt-5.4-mini`, response OK, input 29/output 32 tokens, 40-second deadline/no retry. No credential printed. Sanitized `openai-connectivity.json`; real tool proof NOT_RUN. |
| Ollama connectivity | Same synthetic test-chat route, configured local provider `bd2bffbb-23d5-d152-82f6-e1d37908b169`, installed `gpt-oss:20b` | FAIL: client deadline at 60 seconds, TaskCanceledException, no automatic retry; real tool proof NOT_RUN. Availability alone does not prove usable completion. |
| Documentation | `./tools/Validation/Test-Documentation.ps1` | PASS: 208 maintained Markdown files after correcting two task-note path findings and five missing historical evidence references reproduced in unchanged Governance fixture / UI sandbox READMEs. |
| Portability-static | Two documented Python tooling test commands; `scan_portability.py --repo-root . --output ./artifacts/modules-decoupling/20260910-baseline/portability-scan.json --tracked-only`; enforcement with the canonical baseline and no write flag | PASS: tooling 6 + 4 tests; 5627 files / 29249 findings scanned; 14403 reviewed executable-source findings unchanged. No baseline rewrite. |
| Runtime analysis | CodeAnalytics `snap-20260910124606-06a9d455`, five scoped projects, 443 documents, 1067 types, 9528 members | Source evidence; no blocking load error. Generated attribute warnings and partial factory-DI interpretation; not runtime PASS. |
| Persistence analysis | CodeAnalytics `snap-20260910124744-63054d66`, 11 scoped projects, 493 documents, 1467 types, 9427 members | Source evidence; no blocking load error. EF broad assembly interpretation/out-of-scope entities means reported entity count is not actual model membership. |

## Selected baseline proof to discover

Commands run from the repository root. Build the owning test assembly first and use `--list-tests --filter "FullyQualifiedName~<class>"`; confirm the counts below before executing the same filter with refreshed binaries. These are source-based expectations, not executed results.

| Suite / class | Expected cases | Actual / result |
| --- | --- | --- |
| Integration / CollaborationIntegrationTests | 2 | PASS: expected/actual 2, 2 passed / 0 failed / 0 skipped, 17 seconds; `collaboration-baseline.trx` / `.log` |
| Integration / ProjectStructureAgentRuntimeToolRoundTripIntegrationTests | 12 | NOT_RUN |
| Integration / ProjectStructureAssetEffectIntegrationTests | 6 | NOT_RUN |
| Integration / ProjectStructureWorkflowScenarioHarnessTests | 2 (each covers 21 scenarios internally) | NOT_RUN |
| Integration / ProjectStructureProcessRunRecordIntegrationTests | 5 | NOT_RUN |
| Integration / ProjectDeletionIntegrationTests | 16 | NOT_RUN |
| Integration / CrmHrApiIntegrationTests | 2 | NOT_RUN |
| Integration / LlmChatsApiPostgreSqlIntegrationTests | 10 | NOT_RUN |
| Integration / MigrationBootstrapIntegrationTests | 6 | NOT_RUN |
| Unit / HrAgentRuntimeToolProviderTests | 13 | NOT_RUN |
| Unit / SchedulerAgentRuntimeToolProviderTests | 3 | NOT_RUN |

## Gate and safety requirements

Follow [Testing](../../testing.md) and current CI exactly, with any isolated-output adjustment stated. During slices use affected production builds, focused discovery/execution and real PostgreSQL when persistence is affected. Protected-source closure requires the complete portability scan, reviewed ADDED/STALE deltas, and final enforcement without `--write-baseline`. Maintained documentation changes require `tools/Validation/Test-Documentation.ps1`.

Final cross-cutting closure explicitly requires the frozen product build, broad stable gate with its documented exclusions, separate browser and relevant LiveProcess/host gates, migration/upgrade/restart, portability, documentation, and real journeys A-H. Windows proof cannot claim Linux/macOS execution. Missing real providers or critical journeys prevents end-to-end success.

Use disposable database/storage/profile fixtures. Browser default fixtures isolate these; attaching through `CANDOITALL_PLAYWRIGHT_BASEURL` does not own the existing host's data. Scheduler firing requires a separate test-owned host with workers enabled because the default browser fixture uses `McpToolHost`. Do not expose copied user records to providers, touch existing schedules, or delete user data/volumes.

Build/test isolation uses the same Release configuration with `--artifacts-path ./artifacts/modules-decoupling/build`. During child-host test execution, set process-scoped `ArtifactsPath` to that absolute root and `CANDOITALL_TEST_CONFIGURATION=Release`; the existing Playwright child `dotnet run` then resolves the isolated Web executable. Keep those settings scoped to the tool process. SDK property evaluation confirmed this path without modifying projects; runtime verification is still required.

## Collaboration checkpoint and intermediate broad gate

Source base: signed documentation checkpoint `49ef77a3d` plus the staged Collaboration context/stamping change, source tree `a8e5ed489c4c86e1fdbd26d8d0b56781c5a36669` (index before evidence-text updates). Both changed production projects built directly in Release with isolated artifacts: Infrastructure and Collaboration each report 0 warnings / 0 errors. Evidence: `artifacts/modules-decoupling/collaboration/`. xUnit 2.9.3 runs through VSTest on the existing SDK; no test-platform change.

| Check | Discovery and result |
| --- | --- |
| Integration, `FullyQualifiedName~CollaborationIntegrationTests` | Expected/actual 4; PASS 4, failed 0, skipped 0, 28 seconds. Preserves two baseline cases and proves complete-schema mapping parity, historical records/restart and profile isolation on disposable PostgreSQL. `collaboration-integration.trx`. |
| Unit, `FullyQualifiedName~CollaborationDbContextTests` | Expected/actual 5; PASS 5, failed 0, skipped 0, 1 second. Exact bounded model/foreign-query rejection and save-overload GUID stamping matrix. `collaboration-unit.trx`. |
| Components, `FullyQualifiedName~MainLayoutCollaborationTests` | Expected/actual 1; PASS 1, failed 0, skipped 0, 15 seconds. Existing shell unread badge preserved. `collaboration-components.trx`. |
| Portability-static | PASS: tooling 6 + 4; complete staged-source scan 5630 files / 29250 findings; final no-write enforcement reports 14403 reviewed executable findings unchanged. No baseline rewrite. |
| Evidence secret scan | PASS: 13 text artifacts scanned, no oversized/unreadable files or findings, before broad execution. |
| Documentation | PASS: 208 maintained Markdown files after slice and evidence updates. |
| Broad stable gate | Trigger: shared persistence stamping and owner composition. Frozen revision d96ef27796cf7648cb42cfd42c2b832c0b432180. Stable aggregate build PASS, 0 errors / 3 existing component warnings. Discovery 10772; execution PASS 10827, failed 0, skipped 0: Components 1896, Integration 1412, Memory 196, AgentFramework Memory 22, Unit 7301. Run 2026-09-10 13:19:41-15:22:07 UTC, exact documented exclusions. Evidence: collaboration/stable-tests.log and five TRX files under collaboration/stable. This predates Memory and does not close the final gate. |
| Independent architecture/QA source review | No actionable findings. Explicit model membership, inherited save dispatch, complete mappings, profile binding and removed overload callers checked; runtime closure is evaluated separately. |

No schema migration or data copy is introduced by this slice. Complete migration mapping remains unchanged. Migration rehearsal, cumulative review, remaining owner changes and final gates are NOT_RUN. Complete implementation and end-to-end validation are **not complete**.

The Collaboration slice preserves current local-operator behavior. It does not establish a new membership/private-thread authorization contract: the current service stores participant identities but has no member-removal operation or per-thread authorization policy. Foundation QA-070's broader scenario therefore cannot be claimed PASS from these persistence and shell tests.

Runtime result names reconcile the complete discovery/execution difference: two MemoryOperationHandler theories expand 1 to 5 each; MemoryOperationAccessAuthorizer ownership dimensions expand 1 to 9; PluginCatalog preview expands 1 to 6; FloatingAgentChatSettingsValidator and MafFinalizerToolFactorySchemaCharacterization each expand 1 to 8; ProcessRuntimeToolPreflight routes expand 1 to 21. No discovered method is missing. Ignored reconciliation: collaboration/stable-discovery-expansion.json.

Captured broad-test output required artifact sanitation: synthetic secret-scanner/redaction samples and a disposable authorization fixture token in an intentionally rejected query-string request appeared in discovery/TRX text. Nineteen fragments in three files were redacted, preserving XML validity and every test ID/outcome, with value-free scan/fingerprint metadata retained. The initial scan's oversized integration TRX was subsequently scanned in full at a 100 MB limit. Final scan PASS: 31 text artifacts, no oversized/unreadable files or findings. This is artifact hygiene, not a source-test change or a claim about real-provider credentials.

## Memory applied-source checkpoint

Base: signed Collaboration d96ef27796cf7648cb42cfd42c2b832c0b432180. Applied source tree before this record update: `19f0cc204b15ac71c3e40ba4f8b170899558413c`. Every check below uses actual working-tree files and the normal isolated build root, without the candidate compiler overlay. Evidence root: `artifacts/modules-decoupling/memory/`.

| Check | Discovery and result |
| --- | --- |
| Direct builds | Memory.Persistence and Composition PASS, 0 warnings / 0 errors. Refreshed Memory test assembly PASS 0/0; Integration PASS with five preexisting analyzer warnings; Components PASS with three preexisting warnings. No SDK/package change. |
| Memory focused | memory.filter selects the 11 changed stable fixture classes, MemoryWorkerHostingTests and new MemoryDbContextTests. Discovery 77, execution PASS 85 / failed 0 / skipped 0, 5 seconds; two CrossCallerRoutes theories add eight runtime rows. Existing 392 assertions retained in migrated fixtures. memory.trx. |
| PostgreSQL/API | FullyQualifiedName~MemoryOwnerPersistenceTests\|FullyQualifiedName~MemoryProvidersApiIntegrationTests: discovery/execution 19, PASS 19 / failed 0 / skipped 0, one minute. New owner tests 3 plus existing API tests 16. memory-integration.trx. |
| Components | FullyQualifiedName~MemoryProvidersPageTests\|FullyQualifiedName~MemoryProviderOperationsPageTests\|FullyQualifiedName~MemoryProviderUiSurfacePageTests: discovery/execution 17, PASS 17 / failed 0 / skipped 0, 3 seconds. memory-components.trx. |
| Portability-static | PASS: tooling 6 + 4; complete staged-source scan 5633 files / 29250 findings; final no-write enforcement 14403 reviewed executable-source findings unchanged. No baseline rewrite. |
| Evidence secret scan | Memory PASS: 17 text artifacts, no oversized/unreadable files or findings. |
| Documentation | PASS: 208 maintained Markdown files after checkpoint/evidence updates. |
| Architecture/QA review | Independent review found no material issue or missed callers. Root reviewed the staged source/fixture diff. Exact model membership, all six save entry points, canonical mapping/JSON retention/readback and profile binding covered. |

Reproduce builds with `dotnet build <owning-project> --configuration Release --artifacts-path ./artifacts/modules-decoupling/build /m:1`. After refreshing each owning assembly, run `dotnet test <owning-project> --configuration Release --artifacts-path ./artifacts/modules-decoupling/build --no-build --no-restore --list-tests --filter <filter> /m:1`, then the same filter without `--list-tests`, adding a TRX logger/results directory. Set the scoped configuration/artifact environment above and the explicit disposable PostgreSQL fixture connection; each test leases its own database. The Memory filter file is reproducible from the listed changed fixture classes and the two named additions.

The exact Memory class selection is AgentMemoryProviderEndToEndTests, ManualMemorySourceIngestionTests, MemoryAsyncWorkerTests, MemoryContextPackValidationTests, MemoryEndToEndObservabilityProofTests, MemoryFeedbackHandleSecurityTests, MemoryOperationHandlerTests, MemoryProviderRuntimeContractTests, MemoryRuntimeCheckpointTests, MemoryRuntimePersistenceTests, MemoryWorkerLeaseTests, MemoryWorkerHostingTests and MemoryDbContextTests. Join `FullyQualifiedName~<class>` clauses with `|`.

No schema migration or data copy. The PostgreSQL lease test uses two independent service providers/connections in one process; it proves restart/expiry/forged ownership behavior, not a simultaneous distributed race. ExternalCognitiveMemoryLiveConformanceTests was adapted but is an opt-in LiveProcess test and was not selected. Final frozen product/stable/browser/live/provider/upgrade journeys remain open.

## Catalog owners and migration authority applied-source checkpoint

Base: signed Memory `f9908080ebe38f342ab4d1799fc7cef8481e3ce8`. Applied source tree before README corrections and evidence/baseline updates: `77f484cf2339fb57283a65d8f044bffc70d23d02`. Builds and tests use actual source files and the normal isolated build root without a compiler overlay. Evidence: `artifacts/modules-decoupling/catalog-owners/`. Siblings, host preservation, process-scoped Release/artifact configuration and disposable PostgreSQL connection remain as recorded above.

| Check | Discovery and result |
| --- | --- |
| Product build | Complete product solution PASS, 0 warnings / 0 errors, 2 minutes 29 seconds. Refreshed Unit assembly 0/0; Integration five existing warnings; Components three existing warnings. |
| Unit | Expected/discovered/executed 62, PASS 62 / failed 0 / skipped 0, 9 seconds. New Plugins mapping/stamping and migration-authority tests plus existing registry, database configuration, Resource connectors/catalog, CRM gateway and Plugin executor contracts. results/unit.trx. |
| PostgreSQL/integration | Expected/discovered 110, executed 115, PASS 115 / failed 0 / skipped 0, 8 minutes 12 seconds. The existing packaged-plugin preview theory expands one discovery to six cases. New TestLab, Resources, Plugins and migration-authority classes contribute three each; retained bootstrap, CRM/Structure, Plugin catalog and Resource Storage integration paths also pass. results/integration.trx. |
| Components | PluginsPageTests expected/discovered/executed 7, PASS 7 / failed 0 / skipped 0, 44 seconds. results/components.trx. |
| Portability-static | PASS: tooling 6 + 4, final complete scan 5640 files / 29260 findings, no-write enforcement 14401 reviewed executable-source findings unchanged. Initial two STALE findings were the deleted PluginSchemaInitializer and SchedulerPlannerSchemaInitializer case-policy allowances; removed those only (14403 to 14401), with no added executable finding. |
| Documentation and artifacts | Documentation PASS for 208 maintained Markdown files. Initial validation caught a TestLab README relative-link error, repaired and rechecked. Final evidence secret scan PASS: 23 text artifacts, no oversized/unreadable files or findings at a 100 MB limit. |
| Independent review | No material finding in migration authority cleanup or its PostgreSQL fixtures; root reviewed all applied owner/test changes and the baseline diff. |

The exact unit class selection is StorageObjectResourceConnectorTests, ResourceFileSourceCatalogTests, CrmHrResourceSourceGatewayAdapterTests, PluginsDbContextTests, AppDbContextMigrationAuthorityTests, AppDbContextModelRegistryTests, DatabaseConfigurationTests, BundledPluginWorkflowExecutorTests and PluginWorkflowExecutorBoundaryTests. Integration selects TestLabOwnerPersistenceTests, ResourcesOwnerPersistenceTests, PluginsOwnerPersistenceTests, MigrationAuthorityIntegrationTests, MigrationBootstrapIntegrationTests, CrmHrCrossModuleIntegrationTests, ProjectStructureAgentIntegrationTests, ProjectStructureAutomaticPlacementIntegrationTests, PluginCatalogIntegrationTests and ResourceStorageObjectIntegrationTests. Saved `unit.filter`, `integration.filter` and `components.filter` combine these classes with the exact documented stable exclusions. Reproduce with the Memory command pattern above against the owning Unit/Integration/Components project.

Fresh-schema checks verify the eight affected Plugin/Scheduler tables' columns, types, defaults, nullability, primary/foreign keys and indexes. Populated starting-migration fixtures retain eight representative records and historical extra indexes/defaults through two bootstraps/restarts; CRM lookup seeding preserves edited existing records and fills missing entries. OAuth invalid-callback/replay behavior and existing session/connection save boundaries remain. No new migration/data copy or physical mapping change is introduced. Removal of opportunistic runtime DDL repair does not establish support for manually corrupted, already-baselined databases.

Broader persistence/composition proof remains the final frozen gate; the earlier Collaboration gate is explicitly intermediate. These focused fixtures do not close provider/browser, complete transfer/lifecycle or journeys A-H.
