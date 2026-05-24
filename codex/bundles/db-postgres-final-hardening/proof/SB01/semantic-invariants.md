# SB01 semantic invariants

## Invariant protected

Bundle proof and residue checks must be executable locally and must not report false failure when a searched-for anti-pattern is absent.

## Producer/consumer lifecycle

No runtime state was added. The bundle validator and residue audit are proof producers; later subbundle manifests consume their transcripts.

## Positive proof

`validate_bundle.py` passed, and the residue audit completed with no unexpected PostgreSQL-removal regressions.

## Adversarial negative proof

The audit explicitly searches for SQLite/hot-switch/drain/fake-proof source drift and returns success only when no unapproved matches remain.

## Anti-stub proof

The validator checks required files, subbundle directories, and proof placeholders instead of only printing a success banner.
