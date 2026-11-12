# Original inherited Process source history

Status: implemented and validated on backend `concurrent-mutations`. SPA remains on
`codex/concurrent-mutations`, without merge or push. Signed checkpoint creation remains
pending at the local PGP prompt; this slice is unstaged.

The [design](../plan/P07-inherited-original-source-history.md) adds selected historical
observation to the existing Process receipt-reader boundary and persistence owner. It
resolves the accepted launch, original assignment, exact claim interval, retained child
and material stamp without depending on the current assignment. Repairs, expiry, release,
closure and supersession preserve original evidence. Every historical result reports
`ObservedCurrentDispatch=false`; current disclosure permission remains a separate check.
Missing accepted admission fails explicitly. No new interface, registration, project
dependency, transaction, lock or provider invocation is added.

The existing fingerprint expression is byte-identical to committed predecessor
`065f2a297af01458637352b0cd492b902fef4df2` after only the local `row.State` to `state` rename.
Its normalized expression SHA-256 is
`3C5B0CB1576854DBEF0239F3916E184607AD9E747F7A70B3FBBC1C0EBE0DC9E9`.
Actual SQL tests cover finer timestamp ticks on both sides of PostgreSQL's epoch, retaining
the original stored fingerprint. The persistence adapter reproduces the existing
[Npgsql timestamp encoding](https://github.com/npgsql/npgsql/blob/v10.0.0/src/Npgsql/Internal/Converters/Temporal/PgTimestamp.cs).

**107 distinct selected cases pass across an exact one-test-file repair.** The first run
passed 100/107, including all 77 existing dispatch, ordinary source and material-reader
regressions. Seven new cases exposed fixture mistakes: fabricated operator authority,
a root-budget helper without canonical database, and an assertion that also excluded the
required accepted-launch JSON. After repair, all 30 new cases pass. The source bridge
confirms only that new test file changed and all 1399 production runtime files are identical.
The initial failed evidence is retained.

Four bounded queries return one row each with zero writes, no current-assignment join and
no material payload. Eight retained EXPLAIN plans, with zero and twelve siblings, use exact
indexes throughout. Measured SQL execution is 0.031-0.408 ms per query; planning is
0.036-2.453 ms. These small-fixture measurements establish query shape, not sustained capacity.

Standard Release builds pass: Application 5.9867318 seconds, Persistence 4.2964134,
Processes 28.9363104 and Integration 51.3585883. The test-only repeat rebuilds Integration
in 65.4228123 seconds. Exact discovery is 107 initially and 30 finally; execution is
442.3581158 and 130.5606543 seconds. Full portability enforcement passes twice with
**15,647 unchanged reviewed findings**, without write-baseline. Validation-tool, rendering,
documentation, diff and exact retained-proof secret checks pass. Broad Stable remains open.

Architecture gate: Pass with follow-up. Scoped snapshots `snap-20261020085853-af9e816a`
and `snap-20261020093530-af9e816a` retain 830 types, one registration and 31 entities.
Members change 7876 to 7884 and findings 289 to 290. The new informational 12-member
query-owner finding is reviewed as cohesive. No scoped project/module/type cycles exist.
The same 190 CodeAnalytics diagnostics remain; direct builds provide compilation evidence.

Both validation roots are Released. Frozen identities and complete observer coverage
pass; observer durations are 456.8068605 and 145.796292 seconds. Each has zero remaining
observed processes and zero database-name delta. Four initial and three final unowned TEMP
entries are preserved. The shared isolated PostgreSQL 18 fixture on 53619 remains needed;
ordinary 5032, installed tooling and historical cleanup denials are unchanged.

Final source: 9627 files,
`45139ED419A19712D265D7B2BFB1B1E7C8096DF1ABFAE08CE4483F0FAFE6CB5F`.
Final runtime: 2504 files,
`E897BA77FE4F3B3C0D8FC50AE205625B7FBB3EB81D88E732ED90290C8D875B23`.

UTF-8/LF proofs under `proof/2026-10-19-planning-authority`:

| File | SHA-256 |
| --- | --- |
| `c4-inherited-original-source-history.json` | `9154573D7784D1EAEA85E74FA0F59FA944DC1D938E7C8740A18B26C40BACB12C` |
| `c4-inherited-original-source-history-final.json` | `2111389D6148A9B6C559FE952061743F81752B7FE5FEEA4C328286D8C6F532C4` |

Both retain command/results/failure evidence, architecture, fingerprint compatibility,
source/runtime identity and resource reconciliation. Final proof includes the exact bridge
and eight query plans. Core schema 7 receipt reads, product historical provider/producer,
manager/approval, atomic outcomes, Workflow, settings and capacity remain open.
