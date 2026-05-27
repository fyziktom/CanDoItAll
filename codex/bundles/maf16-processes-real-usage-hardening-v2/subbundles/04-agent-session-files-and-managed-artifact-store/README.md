# SB04: 04-agent-session-files-and-managed-artifact-store

## Goal

Evaluate and adopt session file/file store support for process artifacts.

## Required work

- Map current workspace/session file writes to MAF 1.6 AgentSessionFiles/file store concepts.
- Decide whether to store process artifacts through MAF session files, CanDoItAll storage placement, or both.
- Ensure current-run artifact lineage points to durable file/session identifiers.
- Add tests for file-store artifacts becoming process artifacts with content hash.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB04` are updated and downstream subbundles can rely on the behavior.
