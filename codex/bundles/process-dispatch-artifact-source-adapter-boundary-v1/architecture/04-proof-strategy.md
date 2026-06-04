# Proof Strategy

## Required Proof Classes

- Failing-first source scans for unplanned `Processes.Core`, driver-pack, or MAF product dependency movement.
- Exact external-reference-key parity tests for every migrated source adapter.
- Artifact lineage smoke tests proving recovery projection still creates compact keys and lineage records.
- Duplicate projection prevention tests for each migrated source path.
- Required-artifact satisfaction tests proving validation modes are unchanged.
- Full build and focused integration artifact tests after each refactor gate.

## Large-Screen Policy

Browser validation is N/A unless rendered UI changes unexpectedly. Do not create small/medium/mobile proof artifacts.
