# Agent model label repair and startup latency analysis

Date: 2026-08-31. Startup investigation is read-only: no startup pipeline, persistence, locking, provider execution, or durability code was changed. No new agent runs or model requests were launched for the analysis.

## Model label: repaired and deployed on 5214

The floating Chats list used `AgentDefinition.Model` directly. For an imported shared provider, that is an opaque routing identifier (`sp1...`), not a display name. The contributor now requests the existing provider reference data alongside agents and passes the associated provider into the compact presentation mapper. The mapper uses the existing `ProviderProfile.GetModelDisplayName` method for the visible label, accessible details and search text. Stored identifiers and execution routing remain unchanged.

Only these source files changed:

- [AgentParticipantPresentationMapper.cs](/C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentParticipantPresentationMapper.cs:54)
- [AgentConversationShellContributor.cs](/C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentConversationShellContributor.cs:194)

Validation: five existing focused component tests passed; Release Docker build succeeded. Browser verification on 5214 showed **gpt-5.6-luna** for Portfolio Architect, including searching by that name, with no console warnings/errors. The prior file-browser CSS repair still works. Only the idle 5214 container was replaced; its data mounts, environment and security/resource settings were preserved. 5032 and central 5210 were not restarted. The previous container is retained, stopped, as `candoitall-shared-providers-manual-client-a-1-before-agent-model-label-20260831`.

Image: `candoitall-app:agent-model-label-20260831`; digest `sha256:168dc1535d4134a6621db8e3eae8bdc0628c3439e62fc7daade24340691b6bdc`.

Proof: [screenshot](/C:/repositories/CanDoItAll/.artifacts/agent-label-startup-analysis/model-label-after.png), [test results](/C:/repositories/CanDoItAll/.artifacts/agent-label-startup-analysis/test-results/label-components.trx), [Docker build log](/C:/repositories/CanDoItAll/.artifacts/agent-label-startup-analysis/docker-build.log).

## Pass 1: Initial Performance Review

### Assessment and measurements

The strongest explanation for the repeated pauses is **awaited durable progress recording**, amplified by repeated filesystem case-sensitivity probes. The evidence does not show that each named component, such as Skills, needs several seconds of its own initialization work. Exact attribution between lock wait, validation, serialization, probe I/O and payload I/O remains unmeasured in live runs.

| Instance and existing run | Created to Planning | Created to first Run stage | Consecutive preparation log gaps |
|---|---:|---:|---:|
| Docker 5214, `f4b7e37d-84ac-4677-9ab4-489321c8a3a6`, Aug 31 07:45 | 2.542 s | 30.158 s | 2.071-3.203 s |
| Native 5032, `9320c5b5-b548-417c-b1ac-e1375a4a4b7e`, Aug 31 07:46 | 7.053 s | 18.216 s | 0.905-1.474 s |

Historical Aug 30-31 samples: all nine available 5214 runs reached first Run in **19.416-36.270 s**, median **25.696 s**. Six recent initial 5032 preparations took **10.263-18.216 s**, median **12.393 s**; one resumed segment was excluded. These are different runs/workloads, not a controlled Docker-versus-native benchmark.

The first Run entry is a runtime stage boundary, **not the exact provider network-send timestamp**. Provider preparation before run creation is excluded. Each log timestamp is assigned before its own persistence, so adjacent timestamps include the preceding log's durable commit plus intervening work; they are not pure durations of the named stage.

