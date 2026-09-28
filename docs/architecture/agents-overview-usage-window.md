# Bounded Agents Overview usage

## Behavior and contracts

Implementation started on clean `development`, HEAD
`82a5b5a0133af31ea0420449905601c87c4f01ba`. That revision is provenance, not a
checkout/reset target. The actual starting source was inspected again. Work remains
uncommitted on that branch; final HEAD is unchanged. No branch switch, push or PR was performed.

A plain `/agents` selects Both and Last 7d. `usageScope` accepts `agents`,
`simple-chats`, `both`; `usagePeriod` accepts the following rolling periods:

| Value | Label | Inclusive lower boundary |
| --- | --- | --- |
| `7d` | Last 7d | `ToUtc.AddDays(-7)` |
| `14d` | Last 14d | `ToUtc.AddDays(-14)` |
| `1m` | Last 1m | `ToUtc.AddMonths(-1)` |
| `1q` | Last 3m | `ToUtc.AddMonths(-3)` |
| `1y` | Last 1y | `ToUtc.AddYears(-1)` |

The usage owner resolves one UTC upper boundary through `TimeProvider`. Every source
and detail dialog receives that same immutable `[FromUtc, ToUtc)` interval. Agent
occurrence is observation `CreatedAtUtc`; Chat occurrence is invocation `CompletedAtUtc`.
Old runs/conversations with recent usage remain eligible. Execution outcomes reflect
current related evidence, not a cross-store historical transaction snapshot.

The module owns desired versus accepted query identity, cancellation, generation fences,
route changes and dialogs. A render does not advance the clock. A refresh can advance
it; a failed refresh preserves the actual accepted interval and generation timestamp.
Changing selection or profile invalidates dialogs. Profile changes also fence owner
results before publication. Invalid UI periods normalize to 7d; API validation is strict.

The rendering library remains free of I/O. Workspace-wide catalog/runtime counters and
team shortcuts use `AgentRuntimeSummary`, composed from `GetDashboardAsync` and the
existing team owner. Header, summary and usage retain independent lanes. The default
page and its refreshes no longer call `GetAgentOverviewAsync` or its all-time usage
projection. Existing explicitly unbounded callers and shared-provider relays retain
their contracts. Bounded sources cannot fall back to their unbounded method.

Usage keeps workload/execution identity, canonical deduplication and conflicts, Unknown
inclusion only in Both, missing/unpriced evidence and recorded decimal cost. Aggregate
`ProviderUsageTokenCounts` properties are now **Int64**; canonical per-observation
contracts remain unchanged. Consumers generating typed clients should regenerate them.
JSON still uses integer values. Counts of observations/executions remain Int32.

## Public API

`GET /api/agents/usage` defaults to `usageScope=both&usagePeriod=7d`.

```http
GET /api/agents/usage
GET /api/agents/usage?usageScope=agents&usagePeriod=14d
GET /api/agents/usage?usageScope=simple-chats&usagePeriod=1q
GET /api/agents/usage?usageScope=both&usagePeriod=1y
```

With authorization enabled, every request requires `api.agents.read`; Both and Chats
also require `api.llm-chats.read`, checked before reading either dataset. Chat-only
permission alone does not grant access to this Agents route. Invalid, empty, differently
cased or repeated values return 400 using the existing error envelope. Missing identity
returns 401 and insufficient permission returns 403. Cancellation reaches both sources.

A 200 result contains `query` (selection, period, exact UTC window), `generatedAtUtc`,
`updatedAtUtc` (latest included evidence), totals, consumer/provider/model rows and
`sources`. Each source has state, coverage verification time and a safe error if relevant.
Enums serialize as integers, documented in OpenAPI. `isComplete` requires every selected
source to be present and Complete. Partial, Indexing and Failed sources remain explicit
in a 200 response, including when none can contribute. Empty sources never certify zero.
Unix epoch means no latest evidence, not verified coverage. An absent coverage timestamp
means unverified coverage. Successful empty selected sources do certify zero in that interval.

## File storage and rollout

The persistence-owned `usage-locators` index addresses canonical usage by UTC date. Its
versioned header contains generation, completeness, coverage timestamp and a hash per
bucket. A bucket contains validated relative canonical locators. No absolute machine
paths or observation bodies are duplicated. Request History is deliberately not used as
the usage source: it can omit secondary observations and substitute request attempts.

