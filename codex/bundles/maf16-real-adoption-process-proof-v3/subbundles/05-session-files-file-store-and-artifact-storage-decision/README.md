# SB05: 05-session-files-file-store-and-artifact-storage-decision

## Goal

Clarify MAF session files/file store vs CanDoItAll managed artifact storage.

## Required work

- Adopt AgentSessionFiles if available.
- If unavailable, document and test CanDoItAll storage as authoritative.
- Ensure file/session evidence is correlated to process artifacts, content hashes, and tool receipts.
- Add a test for an artifact written through session/tool receipt becoming a process artifact.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB05` are updated and downstream subbundles can rely on it.
