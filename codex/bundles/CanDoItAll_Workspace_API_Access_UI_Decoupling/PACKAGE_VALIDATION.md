# Handoff validation

## Executed checks

| Check | Result |
| --- | --- |
| UTF-8 file inventory | 39 files, including the root manifest |
| Root SHA-256 manifest | 38 entries, complete and matching |
| Declared source records | 40 unique entries with coverage; repository links pinned to review SHA |
| Local Markdown file links | 54 resolved inside the package |
| Root utility tests | 11 passed, zero failed/skipped |
| Shared utility tests | 14 passed, zero failed/skipped |
| Shared foundation | All 22 files byte-for-byte identical to the supplied Core handoff |
| ZIP CRC and fresh extraction | Checked; extracted package passes the same validator |

Metadata, UTF-8 types, local links, source IDs, safe manifest paths and hashes were checked.
The utilities reject added/unsealed files, unexpected binary extensions, symlinks, an execution
pin, duplicate source IDs, unknown source references and inconsistent repository provenance.
The root validator now recognizes AP source IDs; the corresponding negative test exercises it.
No source or shared foundation file is silently repaired by validation.

The ZIP was opened, CRC-tested and extracted to a fresh temporary directory. File bytes and
its manifest were compared to the sealed source package and all shared files to the input.
The same checks were repeated on the extracted copy. External URLs, source authenticity,
Markdown anchors and product behavior are not certified by these utilities.

## Limits

These 25 tests validate handoff tooling, not C#, the application, authorization, database,
control-plane persistence, browser behavior or performance. No product build/test/browser run
was executed in this review environment. Real source provenance was obtained through connected
GitHub reads. The separate PROOF_STATUS.md records that coverage and implementation evidence.
