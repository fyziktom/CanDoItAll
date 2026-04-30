# 05 Proof Fingerprints and Delta Validation

## Goal

Reuse prior proof only when relevant inputs are unchanged.

## Tasks

1. Add proof fingerprint DTO/entity for tool receipts.
2. Compute fingerprints for build/test/browser/inspection proofs.
3. Include normalized args, working directory, relevant file hashes, artifact hashes, environment/tool versions, status, receipt id, and timestamp.
4. Add invalidation rules for changed source/config/artifact files.
5. Replace successful-tool-name carry-forward with fingerprint-valid reusable proof references.
6. Tests: unchanged files reuse proof; changed `.cs` invalidates build/test; changed UI invalidates browser proof; failed proof is never reusable as success.

## Acceptance criteria

- Proof reuse is precise, explainable, and test-covered.
