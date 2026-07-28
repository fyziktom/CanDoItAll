# SB05 Governed Proof Manifest

## Identity

- Subbundle: `SB05 Backend Performance and Concurrency Gates`
- Status: `Complete — A5 Go with three P2 follow-ups`
- Date: `2026-07-27`
- Owned requirements: R08, R09, and the SB05 validation portion of R11.
- Raw-note ownership: use safe read-only parallelism only where dependencies are
  independent; measure real backend improvement before UI work; review architecture
  and preserve general module behavior.
- Semantic invariant contract:
  `bundle://proof/SB05/semantic-invariants.md`
- Production behavior artifact matrix:
  `bundle://proof/SB05/semantic-invariants.md`
- Independent verifier record:
  `bundle://proof/SB05/independent-a5-review.md`
- Downstream decision:
  `bundle://proof/SB05/a5-decision.md`

## A5 decision

`GO with three P2 follow-ups`

SB06 UI work may proceed. The gate is based primarily on typed milestone ordering,
deterministic operation/query/open counts, recovery semantics, architecture
direction, and focused regression groups. The timing comparison is descriptive, not
statistically proven: the start milestone changed, each scenario has five local
samples, and nearest-rank p95 is therefore the observed maximum.

The three open P2 risks are:

1. a synchronous database-switch subscriber can block the switching thread;
2. the WAL has no physical disk/directory flush proof across power loss;
3. final provider revision validation has an in-memory cross-host race before
   external provider use.

No P0/P1 finding blocks progression, and none of these P2 items is silently
converted into a stronger runtime guarantee.

## Required evidence status

| Evidence | Status | Artifact |
| --- | --- | --- |
| Immediate typed activity and final startup matrix | Pass — 5 repetitions × 4 scenarios; identical order/count invariants | `bundle://proof/SB05/startup-raw.md` |
| Before/after report | Pass — medians and observed p95 recorded separately; comparison explicitly descriptive | `bundle://proof/SB05/before-after.md` |
| Startup operation counts | Pass — deterministic duplicate-work reduction | `bundle://proof/SB05/operation-counts.md` |
| Targeted .NET performance scan | Pass with one moderate legacy debt outside the measured path | `bundle://proof/SB05/performance-scan-classification.md` |
| EF provider/process query review | Pass — provider 1/0/3; selected process shape estimated at 8 commands and batched | `bundle://proof/SB05/ef-query-proof.md` |
| Concurrency/storage invariants | Pass with three explicit P2 residuals | `bundle://proof/SB05/concurrency-invariants.md` |
| Generic WAL crash matrix | Pass — 6/6 confirmed handoff | `bundle://proof/SB05/transcripts/confirmed-validation-handoff.md` |
| Combined generic/chat/update recovery matrix | Pass — 33/33 confirmed handoff | `bundle://proof/SB05/transcripts/confirmed-validation-handoff.md` |
| Process snapshot/redaction group | Pass — 18/18 confirmed handoff | `bundle://proof/SB05/transcripts/confirmed-validation-handoff.md` |
| Activity admission/profile group | Pass — 11/11 confirmed handoff | `bundle://proof/SB05/transcripts/confirmed-validation-handoff.md` |
| Storage scaling/usage group | Pass — 10/10 confirmed handoff | `bundle://proof/SB05/transcripts/confirmed-validation-handoff.md` |
| Full serial solution build | Pass — 0 errors, 166 warnings confirmed handoff | `bundle://proof/SB05/transcripts/confirmed-validation-handoff.md` |
| CodeAnalytics snapshot | Pass — project graph acyclic; non-project cycles disclosed | `bundle://proof/SB05/transcripts/codeanalytics-snapshot.md` |
| C# architecture review | Pass with three P2 follow-ups | `bundle://proof/SB05/architecture-review.md` |
| Production anti-stub audit | Pass — no stub/template/fixture path carries the gate | `bundle://proof/SB05/anti-stub-audit.md` |
| Source assertion identity | Present — reviewed production/test SHA-256 values | `bundle://proof/SB05/transcripts/source-hashes.md` |
| Independent A5 verification | Go with the same three P2 residuals | `bundle://proof/SB05/independent-a5-review.md` |

## Deterministic acceptance summary

- Startup medians/observed p95 in milliseconds:
  cold-new `231.915/578.993`, warm-new `241.942/316.867`,
  cold-existing `168.137/185.760`, and
  warm-existing `407.779/527.575`.
- Every final startup scenario execution recorded:
  `Accepted1`, `CatalogLoad0`, `CatalogSnapshot1`, `ProviderGet0`,
  `ProviderAcquire1`, `ProviderCapture3`, `SessionGet0`, `SummaryList0`,
  `AtomicStart1`, `DetailGet0`, `DetailSave0`, and `DetailUpdate1`.
