# SB01: Fix writable/read-only alias overlap and make prompt-grounding merge safe.

## Objective

Fix writable/read-only alias overlap and make prompt-grounding merge safe.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add tests where an alias is already in `AllowedExternalTargetAliases` and prompt grounding sees the same alias; it must not appear in read-only aliases.
- Add tests for child alias covered by writable parent and sibling alias outside writable parent.
- Modify `GroundPromptExternalTargetAliases` and metadata merge helpers to remove writable-covered aliases from read-only list.
- Ensure `EvaluateReadOnlyExternalTargetMutation` does not deny aliases that are explicitly writable from a trusted ledger source.
- Add a source assertion proving the ledger/writable authority wins over prompt-only read-only discovery.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
