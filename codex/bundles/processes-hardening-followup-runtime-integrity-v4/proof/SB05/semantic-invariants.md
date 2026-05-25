# SB05 Semantic Invariants

## SB05-INV-001

Expected behavior:

- Required JSON artifacts recorded with relative managed storage paths are validated by reading stored content from the active workspace root, not by extension or inline summary alone.
- Missing relative managed artifact content fails with a readable diagnostic that can be persisted by the existing artifact validation diagnostic path.
- Oversized managed artifact content fails before parsing to keep finalizer validation bounded.
- YAML and Markdown declarations require readable non-empty text when a production content reader is available.
- Image/screenshot declarations require readable stored bytes with an image content type and matching image signature when a production content reader is available.

Disallowed shallow implementation:

- prompt-only change
- source-assertion-only proof
- tests that manually seed final state instead of exercising producer/consumer lifecycle
- branch-specific hardcoding
- software-only behavior for generic process runtime

Required proof:

- failing-first/red-team proof
- passing proof
- source assertions
- anti-stub audit
- changed-file hashes
- production behavior artifact matrix when new runtime state is introduced
