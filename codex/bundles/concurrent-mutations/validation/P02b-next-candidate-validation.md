# Current authority, staging and receipt-evidence checkpoint

Status on 2026-10-16: frozen candidate execution completed with failures. Production
and Unit/Components/Integration builds and final portability enforcement passed.
All 361 selected rows executed: **347 passed and 14 failed**. This is a
new candidate after the retained [404-case checkpoint](P02b-managed-candidate-validation.md),
not a replacement or relabeling of its failures.

The parent selectively copied 45 reviewed files, followed by the two H2 PostgreSQL
test files and their source inventory. Ignored transfer records preserve each old
main/incoming hash and verify the copies. The managed checkout continues separate
C2/W1b/runtime authoring; it does not alter this main validation candidate.

## Review and changes

- H2 separates immutable middleware evidence from bounded current write observations.
  Projects has no Web/Workspace dependency. Current authority is checked after owner
  admission waits and after the actual final source/lifetime guard, before recording
  a commit attempt. A trusted precommit refusal survives cleanup failure. Existing
  uncertain-commit and historical receipt rules remain distinct.
- C1 prepares a complete bounded plain-task batch before tracking anything. It uses
  the exact caller-owned Read Committed transaction, excludes external bindings,
  enforces stored metadata and combined basis limits, and adds no global binding
  lock or per-task SELECT. Independent source review found no blocker; C2 and the
  complete publisher remain separate gates.
- R1b-first retains immutable evidence from actual committed lease admission and
  fences read-only leases from dispatch/provider/approval paths. Claims require
  current read access. Its test counts are four new rows, 95 including the existing
  41 Unit/50 Integration journal regressions. Full reconciliation/one final CAS,
  Process evidence adapters and live producer activation remain unimplemented.
- The previous nine failures have targeted fixes: include the existing fifth HR
  receipt provider; describe actual array/enum/reference and routed 415 schemas;
  and assert successful starter removal while retaining the component owner before
  disposal. The two Workbench changes are test-only.

Seven affected production projects built directly in standard Release with local
libraries and `/m:1`: Infrastructure, AgentFramework.Core, Projects.Contracts,
Projects, Workspace, Workbench and Web. Unit, Components and Integration also built successfully;
Integration refreshed the existing maintenance child host.
Existing package warnings remain. The Web startup rendering-register delta was
reviewed: authority preparation belongs before readiness in the existing composition
host. Only that input fingerprint was refreshed, and all 1,196 inputs pass.
Portability/redaction/rendering tool self-tests passed 6 +4 +7. The complete scan
included untracked source: 8,445 scanned files of 8,470 candidates and 36,420 findings,
without truncation. Its sole added/stale executable baseline pair was the reviewed
private `Open` to internal `OpenRead` test seam. Read access, delete sharing and
asynchronous handle options are unchanged. The inspected baseline refresh changes
that fingerprint only, apart from its timestamp. Final enforcement **without
`--write-baseline` passed with 15,454 allowances**.

Scan SHA-256: `23d445ce29ff8447946b221c6ac8aead1efc15917efd4e3653ff322f59f09efa`.
Baseline SHA-256: `f943527fc2792d2120f717b9ae4b210c2f9d51fabfdab9deb5862161b174a5b9`.

## Source expectations

The selection preserves relevant existing owner, HTTP, account-authority,
journal, B1 and Workbench cases and adds H2's 30 Facts, C1's 16 Facts and R1b-first's
four rows. The existing ten session-boundary rows are added as regressions for the
shared validation-core change. No source count is taken from test runner output.
The source-derived manifest selects **361 rows in 21 lanes**, including **50 new
rows** and the ten existing session-boundary regressions. Its 91 source/project/card
identities were frozen before discovery and confirmed around execution. All 21
discoveries matched; unique execution IDs, labels and definitions matched every
selection. Failed commands and identities are retained separately.

## Executed result and follow-up repairs

| Selection | Executed | Passed | Failed |
| --- | ---: | ---: | ---: |
| H2 current-write Unit | 10 | 9 | 1 |
| H2 current-write observer | 8 | 8 | 0 |
| H2 current-write PostgreSQL | 12 | 2 | 10 |
| C1 plain-task batch Unit / Integration | 16 | 16 | 0 |
| R1b pinned-evidence checkpoint | 4 | 4 | 0 |
| Definition HTTP/schema | 49 | 49 | 0 |
| API documentation | 23 | 21 | 2 |
| Workbench protected creation components | 17 | 17 | 0 |
| Receipt-recovery composition Unit / Integration | 21 | 21 | 0 |
| H1 file authority Unit / Components / HTTP | 54 | 54 | 0 |
| R1a journal Unit / Integration | 91 | 91 | 0 |
| B1 basis capture Unit / Integration | 22 | 21 | 1 |
| Owner commit knowledge | 23 | 23 | 0 |
| Originally stalled continuation | 1 | 1 | 0 |
| Session boundaries | 10 | 10 | 0 |
| Total | **361** | **347** | **14** |

The Windows lifecycle test exposed a real production defect: `File.Move` could
not replace the target while its production read handle remained open. The new
[publication repair](../reviews/H2-windows-publication-repair.md) changes the primitive
and preserves partial-failure recovery bytes; it is not part of this result.
The ten PostgreSQL failures concern the fixture observing connection disposal when
the factory actually pools contexts. The repaired probe observes actual closure
and independently reacquires the project guard. The two remaining documentation
failures require annotations around serialized references, not descriptions on
reference siblings. The B1 observer queried a transaction-cached activity snapshot;
its test-only repair observes the exact live blocking backend. Each repair requires
new build-backed discovery/execution; none changes these retained failures.

Manifest SHA-256: `11d1a1873465f4fbcf742e6e0e92e42b4a86ab2d91cbd5d5ac76e1dee79e8b8f`.
Whole source identity: `e2e9d930b22a0216db10fbbf454f772ddfa7192b226a81a502419e4bf9ebe5c5`.
The ignored completed summary and original per-lane TRX/logs retain the evidence.
No source/runtime edits occurred between discovery and the final lane.

This does not close broad Stable, capacity, remaining owners, persisted execution
settings or end-to-end concurrent mutations. The current broad result remains
16,199 executed, 17 failures and 2,444 unexecuted. The outstanding editor acquisition
failure is still diagnostic-only. The configured GPG signer is still waiting for
user interaction; no signing bypass or additional blocked commit process was started.
