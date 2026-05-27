# SB13: 13-recovery-manager-and-operator-approval-correctness

## Goal

Fix manager recovery and operator approval semantics.

## Required work

- Ensure operator decision artifacts cannot satisfy the original required deliverable/brief unless explicitly mapped.
- Ensure manager recovery creates current-run evidence with lineage and content hash.
- Ensure pending approvals do not leave process in confusing active/failed mixed state.
- Add tests for valid recovery artifact and invalid operator-decision substitute.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB13` are updated and downstream subbundles can rely on the behavior.
