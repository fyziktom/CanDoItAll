# Accepted Process source persistence candidate

Status: persistence checkpoint passes 193 distinct cases. This does not close P07 or enable
the inherited producer, manager, approval or Workflow paths.

## Scope and boundaries

Implements checkpoint 2 of the [accepted-source design](../plan/P07-accepted-source-projection.md).
Processes.Persistence captures independently hashed original header/assignment evidence
at the existing initial acceptance point. The Agent dispatch and original-history query
read one exact assignment through a conditional legacy/projection join. Current claim,
generation, material, source and executor checks remain with their existing owners.
No new project, runtime interface or DI registration is introduced. Integration tests gain
friend access to the internal persistence adapter; this does not create a public API.

Migration `20261020131404_AddAcceptedProcessSourceProjection` adds three header fields and
one composite-key/FK assignment table. Existing rows remain explicit version 0. New
acceptance captures version 1 atomically with runtime state and receipts. Downgrade refuses
retained version-1 or assignment evidence; parent cleanup cascades. Missing/corrupt new
evidence never retries through the legacy payload. The 1 MiB UTF-8 metadata limits apply
to new capture; retained legacy payloads keep their original limits.

## Required proof

The owned `c4-accepted-source-persistence` root retains serialized standard Release builds,
migration model/script checks, exact test discovery, full portability, scoped architecture,
source/runtime freeze and resource observation. It reuses the recorded isolated PG18
fixture, never the ordinary application/database on 5032.

Build-backed discovery initially confirmed191 executions:31 pure codec cases,16 source/migration cases,
2 strengthened existing rollback/restart cases,137 registered-source regressions and5 SQL
measurement shapes. Selected proof covers pure source-principal/legacy compatibility; actual migration,
restart, downgrade and constraints; corrupt/missing evidence; exact live/history bytes
including PostgreSQL submicrosecond timestamps; independent full-review corruption;
duplicate acceptance without repair; rollback after actual flush; lost acknowledgement;
and current registered source/dispatch/history regressions.

The real SQL measurement uses the same five plan/prompt shapes as the codec baseline,
three rounds per format, and one exact source query per observation. It reports actual
initial commit and transaction duration, projected storage size, selected source bytes,
SQL and EXPLAIN. It excludes test assertions/serialization from measured read intervals.
These are exploratory local measurements, not a sustained capacity result or latency SLA.
Legacy backfill and Workflow adoption remain separate required work.

## Pending closure

Record every attempted result, exact artifacts and hashes after execution. A failed
candidate must be verified/reconciled/retained before repair. The unrelated owned probe
outputs remain CleanupBlocked by automatic approval review; this candidate does not retry
their deletion or claim global resource closure. SPA stays on `codex/concurrent-mutations`.

## Initial candidate and repair

The initial frozen candidate executes28/31 codec cases successfully, with three failures
and160 integration/measurement cases not executed. Ordinary source principals have no
optional decision policy; the new assignment record incorrectly wrote that null through
the existing strict converter. This is a production capture defect, not a test-only repair.
The record now omits the absent optional field, matching the existing runtime wire.
A local-principal case and explicit-null rejection case extend the full repeat to193
expected executions. No database execution is claimed for the initial attempt.

Initial source9661 `299E792ADD07404476BE59F51418BE2165A687AB172B3FFA60897C2DAB39189C`
and runtime4166 `FF519A8A71F35DC5A1D69E54B1347F107FCA9793780ADAC3C708B9E37B298FE8`
remain unchanged through failure. Complete21.477-second observation, zero remaining
observed processes/database-name delta, four unowned TEMP paths preserved. The root is
Released. Secret-reviewed proof `c4-accepted-source-persistence.json` has SHA-256
`416F2E035FAC5168D37B5EF7ECF1F1E834EB7A317F7478E8D562E23808AD6FDF`.
The repaired root rebuilds standard outputs and starts the entire selection.

## Final evidence and limits

The repaired production candidate passes 33 codec, 16 persistence/migration and two
rollback/restart cases. Its combined 137-case regression command exceeds the unchanged
900-second deadline without a complete TRX; no partial green count is inferred. That
attempt is retained with source/runtime verification. The subsequent SQL attempt fails
five assertions because EXPLAIN emits `Actual Rows` as JSON `1.0`, which `GetInt32`
rejects. The measured production operations had run, but that attempt is not green proof.

