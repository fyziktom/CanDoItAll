# QA Prompt

Review the active boundary-hardening subbundle as a senior C#/.NET architect.

Reject the work if:

- Cognitive Memory implementation starts in this bundle.
- Any source provider still silently restarts on invalid/stale cursor.
- Any provider intended for large scans still materializes unbounded source data before paging without an explicit bounded-source exception.
- Workbench notes or metadata can be projected as unrestricted content.
- Raw sensitive payload hashes can be logged, displayed, or projected without classification.
- MAF contributor trace metadata is still dropped before future Cognitive Memory can inspect it.
- Existing targeted tests are not rerun or closure evidence is missing.

Record findings and gate status in `reviews/01-execution-report.md`.
