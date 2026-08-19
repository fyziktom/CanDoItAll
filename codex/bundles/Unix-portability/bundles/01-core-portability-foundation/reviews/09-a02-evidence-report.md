# A02 evidence report

## Decision state

- Implementation: complete for A02 scope.
- Evidence: complete, independently reviewed, and frozen for C1 closure.
- Gate: C1 GO; A03 becomes eligible after canonical integrity closure.
- Source anchor: `a2856070e7303de077088fc7f2f7e96a5bcf0e70` plus the reviewed A01/A02 working tree.

## Design result

A02 establishes a narrow physical-filesystem authority rather than scattering host rules through Core and MAF:

- `CanDoItAll.Infrastructure.Abstractions` owns the root-scoped path-policy port and typed case/validation contracts. It remains dependency-free.
- `CanDoItAll.Infrastructure` owns case probing, containment, link/reparse traversal, mutation-parent revalidation, bounded cross-process locks, Unix mode verification, and durable same-directory commit.
- SharedKernel owns only the pure portable filename codec. It performs no I/O or host probing.
- Portable filename identity and allocation are explicit: `Encode` is stable and context-free,
  while `Allocate` consumes the occupied physical-name set, treats exact generated-name
  occupancy as a collision, and rechecks every deterministic suffix candidate.
- Logical identities are ordinal. Physical equality is captured per root/volume; an unknown case model fails conservatively.
- The mutable external-target registry is scoped in Infrastructure, Hosting, AgentFramework, and Processes composition roots. Its stateless factory is singleton. Strict DI and two-scope tests prove that physical authority does not leak across scopes.
- Managed reads, mutations, process targets, workflow artifacts, storage, packages, conversations, sandbox persistence, and control-plane/catalog writers use the same root/link and atomicity contracts at their physical boundaries.
- Watchers are hints: debounced generations converge through deterministic fingerprint/rescan and polling fallback.

The detailed boundary, link, residual TOCTOU, durable-write, portable-name, enumeration, and watcher decisions are recorded in `architecture/08-a02-filesystem-semantics.md`.

## Requirement evidence

| Requirement | Result | Principal proof |
|---|---|---|
| FS-001 | Verified candidate | Root-specific case detection; ordinal logical identity; case-sensitive/insensitive/unknown fixtures; Windows and actual Linux execution. |
| FS-002 | Verified candidate | Deterministic storage, workspace, workflow, catalog, package, lease, query, and projection enumeration tests plus final static audit. |
| FS-003 | Verified candidate | One root policy applied to read/write/open/process/migration boundaries; Windows reparse and Linux symlink escape fixtures fail closed. |
| FS-004 | Verified candidate | Immediate parent revalidation, same-directory staging, bounded cleanup, malicious link-swap fixtures, and explicit managed-API residual documented. |
| FS-005 | Verified candidate | Shared durable writer, flush-to-disk, atomic replace/rename, backup and crash-child proof across authority writers. |
| FS-006 | Verified candidate | Cross-process lock contention, timeout, cancellation, process-death recovery, and no-corruption fixtures. |
| FS-007 | Verified candidate | Actual Linux `0700` directory/`0600` file verification and failure-closed hardening tests; actual macOS remains a C4 condition. |
| FS-008 | Verified candidate after remediation | Host-independent golden filename cases, reserved names, Unicode/backslash/colon handling, stable identity encoding, exact/generated-name collision allocation, occupied post-suffix allocation, storage-level distinct-object preservation, atomic create-new/no-replace commit against post-guard occupancy, and length proof; zero `Path.GetInvalidFileNameChars()` calls. |
| FS-009 | Verified candidate | Watch overflow/error, duplicate/rename storm, generation, fingerprint, and polling convergence tests. |
| FS-010 | Verified candidate | Exact-name temporary/backup cleanup, malicious-link cleanup boundaries, cancellation, crash, and failure-injection tests. |

## Final commands and results

