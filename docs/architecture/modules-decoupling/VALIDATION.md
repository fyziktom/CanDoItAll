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

## Collaboration slice in progress

Source base: signed documentation checkpoint `49ef77a3d` plus the staged Collaboration context/stamping change, source tree `a8e5ed489c4c86e1fdbd26d8d0b56781c5a36669` (index before evidence-text updates). Both changed production projects built directly in Release with isolated artifacts: Infrastructure and Collaboration each report 0 warnings / 0 errors. Evidence: `artifacts/modules-decoupling/collaboration/`. xUnit 2.9.3 runs through VSTest on the existing SDK; no test-platform change.

| Check | Discovery and result |
| --- | --- |
| Integration, `FullyQualifiedName~CollaborationIntegrationTests` | Expected/actual 4; PASS 4, failed 0, skipped 0, 28 seconds. Preserves two baseline cases and proves complete-schema mapping parity, historical records/restart and profile isolation on disposable PostgreSQL. `collaboration-integration.trx`. |
| Unit, `FullyQualifiedName~CollaborationDbContextTests` | Expected/actual 5; PASS 5, failed 0, skipped 0, 1 second. Exact bounded model/foreign-query rejection and save-overload GUID stamping matrix. `collaboration-unit.trx`. |
| Components, `FullyQualifiedName~MainLayoutCollaborationTests` | Expected/actual 1; PASS 1, failed 0, skipped 0, 15 seconds. Existing shell unread badge preserved. `collaboration-components.trx`. |
| Portability-static | PASS: tooling 6 + 4; complete staged-source scan 5630 files / 29250 findings; final no-write enforcement reports 14403 reviewed executable findings unchanged. No baseline rewrite. |
| Evidence secret scan | PASS: 13 text artifacts scanned, no oversized/unreadable files or findings, before broad execution. |
| Documentation | PASS: 208 maintained Markdown files after slice and evidence updates. |
| Broad stable gate | Trigger: shared persistence stamping and owner composition. Refreshed stable aggregate build PASS, 0 errors / 3 warnings in unchanged component tests (CS8602 twice, xUnit2031 once). Discovery established execution expectations: Components 1896, Integration 1407, Memory 180, AgentFramework Memory 22, Unit 7267 (total 10772). Execution in progress with the documented exact exclusions; compare executed totals and report skips independently. |
| Independent architecture/QA source review | No actionable findings. Explicit model membership, inherited save dispatch, complete mappings, profile binding and removed overload callers checked; runtime closure is evaluated separately. |

No schema migration or data copy is introduced by this slice. Complete migration mapping remains unchanged. Migration rehearsal, cumulative review, remaining owner changes and final gates are NOT_RUN. Complete implementation and end-to-end validation are **not complete**.

The Collaboration slice preserves current local-operator behavior. It does not establish a new membership/private-thread authorization contract: the current service stores participant identities but has no member-removal operation or per-thread authorization policy. Foundation QA-070's broader scenario therefore cannot be claimed PASS from these persistence and shell tests.
