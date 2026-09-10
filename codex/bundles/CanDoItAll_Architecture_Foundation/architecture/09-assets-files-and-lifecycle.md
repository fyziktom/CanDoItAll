# 9. Files, assets, deletion, and transfer

## Three owners around one file

The artifact producer owns origin/manifest; Storage owns physical bytes, integrity, provenance, and cleanup; Structure/Resources/TestLab owns its attachment and usage metadata. Two references do not mean two content masters. Use logical identity/version and provider/catalog scope plus integrity data, not merely an absolute path. Content identity grants no read permission.

## Creation and content editing

Validate typed intent, format, size, parent, and actor. Native notes stay inline. File assets use authorized managed storage. Staging handles bind to origin operation, expire under explicit recovery policy, and cannot cross scopes.

Depending on driver semantics, finalize before DB commit with safe compensation, or commit PendingContent then finalize. Both report truthful availability and recover missing content/orphan bindings. No AvailableFile for a row without bytes. Cleanup rechecks references/ownership before removing unbound content. Local files, object storage, remote imports, and generation have different rename/version/finalization capabilities; the protocol does not pretend universal SQL atomicity.

Metadata and byte edits are different commands. Expected content version/hash prevents lost updates. Historical run/test evidence stays on its version. Do not perform read-time Notes-to-file migration. Validate MIME/extensions and constrain active SVG/HTML preview. Thumbnails are derived, never replacement masters. Prefer bounded streams/authorized handles for large content; retained base64 APIs need explicit limits.

## Paths and execution

FileTools scope comes from an authorized Project/Resource/Run owner. Execution uses typed executable, arguments, environment, working directory, declared shell/runtime, and elevation capability. Do not concatenate unvalidated shell strings. A .sh file or node cannot silently select another shell or administrator context.

Recheck traversal, root retargeting, wrong run folders, cross-profile/project scope, and symlink escapes at I/O. Lexical prefix validation at node creation is insufficient. External aliases are registry references, not agent-issued root grants. Preserve headless direct launch and platform-gated convenience actions [SRC-008].

## Deletion

External delete explicitly chooses RetainManagedFiles or DeleteOwnedManagedFiles; Unspecified is invalid. Map legacy durable payloads to their actual historical meaning [SRC-005]. Retain removes intended graph/binding/assignment/view effects but preserves bytes. Delete-owned requires ownership, live root/namespace, remaining references, and active-run protection. Confirmation is not force.

A durable plan pins selection, disposition, and cleanup scope. Retrying cannot change the choice. Partial multi-root completion reports completed roots and safe failures; an exception cannot erase earlier commits or justify retrying everything as untouched.

## Project lifecycle and transfer

Projects owns lifecycle and a named coordinator tracks participant effects. Each owner changes its own records. Long external cleanup uses Deleting/PendingCleanup and fences new writes; SQL rollback does not undo completed remote deletion.

Cross-profile transfer requires versioned manifest, owner validation, identity/relation remapping, storage rebinding, generation, and unresolved-source handling. Export neither arbitrary current UI state nor secret values as ownership. Preserve native notes and history; rebuild only derived data.

Active run destinations are not retargeted by move. Either use an explicitly supported atomic binding operation, block the move, or retain historical origin under a clear result. Never infer a new project from a name.

## Recovery and cleanup

Cleanup requires proven ownership and no live references/work. TTL alone cannot authorize deleting an unfinished receipt/finalization object. Distinguish staging orphan, committed binding with missing bytes, failed cleanup, and unavailable external reference. Repairs preserve operation/causation and runtime provenance. For automation content reads also apply source-to-provider and source-to-destination disclosure controls in [16](16-cross-module-queries-and-context-use.md).
