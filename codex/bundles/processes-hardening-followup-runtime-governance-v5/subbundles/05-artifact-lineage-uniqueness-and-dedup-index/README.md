# SB05 - Artifact Lineage Uniqueness and Dedup Index

    ## Mission

    Use stable typed lineage identity for artifact dedupe and audit rather than bounded ExternalReferenceKey.

    ## Requirements

    - Add projection identity hash.
- Use typed lineage as primary identity.
- Keep ExternalReferenceKey for compatibility/display.
- Prevent duplicate records from concurrent projection.

    ## Implementation Guidance

    - Start with failing-first or red-team tests.
    - Implement production runtime changes.
    - Keep behavior generic; avoid software-only assumptions.
    - Update proof manifest and semantic invariants.
    - Add source assertions and changed-file hashes.

    ## Required Tests

    - Long lineage does not collide after key truncation.
- Manager recovery artifact dedupes correctly.
- Workflow/subprocess artifacts dedupe by typed source IDs.

    ## Acceptance Criteria

    - Old shallow behavior fails.
    - New production behavior passes.
    - No prompt-only fixes.
    - No SQLite runtime reintroduction.
    - Bundle proof files are updated.
