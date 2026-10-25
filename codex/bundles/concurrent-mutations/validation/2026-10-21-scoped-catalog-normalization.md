# Scoped catalog normalization: measured checkpoint

The bounded normalization memo is implemented, but this checkpoint is not fully qualified. The 192 preceding correctness cases pass. Four actual-host approval/restart cases now pass under bounded request tracing; earlier 30-second failures remain recorded. The measured read repeat still exceeds one second in one of six shapes. Continue diagnosing before downstream activation.

## Change and architecture

The [design](../plan/P09-scoped-catalog-normalization.md) adds one memo per existing file-store instance. Raw catalog caching still reads and hashes current bytes. Reuse requires the exact immutable raw catalog, unchanged resolved template roots and hashes of every consumed seed file. Capture includes manifests, configuration, instructions and inline assets. Policy, invariant, metadata and publication checks still run on every locked read. Missing, denied or changed inputs require a fresh normalization; errors cannot return cached data.

The new seed-input reader owns bounded input capture; the normalizer owns pure-result reuse and revision-independent payload comparison removed from the main store. Loader delegates carry reads explicitly. There is no new project, DI lifetime, global cache or authorization cache. Hot reload disables normalization reuse.

The raw cache retains at most 16 entries / 64 MiB estimated weight. One memo accepts a raw entry estimated at most 8 MiB and retains both raw and normalized references. It may keep the raw value alive after raw-cache eviction, so it can add roughly two eligible catalog graphs, not just one 8 MiB graph. Input identity is bounded to 512 consumed files / 8 MiB. These are retention estimates, not peak heap bounds.

## Validation and failed evidence

Direct standard Release Persistence and Web builds passed, followed by refreshed Unit and Integration outputs. The first Integration build had two strongly typed secret-reference mistakes in the new test; only that test changed before the successful rebuild. Full source scan, reviewed baseline and final no-write enforcement pass with 15,665 findings. Two added and one stale fingerprints were reviewed: delegated asset reading and normalized input paths. Rendering, validation-tool and documentation checks passed before execution.

Candidate A executed 196 of 208 cases: 194 passed, two approval HTTP cases timed out, and twelve later cases were not started. All 192 preceding owner, seed, publication, current-authority, narrow-reader, coordinator and review cases passed. The new tests include equal-size/equal-time catalog revocation, current policy denial after warming, deletion/corruption/repair, root separation, actual inline assets, failures, retained bounds and concurrent readers. External CPU saturation was observed during this run; that is context, not evidence that the failures were harmless.

Candidate B repeated the four HTTP restart cases on byte-identical source and binaries after the CPU-heavy unrelated test finished. Three passed; StepSnapshot approval timed out on its initial POST at the unchanged 30-second deadline. The remaining twelve cases did not start. This prevents attributing all failures to the earlier external load.

Candidate C deliberately runs those twelve previously unstarted cases for diagnosis, preserving the original assertions. It does not supersede or qualify the failed approval. All twelve cases executed: eleven passed and the RunSnapshot / 128-step latency case failed. Strict transport, accounting refusal and lost commit acknowledgement passed. Across the unchanged candidate, the latest results cover 206 passing distinct cases and two unresolved failures out of 208; this is not an all-green run.

## Isolated read-cost observations

All six shapes completed two warmups and five samples. They retained 27 Process SQL commands, one provider invocation, unchanged child revision, zero Project writes and the prohibition on execution-factory construction in the selected GET. Five latency cases pass. RunSnapshot / 128 steps fails: two samples take 3,312.20 and 3,085.00 ms; its other three take 612.00â€“884.18 ms. No sample is discarded and the one-second limit is unchanged.

| Mode | Steps | Median ms | Maximum ms | Median process allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| RunSnapshot | 1 | 771.23 | 805.69 | 43,440,680 |
| RunSnapshot | 128 | 884.18 | 3312.20 | 43,795,960 |
| RunSnapshot | 1024 | 780.86 | 857.23 | 45,829,368 |
| StepSnapshot | 1 | 771.44 | 986.91 | 43,496,048 |
| StepSnapshot | 128 | 843.24 | 947.75 | 43,963,888 |
| StepSnapshot | 1024 | 872.93 | 921.14 | 45,812,840 |

Median whole-test-process allocation is about 43â€“46 MB, compared with 79â€“81 MB in P08, a reduction of roughly 44â€“45%. Those include concurrent process activity, not just request allocation. Host timing varies: the unchanged component algorithms are also faster in this run, so timing differences are not a controlled causal speedup claim. Sustained concurrent capacity with background recovery remains open.

