# SB03 - Trusted Grounding Source Model and Alias Ledger

    ## Mission

    Replace broad text-scraped external alias grants with a typed grounded-target ledger.

    ## Requirements

    - Record alias source kind, intended use, trust level, confidence, and scope.
- Only trusted launch/project-structure sources can grant writable product targets.
- Artifact summaries/provenance may provide read context but not write permission.
- Persist ledger entries for audit.

    ## Implementation Guidance

    - Start with failing-first or red-team tests.
    - Implement production runtime changes.
    - Keep behavior generic; avoid software-only assumptions.
    - Update proof manifest and semantic invariants.
    - Add source assertions and changed-file hashes.

    ## Required Tests

    - Old sibling path in upstream summary does not become writable.
- Current project-structure target becomes read-only or writable according to operation contract.
- No-go/exclusion paths never become targets.

    ## Acceptance Criteria

    - Old shallow behavior fails.
    - New production behavior passes.
    - No prompt-only fixes.
    - No SQLite runtime reintroduction.
    - Bundle proof files are updated.
