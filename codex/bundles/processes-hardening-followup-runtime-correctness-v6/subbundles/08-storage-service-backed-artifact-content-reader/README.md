# SB08: Replace workspace-only artifact content validation with storage abstraction.

## Objective

Replace workspace-only artifact content validation with storage abstraction.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Introduce `IProcessArtifactContentReader` backed by storage placement/service driver.
- Keep workspace reader as fallback only for managed workspace paths.
- Validate artifacts stored outside workspace root or via future IPFS/storage drivers.
- Add tests with fake storage reader and workspace reader.
- Ensure finalizer and manual transition use the same reader.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