The 31-Agent / 734,165-byte component diagnostic still allocates about 13.13 MB for normalization (including seed construction), 5.93 MB for seeds and 2.95 MB for equivalent comparison. The optimization skips repeated work; it does not make those algorithms allocation-free.

## C# Architecture Gate Result

Status: Pass with follow-up for responsibility and dependencies; blocked for dependent activation by the measured HTTP failures.

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Blocking | Actual approval can exceed its deadline | Retained A and B failures | Request-level diagnosis, correction and original HTTP regression proof |
| Blocking for capacity | One isolated read shape exceeds one second | All 30 retained samples | Explain and qualify the tail without dropping samples or relaxing the bound |
| Informational | Seed input reader has 11 members | 72-line cohesive owner | Accepted; no unrelated responsibilities |

### Dependency direction

Persistence snapshots `snap-20261021203734-f5942be3` and `snap-20261021204838-8e97ad3a` retain the same existing type cycle. Documents change 47 to 49, types 136 to 140, members 1,454 to 1,484, findings 52 to 53. The ten actual project references are unchanged; scoped edges omit outside projects. Existing NuGet audit diagnostics were reviewed; successful direct builds are compilation evidence.

### Partial-class policy and testability

No new partial class or project. The main store shrinks from 2,561 to 2,546 lines and keeps 157 members. New isolated owners use explicit stream/root/normalization delegates; pure tests cannot construct the runtime. Real-store tests establish current-file and current-policy behavior.

### Closure decision

Keep producer activation gated. The next implementation is request-level approval diagnosis, then the separate reviewed accounting-recovery design. Parent cancellation and snapshot refresh, Workflow/settings/UI, remaining writers, sustained traffic and broad Stable closure remain open. SPA remains on `codex/concurrent-mutations`, without main merge or push.

## Identity, resources and retained proof

All A/B/C executions use source manifest `B58D9D308DD1EBD197F6D8296F4EF72C078E59560A08CE348B95C28BFDB14FFE` (9,898 inputs) and runtime manifest `4F11DB2E6E98AD7A009FDA5DC725F3B652572A375C46BA61C9C18A30EFD04F06` (7,013 files). Direct build, test-only correction and baseline bridges are retained. A and B verified unchanged bytes after execution and reconcile zero new surviving processes, database names or temporary entries. C also verified unchanged source/runtime bytes. Its 383.34-second observer covers all four test commands and reconciles zero new surviving processes, database names or temporary entries.

B's first observer attempt failed before startup because the ownership sidecar used `path` rather than `root`; original ownership preceded directory creation and is retained with the schema correction. C's first observer launch reused a PowerShell process with an already loaded deadline type; it failed before starting a child and was retried in a fresh process, preserving the pre-run baseline. One C enforcement invocation used a nonexistent script name; the corrected existing enforcement passes. These are explicit preflight corrections, not test passes or automatic approval denials.

Retained local proof is secret-scanned; generated proof remains ignored by Git:

- [Candidate A](../proof/2026-10-19-planning-authority/c4-scoped-catalog-normalization.json): 428,752 bytes, `3084E2B8273B96DD89779FDF19E4F29CE23DF67025AEBDA8E3A95F539446F942`.
- [Candidate B](../proof/2026-10-19-planning-authority/c4-scoped-catalog-normalization-http.json): 43,146 bytes, `4CBDDBFCC39405C8317526FB56B64AD3ECD347B09D70F93DD3EF9D7FE926B85C`.
- [Candidate C and all measured rows](../proof/2026-10-19-planning-authority/c4-scoped-catalog-normalization-cost.json): 78,663 bytes, `E471A4ADE383BCC09AF286332E42B9B13A2579B6871602530D0D6CAA6CA8F14C`.

Historical cleanup denials remain in [resources](../resources.md). Automatic approval review rejected those deletions with â€śblocked by policyâ€ť; they have not been retried. Zero new fixture deltas do not mean historical cleanup is complete.


## Measured diagnostic successors D, E and F

D changes only the existing HTTP test's bounded request diagnostics (plus documentation), with byte-identical production to A/B/C. All four original approval/restart cases pass in 167.65 seconds. Initial approval POSTs take 12,439 and 12,856 ms; rejects take 3,360 and 3,876 ms. Each approval records 1,637 completed activities without truncation. File waits are short in this run (maximum individual coordination/key waits below 27 ms); these observations do not reproduce or explain the earlier timeouts. The 55 visible CPU samples have median 8.27% and maximum 12.98%. This measures visible processes only.