- Provider validation is bounded at one scalar SQL command when warm, zero for the
  synthetic provider, and three for a changed provider.
- File admission uses 11 physical JSON opens for new and 15 for existing at both 4
  and 96 historical runs.
- Chat index record lookup is O(1); the current JSON representation still incurs
  O(R) bytes and parse CPU.
- Usage delta work is O(A + P + M) across affected agent, provider, and model
  aggregates.
- Selected process enrichment uses `TakeRuns: 10` and an estimated eight SQL
  commands because the runtime-state query is split; the estimate is not a universal
  measured promise.

## Failing-first boundary

SB05 is validation-only and changed no production or test source. Creating a new
post-implementation red transcript would be artificial. Its failing-first boundary
is the preserved SB01 baseline and the earlier backend subbundles:

- `bundle://proof/SB01/startup-baseline.md`
- `bundle://proof/SB01/performance-scan-baseline.md`
- `bundle://proof/SB01/ef-query-review.md`

The governed semantic contract maps each positive result to the historical baseline,
production assertion, and adversarial negative:
`bundle://proof/SB05/semantic-invariants.md`.

- Failing-first: N/A — validation-only process/non-production proof assembly; SB05
  changed no production or test behavior, and the preserved SB01 baselines above are
  the truthful pre-implementation boundary.
- Passing transcript: `bundle://proof/SB05/transcripts/confirmed-validation-handoff.md`
- Anti-stub transcript: `bundle://proof/SB05/transcripts/anti-stub-scan.md`

## Evidence provenance

Direct evidence produced during SB05 proof assembly:

- CodeAnalytics snapshot/inventory/dependency queries in
  `bundle://proof/SB05/transcripts/codeanalytics-snapshot.md`;
- targeted anti-stub scans and manual classification in
  `bundle://proof/SB05/transcripts/anti-stub-scan.md`;
- working-tree source/test hashes in
  `bundle://proof/SB05/transcripts/source-hashes.md`.

The startup samples and diagnostics, build, focused test-group totals, recovery
totals, and provider SQL counts were explicitly confirmed by the parent validation
workflow. Their original command lines, raw console streams, TRX files, and SQL logs
were not retained. They are preserved as `Confirmed handoff`, not relabeled as raw
transcripts:
`bundle://proof/SB05/transcripts/confirmed-validation-handoff.md`.
The startup sample artifact also states that limitation directly:
`bundle://proof/SB05/startup-raw.md`.
Commands shown there are reproducible suggestions rather than reconstructed original
commands. This is an explicit evidence-provenance limitation; a later external audit
may augment it with fresh retained console/TRX/SQL artifacts.

## Architecture and production assertions

- Reviewed CodeAnalytics snapshot: `snap-20260727233256-654bc9d9`.
- The 12-project graph is acyclic and preserves inward dependency direction.
- Three module cycles and two nested-type cycles are disclosed; this manifest does
  not make a zero-cycle claim beyond the project graph.
- Provider contexts are factory-created and no-tracking.
- Shared scoped process EF work remains sequential; no `Task.WhenAll` or
  `Parallel.*` occurs in the reviewed chain.
- Immutable preparation/provider snapshots contain configuration and revision
  identity, not live agents, clients, credentials, sessions, tools, approvals,
  authorization, or `DbContext`.
- The targeted performance scan found only intentional process strategy
  `Task.Run` timeout isolation plus a legacy unscoped synchronous credential bridge
  outside the measured dispatch path.
- Exact source statements are in
  `bundle://proof/SB05/source-assertions.md`.

## Production behavior artifact matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Typed `Accepted` activity | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityCoordinator.cs` | current-profile admission and scoped activity reader | operation lease binds, transitions, and terminalizes exactly once | activity admission/profile group 11/11 and startup ordering across 20 executions |
| Immutable provider lease | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs` | preparation/provider acquisition | scalar revision probe, generation fencing, immutable publication, profile invalidation | current/change/delete/fault/superseded tests; cross-host window remains P2 |
| Atomic file-store admission/WAL | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs` | recovery before later store reads and mutations | gate, cross-process lock, journal-before-commit, idempotent roll-forward | generic 6/6, combined 33/33, corrupt-journal/cancellation cases; physical flush remains P2 |
| Bounded process enrichment | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs` | selected live process projection | one bounded state batch and one assignment batch on sequential scoped EF work | six selected runs do not trigger six state or assignment calls |

## Changed-file boundary

SB05 itself changed no production source, test source, skill, API, or SharedInfo file.
The production/test hashes in
`bundle://proof/SB05/transcripts/source-hashes.md` identify the reviewed integrated
working-tree state; they are not attributed to SB05.

