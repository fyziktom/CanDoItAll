# A02 filesystem semantics decision

## Boundary

`CanDoItAll.Infrastructure.Abstractions` owns only the narrow physical-path policy port:

- a root-scoped path-policy factory;
- a root-scoped policy exposing the normalized root, detected case model, comparer,
  containment, link traversal validation, and mutation-parent revalidation;
- typed case-model and validation-failure contracts.

`CanDoItAll.Infrastructure` owns the implementation and every filesystem probe. It validates
the managed root from its native volume root, detects case behavior against the actual root,
walks every existing ancestor without following links, and fails closed when it cannot
establish safe traversal. MAF Core may consume the abstraction but must not reference the
Infrastructure implementation or retain a second filesystem-authority algorithm.

The pure `PortablePhysicalFileNamePolicy` codec lives in `CanDoItAll.SharedKernel` because it
contains no host probing or I/O and is consumed by Infrastructure, MAF, process, module, web,
and test-support boundaries. Physical owners remain responsible for applying the codec,
collision context, containment, and persistence policy.

The codec separates stable identity from new-object allocation. `Encode` is context-free and
repeatable for owners that must resolve the same logical identity across runs. `Allocate`
accepts the existing physical names and the root's physical comparer, treats exact occupancy as
a collision, and rechecks every deterministic suffix candidate. This prevents an already
portable display name from aliasing another display name's generated physical name. Filesystem
storage uses allocation only when the request has no explicit relative-path hint; a hint is an
intentional update locator. Auto-allocation selects the durable writer's typed `CreateNew`
commit mode, whose final `File.Move` is atomic no-replace. A non-cooperating creator that appears
after path validation or any pre-commit guard therefore causes the save to fail without
overwriting the winner. Default replacement and optional backup behavior remain explicit for
callers that own an existing target.

The policy is root-scoped because case behavior can vary by volume and directory. Detection
must not use `OperatingSystem.IsWindows()` as the answer. A writable-root probe determines
sensitive or insensitive behavior; an unavailable or read-only probe yields `Unknown`, whose
authorization comparison is ordinal and therefore conservative. Unknown behavior is exposed
to diagnostics and never upgraded silently.

Logical identity remains ordinal even when the owned physical root is insensitive. Workflow
artifact paths, storage transfer result order, project-media identifiers, scoped managed-root
mapping, and process-lease identities therefore use ordinal comparison. Process-lease hashes
are derived from the exact canonical logical path and do not case-fold. A
`WorkspaceExecutionScope` captures the detected root case model when it is constructed;
identity comparison uses ordinal scope equality plus the captured physical-root comparer.

## Link and mutation contract

- The managed root itself and every existing ancestor from the native filesystem root are
  rejected when they are a symbolic link, junction, mount-style reparse point, or other
  reparse point.
- Existing candidate ancestors are checked with the same rule. A missing leaf is permitted
  only for an operation that explicitly creates it; walking stops at the first missing
  ancestor.
- Callers revalidate the target parent immediately before creating a directory, opening a
  temporary file, replacing a file, launching a process, or handing a path to another API.
- Temporary and backup files remain in the verified target directory. Cleanup addresses only
  exact generated names and never recursively follows a link.
- Managed `System.IO` path APIs cannot provide a portable directory-handle-relative open, so a
  malicious link swap can still occur between final validation and open. The implementation
  minimizes that interval, validates again after directory creation, and records this residual
  TOCTOU limitation instead of claiming elimination.

## Durable persistence

Infrastructure owns one durable-file primitive for catalog, control-plane, preference,
quarantine, export-manifest, and filesystem-storage writes. The primitive uses:

1. a bounded cross-process exclusive lock with timeout and cancellation;
2. a same-directory, random, create-new temporary file;
3. complete write and `Flush(flushToDisk: true)`;
4. final parent/link revalidation;
5. typed atomic commit: create-new/no-replace for new objects, or rename/replace with an optional
   same-directory backup where recovery requires it;
6. exact temporary cleanup on failure or cancellation.

The lock file is a persistent coordination inode rather than ownership state: an open handle
with `FileShare.None` is the authority, so process death releases the lock without stale-lock
deletion races. The primitive never interprets an unlocked file as an active owner.

Secret-bearing callers request private mode. On Unix the primitive creates/verifies directories
as `0700` and files as `0600`, re-verifies after commit, and fails closed if hardening cannot be
established. Non-secret storage payloads do not acquire secret-mode semantics implicitly.

Workflow artifact content is persisted through the scope-bound workspace path-resolution and
file-mutation services. This keeps link validation, canonical logical paths, and atomic mutation
semantics aligned with other workspace writers instead of maintaining a workflow-local path or
overwrite implementation.

## Portable names and deterministic enumeration

Physical storage filenames use one application policy independent of
`Path.GetInvalidFileNameChars()`: Unicode NFC, the Windows forbidden-character set plus control
characters, dot-segment and trailing-dot/space rejection, DOS device-name rejection, a UTF-8
byte budget, stable identity hashes, and deterministic rechecked allocation suffixes for occupied
names. Display names remain the original user value; only the physical locator is encoded.

Decision-bearing filesystem enumeration is materialized and sorted by canonical encoded
logical key with `StringComparer.Ordinal`, followed by a deterministic ordinal physical-name
tie-breaker. Native provider order is not exposed as deterministic behavior.

## Watcher convergence

`FileSystemWatcher` events are hints. The Manager watcher keeps a generation, debounces and
deduplicates relevant paths with the root policy comparer, computes a deterministic source
fingerprint after each generation, and periodically rescans. Watcher overflow, watcher creation
failure, and unreliable/unsupported roots schedule the same rescan path; polling remains active
even when watchers work. A build is skipped only when the deterministic fingerprint is
unchanged.

## Rejected alternatives

- OS-wide case comparison: incorrect for case-sensitive Windows directories and configurable
  macOS volumes.
- Duplicate link walkers in Core and Infrastructure: divergent authorization behavior.
- In-process semaphores: no protection from another host process.
- direct overwrite: permits truncation after crash or cancellation.
- deleting a lock file to recover: can split lock ownership across two inodes.
- watcher-event-only rebuilds: overflow and dropped events never converge.