The existing JSON mutation helper publishes the locator before the canonical write and
history journal commit, under the existing workspace coordination lock. A crash can
leave an extra candidate or an invalid bucket/header pair; it cannot publish canonical
evidence first and silently omit it from a complete index. Hash mismatch makes coverage
incomplete. Recovery keeps the existing execution/deletion journals and lock ordering.
Canonical replacement/import, updates and orphan moves use the same helper. Deletes and
timestamp corrections leave conservative stale locators, checked against canonical data.
Only included observations' referenced runs are read, once per run, for current outcomes.
Legacy empty observation IDs use a stable hash of their canonical relative locator.

Normal indexed reads open selected date buckets and candidate usage files, with no run
or usage directory discovery. There is still **O(historical dates)** header deserialization,
plus conservative candidates in selected buckets. Daily bucket rewrites also grow with
that day's locator count. This is a read optimization, not a claim of constant metadata
size or a production-scale write-throughput benchmark. It opens no separate transcript,
approval, artifact or tool-receipt files. Selected canonical `run.json` records can still
contain large embedded execution journals; the measured cost is documented below.
Existing compact global runtime indexes remain workspace-wide. Pending canonical
transaction recovery remains mandatory before reads.

Initialize each actual workspace partition explicitly after upgrading **all writers**:

```powershell
$workspaceRoot = '<workspace root for the active database profile>'
$profileKey = '<active profile GUID as 32 hexadecimal digits without dashes>'
dotnet run --project ./tools/UsageIndex --configuration Release -- $workspaceRoot organization $profileKey
```

The application uses the active profile's Organization scope. For an explicitly Sandbox
workspace omit the scope arguments; project/tenant/process partitions accept their own
kind and key. Do not index another profile or the unscoped root by accident.

The maintenance command performs the unavoidable initial full **metadata discovery**
under the workspace lock, then processes at most 100 canonical usage payloads per lock
lease (the maintenance API supports 1–1,000). Its durable cursor resumes after Ctrl+C or
restart; canonical writes between batches participate in the same generation. Coverage
becomes Complete only after the discovered records have been processed. No ordinary usage
GET starts this backfill. Until then the UI/API explicitly report Indexing.

Use `--rebuild` for a missing/corrupt/old-version index. It reconstructs a new generation
from canonical evidence. An inconsistent bucket blocks affected writes explicitly until
repair, rather than allowing an omitted committed observation. Old derived generations
are retained; this change does not add automatic index-directory pruning.

For pre-split legacy storage, run the command with `--migrate-legacy` before serving the
Overview. It invokes the existing canonical migration explicitly and then indexes it.
The Overview catalog/runtime lanes use a storage-owned readiness guard and report an
unavailable summary instead of initializing a legacy archive. The runtime lane also
refuses a missing or old summary index that would otherwise scan every run; header
reads remain independent of that summary-index check. The explicit maintenance option
repairs the runtime index as well. Maintenance rejects both legacy storage layouts
until migration, so it cannot certify a legacy single-file archive as empty.
Unrelated callers retain their existing initialization behavior. The bounded usage
source also never migrates or scans an unindexed archive.
After restoring external canonical files or running an older writer, rebuild the index;
mixed-version writers that do not publish locators are unsupported.

## Relational storage

Simple Chat filtering occurs in SQL before joining the operation/conversation/revision
and before the narrow no-tracking projection is materialized. Migration
`20260927120448_AddInvocationCompletionUsageIndex` adds
`IX_LlmChats_InvocationRecords_CompletedAtUtc`; its model snapshot change is only that
index. Apply it with the normal application migration authority before enabling the
bounded API. The preceding-migration-to-current upgrade was executed in PostgreSQL 18.6.

## Validation and measured storage work

Measured on Windows, .NET SDK 10.0.303/runtime 10.0.4, optimized Debug binaries,
2026-09-27. PostgreSQL was an owned disposable 18.6 container on loopback port 62652
(`server_version_num=180006`), separate from the development database. Fixtures are
synthetic; no provider calls or retained user history were used. Other validation work
ran concurrently, so latency and process-wide allocation deltas are illustrative.

The file fixture holds two included observations constant (one orphan and one belonging
to a two-year-old run), plus exact-boundary candidates. Actual JSON diagnostics record:

