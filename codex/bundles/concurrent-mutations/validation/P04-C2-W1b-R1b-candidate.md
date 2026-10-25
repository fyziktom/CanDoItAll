# C2 / W1b / R1b integrated candidate

Status: reviewed source integrated into the main checkout after the 178-case repair
checkpoint. All 22 affected production/support/test projects built in standard Release.
The complete portability scan includes untracked files and final no-write enforcement
passes with 15,465 reviewed allowances. Rendering review passes for 1,197 inputs.
Runtime discovery and execution completed: **482 cases, 468 passed, 14 failed, no skips**.
All 19 lanes retained exact source, binary, label and definition identities. This is a
failed checkpoint, preserved before repairs. These statements do not close the broader
engine request, owner cutover, maintenance capacity or the incomplete broad Stable run.

## Source and review

- C2 uses the [48-file authored inventory](P04-C2-source-inventory.json), its four explicit
  removals and [boundary review](P04-C2-neutral-source.md). The lossless generated migration
  is `20261016233100_ExtractFiniteOperationEpochMetadata`; its final body replaces the
  unexecuted destructive scaffold. Four root-authored migration Facts additionally check
  exact retained tuples/receipt observation, failed-history rollback, catalog extensions
  and drift, fresh initialization, model parity, constraints and unconditional Down refusal.
- W1b uses the [41-file authored inventory](P05-W1b-source-inventory.json). Three migration
  Facts cover empty and retained Workspace heads; two overlap theories provide four rows
  through real bootstrap/save and transfer/save lock-manager barriers. The API schema's
  referenced model annotations use an explicit `allOf` wrapper, matching the concrete
  OpenAPI schema-generation API and existing structural resolver.
- R1b uses the [authored inventory](../reviews/R1b-frozen-source-inventory.json). The retained
  current-read assertion now expects the typed unavailable exception and original inner
  error. Two receipt-reader test helpers use explicit `ValueTask.FromResult` to avoid
  constructor overload ambiguity. The final bounded-receipt selection is 24 rows.
- The immutable authored inventories remain historical source-transfer identities.
  Post-transfer corrections above, root migration tests and generated migration assets
  are additional integrated inputs; no authored inventory is relabeled as executed.

The migration source has independent review. It compares actual PostgreSQL-deparsed
constraints/indexes before and after, preserves table OIDs through rename, and copies only
metadata for correspondence checks. The generated idempotent SQL is transaction-bound and
guards both migration work and its history insertion. Receipt payloads remain unchanged.
Old-binary deployment, retained-volume timing and restore proof remain separate gates.

## Validation record

Local evidence root:
`.artifacts/concurrent-mutations/validation/c2-w1b-r1b-validation`.
The exact source-derived manifest is its sibling `c2-w1b-r1b-focused-selections.json`;
runner `Invoke-NeutralWorkspaceFocused.ps1` preserves source and binary identities before
and after discovery/execution. Planned selection: **477 stable rows**, **2 separately
selected LiveProcess rows**, and **3 separately selected browser rows**. A count mismatch,
failure or skip is not a pass and must not be hidden by regenerating expectations.
Independent source review rejected the initial whole-file count for a file containing
ten test classes. The corrected transfer lane explicitly selects seven transfer Facts
and two ordinary persistence Facts alongside its other nine migration/transfer rows.
All other selector counts, partial files, traits and fixed theory inputs were verified;
the rejected manifest is retained and was never used for discovery or execution.

The executed manifest SHA256 is
`D9514D6B2958FFB43A52B0A71514D309623DFAAE28BEC44AF9E07DE15D857BC1`;
the source identity is `5990547ce6868273eda5cec43ed42de65fde23b93eac73102b278448d1a29442`.
`completed-summary.json` has SHA256
`F93043A6166BC6B39BD8E872CBF4A3D9ECAE6369EBAE50E4D34E75BB0BBB2415`.
No failed selection was retried or overwritten in this candidate.

