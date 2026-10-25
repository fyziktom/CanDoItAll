# Protected-operation foundation checkpoint

This is an implementation checkpoint within the unfinished concurrent-mutations
bundle. It does not close Projects producer cutover, the engine-wide requirement,
agent/process freshness settings, external effects or the final capacity gate.
The [requirements ledger](../requirements-and-closure.md) remains authoritative.

## Implemented boundary

Projects owns immutable original-state commands, atomic accepted receipts and
Search projection updates. Infrastructure owns dataset activation and transfer
lifecycle, without a dependency on Projects. Web and Workbench adapt the owner
protocol at their existing boundaries. Protected dispatch preserves caller,
dataset, epoch and operation identity; a failed acknowledgement is not evidence
that an earlier attempt was rejected. Native Projects recovery persists a claim
before dispatch and preserves known acceptance before browser acknowledgement.

Transfer participants now contribute their complete target lock plan before a
Serializable snapshot is established. Import outcomes distinguish a confirmed
commit followed by cleanup failure from an unconfirmed commit. Neither permits
automatic compensation or replay as though the import had not run. The offline
activation command composes its required services without starting the Web host.

## Focused evidence

All results below used the owned PostgreSQL fixture when required and standard
Release outputs. Original failing runs remain retained; later passing selections
qualify their repaired cases rather than rewriting the original run as green.

| Proof | Retained result | Outcome |
|---|---|---|
| Owner/transfer boundaries | `p02bc-transfer.trx` | 89 passed, 0 skipped, 5m19s |
| Codec, adapter and governed repair selection | `p02bc-repairs-unit.trx` | 38 passed, 0 skipped |
| HTTP/body/epoch and actual MAF repair | `p02bc-final-repair.trx` | HTTP 55, MAF 1 and row import 2 passed; package 4 initially failed |
| Repaired package import | `p02bc-package-fixed.trx` | 4 passed, 0 skipped, 36s |
| Actual offline activation executable | `p02bc-offline-repaired.trx` | 2 passed, 0 skipped, 49s |
| Canonical byte bounds and hash compatibility | `p02bc-canonical-codec.trx` | 25 passed, 0 skipped |
| Native Projects host after wrapper repair | `p02bc-native-fixed.trx` | 61 passed, 1 failed, 0 skipped |
| Native submission-readiness repair | `p02bc-readiness-native.trx` | All 5 selected cases passed, including the remaining card case |
| Shared host readiness/capture contract | `p02bc-readiness-light.trx` | 1 passed, 0 skipped |
| Browser recovery/store repair | `p02bc-browser-repair.trx` | 11 passed, 3 failed, 0 skipped; native recovery passed |
| Final paging/quota/native selection | `p02bc-browser-final-four.trx` | All 4 passed, 0 skipped, 30s |

All 18 affected production owners were built directly during this checkpoint.
The final native-readiness changes rebuilt Projects.UI and Projects; Web was then
rebuilt in 32.60 seconds. The final comparison-heading correction rebuilt
Projects.UI and Web in 3.23 and 29.96 seconds. Builds had no errors; existing
NuGet advisories remain.
Model parity reported no changes and the generated idempotent migration was
reviewed. Detailed commands and source identities remain in the bounded proof
directory and the owning validation records.

The first combined browser run discovered exactly 15 cases and produced 1 pass
and 14 failures. The real two-context conflict/reconciliation scenario passed;
its two screenshots were visually reviewed at 1920x1080. Thirteen IndexedDB
tests failed in Playwright's argument conversion before calling production
JavaScript. The native recovery scenario did not display its injected-failure
state. The bridge repair and verified accepted-write failure injection then passed
11 cases; two cursor bridge cases and the quota fixture still failed. The final
four-case selection passed those three repaired cases and native recovery again.
All 13 store cases and both native browser cases therefore have qualifying passes
across the retained runs. No original failing run is presented as wholly green.

The fresh native screenshot shows the preserved historical draft beside the
later saved definition with the neutral heading `Your draft`. Its exact original
operation was recovered after a page reload and authorized owner lookup. The
quota case uses a real finite browser quota established before initialization,
requiring genuine `QuotaExceededError` and unchanged record/accounting state.

Final complete portability inventory covered 8,350 files and 36,268 findings,
including untracked source. Enforcement without `--write-baseline` passed with
15,439 unchanged reviewed allowances. Rendering review passed 1,195 inputs.
The source secret scan completely covered 159 files / 6,745,763 snapshot bytes:
three reviewed source-expression false positives, zero fixture-password matches.
The proof scan covered all 318 text inputs at its snapshot, including SQL/Python
probes and the previously oversized inventories, with no unreadable/oversized
exclusions. Four probe-expression false positives and zero sentinel matches were
reviewed; raw scanner FAIL statuses remain retained. Six exact browser-evidence
roots separately passed their text scans. See the
[portability and scan review](P02b-portability-review.md) for qualifications.

## Measured performance limits

The matched baseline and encoding-reuse candidate each passed two cases with
33 samples and identical outcomes across 3,300 attempts: 2,151 new acceptances,
549 expected conflicts, 300 replays and 300 receipt lookups. The large-definition
mean allocation fell from 13.352690 to 12.336433 MB per attempt (7.61%). Latency
was mixed; no general speedup or latency nonregression is established. Mixed-load
receipt retention, two-host contention and vacuum capacity remain open. See the
[matched comparison](P02bc-protected-performance-baseline.md).

## Integrated gate and continuation

The shared persistence/composition/test-support changes trigger one frozen Broad
Stable run. Independent source accounting expects 36 assemblies, 18,588 discovery
entries and 18,643 executed rows under the canonical filter. These are expected
counts, not results. The seven deferred theories explain the 55-row difference;
the [source accounting](broad-stable-source-expectations.md) records every assembly.

Portability/rendering, scoped source/proof review and owned browser-host cleanup
have completed as recorded above; maintained documentation is checked after the
final record edits. Build and discover the full frozen candidate before execution. Keep the main
checkout unchanged while Stable runs because architecture tests read source.
Subsequent runtime journal, retention and Workbench implementation belongs in the
single managed checkout declared in the [resource record](../resources.md).

Remaining producer routes are listed in the
[cutover review](../reviews/P02b-final-boundary-and-legacy-cutover.md). In particular,
legacy save/Structure producers and the SPA are still open. Runtime journal
schema changes must precede resumable R1/R2 protected dispatch. No legacy route
is represented as protected merely because its owner now supports the protocol.
