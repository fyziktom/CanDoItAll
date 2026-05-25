# SB08 - Runtime Invariant Audit

    ## Mission

    Add durable post-step invariant auditing and surface violations in process health.

    ## Requirements

    - Audit tool receipts, artifact lineage, changed paths, branch disposition, and operation contract.
- Persist ProcessRuntimeInvariantViolation records or journal entries.
- Block/escalate severe violations.
- Expose in run/step health view models.

    ## Implementation Guidance

    - Start with failing-first or red-team tests.
    - Implement production runtime changes.
    - Keep behavior generic; avoid software-only assumptions.
    - Update proof manifest and semantic invariants.
    - Add source assertions and changed-file hashes.

    ## Required Tests

    - Non-mutating step with product mutation receipt is flagged.
- Wrong-root artifact is flagged.
- Missing lineage is flagged for evidence/deliverable artifacts.

    ## Acceptance Criteria

    - Old shallow behavior fails.
    - New production behavior passes.
    - No prompt-only fixes.
    - No SQLite runtime reintroduction.
    - Bundle proof files are updated.
