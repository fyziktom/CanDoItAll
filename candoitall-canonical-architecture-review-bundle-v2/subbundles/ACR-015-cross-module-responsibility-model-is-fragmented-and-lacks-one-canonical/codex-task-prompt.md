# Codex task prompt — ACR-015

Implement finding `ACR-015` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 3`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

Resources, Validation, TestLab, project-party assignments, and workbench node metadata each represent responsibility in their own way. There is no explicit ownership matrix for who owns project-level, node-level, and module-level actor assignments.

## Ordered implementation steps

- Define an explicit ownership matrix for project-level, node-level, and aggregate-specific responsibility semantics.
- Decide which scopes are centrally owned now (project + node) and which remain module-native for the current stabilization wave (resource/validation/test plan if needed).
- Provide adapters so assembled graph/read models can show one coherent actor picture without forcing an unsafe big-bang migration.
- Plan a later migration only after the ownership matrix and tests prove stable.

## Guardrails

- Do not leave the ownership matrix implicit in page code or service trivia.
- Do not migrate every module field in one risky big bang; use explicit mirrors if needed.

## Done means

- Project-, node-, and module-scoped responsibility have explicit canonical owners.
- Mirrored fields are documented and test-backed until they can be removed or fully derived.
