# SB07: Refactor policy and artifact validation after SB05-SB06.

## Objective

Refactor policy and artifact validation after SB05-SB06.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Extract `ProcessToolOperationAuthorizer`.
- Extract `ProcessScriptSideEffectAnalyzer`.
- Extract `ProcessCompletionArtifactValidator`.
- Extract `ProcessArtifactIdentityService`.
- Ensure unit tests cover services without full MAF runtime.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
