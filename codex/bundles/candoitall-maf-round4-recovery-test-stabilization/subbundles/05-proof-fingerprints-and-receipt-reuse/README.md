# 05 — Proof Fingerprints and Receipt Reuse


## Problem

Successful tool names are too coarse for proof reuse. Build/test/browser proof validity depends on inputs.

## Tasks

1. Add `ProofFingerprint` model.
2. Capture normalized tool name, normalized arguments, working directory, relevant input file hashes, artifact hashes, environment versions, status, timestamp, and receipt id.
3. Define which proof tools require current-attempt execution vs reusable proof.
4. Add invalidation rules for source file changes, project file changes, environment changes, and command/argument changes.
5. Use fingerprints in recovery decisions and rework packets.
6. Keep conservative defaults: when fingerprint cannot be computed, rerun proof.

## Acceptance criteria

- Proof receipt reuse is based on fingerprint equality/compatibility, not tool name alone.
- Changing a relevant file invalidates build/test/browser proof.
- Unchanged inputs allow selected proof reuse where policy permits.
- Tests cover positive and negative reuse cases.

