# H2 Windows publication repair

Status: design and implementation independently reviewed with no blocking finding;
source prepared, no repaired runtime proof yet.

The actual production read handle uses `FileShare.Read | FileShare.Delete`. The
frozen H2 lifecycle test fails when `DurableFileWriter` publishes with
`File.Move(overwrite: true)` while that handle is held. Widening read access or
waiting for readers would defeat the intended bounded snapshot contract.

Use `File.Replace` for an existing Windows destination, retaining CreateNew and
Unix behavior. The choice is made before invoking the primitive; there is no
delete-then-move or fallback. A successful replacement leaves the held reader on
the complete old payload and subsequent readers on the complete new payload.
This is a visibility contract, not a power-loss durability guarantee.

The [Windows replacement contract](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew)
documents partial transitions for native errors 1176/1177. Blind retry followed by
temporary-file cleanup can destroy the only remaining payload. Immediately before
the Windows replacement, rename the exact flushed staging file from `.tmp` to
`.pending` in the same directory. Preserve it and throw an explicit recovery
exception on those partial failures or an unclassified exception from replacement.
Under the existing coordination, both later writes and deletes reject any exact
target-specific generated pending file before stale temporary cleanup. A crash in
this short publication window also requires explicit inspection. No automatic
republish, receipt acceptance or rollback claim follows from pending bytes.

Documented nontransition failures may retry within the existing bound and clean
only this attempt's staging file. Success consumes the pending file; subsequent
observer/mode/disposal failures still require the owner's existing outcome
reconciliation. The same protocol covers the pre-existing Windows backup branch.
Replacement preserves destination metadata subject to the existing
`ignoreMetadataErrors` behavior and same-directory staging permissions.

The change stays in Infrastructure and adds one same-directory rename per Windows
replacement, no extra flush, database access, reader lock or module dependency.
The existing directory scan is reused to detect pending evidence before cleanup.
Explicit delete additionally checks that evidence. Measure this cost in the
remaining authority capacity work; no throughput claim is made here.

Validation: unchanged held-production-handle regression; existing 17 durable-writer
rows; eight new rows (four Facts and four fixed InlineData rows), including both
partial codes with/without backup, exact pending
bytes and subsequent write/delete refusal; CreateNew non-overwrite; bounded
nontransition retry/cleanup; unclassified replacement failure; and pre-existing
target-specific pending evidence checked before stale cleanup while another target
remains writable. A narrow internal replacement delegate supplies faults
without introducing a production interface. Record actual Windows execution;
Linux paths and platform-conditional rows require their own affirmative proof.

Operator recovery: stop affected writers, preserve target/pending/backup files,
inspect their complete contents and owner revision, restore the owner-authoritative
version under exclusive coordination, and remove only the inspected pending file.
Never infer acceptance merely from the presence of the staged payload. Pending
files can contain the same sensitive content as their target and must retain the
same access controls. Automatic cleanup must never discard them.
