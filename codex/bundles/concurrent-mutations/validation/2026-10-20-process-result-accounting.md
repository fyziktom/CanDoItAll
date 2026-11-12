# Process result accounting validation

Status: PASS for result/accounting integration, with 369 distinct passing behaviors
across the exact repair boundary below. Product activation and E2E closure remain open.
Signed prerequisites: bd1bc1991 and d2c57247b.

[Accepted design](../plan/P07-process-result-accounting.md) defines original identity,
bounded reads, immutable inputs, compound closure, replay and atomic acceptance.

## Candidate and ownership

Owned candidate: `.artifacts/concurrent-mutations/validation/c4-process-result-accounting`.
It reuses standard Release outputs and the retained isolated PostgreSQL 18 fixture on
53619. The ordinary instance on 5032, unrelated databases and installed tools remain
outside this candidate. No new fixture family, container or persistent host is created.

Runtime, Application, Persistence and the Processes module are built directly. Unit and
Integration assemblies are refreshed for the same source. Expected discovery: 282 cases
across eleven explicit class filters recorded in the candidate manifest. They cover
snapshot validation, the new coordinator, decision transitions/receipts, existing runtime,
dispatcher and interrupted-execution callers, real persistence and registered saved-tool
owner reads. The counts and results must be confirmed before this record can pass.

The frozen initial candidate executed all 282 discovered cases: 273 passed and 9 failed.
Six terminal round-trip assertions and three registered replay cases exposed loss of
ExecutionRunId for an empty diagnostics array without host evidence. All 14 historical
reader cases passed. Initial proof: `c4-process-result-accounting.json`, SHA-256
`BE6E328285DA1051D2D1540695CEAF222C3CF0AB0126815B5CD41969D2538DDB`.
The source (9735 files) and runtime (3976 files) remained unchanged throughout execution.
All eleven commands were observed; no owned process or database-name delta remained.

The repair uses the existing version-one envelope only for the otherwise lossy empty
array and moves acceptance to the already-reloaded persisted state inside the transaction.
It adds independent mapper regressions and real SQL identity-loss rollback injection.
The successor candidate `c4-process-result-accounting-repaired` repeats every original
filter and adds the mapper cases and the existing ProcessPersistenceStoreTests.
Expected discovery is 367 across thirteen filters: 300 Unit and 67 Integration cases.
The same isolated fixture and standard outputs are reused. Repair architecture snapshot
`snap-20261020210600-5874c340` retains the same types, findings, diagnostics and six
project edges as the initial implementation; zero scoped cycles and no added findings.

The repaired candidate executed all 367 discovered cases: 363 passed, four failed.
Every PostgreSQL case and all seven mapper cases passed. Four existing InMemory
persistence tests exposed non-nullable join keys on an absent decision head.
Retained proof: `c4-process-result-accounting-repaired.json`, SHA-256
`6C10892F70344AE4533BFC0FB36B1C3B59CD100AC92D0A9A31B1831E2B489A67`.
Source and runtime identities stayed unchanged; all thirteen test commands were covered
and no owned process or database-name delta remained. An initial observer setup failed
before tests because its ownership path used the wrong field name; the identical recorded
path was supplied under `root` and the successful retry covered the entire test run.

The optional-join successor `c4-process-result-accounting-joins` changes only the result
query and adds two real PostgreSQL legacy cases. All 111 discovered cases passed:
77 persistence, 25 completion and nine registered result cases. The complete source
comparison confirms exactly that executable delta plus the design and validation records.
It retains 258 disjoint unaffected passing cases: 223 pure/dispatcher/recovery/mapping,
21 budget and 14 historical-reader cases. This yields 369 distinct passing behaviors.
Final proof: `c4-process-result-accounting-joins.json`, SHA-256
`32B7FF6C61EA271CD06ABAC7665E38FEA6CF755A335A69C9EED7698799EBA282`,
137637 bytes, zero secret findings.

Final source identity: 9736 files,
`8514E9E496C205B98D2267D3C52B9924D7A08777CE904884CA65AD76B0CC55E1`.
Final runtime identity: 3976 files,
`D99EB7BA16F53E6D1BB7F01CB8F96F1F0B83F0EF4C11375CE790EAF4AB5B7465`.
Both remained unchanged through execution. Persistence, Unit and Integration Release
outputs were refreshed after the join repair; the preceding candidate directly built
Runtime, Application, Persistence and Modules.Processes. Exact discovery, rendering
register, documentation and diff checks pass. Full portability enforcement passes with
15651 unchanged findings and no baseline rewrite.

The final 371.674-second observer covers all three test commands. Reconciliation found
zero remaining observed processes and zero database-name delta. Eleven TEMP entries
without proven ownership were preserved. This releases test execution, not the shared
fixture or the historical cleanup blockers.

## Review findings

Legacy result submission adds two indexed governance checks, including the authoritative
check inside the existing fenced transaction. Selected binding lookup uses four bounded
metadata reads; original launch projection and material metadata remain separate from
material payload. Registered query tests record latency and count; no sustained-capacity
or performance improvement claim is made.

The initial rendering-register check identified the Application logging package and
the previous checkpoint's canonical historical-reader registration. Both were reviewed
against the UI seams guidance and only their two entries were updated. Neither change
adds rendering or changes editor lifetime. Portability enforcement must pass without
rewriting its baseline.

Architecture before: snap-20261020194602-5874c340, 1094 types, 9587 members,
350 findings, 190 existing diagnostics. After: snap-20261020203938-5874c340,
1104 types, 9630 members, 352 findings, the same 190 diagnostics. Six scoped
project edges are unchanged and there are no scoped cycles. Dashboard inspected.

The two added member-count advisories identify the immutable selection contract
(properties and original-identity validation) and the result accounting coordinator.
Both have one responsibility. No wrapper was introduced to hide the count. Bounded
envelope validation left the existing Runtime partial and has independent tests; the
new coordinator is instantiated independently with query, reader and receipt seams.
Production dispatcher and interrupted-execution recovery require it. No runtime partial
or project reference was added. Application adds only Logging.Abstractions at the
repository version for bounded read-failure diagnostics.

Final snapshot `snap-20261020212822-5874c340` preserves the implementation's 1104 types,
9630 members, 352 findings, 190 existing diagnostics and six edges, with no scoped cycles
or added findings after either repair. The dashboard was inspected. Persisted-state
acceptance runs inside the existing fenced transaction and its rollback is exercised
against PostgreSQL. No healthy whole-solution graph or sustained-capacity claim is made.

## Remaining gates and scope

Exact discovery/execution, full portability enforcement, source/runtime identity,
secret-reviewed retained proof and observed resource reconciliation pass. Producer
capture/activation, manager continuation, Workflow, settings,
SPA product activation, sustained capacity and the final broad closure remain open.
SPA stays on `codex/concurrent-mutations`; it is not merged to main.
The historical broad Stable run remains incomplete: 16199 executed, 16182 passed,
17 failed and 2444 unexecuted. This scoped checkpoint does not supersede that result.

Previously denied cleanup remains blocked: atomic-outcome candidate directories,
root-sequence-probe outputs, SPA hook directory, and the owner-outcome verified/final
directories listed in the resource ledger. No denied deletion is retried.
