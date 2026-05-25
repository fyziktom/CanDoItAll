# SB01 - Persisted Step Operation Contract

    ## Mission

    Add first-class persisted operation-contract fields to process step definitions, editor models, import/export, templates, and UI.

    ## Requirements

    - ProcessStepOperationContract must not be inferred only from text.
- Existing heuristic parser must remain migration fallback.
- UI must show and edit operation contract fields.
- Import/export must preserve the fields.

    ## Implementation Guidance

    - Start with failing-first or red-team tests.
    - Implement production runtime changes.
    - Keep behavior generic; avoid software-only assumptions.
    - Update proof manifest and semantic invariants.
    - Add source assertions and changed-file hashes.

    ## Required Tests

    - A business-plan report step with words create/generate must stay artifact-only.
- A software implementation step must explicitly allow MutateProductTarget.
- Imported/exported definitions preserve operations and target scope.

    ## Acceptance Criteria

    - Old shallow behavior fails.
    - New production behavior passes.
    - No prompt-only fixes.
    - No SQLite runtime reintroduction.
    - Bundle proof files are updated.
