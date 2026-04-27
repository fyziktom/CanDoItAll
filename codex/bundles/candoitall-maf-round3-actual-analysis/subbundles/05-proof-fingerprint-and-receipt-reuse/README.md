# 05 - Proof Fingerprint and Receipt Reuse

## Problem

Carrying successful tool names across attempts is too coarse. A previous `workspace_dotnet_build` call is only reusable if the build inputs are unchanged.

## Required implementation

Add proof receipts/fingerprints for build/test/browser proof tools.

Receipt fields:

```text
tool name
command/arguments
working directory/project path
relevant source file hashes
project/config/test file hashes
artifact dependency hashes
environment/tool version
started/finished timestamp
status
receipt id
```

Proof reuse rules:

- build proof invalidated by code/project/config changes;
- test proof invalidated by code/project/test changes;
- browser proof invalidated by UI/static/web app changes;
- proof may expire based on age/configuration;
- proof reuse must be explicitly logged with reason.

## Acceptance criteria

- Rework packet includes `ProofsToRerun` and `ReusableProofs`.
- Tool-name carry-forward remains only as a fallback/compatibility signal, not final proof truth.
- Mutating a relevant file invalidates dependent proofs.
- Reusing proof produces a traceable explanation.

## Tests

- Reuse build proof when only a QA note changes.
- Invalidate build proof when `.cs` or project file changes.
- Invalidate browser proof when UI file changes.
- Rework packet includes correct proof rerun list.

## Execution status

Completed. Proof fingerprints include command, working directory, source/artifact hashes, environment, tool version, status, and timestamp; reuse and invalidation tests pass.