E repeats the six unchanged acceptance shapes on D's exact source and binaries. Five pass; StepSnapshot/1024 fails with samples 759.81, 1075.17, 1250.26, 696.67 and 568.15 ms. All 30 samples and safety assertions remain retained: 27 Process SQL commands, one provider invocation, unchanged child revision, zero Project writes and no execution-factory construction in the GET. Visible CPU has median 7.70% and maximum 12.40% across 61 samples, so earlier CPU saturation does not explain this repeat. No threshold was relaxed.

F executes the three existing profiled GET shapes, all passing their correctness assertions in 108.43 seconds. Its 15 samples remain diagnostic, not finite-latency passes. Five JSON comparisons / ten catalog serializations take median 18–43 ms per request; they overlap and must not be summed. Whole-process allocation remains 43–47 MB. Seed construction and file validation are not separately timed yet; bounded activities are the next measurement before any functional optimization.

D/E/F use source manifest `0C13023497F2C3AE0D4C43F9637A29FAA96F464D3BAF915AF6652C46733A5340` (9,899 inputs) and runtime `91027C089E2ECEE9ABDB31464244A6873F47B7E0509EBB4E467097C475D239F2` (7,013 files), verified unchanged after each run. D refreshed standard Release Integration, ran a full source scan and final no-write enforcement (15,665 findings); E/F used the same protected source and reran enforcement. Resource observers cover every selected test command and reconcile zero new surviving processes, database names or temporary entries. Earlier denied cleanup remains unresolved.

D's first CPU sampler failed before tests on a process with null StartTime; the initial observer closed with zero execution commands and no resource delta. The corrected sampler skips inaccessible/null identity observations. The original helper and preflight records remain retained. An initial unsupported outer deadline was rejected before a child started. These are harness preflight corrections, not approval-review denials.

- [D approval diagnostic](../proof/2026-10-19-planning-authority/c4-review-approval-diagnostic.json): 191,441 bytes, `350550A20F7CCCEFF60D64B476D1FC99FFC14CB2E93EC135E37B770D0A433FFD`.
- [E failed read-cost confirmation](../proof/2026-10-19-planning-authority/c4-review-read-cost-confirmation.json): 144,448 bytes, `882B22D1D59C6FD857670E0ED0ABC0226A2F0029A0889A176A30C54B0D539768`.
- [F request profile](../proof/2026-10-19-planning-authority/c4-review-read-profile-followup.json): 195,633 bytes, `8FA9B7348EFB831407FFD9EEFA85E98449495181B0BA9A61D68E0C9C22E18F19`.

All retained proof passed secret scanning. Finite latency and sustained capacity remain open; the separate accounting action may be implemented and tested without enabling producers. Parent cancellation and refresh remain separate prerequisites for activation.


G adds only the planned seed activities. Direct Release Persistence/Web/Integration
builds pass (29.28/125.27/35.68 seconds); source stayed unchanged during compilation.
Full portability scan (164.55 seconds), final no-write enforcement at 15,665 findings,
rendering, documentation and diff checks pass. Three actual profiled shapes pass in
108.28 seconds. Their 15 samples show normalization total median 58–105 ms, including
seed validation 24–26 ms and seed construction 22–35 ms; pure normalization is 13–31 ms.
These nested timings overlap. They do not explain most elapsed request time, so seed
validation is retained unchanged. Finite acceptance remains failed; request construction
and current authority are the next measured boundary alongside independent accounting.

G verifies source `9FA853BA7E4CDF7E71220B64EE4227D1B05BC091395091CF2D972C8C6ACC66C3`
and runtime `F76883E171629840451891758EF2C37AF09328BD1B9CE569E97C1488FCAA125B`
unchanged after execution (9,899 / 7,013 inputs). Its 134.16-second observer covers the
execution with zero new resource deltas. The first preparation assertion found that the
two new P09 helpers were not resource-allocation pins; complete source capture already
covered them. Explicit pins were added before tests. No fixture or source was altered
by that preparation correction. Fresh runtime hashes followed successful post-build
source verification, reusing that proven source manifest rather than rescanning twice.

[G seed diagnostic](../proof/2026-10-19-planning-authority/c4-review-seed-diagnostic.json)
is 216,271 bytes, SHA256 `98EA8BD648C1232B1240EF3AB1298FA81D184EC27C0D3D454DA4F97701D73F7B`,
with zero secret findings. No new architecture boundary, class, project reference,
cache or authority behavior was introduced. The signed checkpoint remains diagnostic,
not a declaration of latency, sustained capacity or E2E completion.