The only subsequent source edit changes that assertion to `GetDouble` with expected
`1d`. The explicit bridge proves every production and other test input unchanged, with
only the Integration DLL/PDB rebuilt. The split runs pass 25 SQL/history, 48 authority,
39 dispatch and 30 original-source cases. Together with the earlier 51 unchanged cases,
all 193 distinct selected cases pass. Every split command meets the original deadline.

Five affected Release builds, migration model and idempotent script checks pass.
The corrected Integration rebuild passes. Full portability enforcement passes all
15,651 reviewed findings without `--write-baseline`; four generated migration metadata
findings were reviewed. Documentation and diff checks pass before the frozen run.
Scoped architecture `snap-20261020134638-6de05ed1` covers three projects, 843 types and
7,971 members; no project reference is added. Existing 190 diagnostics remain.

All final groups independently verify 9,661 source inputs with SHA-256
`286E88C501E48A731416B41E277EEB0C8DDE4CF21A6D012DD2DBBEB99A67E3DA`
and 4,166 runtime files with SHA-256
`226BA1BF1E4A349B09CD608948B86AECC420BF189490FBC2904BC2B68977AE1B`.
The 345.381, 642.467 and 398.242-second observers cover every respective command;
no observed process or new database-name delta remains. Unowned TEMP entries remain
untouched. These roots are Released after secret-reviewed retention.

Retained JSON under `proof/2026-10-19-planning-authority`:

| Candidate | Result | SHA-256 |
| --- | --- | --- |
| `c4-accepted-source-persistence-final` | 51 pass; combined regression timeout | `AAE0200A3B2C9C206C58FA71BD70DD65D550CEF38B209DCD894C0C80785B1D9D` |
| `c4-accepted-source-sql-verified` | Five EXPLAIN assertion failures | `3447EFC349F997CC1DBC9B9E4A71D1E757BA99917452E186F182ABA76B44A741` |
| `c4-accepted-source-sql-final` | 25 pass | `DF705EB4DE3916F1CCE1F05A8947DB75E43E597A8D67128B1D1C018901C58F33` |
| `c4-accepted-source-authority-verified` | 48 pass | `50ED8E8B5568DD68BFA5D3C7111E06C78C7658A8AF9F48DE3F5C75FF3BC975B4` |
| `c4-accepted-source-dispatch-verified` | 69 pass | `6B87B0430D508F66D400F04793E7CFBEC3DCCEB1BBCE85DCC571AE7913286893` |

The timeout leaves isolated database `cditall_primary_3c41089b37074f30b5d6889519d95021`
with zero connections. Exact lease ownership is not recoverable from the available
evidence; it is preserved and the timed-out root is ReleasedWithUnattributedDatabase.
This remains a resource-closure blocker. Historical automatic cleanup denials also
remain open; later zero deltas do not erase them.

## Actual PostgreSQL measurements

The five shapes use one process, three rounds of eight reads per format, one reused
DbContext/open connection, and five independent acceptance observations. Read timings
exclude connection/context checkout and other authority queries. SQL/EXPLAIN and raw
measurements are in the retained SQL proof's `measurements` evidence. All SQL cases
verify the version conditional, exact join and limit; wide plans verify index use.

| Steps / prompt characters | Version-1 bytes | Version-1 median ms | Legacy bytes | Legacy median ms | Acceptance / transaction ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 / 256 | 2,316 | 2.061 | 9,724 | 3.460 | 38.874 / 35.304 |
| 64 / 256 | 2,317 | 2.643 | 282,928 | 21.766 | 1,150.193 / 1,084.419 |
| 256 / 256 | 2,318 | 1.220 | 1,116,100 | 30.695 | 384.014 / 323.585 |
| 1024 / 256 | 2,319 | 2.904 | 4,449,292 | 137.054 | 2,593.311 / 2,243.063 |
| 1 / 1 MiB | 2,316 | 1.640 | 1,058,044 | 13.391 | 90.284 / 72.980 |

Projection storage grows from 2,316 to 1,825,218 bytes across one to 1024 steps, paid
once on acceptance. Selected reads are independent of unrelated steps/prompts. Initial
acceptance remains proportional to plan size and requires sustained contention/capacity
qualification. These local observations do not establish HTTP latency, throughput or
an SLA. Workflow, bounded legacy conversion and actual inherited producer integration
remain open. The historical full Stable run is still not green.
