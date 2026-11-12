# C3 focused runtime checkpoint

All 190 application cases passed: 60 Unit, 127 Integration, two LiveProcess and one
LongRunning, with no skips. The final candidate retained identical source and Release
runtime inputs across these lanes. Independent observation of all nine expected CLI
children is incomplete; application success does not close that operational proof.
The publisher remains dormant and does not establish real Agent/Process provenance,
persisted planning settings, automatic replanning or sustained capacity.

## Candidate and results

HEAD is `702d0a93b1081c42d5a06893e7070219aee5d089`. The complete source manifest contains
8,777 paths, SHA `93188F178E1FAE3A4F3065F7D451690606F8830A573A4CA5BCD5219D5A2EA3E8`.
The Release runtime manifest contains 4,340 files, SHA
`4C351F7CECFFA9FDFB4343B25A3C4CEA81592AF333484AE6B0D45A3C6937CB39`.
The parent independently verified exact path sets, existence, bytes and hashes before
releasing the freeze at 2026-10-17T07:02:11.8227579Z for dormant D1 integration.

| Lane | Passed | Execution seconds | Evidence root |
| --- | ---: | ---: | --- |
| Unit | 60 | 8.663 | `c3-fixture-repair-validation` |
| Integration | 127 | 360.30 | `c3-fixture-repair-validation` |
| LiveProcess | 2 | 54.4308 | `c3-live-getter-validation` |
| LongRunning | 1 | 27.8299705 | `c3-fixture-repair-validation` |

Evidence roots are beneath `.artifacts/concurrent-mutations/validation`. Exact built
discovery, labels, definitions, execution IDs, all TRX counters and identity checks
passed. The final live TRX SHA is
`497c2d3130ec5efcc47073a49cfa781c31c1e90298ceac46889bb897de9ce558`.
The two CLI tests exercise exact prepared bootstrap resumption after SQL rollback,
and bootstrap/resume/reactivation without starting the Web host. Their actual
application results passed even though the independent observer wrapper returned 1.

All six production/test Release builds passed before the test-only successor, whose
Integration Release rebuild also passed. Final portability enforcement passed with
15,504 reviewed baseline findings; the full untracked-inclusive scan covered 8,708
files and 36,907 findings without truncation. Rendering checked 1,201 entries,
documentation checked 427 maintained Markdown files, and `git diff --check` passed. Later source changes
require their own scan and frozen candidate.

## Process observation and resources

The final observer failed while retaining a nonroot process handle:
`MethodInvocationException` wrapping `Win32Exception`, stage `RetainObservedProcess`,
worker line 103. It did not record the native error code or attempted PID, so those
cannot be reconstructed. Ten retained processes and one unretained observation,
current descendants and observed listeners were absent at reconciliation. Only the
first required CLI was positively correlated; complete nine-child ancestry is not proved.

The observer allowed the owned test command to finish naturally; both tests passed
and normal fixture cleanup completed. All 27 temporary-root inventories and the
PostgreSQL database/client inventory matched their pre-run state, with no new residue.
`c3-live-getter-validation/final-reconciliation-and-freeze-release.json` records this
limited closure. Earlier blocked cleanup remains outstanding, including the empty
bootstrap-registration root from the first crash and the activation fixture root
from an interrupted observer attempt. Automatic approval review rejected their exact
removal; no alternate deletion was attempted. Resource cleanup is not complete.

The original stack-overflow candidate, the 125/127 Integration successor and every
failed observer attempt remain separately retained. None is relabelled as passing.
See [the codec and fixture repair history](P04-C3-codec-runtime-repair.md).

## Performance characterization

The LongRunning test records 54 samples and 282 attempts: 168 Accepted and 114
BasisChanged, with no Unconfirmed/other refusal and exact replay of all 168 accepted
operations. Each arrangement has three samples, no discarded warmup, a shared growing
fixture and maintenance disabled. The 1,000-task boundary passes and 1,001 refuses.
Sample file SHA is `2C62A0EA0BA20C050CB1982ECEE404A7485110C63484ED127585F32DDB2EB06E`.

| Arrangement | Outcomes | Median batch time | Other observed cost |
| --- | --- | ---: | --- |
| 32 disjoint writers | 96 accepted / 96 | 160.9103 ms | Save P95 115.6753–215.4933 ms; median batch allocation 34,685,920 bytes |
| 32 same-target writers | 3 accepted, 93 conflicts / 96 | 438.3055 ms | Save P95 188.8495–249.8832 ms; median batch allocation 25,500,352 bytes |
| Eight same-target writers | 3 accepted, 21 conflicts / 24 | 114.0364 ms | Expected conditional-write conflict behavior |

Uncontended append-one median allocation increases from 1,118,464 bytes with an empty
basis to 19,275,152 bytes at basis 900; appending 100 at basis 900 uses 30,639,536 bytes.
Capture and publication are measured; seeding and replay allocation are excluded.
SQL command counts cover Workbench EF commands, and WAL is cluster-wide. Internal
capture, validation, encoding and lock phases are not separately instrumented. A
single-writer percentile is one observation. These are finite characterizations,
not sustained throughput, fairness or latency guarantees.

Source inspection suggests basis-sized materialization and encoding costs in opening
and publication recapture; digest calculation also encodes the basis. Attribution is
not measured. Before changing that protocol, measure basis 900 with 32 same/disjoint
writers, maximum escaped payloads, all-owner SQL, lock wait/hold and defined arrival
rates/SLOs. Preserve exact validation and receipt semantics while addressing measured
bottlenecks. The separate Agent journal publication cost probe is the next dependency
for C4 activation; C3 alone does not qualify that path.