The SB05 proof directory and its subbundle README are absent at repository `HEAD`
because the initiative bundle is currently untracked. `ABSENT` therefore means
repository-HEAD absence, not deletion. SHA-256 values below hash exact current file
bytes. The manifest omits its own hash because that would be self-referential.

| Artifact | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `bundle://proof/SB05/a5-decision.md` | `ABSENT` | `1092D87F959991FAEA117C3FB88371B351E286CF516A323473185CE9EE2ECEC4` |
| `bundle://proof/SB05/anti-stub-audit.md` | `ABSENT` | `B1FBAAAC0CDB4AAC94B353FE5BDDA2FE3DD0EFE1E20459EBBD46DA66538DCF69` |
| `bundle://proof/SB05/architecture-review.md` | `ABSENT` | `EDC1A39550B4B04F59ACD692FFF9B0733E6C08A0B0F094C4305725FD30E1C26C` |
| `bundle://proof/SB05/before-after.md` | `ABSENT` | `7413B2E8BBA99EADB553BC218B05A649C3DCE854BAD1E5CE72589D8C0891E0DC` |
| `bundle://proof/SB05/concurrency-invariants.md` | `ABSENT` | `8B7592A3C6A77AE4ABE899CDDD7BB3C0922EFACF652D4FF5D27A1CEF122CDF2E` |
| `bundle://proof/SB05/ef-query-proof.md` | `ABSENT` | `3DE3C07812BF003AB132CB1B57381414BA4E0B062CE5A6483127937389E07F55` |
| `bundle://proof/SB05/independent-a5-review.md` | `ABSENT` | `1481981438487B238E060517381EAC150F0EC696BB99CEE9FA61890F9C77212E` |
| `bundle://proof/SB05/operation-counts.md` | `ABSENT` | `0FF001320AB7DD0AA077BBD7EC63AF841AE067A57BDF20A0C5CBBC90E056B6A3` |
| `bundle://proof/SB05/performance-scan-classification.md` | `ABSENT` | `E33CA4BD679FF27C39FBD60DFBFE0E28F1835F0E9BA87D3CB75A05DFFEB51278` |
| `bundle://proof/SB05/semantic-invariants.md` | `ABSENT` | `17D8E2450E45612A4B5965C8E8FB4AF2009F273EC8651E4294EDDE3A95779E9A` |
| `bundle://proof/SB05/source-assertions.md` | `ABSENT` | `12FE853C73B2B674B9243B521F8086444C910F154C54E54C94D3F115FA53042B` |
| `bundle://proof/SB05/startup-raw.md` | `ABSENT` | `BD2CE73499A7306C0AB986775C5ECEF5B6017C8E461FCCF44F4F8E8CA91950DF` |
| `bundle://proof/SB05/transcripts/anti-stub-scan.md` | `ABSENT` | `29EC9F3B19EA2C2C6CD216AFC040305DCDAECFF947DB6E7B00B6DFF18772DAA3` |
| `bundle://proof/SB05/transcripts/codeanalytics-snapshot.md` | `ABSENT` | `4D3D8265E042105679ADCDE3B0EE74E8FF04421A31A38CCB05DDF7F7742EA4FD` |
| `bundle://proof/SB05/transcripts/confirmed-validation-handoff.md` | `ABSENT` | `FF326A553B13D04AA3B8A92577FEAEA38C2F2739997643D09487F1DEC58A5FE5` |
| `bundle://proof/SB05/transcripts/README.md` | `ABSENT` | `1CE4934D400D5EC98F37F45AAA26892AECE1A78C7FCF297BF245260BAEA3EB5E` |
| `bundle://proof/SB05/transcripts/source-hashes.md` | `ABSENT` | `9AA4B6CBBCDF2886FCDC68D8B3BFABA4D7D345860A4FB4BD1C4A6AFCE1179144` |
| `bundle://subbundles/05-05-backend-performance-and-concurrency-gates/README.md` | `ABSENT` | `88549AB18B692E7BE0C147A580EDFA775282D6697C3BE9E413654551CC26607B` |

## Anti-stub and shallow-pass decision

No `TODO`, `FIXME`, `HACK`, `NotImplementedException`, fixture-specific branch, or
template-only marker carries a positive A5 claim. Seven completion-task sites were
manually classified as synchronous disposal/removal, explicit null policy, or
bounded enqueue behavior. Explicit `NotSupportedException` guards remain negative
boundaries rather than fake implementations:
`bundle://proof/SB05/anti-stub-audit.md`.

## Closure

- Independent A5 disposition: `GO`.
- Current A5 closure: `GO with three P2 follow-ups`.
- SB06 authorization: **Granted**.
- Reopen if UI/browser evidence shows pre-activity stalling, a real provider run
  loses correlation, a final architecture snapshot reveals a project dependency
  reversal, or a residual P2 manifests as stale or corrupt user behavior.
