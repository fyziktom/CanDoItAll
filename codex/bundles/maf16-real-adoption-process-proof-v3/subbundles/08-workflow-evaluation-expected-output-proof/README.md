# SB08: 08-workflow-evaluation-expected-output-proof

## Goal

Adopt or explicitly defer workflow expected-output evaluation.

## Required work

- Use MAF workflow expected output/ground truth if package exposes it.
- If not adopted, add a clear bridge test using CanDoItAll process/workflow assertions and mark MAF evaluator deferred.
- Ensure workflow-backed process steps produce mapped artifacts that pass process-owned validation.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB08` are updated and downstream subbundles can rely on it.
