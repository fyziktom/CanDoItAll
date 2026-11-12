# Projects lifecycle guard diagnostic

Recorded 2026-10-16 against source `1452352f07d14a4b526ae3840bba298ea11db1a0`.
This is a direct PostgreSQL protocol comparison, not an application capacity result.
No production guard code, build or test runner was used by this diagnostic.

## Decision

Select A, dataset and Projects state `FOR SHARE` guards, for the first owner
implementation. Install finite transaction timeout before admission, reject expired
new effects in a read-only database-clock precheck before acquiring either shared
guard, and repeat authoritative state/deadline validation after waits. Preserve
Serializable locking-read validation. Do not expose an A/B production setting.

This choice meets the previously published comparison rule: B was slower at every
owner-like concurrency, including an 18% median regression at8. A's additional
MultiXact/WAL work is real and remains a measured cost, not a claim that shared row
locks are free. A later shared-advisory implementation using reviewed command
batching could change this tradeoff; this experiment compared explicit sequential
statements and does not rule that optimization out.

The protocol has two necessary corrections independent of A/B:

1. Shared row newcomers can bypass an exclusive row locker already waiting for
   other holders. Deadline rejection only after acquiring the shared guard does
   not prove bounded aggregate drain. The precheck rejects expired newcomers
   without acquiring a guard. Transactions that passed before the deadline are
   finite because their server transaction timeout was already installed.
2. Lookup uses one coherent MVCC statement joining lifecycle, owner watermark,
   epoch and exact caller receipt, without shared lifecycle guards. Advance the
   retired watermark only during expired-current-epoch rotation, before publishing
   the successor. Open-epoch maintenance only purges previously retired evidence.
   It must not compete for an exclusive global state row against an endless stream
   of valid open-epoch writers. Check database time before maintenance tries that
   exclusive lock too. Retention capacity includes up to one additional configured
   admission-epoch cadence before eligible evidence is retired.

## Environment, ownership and reproducibility

- PostgreSQL18.6, task-owned container
  `candoitall-concurrent-mutations-pg18-20261016`, existing2CPU/2GiB limit;
  Python threaded client, psycopg3.2.1 on Windows. No connection pool.
- Parent authorized changing only this container from max_connections100 to160.
  Container ID, task label and volume remained unchanged. Docker reassigned its
  dynamic localhost port50929 to60920; the fixture ownership record was refreshed.
  All recorded A/B cells used160, with reserved slots excluded from preflight.
- Dedicated database `candoitall_concurrent_guard_probe_20261016` was recorded
  before creation, refused if already present, then dropped and verified absent.
  The existing fixture/container/volume remain for later parent-owned validation.
- Ignored script: `.artifacts/concurrent-mutations/validation/projects_guard_probe.py`.
  SHA-256 `d5d194774f502889656d4c3b244d625700fc7042e8cb5473ff8ac4f2d83c9d43`.
  Run from repository root with `python .artifacts/concurrent-mutations/validation/projects_guard_probe.py --execute`
  only after reserving the owned fixture. Without that switch it does not connect.
- Raw sanitized result: `.artifacts/concurrent-mutations/validation/projects-guard-diagnostic.json`.
  Ownership/configuration records are in the task's fixture directory; passwords
  and connection strings are absent from measurement output.
- Final run16:04:26.980–16:05:20.288UTC,53.3seconds, under a ten-minute overall bound.

Each of8/32/128 writers has an independent preopened physical connection. The
closure barriers additionally prove all8/32/128 hold their guards simultaneously.
Each strategy/workload/concurrency cell has64 total warmup operations and three
interleaved256-operation samples. At128, warmup exercises64 connections; measured
samples exercise all128, twice each. Total measured attempts9,216: all accepted,
zero conflicts/timeouts/faults. Owner-like acceptance equals conditional root
increments and receipt row count in every sample.

A uses shared dataset then owner-state row locks. B uses shared transaction
advisory guards and later state reads; Serializable additionally uses A's locking
row validation. Both use the same precheck, root CAS, fixed2KiB synthetic receipt,
transaction deadlines, observer and setup. Receipt has no FK to a hot epoch row.
There is no sleep inside measured transactions.

## Measurements

Median accepted operations/second and the range of sample p95 transaction latency:

