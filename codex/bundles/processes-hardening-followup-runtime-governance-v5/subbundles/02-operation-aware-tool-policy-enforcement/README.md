# SB02 - Operation-Aware Tool Policy

    ## Mission

    Make tool policy enforce allowed operations, not only ProcessAllowsProductMutation.

    ## Requirements

    - Use agentProcessStepAllowedOperations and agentProcessStepTargetScope in policy context.
- Map every tool to a generic operation class.
- Deny validation/runtime/external action tools unless allowed.
- Keep current-run artifact writes allowed when WriteManagedProcessArtifacts is allowed.

    ## Implementation Guidance

    - Start with failing-first or red-team tests.
    - Implement production runtime changes.
    - Keep behavior generic; avoid software-only assumptions.
    - Update proof manifest and semantic invariants.
    - Add source assertions and changed-file hashes.

    ## Required Tests

    - Architecture step cannot run product validation or launch runtime.
- Review step can read and validate only if RunValidation is allowed.
- Artifact-only step can write current-run process artifact.

    ## Acceptance Criteria

    - Old shallow behavior fails.
    - New production behavior passes.
    - No prompt-only fixes.
    - No SQLite runtime reintroduction.
    - Bundle proof files are updated.