Evidence: [5214 metadata trace](/C:/repositories/CanDoItAll/.artifacts/agent-label-startup-analysis/5214-latest-stage-timings.json), [5032 run-detail endpoint](http://localhost:5032/api/agents/execution-runs/9320c5b5-b548-417c-b1ac-e1375a4a4b7e), [5032 offsets captured from that endpoint](/C:/repositories/CanDoItAll/.artifacts/agent-label-startup-analysis/5032-latest-stage-timings.json). Docker records are under the [organization workspace](/C:/repositories/CanDoItAll/.artifacts/shared-providers-e2e/client-a/data/workspace/data/scopes/organization/3dfd771ef0fef5ef9ff8845e3efa2580).

### Why progress recording is expensive

1. Each startup progress callback awaits [AppendExecutionLogAsync](/C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs:42), which waits for a persisted run update before publishing the notification.
2. The update holds the shared workspace lock, loads existing records and prepares/validates a transaction. An ordinary chat progress transaction changes eight files: pending journal, session, run, new log, execution index, usage index, workspace index and chat index; then removes the journal. The journal contains previous and target snapshots. See [transaction preparation](/C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs:957) and [commit](/C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs:1043).
3. [PhysicalFileSystemPathPolicyFactory](/C:/repositories/CanDoItAll/src/Foundation/CanDoItAll.Infrastructure/FileSystem/PhysicalFileSystemPathPolicy.cs:7) constructs a fresh policy on every call. Construction [probes case sensitivity](/C:/repositories/CanDoItAll/src/Foundation/CanDoItAll.Infrastructure/FileSystem/PhysicalFileSystemPathPolicy.cs:187) by creating a temporary file with WriteThrough, flushing it to disk, checking an alternate-case path and deleting it.
4. [DurableFileWriter](/C:/repositories/CanDoItAll/src/Foundation/CanDoItAll.Infrastructure/FileSystem/DurableFileWriter.cs:183) repeatedly constructs policies at direct boundaries and directory traversals. With an existing root and parent directories, a payload replacement performs **8 + 2D** constructions, where D is parent-directory depth below the managed root. Organization-scoped session files have D6 parents; run files D7; logs D8.
5. Reads also perform probe writes. [ReadTextAsync](/C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceJsonStore.cs:132) validates through nested paths twice. Collections are read again during [commit validation](/C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs:2752). Usage projections and global chat summaries are also rebuilt when only progress changed.

The Docker `/data` path is a Windows bind mount. Its filesystem cost can amplify this pattern, but no controlled mount comparison was performed. Current indexes are small: workspace 76 bytes, execution 341 bytes, usage 1,882 bytes, chat 15,250 bytes; latest run 62,267 bytes. Large indexes are therefore not the obvious current explanation.

### Isolated I/O measurement

An ignored scratch diagnostic called the unchanged production Release DLLs with synthetic 1 KiB payloads. It counted/timed public policy-factory calls; no live data was modified. Two warmups were excluded, then ten durable writes measured per depth.

| Existing parent depth | Mean durable write | Median | Policy constructions/write | Time in policy construction |
|---|---:|---:|---:|---:|
| D0 | 22.26 ms | 21.90 ms | 8 | 11.46 ms / 51.5% |
| D6 | 48.37 ms | 48.04 ms | 20 | 24.38 ms / 50.4% |

Equivalent case probes had medians around 0.92-1.03 ms with WriteThrough plus Flush(true), versus 0.43-0.56 ms without both. This supports repeated construction/probing as meaningful overhead; it does **not** establish fsync alone as the dominant cost. Half of this small-write microbenchmark is not a prediction of halving total agent startup.

Raw results: [D0](/C:/repositories/CanDoItAll/.artifacts/agent-label-startup-analysis/io-probe/timing-summary.txt), [D6](/C:/repositories/CanDoItAll/.artifacts/agent-label-startup-analysis/io-probe/timing-summary-D6.txt).

### What changed in this branch

Branch baseline `1625b336e` was established from reflog and merge-base with main/development. The progress-persistence path, durable writer, physical path policy, capability composer and execution adapter are unchanged from that baseline. Similar uniform delays already appear in native Aug 26 records. The same agent reached Run in 16.660 s then, versus 10.263-13.190 s in sampled Aug 30 runs. Historical records do not identify their exact running commits. **There is no controlled evidence that this branch introduced the entire slowdown.**

One definite branch change adds unnecessary preparation work: commit `3045385c7` changed [LoadRevisionAsync](/C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedAwareProviderRuntimeProfileSnapshotLoader.cs:114) from a narrow revision projection to a full profile load. Shared profiles involve three serial entity queries, publication parsing/hash validation and model-catalog copying. A normal warm chat turn still performs one revision probe. This occurs **before run creation**, so it cannot explain the repeated recorded stage gaps. The full-set revision method similarly materializes all profiles/imports/sources.

Provider history background processing added in `ee51b56f3` shares the workspace lock and could add contention. Existing traces do not establish that contention. Ordinary progress notifications do not create provider-usage history journal writes; only actual usage observations do. Do not attribute every progress delay to the new history hook.

### Recommended improvements, not implemented

| Priority | Proposed bounded change | Safety requirements |
|---|---|---|
| 1 | Reuse case-sensitivity facts within a durable operation instead of repeatedly probing the same unchanged root. | Keep fresh containment, symlink and mutation checks. Account for root replacement/recreation, mount/case-setting changes; do not permanently cache Unknown or authorization decisions. |
| 2 | Restore lightweight provider revision projections that combine profile/import/source revision tokens and relationship validity. Materialize the full profile only on change. | Preserve fail-closed availability, source policy/catalog invalidation, database-generation guards and dispatch-time secret resolution. |
| 3 | Reuse already validated snapshots while holding the same uninterrupted lock; avoid rebuilding unaffected projections. | Preserve complete persisted-state checks for crash recovery and prove equivalent concurrent-update behavior. |
| 4 | Consider batching startup progress commits only after defining their durability contract. | Higher risk: ordered events, cancellation/failure, approvals and crash behavior must remain explicit; flush before provider/tool dispatch and critical transitions. |

The first two are the best initial candidates for a focused optimization. Do not simply remove durable payload flushes, fire-and-forget progress callbacks, parallelize all startup stages, or split the history lock: these can change correctness and recovery guarantees.

Before applying an optimization, capture a controlled baseline with the same agent, workspace and storage on both instances. Measure lock acquisition, run loading, policy construction, journal/payload writes and recovery validation separately. Validate case/symlink/root-replacement security, concurrent writers, interrupted commits/recovery, cancellation and provider source/catalog invalidation. Report median and tail latency for repeated warm/cold runs; existing evidence does not justify a numerical end-to-end speedup promise.

## Pass 2: Deep Pattern Scan

A targeted scan of the four provider revision/cache files found no independent critical API-pattern issue beyond full materialization already described. No async-void, sync-over-async, per-call HttpClient/options, regex or repeated dictionary lookup problem was found. LINQ and bounded stack allocations did not warrant speculative changes. The actionable finding is repeated I/O/materialization on the preparation path, not generic allocation cleanup.

> ⚠️ **Disclaimer:** These results are generated by an AI assistant and are non-deterministic. Findings may include false positives, miss real issues, or suggest changes that are incorrect for your specific context. Always verify recommendations with benchmarks and human review before applying changes to production code.