| Old observations | Legacy JSON / bytes | Indexed JSON / bytes | Indexed usage / run payloads | Legacy ms | Indexed first / repeat ms |
| --- | --- | --- | --- | --- | --- |
| 20 | 46 / 53,507 | 9 / 7,677 | 4 / 1 | 127.52 | 25.21 / 24.65 |
| 400 | 806 / 965,509 | 9 / 40,737 | 4 / 1 | 4,561.48 | 49.53 / 95.39 |

Every indexed read returned exactly two observations. The nine reads comprise one header,
three date buckets, four boundary candidates and one referenced run. The extra bytes at
400 are compact historical-date metadata. Separate first-time bootstrap took 1.62 s and
55.94 s; it is not part of a GET. These end-to-end store timings include lock acquisition
and bound the uncontended lock duration; no independent lock-duration probe was added.
Source inspection confirms zero directory enumeration on the ordinary bounded read path.

An additional local probe seeded the same data in one process, exited, then read the
persisted index in a new process: 369.44/21.55 ms (first/repeat, 20 old) and 434.52/56.68 ms
(400 old), always two observations and one run. First process-wide allocation deltas were
3.63/3.76 MB, including startup/JIT; repeats were 130/237 KB. This is a fresh application
process, not a flushed OS cache. Probe source and output remain ignored under
`output/usage-window/fresh-process/` and `fresh-process-results.log`.

PostgreSQL materialized 24 then 2,004 rows through the legacy source, versus **two** through
the bounded source with 20 then 2,000 old invocations. Bounded first/repeat timings were
17.09/2.27 ms and 3.24/2.72 ms; allocation deltas 369/142 KB and 115/123 KB. Captured SQL:

```sql
WHERE l."CompletedAtUtc" >= @window_FromUtc
  AND l."CompletedAtUtc" < @window_ToUtc
```

`EXPLAIN (ANALYZE, BUFFERS)` used the new completion-time index, returned two rows, touched
15 shared buffers overall and executed in 0.064 ms. No message bodies or system prompts
were selected. The database boundary uses microsecond-representable timestamps.

Focused discovery and execution used the actual solution filters below after optimized
Debug builds. TRX files and captured output remain ignored in `output/usage-window`.
Configure the isolated PostgreSQL test connection using [Testing](../testing.md)
before running the component, integration and browser lanes.

```powershell
dotnet build ./tests/Solutions/CanDoItAll.Tests.Stable.slnx -c Debug -p:Optimize=true --no-restore /m:1
dotnet build ./tests/Solutions/CanDoItAll.Tests.Playwright.slnx -c Debug -p:Optimize=true --no-restore /m:1
$unit = 'FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.WorkflowExecutorTests.WorkflowLlmComponentInvokerUsesProviderUsageObservationsForWorkflowUsage|FullyQualifiedName~AgentFrameworkSimpleChatsRouteTests|FullyQualifiedName~AgentsOverview|FullyQualifiedName~AgentsWorkspace|FullyQualifiedName~ProviderUsage|FullyQualifiedName~WorkflowUsageAnalyticsRedGateTests|FullyQualifiedName~OverviewSandboxContextTests|FullyQualifiedName~AgentUiCompiledBoundaryTests|FullyQualifiedName~HostPlatformTestClassificationTests'
$components = 'FullyQualifiedName~AgentsHomePageTests|FullyQualifiedName~AgentsOverviewEffectLifecycleTests|FullyQualifiedName~AgentsOverviewReadLifecycleTests|FullyQualifiedName~AgentsOverviewSurfaceTests|FullyQualifiedName~ProviderAdministrationLayoutTests|FullyQualifiedName~OverviewSandboxTests'
$integration = 'FullyQualifiedName~AgentsUsageApiTests|FullyQualifiedName~ProviderUsageWindowPostgreSqlTests|FullyQualifiedName~AgentsWorkspaceQueryTests|FullyQualifiedName~ApiDocumentationCoverageTests|FullyQualifiedName~LlmChatsDatabaseTransferIntegrationTests'
$durability = 'FullyQualifiedName~FileSandboxWorkspaceChatRunCommitRecoveryIntegrationTests|FullyQualifiedName~FileSandboxWorkspaceExistingRunUpdateRecoveryIntegrationTests|FullyQualifiedName~FileSandboxWorkspaceGenericNewRunCommitRecoveryIntegrationTests|FullyQualifiedName~FileSandboxWorkspaceUsageProjectionIntegrationTests|FullyQualifiedName~FileSandboxWorkspacePreparedCommitReadIntegrationTests|FullyQualifiedName~FileSandboxWorkspaceStoreLockIntegrationTests|FullyQualifiedName~ProviderHistoryPersistenceIntegrationTests'
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx -c Debug --no-build --no-restore --filter $unit /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Components.slnx -c Debug --no-build --no-restore --filter $components /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Integration.slnx -c Debug --no-build --no-restore --filter $integration /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Integration.slnx -c Debug --no-build --no-restore --filter $durability /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Playwright.slnx -c Debug --no-build --no-restore --filter 'FullyQualifiedName~AgentsUsageWindowBrowserTests' /m:1
```

