# A02 independent Gate C1 review

## Decision

**NO-GO — Gate C1 is blocked.**

A02 must reopen FS-008 before any dependent subbundle advances. The frozen evidence is
green for the exercised cases, but it does not cover an exact collision between an
encoded physical name and a distinct, already-portable display name. The production
storage path can overwrite the first object in that case.

## C# Architecture Gate Result

Status: **Blocked**

### Findings

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| Blocking / High | Distinct display names can resolve to the same physical filename and `SaveAsync` then replaces the existing object. | `PortablePhysicalFileNamePolicy.cs:71-76` recognizes only comparer-equal, ordinal-different collisions; exact equality is explicitly excluded. `FileSystemStorageDriver.cs:243-251` passes existing directory names to that codec, while `FileSystemStorageDriver.cs:80-86` writes the returned path with replacement semantics. Constructively, encode and save `report:final.txt`, then use its portable hash-suffixed physical name as a second display name: the second encode returns the occupied name and the second save overwrites the first object. The codec also does not recheck occupancy after adding a collision suffix. Existing codec tests cover invalid-name hashing and case-only collision, not exact/generated-name collision or occupied post-suffix allocation. | Define explicit allocation/identity semantics so distinct display names cannot alias an occupied physical name. Add codec and storage-level regressions for an exact generated-name collision and an already-occupied suffix result. Refresh the affected Windows/Linux test, build, static, redaction, manifest, and checksum evidence before re-review. |

### Dependency direction

No dependency-direction blocker was found in the reviewed tree. The physical policy port
remains in dependency-free `CanDoItAll.Infrastructure.Abstractions`; Infrastructure owns
host probing, link checks, durable mutation, locks, and Unix modes; SharedKernel contains
the pure filename policy. The supplied deterministic graph reports 105 projects, 631
direct references, and zero project cycles.

### Partial-class policy

No A02 partial-class expansion or fake boundary was identified in the reviewed source.

### Testability proof

The supplied Windows/Linux TRX and Release build artifacts are green for their named
scopes, and scoped registry/DI isolation has explicit coverage. They cannot close C1
because the blocking FS-008 collision path is untested and follows directly from the
current production codec and storage call site.

### Closure decision

A02 and Gate C1 remain open. A03 must not start. Independent re-review is required after
the collision fix and refreshed two-host evidence are stable.

## Residuals and evidence gaps

- Actual macOS execution remains deferred to core Gate C4, as already recorded.
- The managed-API link-swap interval remains an explicit FS-004 residual; it is not the
  reason for this NO-GO.
- Adding this report changes bundle integrity. The primary executor must regenerate the
  bundle index/checksums and rerun the portable validator after the reopened A02 work is
  complete.

## Re-review

### Decision

**NO-GO — Gate C1 remains blocked.**

The identity/allocation portion of the original FS-008 finding is corrected. `Encode`
is now stable and context-free, while `Allocate` treats exact occupancy as a collision
and rechecks deterministic suffix candidates. Filesystem storage uses allocation only
when no explicit relative-path hint is supplied, and the new codec/storage/legacy
identity tests cover the original constructive collision, an occupied suffix, three
distinct stored contents, cooperating concurrent writers, and stable legacy duplicate
identity.

One blocking clobber race remains in the new-object commit path:

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| Blocking / High | Auto-allocation does not atomically enforce target absence, so an uncoordinated creator can still have its file overwritten. | `FileSystemStorageDriver.cs:88-90` supplies a callback that checks `File.Exists`/`Directory.Exists` at `FileSystemStorageDriver.cs:236-247`. After that check, `DurableFileWriter.cs:216-220` revalidates and crosses the `BeforeCommit` observer boundary. If a file appears there, `Commit` observes it and selects `File.Move(..., overwrite: true)` at `DurableFileWriter.cs:444-458`. The new concurrency test covers only participating writers serialized by the target lock; it cannot prove non-clobbering against a creator that does not take that lock. This gap is deterministically injectable through the existing `BeforeCommit` observer. | Give new-object allocation a typed atomic no-replace commit mode and perform `File.Move(temporaryPath, fullPath)` without overwrite, allowing occupancy to fail while preserving the winner. Add a post-guard/pre-commit occupancy regression that proves the independently created content remains intact. Refresh the affected Windows/Linux test and build evidence before re-review. |

This is not the documented unavoidable managed-API link-swap residual: managed
`File.Move` already provides atomic fail-if-present behavior for this case. The refreshed
green artifacts demonstrate the exercised paths but do not cover the remaining window.
A03 must remain blocked. Actual macOS proof remains deferred to C4, and final
index/checksum/validator closure remains the primary executor's responsibility after a
future independent GO.

## Atomic no-clobber re-review

### Final decision

**GO — Gate C1.**

The remaining FS-008 clobber blocker is closed, and no new blocking correctness,
security, portability, dependency-direction, lifetime, or evidence finding was found.
A03 may become eligible only after the primary executor records this final decision in
the canonical gate/status/exit surfaces and regenerates bundle integrity.

### Closure evidence

- `DurableFileCommitMode` makes replacement versus creation an explicit, strongly typed
  choice. `ReplaceExisting` remains the default; `CreateNew` is opt-in, and validation
  rejects the nonsensical `CreateNew` plus backup combination.
- The durable commit branch at `DurableFileWriter.cs:457-481` performs
  `File.Move(temporaryPath, fullPath)` without overwrite for `CreateNew`. Occupancy at
  any point before that atomic move therefore fails without modifying the winner.
- `FileSystemStorageDriver.cs:80-93` selects `CreateNew` only for auto-allocated new
  objects. An explicit `RelativePathHint` retains the documented intentional update
  behavior, and the earlier stable `Encode`/allocating `Allocate` separation remains
  intact.
- The deterministic regression at `FileSystemStorageBrowseDriverTests.cs:96-126`
  creates an outside target at the `BeforeCommit` stage, after the earlier absence
  callback, and proves that `SaveAsync` throws while the outside-winner bytes survive.
  This directly exercises the window that blocked the preceding re-review.
- Parsed the final Windows and Linux atomic TRX artifacts: both pass `60/60`. Parsed the
  final Windows full-unit artifact: `5,442/5,442`. The named Windows solution and Linux
  unit/solution Release logs contain no warning or error findings.
- Independently reran the exact four-class Release/no-build slice on Windows: `60/60`
  passed.
- Independently reran `git diff --check`: passed with only the recorded non-semantic
  line-ending warning.
- Independently reran the portable validator with deferred checksums: 282 files, zero
  errors, zero warnings.
- The refreshed report-only secret scan covers 72 files and reports 96 occurrences in
  the same six synthetic test fingerprints; no new fingerprint or unclassified secret
  category was introduced.

### Residuals and closure actions

- Actual macOS execution remains mandatory before core Gate C4.
- The documented managed-API link-swap interval remains a residual; it is distinct from
  the now-closed no-clobber commit race.
- Existing intra-project cycles remain downstream architecture inputs; the accepted A02
  project graph remains acyclic.
- The primary executor must now add the final review text to `bundle-index.json`,
  regenerate `CHECKSUMS.sha256`, rerun the portable validator without skipped checksum
  verification, and synchronize the canonical C1/status/exit records before starting
  A03.