| Lane | Passed | Failed |
|---|---:|---:|
| Migration retained/catalog | 3 | 4 |
| Finite contract unit | 32 | 0 |
| Finite persistence | 46 | 2 |
| Workspace real overlap | 14 | 0 |
| Workspace HTTP callers | 14 | 0 |
| Workspace owner callers | 81 | 2 |
| Workspace transfer unit | 4 | 0 |
| Workspace presentation | 24 | 0 |
| Transfer/schema regression | 16 | 2 |
| R1b bounded receipts | 24 | 0 |
| Definition HTTP/schema | 48 | 1 |
| API documentation coverage | 23 | 0 |
| R1a journal unit | 41 | 0 |
| R1a journal integration | 50 | 0 |
| Workbench protected-create components | 17 | 0 |
| Commit-knowledge owner regression | 20 | 3 |
| Task-plan C1 integration | 6 | 0 |
| Activation live process | 2 | 0 |
| Workspace browser | 3 | 0 |

The three permanent migration refusals used SQLSTATE55000, which Npgsql classifies as
transient and wraps. The reviewed production repair uses P0001 and exact nontransient
assertions. The remaining migration failure inserted a post-upgrade Workspace row without
explicit `IsCurrent`. Two finite persistence failures incorrectly relied on disposed
pooled transaction-wrapper identity or attempted independent reactivation inside an
ambient owner transaction. Two Workspace caller fixtures supplied incompatible canonical
attachment or a nonexistent provider. Two historical rollback fixtures reached C2's
unconditional Down refusal before their intended historical subject. The epoch OpenAPI
resolver did not understand the reviewed description/reference wrapper. Three owner
recovery rows matched the new neutral epoch query as if it were a receipt observation;
their epoch-read predicate also assumed the former table was the FROM root. Repairs retain
the original safety assertions, exact receipt knowledge and real concurrency barriers.

All three browser tests passed. Retained, visually reviewed production conflict and
reconciliation captures are1920x1080; the standalone scenario capture is1600x1000.
`browser-evidence/reviewed-images-v2.json` records copied immutable image hashes and
dimensions. The post-run endpoint audit found only the preexisting policy-blocked database
with zero clients plus the administration database; no Web or Workspace sandbox process
matched the process snapshot. The shared PostgreSQL fixture remains needed. This current
observation is not a complete historical browser descendant-process ledger.

R1b maximum-journal elapsed time and process-wide allocations are initial cost
characterization, not production performance qualification. The structural contract
requires at most four candidates, a16KiB response, four full-run reads and two durable
intents in core reconciliation, independent of one versus four examined candidates.
R1c adds one application admission read. The five-second slice and two-second checkpoint
budgets are cooperative; they do not bound OS/file wall-clock time. Production capacity
and an operational latency/allocation acceptance threshold remain unresolved until the
recorded costs and the complete actual-host path are reviewed.

The measured maximum-count journal (32,768 proposals, four examined) took2404.72ms and
allocated2,171,814,784 process-wide bytes; the near24MiB journal took1371.54ms and
allocated1,755,345,160 bytes. Trace-scoped JSON write payloads were235,948,901 and
151,304,348 bytes respectively. These are process allocation totals, not peak heap, and
instrumented payload counts, not complete filesystem/WAL I/O. Trace reads count full
ExecutionRunRecord reads, not every file read. Repeated validation/encoding and durable
previous/target checkpoint copies are an activation blocker requiring architectural and
measured correction. A small bounded response does not establish bounded useful work.

The candidate has no R1c source. That continuation is authored in the managed checkout
while the main checkout remains frozen for execution. The existing isolated PostgreSQL18
fixture is reused; ordinary profiles and the application on port5032 are excluded.

Static tooling self-tests passed:6 baseline,4 redaction and7 rendering tests. Review found
eight intentional new portability occurrences: two ordinal JSON duplicate-member checks,
two escaped schema regexes, and four existing model-property names in the new generated
designer. No stale allowances or genuine new platform defect remained. Only those eight
allowances were added; final enforcement ran without `--write-baseline`.

Failed preliminary compile logs are retained separately: the OpenAPI return-type pattern
and two ambiguous test ValueTask constructors were repaired before the final22-build pass.
The EF10.0.3 tooling/runtime10.0.4 warning remains recorded. Build warnings are not presented
as resolved by this concurrency change.

Signing remains pending on the existing SPA commit; no signing configuration was changed.
Previously recorded automatic cleanup rejections remain unresolved and are not bypassed.
The task is not cleanup-complete.