| Lane | Filter/topics | Result |
| --- | --- | --- |
| Unit | Usage/session/query/route/workflow behavior, sandbox context, compiled UI boundaries, host classification | 174 passed, 0 skipped |
| Components | AgentsHomePage, Overview surface/read/effect lifecycles, ProviderAdministrationLayout, UI sandbox | 119 passed, 0 skipped |
| Integration | AgentsUsageApi, PostgreSQL window, workspace adapter, Chat database transfer, ApiDocumentationCoverage | 39 passed, 0 skipped |
| Browser | `AgentsUsageWindowBrowserTests` | 1 passed, 0 skipped |
| Durability | Existing file commit recovery, locks, usage projection, prepared reads, history persistence | 105 passed, 0 skipped |
| Portability tooling | Both required Python test scripts | 6 + 4 passed |
| Portability static | Full scan including new protected files, reviewed baseline, final enforcement without `--write-baseline` | Passed, 15,132 allowances |
| Documentation | `tools/Validation/Test-Documentation.ps1` | Passed, 239 maintained Markdown files |

The real Chromium journey covers all periods/workloads, a year read held behind the real
workspace lock, rapid replacement, accepted-window dialogs, refresh, history and provider
navigation, mobile wrapping and keyboard selection. Page errors and Blazor error UI were
checked. Screenshots and a trace are in `output/playwright/agents-usage-window/`.

`CanDoItAll.slnx`, the Stable and Playwright solutions, and `tools/UsageIndex` build in
optimized Debug; the optional maintenance executable also builds in Release. After an
authorized restart of the operator's 5032 host, the standard product and Stable Release
builds pass. The host is running the rebuilt Release binary on its original database
profile and workspace. Shared persistence/schema changes and merge preparation trigger
the broad stable gate; 15,010 cases were discovered and 15,065 executed. Components
passed all 2,348 cases in 27 minutes 20 seconds. The serial command was then stopped
during Integration; that interrupted portion has no passing claim. The completed
integration run used the existing CI `Invoke-TestShard.ps1` in three whole-class shards
with separate process-local temporary directories, the same Release assemblies and
stable filter, and an isolated PostgreSQL server with unique per-test databases.

| Broad integration shard | Executed | Initially passed | Fixture failures | Minutes |
| --- | ---: | ---: | ---: | ---: |
| 1 | 979 | 972 | 7 | 123.75 |
| 2 | 1,115 | 1,109 | 6 | 114.05 |
| 3 | 1,090 | 1,090 | 0 | 126.50 |

Unit passed 9,308 cases and both Memory projects passed 22 and 203 cases, with zero
failures or skips. Deferred theories account for 34 extra Unit rows, 16 Memory rows
and five Integration rows beyond discovery; the latter are the six plugin-preview
simulation rows in `PluginCatalogIntegrationTests`. The broad run therefore has
15,052 initial passes and 13 fixture failures. After rebuilding the five repaired
fixtures in Release, all 33 affected cases passed with zero skips in 3 minutes 9 seconds.
Every original failed test name has matching passing follow-up evidence. All 15,065
selected cases therefore have passing evidence; the original failed run remains
retained and is not described as a single passing invocation.
Linux/macOS gates have not run locally. The completed-index maintenance command was also
exercised against the synthetic workspace and returned Complete without rebuilding it.