| Work | Writers | A ops/s | B ops/s | A p95 ms | B p95 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Guards only | 8 | 598.0 | 556.7 | 14.38–22.04 | 15.59–21.62 |
| Guards only | 32 | 648.4 | 563.3 | 58.51–72.85 | 66.50–78.29 |
| Guards only | 128 | 640.8 | 455.9 | 216.18–264.94 | 294.01–381.46 |
| Root CAS + receipt | 8 | 468.1 | 382.5 | 19.36–20.77 | 23.34–23.76 |
| Root CAS + receipt | 32 | 466.2 | 371.0 | 79.94–87.03 | 96.79–102.21 |
| Root CAS + receipt | 128 | 452.0 | 316.4 | 315.07–430.45 | 436.24–503.81 |

Executed statements per attempt, including BEGIN/configuration/COMMIT, were6 vs8
for guards only and9 vs11 for owner-like A/B. Connection-open time and explicit
statistics flush requests were outside timed samples. The observer sampled activity
and lock counts every25ms. Short waits can be missed; absence of sampled LockManager
or MultiXact waits is not proof that they cost nothing.

For owner-like samples of256 accepted operations:

| Writers | A observed WAL bytes/sample | B observed WAL bytes/sample | A MultiXact member pages zeroed/sample | A member pages written/sample |
| --- | ---: | ---: | ---: | ---: |
| 8 | 143,853–145,021 | 85,819–85,981 | 1 | 0 |
| 32 | 197,931–198,777 | 86,007–86,809 | 5 | 0–4 |
| 128 | 394,750–413,857 | 102,467–103,217 | 20 | 20 |

B recorded zero MultiXact member/offset activity in these cells. Guard-only B
recorded zero WAL; A recorded62,878–63,742 /98,944–102,254 /255,047–267,323 bytes at
8/32/128. Thus A's shared-lock work grows with concurrency; its owner-like extra WAL
at128 was about1.1–1.2KiB per accepted operation. Counters are shared and reported
as observations, with per-backend flush requests outside timing and no global reset.
The receipt bytes are highly compressible synthetic zeros, so these WAL ratios
must not be extrapolated to real acknowledgement sizes or a steady-state database.

## Correctness evidence

All18 positive cases and both deliberately unsafe negative controls behaved as
required:

- Six closure drills: every physical writer held a shared guard; exclusive closure
  remained blocked with all holders and after half released. After database admission
  expired,4/16/64 independent connections continuously submitted new attempts while
  closure waited. All443 such attempts rejected before any shared lifecycle guard.
  Closure completed after the last original holder released.
- A last-holder-release-to-close latency at8/32/128 was5.77/15.39/61.37ms; B was
  9.98/16.90/78.69ms. These are one deterministic drain observation per cell, not a
  production latency percentile or SLA. Request-to-close includes intentional
  barrier/arrival work and is retained separately in raw JSON.
- Eight namespace/epoch waiter cases: ReadCommitted observed the changed state and
  rejected; an already established Serializable snapshot rejected with40001 after
  waiting and performing the locking validation, for both A and B.
- Two B negative controls omitted Serializable row validation and admitted stale
  namespace/epoch state. They wrote no effects. This proves why a plain read after
  an advisory wait is insufficient.
- Cancellation released both guard variants. A configured2-second server
  transaction timeout terminated the paused backend and allowed closure, verified
  by the setting, elapsed bound, closed client and backend disappearance. This
  psycopg transport returned OperationalError without SQLSTATE; PostgreSQL logs
  confirmed transaction-timeout termination. Do not require every client to receive
  a25P04 response or classify a connection failure as known rollback without lookup.

Earlier diagnostic attempts stopped safely on a JSON Decimal formatting issue,
the incorrect queue-fairness assertion, and the driver's missing timeout SQLSTATE.
One completed preliminary run also used obsolete SLRU names and lacked explicit
statistics flushes. Those outputs are retained separately for provenance; the
table above uses only the final corrected run. Each created database was removed
and absence verified before the next run.

## Remaining proof

This probe does not exercise EF, existing source leases, enlisted search, current
authorization, lookup SQL, watermark retirement or the actual retained journal.
The protocol changes above require owner integration tests, including a predeadline
transaction paused before guard acquisition, concurrent purge during one-statement
lookup, and retirement during rotation. Full Projects performance must include
realistic acknowledgement bytes, retained evidence, cleanup, source-authorized
traffic, query counts and MultiXact/WAL observations. The short fixed workload does
not establish vacuum aging, sustained disk growth or an admission rate limit.
