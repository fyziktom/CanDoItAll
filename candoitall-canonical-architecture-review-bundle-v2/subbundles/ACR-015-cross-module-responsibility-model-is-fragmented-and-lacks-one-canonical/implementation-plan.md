# Implementation plan

## Remediation goal

Define one canonical ActorAssignment model plus an ownership matrix for project-, node-, and module-scoped responsibility. Module-local fields either become derived mirrors or remain authoritative only until their migration phase is complete.

## Ordered steps

- Define an explicit ownership matrix for project-level, node-level, and aggregate-specific responsibility semantics.
- Decide which scopes are centrally owned now (project + node) and which remain module-native for the current stabilization wave (resource/validation/test plan if needed).
- Provide adapters so assembled graph/read models can show one coherent actor picture without forcing an unsafe big-bang migration.
- Plan a later migration only after the ownership matrix and tests prove stable.

## Guardrails

- Do not leave the ownership matrix implicit in page code or service trivia.
- Do not migrate every module field in one risky big bang; use explicit mirrors if needed.

## Acceptance criteria

- Project-, node-, and module-scoped responsibility have explicit canonical owners.
- Mirrored fields are documented and test-backed until they can be removed or fully derived.
