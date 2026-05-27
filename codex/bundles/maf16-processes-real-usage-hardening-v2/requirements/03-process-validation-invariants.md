# Process Validation Invariants

1. A current-run artifact linked to the current process run, step, expectation, execution run, and readable content must satisfy the required artifact.
2. If content cannot be read, report `ContentUnavailable`, not `StaleOrWrongRun`.
3. If content hash is recorded and mismatches actual content, report `ContentHashMismatch`.
4. If content hash is empty for an evidence/runtime proof artifact, validation must either compute it or explicitly record why it is unavailable.
5. The read model cannot show a required artifact as fully satisfied while finalizer validation would reject it.
6. Recovery artifacts must target the original required expectation with lineage; operator decision artifacts are separate decision evidence.
