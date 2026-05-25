# SB08 semantic invariants

## Invariant protected

Merge readiness must report both passing proof and unresolved validation blockers without hiding failures behind focused tests.

## Producer/consumer lifecycle

Build/test/EF/residue commands produce transcripts. Manifests and the final execution report consume those transcripts.

## Positive proof

Solution build, full unit suite, targeted integration filters, EF pending model check, and bundle validation passed.

## Adversarial negative proof

Broad component and integration commands were rerun with longer limits and diagnostics. Their failures are named and preserved as transcripts.

## Anti-stub proof

Proof includes raw command transcripts and the failing test names, not only a summary.
