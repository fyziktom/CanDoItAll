# Original accepted Process input

The [accepted design](../plan/P07-original-process-input.md) is implemented and qualifies
96 distinct behaviors. Original decision assignments are captured atomically beside
accepted metadata; the registered reader selects only the requested input. Current
assignment repairs, corrupted full-launch review copies, retries and legacy rows cannot
replace that input. Reading it grants no dispatch or publication authority.

## Correctness and migration

The first frozen candidate passes 92 of 96 cases. Its four scaling failures counted
`PayloadJson` mentions in SQL text, including both inner and outer projections. The
corrected probe checks actual returned column names. All four repeated cases pass;
production sources and 392 production DLL copies are identical across candidates.
The only source changes between candidates are that test and the resource record.

The 96 behaviors cover the input and authority codecs, actual first acceptance,
rollback after SQL flush, lost commit acknowledgement and restart, original/current
prompt separation, retained source compatibility, inherited workspace composition,
migrations, corruption/substitution refusal and selected-read scaling. Migration
upgrade/restart preserves legacy bytes without backfill; constraints and guarded
downgrade protect captured input. The current EF model has no pending changes and
idempotent SQL generation passes.

Direct Application, Persistence, Processes module and PostgreSQL migrations Release
builds pass, as do the owning Unit and Integration builds. Initial test compilation
needed one missing namespace import; the failed command is retained. Final full
portability enforcement passes with 15,656 unchanged findings after reviewing four
new generated-designer property-name findings. Documentation, rendering register
and whitespace checks pass. No new project references, cycles or analyzer findings
appear in the five-project architecture comparison. Registered-reader tests cover
the DI registration that the static analyzer did not independently enumerate.

## Observed selected-read cost

Each measured shape has three warmed rounds of eight reads. Every read issues one
query and returns exactly one header, metadata payload and input payload. The
1,024-step plan uses the accepted-assignment primary key. Authority-only reads
exclude input JSON and its fingerprint.

| Steps | Neighbor prompt | Selected UTF-8 bytes | Mean read time across 24 reads | Approximate process allocation per read | Initial acceptance |
| --- | --- | ---: | ---: | ---: | ---: |
| 1 | 256 characters | 5,344 | 2.22 ms | 144 KB | 56 ms |
| 128 | 256 characters | 5,348 | 2.87 ms | 145 KB | 500 ms |
| 1,024 | 256 characters | 5,350 | 3.70 ms | 148 KB | 4,828 ms |
| 128 | 1 MiB neighboring prompt | 5,348 | 4.16 ms | 147 KB | 594 ms |

These are local shared-host observations; the portability scan overlapped part of
the run. Allocations are process-wide estimates. They are not percentile or
sustained-load guarantees. Query count, projected bytes and indexed selection are
the deterministic bounds. Initial acceptance remains proportional to the launch and
requires separate capacity qualification.

## Retained proof and resources

The initial 92/96 candidate remains recorded in
`proof/2026-10-19-planning-authority/c4-original-process-input.json`,
167,544 bytes, SHA-256
`44D710FD3ABFA0FA9ADE594148BE29E821C266ED8968E7FB36BC4359253A0840`.
The final 4/4 candidate is
`proof/2026-10-19-planning-authority/c4-original-process-input-final.json`,
78,556 bytes, SHA-256
`C213302243F4C69E7565D9D024C38E05FE0C09A713FD0DC40B85CA20F8E71509`.
Both secret reviews have zero findings.

Final source: 9,800 files,
`0DF9AA2F6AC3C6C94ACD97BEEAAF68BFEC5C1469E342AD7C43966691C958EB4A`.
Final runtime: 2,595 files,
`83BE0ECF3F9BE48D7EDF189F631D84E9E625F77D952438AC2477BC671C0B4FC2`.
Source/runtime identities remain unchanged throughout each test candidate. Observers
cover every test command; no observed process, database-name or temporary-path delta
remains. Both ownership records are Released. Historical cleanup denials remain open.

Startup failure accounting, capacity, executor activation, manager continuation,
cancellation/refresh, Workflow, settings/SPA and broader closure remain open. SPA stays
on `codex/concurrent-mutations` without merge or push.