The final Windows Release runtime-portability script passed its source/assembly stamp
checks and all 482 catalog cases (436 unit, 45 integration, one browser). The Overview
browser journey separately passed in Release. PostgreSQL migration/restart passed all
10 cases. Logical restore initially failed because the local PATH resolved PostgreSQL
16.3 clients; it passed with isolated official 18.6 clients matching the disposable
18.6 server. No machine-wide PATH or installed database was changed.
`Test-CorePortabilityHeadless.ps1` also passed its Windows publish, browser smoke and
two startup/restart cycles. The Windows PowerShell 5.1 installer checks passed.
The canonical build stamp and all 482 cases were rerun successfully after the fixture-only
edits. The final integration rebuild passed with eight existing analyzer warnings; the
product rebuild passed with zero warnings. A file-copy lock required briefly stopping
5032, which was then restored on its original profile and verified Healthy in the browser.

The supplied main-branch CI failure is covered in [Testing](../testing.md#platform-split).
The MCP fixture now allows 30 seconds for functional protocol operations and reports
startup/initialize elapsed time; its deliberate hung-operation case keeps five seconds.
Production timeouts and protocol assertions are unchanged. Five exact peer-ping repeats
passed after the repair. The remote job has not been rerun, so local success is not
reported as a green remote CI run. A fresh fetch confirms application `main` and the
starting `development` HEAD have identical source trees; the two extra main commits
are merges. Components matches the CI source commit and FileTools matches its source tree.

The broad integration run also exposed obsolete fixture assumptions introduced by the
new completion-time migration and usage locator index. Four migration fixtures now
compare the complete applied chain with the available chain, retaining their explicit
historical checkpoints, downgrade protections and canonical-data assertions. Admission
read-budget tests require exactly one typed usage-index header read and retain their
checks against historical run and usage payload reads. Their budgets are 12 reads for a
new session and 16 for an existing terminal session, independent of 4 versus 96 historical
runs. The rebuilt 33-case follow-up passed, including the typed header-read checks;
the original broad run's failures remain in its evidence.

### Bounded parallel loading on the existing 5032 workspace

The subsequent latency investigation used the same persisted data and a fixed UTC
storage interval, with five reads per variant in a fresh process. No provider calls or
canonical history changes were made. These are workstation measurements with normal
OS file caching, not a disk-cache-flushed benchmark.

| Read path | First read | Median of five reads |
| --- | ---: | ---: |
| Sequential baseline | 6,207 ms | 5,445 ms |
| One reader, one path policy per batch | 3,824 ms | 2,565 ms |
| Two readers, one policy per batch | 2,791 ms | 1,603 ms |
| Four readers, one policy per batch | 1,795 ms | 1,003 ms |
| Eight readers, one policy per batch | 1,552 ms | 1,163 ms |

Every sample has the same full evidence SHA-256 fingerprint, 2,127 observations,
115 referenced runs, 2,248 JSON reads, and 265,623,026 bytes. Four readers provide the
best measured median; eight readers consume more CPU. The selected payloads still
include 210 MB of canonical run data, so this change does not claim smaller payloads
or bounded memory independent of the selected interval. Measured per-read allocations
remain approximately 666–721 MB including the complete canonical evidence graph.
Reducing embedded journal materialization would need a separate narrow evidence contract
and equivalence proof; the measured change keeps those canonical deserialization semantics.

The original path-policy factory creates, flushes, and deletes a case-sensitivity probe
each time. A thousand real path checks took 1,292 ms with new policies versus 284 ms
with one policy. Each batch now creates a fresh policy and still validates containment
and every ancestor/leaf against links on **every** read and retry. There is no global
path-validation cache. The ordinary single-file and durable-write paths are unchanged.

The batch owns at most four readers through `Parallel.ForEachAsync`. Each reader writes
to its own result-array slot; aggregation and identity assignment remain sequential in
canonical locator order. Referenced runs are deduplicated before the second batch.
The await drains started readers, including cancellation and failure, before the
enclosing instance gate and cross-process workspace lease can be released. Missing,
corrupt, out-of-window, and interrupted-index evidence retain their existing semantics.

Five actual HTTP reads on 5032 improved from a 9,293 ms median to 3,750 ms (60% less
elapsed time), including the complete API path. Totals, rankings, provider/model groups,
and source completeness match the baseline. Real Blazor refreshes completed in 1,847,
1,649, and 1,762 ms. Browser checks also verified the year total of 3,301, the accepted
year interval in the provider dialog, Chats-only year total of 30, and rapid replacement
back to Both/7d with 2,127 observations. The rebuilt host has no visible Blazor error.
After the final validation rebuild, a fresh 5032 page accepted its seven-day usage in
3,423 ms, again showing 2,127 observations, 109,185,689 tokens and $36.62, with no partial
or error banner and no page error. All catalog metrics also finished loading. This
fresh-page check is separate from the paired five-sample API comparison. Its reviewed
screenshot is `output/playwright/agents-usage-window/live-5032-final.png`.

Storage regression filter:
`FullyQualifiedName~FileProviderUsageWindowTests|FullyQualifiedName~FileSandboxWorkspaceJsonBatchTests`:
19 discovered and passed. The tests cover the concurrency ceiling, ordered results,
missing files, cancellation/failure drain, workspace-lock retention, and a symlink
introduced after policy creation. Timing figures are evidence, not test deadlines.

Pass 1: Initial Performance Review identified serial canonical reads and repeated
case-sensitivity probes. Pass 2: Deep Pattern Scan checked the four read/projection
owners: no async-void, sync-over-async, per-record `Task.Run`, implicit string comparison,
culture casing, chained replacement, regex, new HTTP client, blocking whole-file I/O,
or stack allocation. All five classes are sealed; serializer options are reused and
streams already use asynchronous I/O. Four list allocations, two dictionary allocations,
and ten LINQ sites support the selected-evidence aggregation; no additional micro-change
was justified. Raw measurements and scan recipes are under ignored
`output/usage-optimization/`; the reviewed screenshot is
`output/playwright/agents-usage-window/live-5032-optimized.png`.

## C# Architecture Gate Result

Status: Pass for local architecture and validation closure. Remote CI and Linux/macOS
runtime validation remain unverified.

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Informational | New types have bounded responsibilities | Usage query/value types, persistence locator index and maintenance/readiness guard, module profile fence, runtime summary | None |
| Informational | Existing graph cycles are outside this change | CodeAnalytics found Module/Hosting and image-tool/builder cycles; no project-reference cycle in five inspected owners | Do not expand them |
| Validation | Automatic impacted-test resolution did not complete | CodeAnalytics snapshot/dependencies/dashboard succeeded; impacted-test call was stopped after prolonged non-completion | Source-based caller/test map used above |
| Informational | Existing JSON store size warning | Fresh scoped snapshot reports 464 lines/34 members; batching remains in the existing file-I/O owner | No unrelated extraction or new policy layer |
| Informational | Local validation complete | All 15,065 Stable cases have passing evidence after the 33-case repair run; the refreshed 482-case runtime gate passed | Run remote CI after push |

Dependency direction: Usage owns contracts; Core maps canonical evidence; Persistence
owns file access and durability; SimpleChats.Persistence owns EF translation; the module
owns Blazor effects and profile validity; the Web API calls the same usage owner. The
only new project is the optional maintenance executable referencing Persistence.
No production project reference was added to reverse these boundaries.

Partial-class policy: no new runtime partial split. The EF migration/designer pair is
generated framework structure. The index is a separate cohesive persistence type and
is invoked through the existing canonical JSON writer.

Testability proof: fixed-clock/identity/overflow tests use pure sources; filesystem and
relational behavior have their own real-storage tests. Negative coverage includes missing
and corrupt indexes, interrupted publication, concurrent writes, strict API permission
failures, request cancellation, profile replacement, rejected stale results and dialogs.
The five-owner CodeAnalytics snapshot is `snap-20260927115025-167d76db`; source inspection
also traced unchanged legacy detail methods and current-profile forwarding.
The optimization snapshot is `snap-20260927145231-2272c581` (Core, Persistence, Usage,
MCP; 278 documents). Its dependency query has no project cycle. Existing Core type
cycles do not involve the batch reader or usage locator index. No project reference
or production MCP behavior changed.

Closure decision: production proof, broad coverage, repaired fixtures and the refreshed
runtime gate are verified locally. The original broad failures remain visible in the
evidence. Remote CI and Linux/macOS checks are not claimed as passed. Changes remain
uncommitted on `development`; no push or merge was performed.

