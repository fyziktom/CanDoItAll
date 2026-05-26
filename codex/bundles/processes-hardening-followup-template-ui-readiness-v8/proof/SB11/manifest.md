# SB11 Proof Manifest

## Status

Completed.

## Production-path coverage

- Verified the runtime validation/recovery service boundaries already exist in production code instead of UI or template-only code.
- Covered the block-state classifier, health invariant auditor, workflow/subprocess artifact mapper, shared completion artifact validator, and manual transition call site.
- Re-ran the dependent manual transition stale-lineage regression so SB12 can rely on the service split without weakening SB10 validation behavior.

## Semantic invariant

See `bundle://proof/SB11/semantic-invariants.md`.

## Failing-first or adversarial proof

`bundle://proof/SB11/transcripts/failing-first.txt`

## Passing proof

`bundle://proof/SB11/transcripts/passing.txt`

## Source assertions

`bundle://proof/SB11/transcripts/source-assertions.txt`

## Anti-stub audit

`bundle://proof/SB11/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`bundle://proof/SB11/transcripts/changed-file-hashes.txt`
