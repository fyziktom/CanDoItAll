# P02a Projects candidate measurement

This is the original-state candidate, before P02b's durable receipt and enlisted
mandatory search projection. The matched harness and measurement limitations are in
[the baseline record](projects-performance-baseline.md). No production capacity or
whole-engine performance guarantee follows from this short local run.

The unchanged exact LongRunning filter discovered one test and passed once, zero
skipped, in 1m02s. It produced 24 samples / 2,400 measured operations using the same
Release output mode, .NET 10.0.12 workstation GC, Windows x64, two-CPU/two-GiB owned
PostgreSQL 18.6 fixture and NullActivityStream. Assembly metadata identifies
`efbebfc54`; this is the uncommitted P02a candidate on that commit, not the baseline
binary. Raw TRX/JSON/log are `projects-performance-candidate.*` under the ignored
validation directory. No other database test or benchmark ran concurrently.

| Scenario | Children per collection | Writers | Save p95 across samples (ms) | Accepted / 300 | Rejected / 300 | Faulted / 300 | Aggregate accepted/s |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: |
| Uncontended | 0 | 1 | 34.16 / 35.94 / 38.39 | 300 | 0 | 0 | 32.99 |
| Uncontended | 5 | 1 | 42.20 / 32.82 / 31.50 | 300 | 0 | 0 | 37.31 |
| Uncontended | 100 | 1 | 29.42 / 32.34 / 31.61 | 300 | 0 | 0 | 39.28 |
| Uncontended | 1000 | 1 | 55.82 / 62.10 / 43.30 | 300 | 0 | 0 | 27.04 |
| Disjoint projects | 5 | 8 | 19.01 / 19.96 / 19.29 | 300 | 0 | 0 | 136.48 |
| Disjoint projects | 5 | 32 | 22.17 / 21.11 / 71.69 | 300 | 0 | 0 | 158.66 |
| Same project | 5 | 8 | 28.48 / 36.08 / 38.00 | 39 | 261 | 0 | 16.78 |
| Same project | 5 | 32 | 34.04 / 48.09 / 42.59 | 12 | 288 | 0 | 5.83 |

Every rejection was `projects.definition-conflict`; no observed SQL command failed.
The 8/32 disjoint cases now accept all 300 each, versus baseline 111/148 respectively.
Their accepted throughput increased from 49.06/84.90 per second. Same-project waves
accept exactly one original per wave (13 or 4 per sample); the rest are obsolete
originals. Their lower accepted throughput is the required correctness change, not
a throughput comparison against the baseline's lost updates. Accepted-only p95 is
57.62 / 37.30 / 56.28 ms for eight writers and 32.25 / 30.58 / 32.56 for 32 writers.

The unchanged matched repeat passed once in 1m00s (`projects-performance-candidate-repeat.*`),
again 24 samples / 2,400 operations, zero faults or failed observed SQL. Disjoint
accepted throughput was 139.54/159.75 per second, with all 300 accepted per case.
Same-project acceptance/rejection counts were identical. Disjoint-32 p95 was
21.98/27.66/22.07 ms, so its first-run 71.69 ms tail did not repeat.

## Cost investigation and remaining gate

The 1,000-child p95 crosses the 15% investigation band. Its median save is lower
(21.81–23.30 ms versus 24.95–26.62), and aggregate accepted throughput is similar
(27.04 versus 26.45/s), so the short run does not support a uniform slowdown claim.
Process allocation per read/save increased from approximately 6.278 to 6.626 MiB
(5.5%); collection counts remain linear and this includes all process allocations.
Generation-two collections are 4–5 versus 3–4 per 100 operations. Those correlations
do not establish that GC caused each tail event. The repeat's 1,000-child p95 was
51.25/53.10/62.92 ms and aggregate accepted throughput 27.38/s. That tail regression
did repeat; it remains an explicit P02b investigation/closure item. It is not waived
by the improved median or independent-project throughput, and P02a is not a final
performance release checkpoint.

Observed asynchronous SQL increased from 12 to 15 per uncontended read/save. The
candidate deliberately fences its still-separate postcommit search projection with
the accepted lifetime and revision. P02b moves that projection into the original
transaction, removing the second project-lock/admission/revision round trip while
adding the actual receipt and lifecycle guards. This intermediate cost must be
remeasured on that integrated candidate; neither ignoring failed baseline saves nor
removing original-state checks is an acceptable optimization.

The small-project zero-child aggregate throughput is lower, while the five- and
100-child cases are higher; baseline timing already varied substantially. No precise
production p99, allocation budget, WAL result or cross-process contention result is
claimed. The shared guard comparison and receipt retention/WAL costs remain P02b
entry/closure work, and source-authorized Serializable throughput remains separate.