| Host | Scope | Result | Evidence |
|---|---|---:|---|
| Windows | Full unit project after atomic no-clobber remediation | 5,442/5,442 | `artifacts/unix-portability/A02/windows/A02-windows-full-unit-atomic-no-clobber-final.trx` |
| Windows | FS-008 allocation, durable commit, storage, and stable-identity slice | 60/60 | `artifacts/unix-portability/A02/windows/A02-windows-fs008-atomic-no-clobber-final.trx` |
| Windows | Rejected append no-mutation stabilization | 1/1 | `artifacts/unix-portability/A02/windows/A02-windows-oversized-acceptance-no-mutation-final.trx` |
| Windows | Required A02 unit filter | 266/266 | `artifacts/unix-portability/A02/windows/A02-windows-required-unit-current.trx` |
| Windows | Filesystem portability integration category | 82/82 | `artifacts/unix-portability/A02/windows/A02-windows-integration-current.trx` |
| Windows | Portable host-neutral fixtures | 15/15 | `artifacts/unix-portability/A02/windows/A02-windows-portable-fixtures-final.trx` |
| Windows | Final Hosting/alias scoped-authority slice | 46/46 | `artifacts/unix-portability/A02/windows/A02-windows-hosting-alias-scope-final.trx` |
| Windows | Full Release solution build after atomic no-clobber remediation | 0 warnings/errors | `artifacts/unix-portability/A02/windows/A02-windows-solution-build-atomic-no-clobber-final.log` |
| Linux Docker | FS-008 allocation, durable commit, storage, and stable-identity slice | 60/60 | `artifacts/unix-portability/A02/linux-current/A02-linux-fs008-atomic-no-clobber-final.trx` |
| Linux Docker | Rejected append no-mutation stabilization | 1/1 | `artifacts/unix-portability/A02/linux-current/A02-linux-oversized-acceptance-no-mutation-final.trx` |
| Linux Docker | Required A02 unit filter | 266/266 | `artifacts/unix-portability/A02/linux-current/A02-linux-required-unit-green-final.trx` |
| Linux Docker | Extended A02-owned slice | 376/376 | `artifacts/unix-portability/A02/linux-current/A02-linux-owned-extended-green-current2.trx` |
| Linux Docker | Filesystem portability integration category | 82/82 | `artifacts/unix-portability/A02/linux-current/A02-linux-integration-green-final.trx` |
| Linux Docker | Portable host-neutral fixtures | 12/12 | `artifacts/unix-portability/A02/linux-current/A02-linux-portable-fixtures-current.trx` |
| Linux Docker | Final Hosting/alias scoped-authority slice | 46/46 | `artifacts/unix-portability/A02/linux-current/A02-linux-hosting-alias-scope-final.trx` |
| Linux Docker | Unit build after atomic no-clobber remediation | 0 warnings/errors | `artifacts/unix-portability/A02/linux-current/A02-linux-unit-build-atomic-no-clobber-final.log` |
| Linux Docker | Full Release solution build after atomic no-clobber remediation | 0 warnings/errors | `artifacts/unix-portability/A02/linux-current/A02-linux-solution-build-atomic-no-clobber-final.log` |
| Host-neutral | Portable bundle validator before checksum freeze | 282 files, 0 errors/warnings | `python scripts/validate_bundle.py --bundle-root . --skip-checksums` |

The first Linux required-filter run had one transient PostgreSQL connection timeout and passed unchanged on immediate rerun. The first Linux solution `--no-restore` build exposed seven deliberately absent `project.assets.json` files in the selective source snapshot; the authoritative restore/build completed with zero warnings and errors. Neither was hidden or converted into product policy.

## Independent finding and remediation

The first independent C1 review in `reviews/10-a02-independent-review.md` correctly issued
NO-GO: a display name equal to another display name's generated physical name could select the
occupied target, and `SaveAsync` could replace the first object. The corrected contract is:

- `Encode(displayName)` produces stable identity where a caller requires repeatable lookup;
- `Allocate(displayName, existingPhysicalNames, comparer)` allocates a new physical object,
  treats every occupied result as a collision, and checks `~hash`, `~hash-2`, and later
  deterministic candidates until it finds an unoccupied name;
