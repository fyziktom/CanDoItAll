# SB01 Semantic Invariants

## INV-SB01-BASELINE-SEPARATION

Raw note owned: update Microsoft Agent Framework packages and validate that the application works as before.

Expected behavior: package-update implementation starts only after the current branch, package graph, restore state, build state, focused-test candidates, and architecture snapshot are recorded.

Disallowed shallow implementation: editing package references first and later treating any failure as package-induced without baseline proof.

Semantic positive proof: `bundle://proof/SB01/transcripts/baseline-restore.md` and `bundle://proof/SB01/transcripts/baseline-build.md` show restore and Release build passed before package edits.

Adversarial negative proof: `bundle://proof/SB01/transcripts/no-package-change-assertion.md` proves no package diff existed during `SB01`, preventing a shallow implementation from hiding package edits in the baseline.

Production assertions: no production behavior was changed in `SB01`; it is evidence-only.

Anti-stub audit: `SB01` contains command transcripts and no completed placeholder evidence rows.

Downstream dependency check: `SB02` may proceed because baseline restore/build state and direct package references are known.

## Production Behavior Artifact Matrix

No new production signal, state, record, or event was introduced in `SB01`.
