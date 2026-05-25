# SB04 - Storage-Backed Artifact Validation

    ## Mission

    Validate artifact content through storage abstractions instead of assuming workspace filesystem paths.

    ## Requirements

    - Introduce IProcessArtifactContentResolver backed by storage placement/storage drivers.
- Keep workspace reader as one implementation.
- Support large/binary evidence through metadata and format plugins.
- Do not block valid storage-driver artifacts only because they are not workspace files.

    ## Implementation Guidance

    - Start with failing-first or red-team tests.
    - Implement production runtime changes.
    - Keep behavior generic; avoid software-only assumptions.
    - Update proof manifest and semantic invariants.
    - Add source assertions and changed-file hashes.

    ## Required Tests

    - Malformed JSON in storage is rejected.
- Valid Markdown stored through managed storage passes.
- Non-workspace storage artifact can be validated.

    ## Acceptance Criteria

    - Old shallow behavior fails.
    - New production behavior passes.
    - No prompt-only fixes.
    - No SQLite runtime reintroduction.
    - Bundle proof files are updated.