- filesystem storage uses `Allocate` for new objects and selects the durable writer's typed
  `CreateNew` commit mode; the final `File.Move` is atomic no-replace, so even a non-cooperating
  creator that appears after every pre-commit check wins without being overwritten;
- an explicit relative-path hint retains intentional update semantics;
- legacy Cognitive Memory export continues to use stable `Encode(exportId)` identity, preventing
  duplicate exports from being misclassified as new objects.

Codec and storage regressions cover the reviewer's constructive collision, an occupied first
suffix, three distinct persisted objects and contents, and a controlled concurrent allocation
race. The first broad fix exposed the legacy export identity regression in a 5,440/5,441 run;
that artifact is preserved, the API split fixed the cause, and the authoritative repeated full
run was 5,441/5,441. A separate time-sensitive no-mutation assertion surfaced in that repeat; it
now compares the actual pre-acceptance snapshot with the post-rejection bytes and passes on both
Windows and Linux.

The first FS-008 re-review then identified that an existence guard before a generic overwrite
commit still left a final clobber window. The durable writer now exposes a strongly typed commit
mode: default replacement semantics remain unchanged for existing authority writers, while
auto-allocated storage creation uses atomic `CreateNew`. A deterministic stage-observer fixture
creates an outside winner after the pre-commit guard and immediately before the final move; the
save fails and preserves the winner's bytes on both Windows and Linux. The authoritative Windows
full run after this correction is 5,442/5,442.

## Architecture evidence

- Scoped CodeAnalytics snapshot: `snap-20260809105134-2719bac5`; 15 relevant projects, no blocking diagnostics.
- Deterministic project graph: `artifacts/unix-portability/A02/A02-project-reference-graph-final.json`; 105 projects, 631 direct references, 0 project cycles.
- `CanDoItAll.Infrastructure.Abstractions` has zero project references. Core and Models do not reference the Infrastructure implementation.
- CodeAnalytics reported only existing intra-project module/type cycles; none is a project dependency cycle introduced by A02.
- Final static audit: `artifacts/unix-portability/A02/A02-static-audit-final.md`.

## Redaction evidence

`artifacts/unix-portability/A02/A02-secret-scan-final.json` scanned 72 generated proof files up to 20 MiB while explicitly excluding the three full Linux source-snapshot directories. Source snapshots are reproducibility inputs, not generated proof, and contain known repository development credentials and malicious fixture strings.

The report-only scan found 96 occurrences grouped into the same six unique fingerprints. Every occurrence is embedded in a test display name for a deliberately simulated OpenAI/GitHub token, API-key redaction vector, invalid endpoint, or spoofed audit-envelope vector. The report stores only redacted excerpts and truncated SHA-256 fingerprints. No real credential, private key, or unclassified secret-bearing value was found. The scanner includes `.trx`, records exclusions, and never scans its own output.

## Static audit and residuals

- `git diff --check` exits 0; one line-ending conversion warning is non-semantic.
- No `Path.GetInvalidFileNameChars()` call remains in `src`, `tests`, or `tools`.
- Decision-bearing enumeration hits are explicitly ordered. Unordered hits are order-independent count/existence queries.
- Authority writes use the durable primitive or a validated same-directory commit. Raw direct-write hits outside that set are operational outputs or later tool/runtime owners and are not silently treated as A02 authority.
- Managed `System.IO` cannot eliminate the final link-swap interval without native directory-handle-relative APIs. A02 minimizes, revalidates, tests, and documents that residual.
- Actual macOS execution is unavailable locally. Golden/macOS-uncertainty contracts pass on both available hosts, but actual macOS remains mandatory before C4.

## Review decision

The independent review history and final C1 GO are recorded in `reviews/10-a02-independent-review.md`. The reviewer accepted the FS-008 identity/allocation split and, after a second correction, verified the typed atomic create-new/no-replace commit and post-guard outside-winner preservation on both hosts. No A02 blocker remains. Actual macOS proof remains mandatory before C4.
