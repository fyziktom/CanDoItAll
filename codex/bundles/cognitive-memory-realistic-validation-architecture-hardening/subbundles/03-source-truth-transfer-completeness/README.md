# 03-source-truth-transfer-completeness

## Status

- `Ready`

## Objective

Transfer complete validation source truth, including project structures and external file/data manifests, without direct truth-table writes.

## Required Edits

- Extend database transfer preview and execution with file/data manifest groups.
- Include content hash, locator, redaction state, and skip reason in transfer proof.
- Add tests for idempotent re-transfer into a clean profile.

## Closure Proof

- Transfer preview lists projects, structures, files/data manifests, and excluded items.
- Transfer execution proof shows stable counts and hashes.
