# SB09 - Typed Blocked/Failed Escalation Lifecycle

    ## Mission

    Replace fragile free-text blocked reasons with typed block codes and recovery options.

    ## Requirements

    - Add typed block reason code and recovery option metadata.
- Use typed codes for missing upstream artifact, policy denial, unavailable tool, missing credentials, validation failure, no-progress.
- Support auto-retry, manager recovery, human escalation, or repair branch routing.
- Do not rely on BlockedReason substring matching.

    ## Implementation Guidance

    - Start with failing-first or red-team tests.
    - Implement production runtime changes.
    - Keep behavior generic; avoid software-only assumptions.
    - Update proof manifest and semantic invariants.
    - Add source assertions and changed-file hashes.

    ## Required Tests

    - Materialization block reopens by typed code.
- Policy-denied external path becomes actionable escalation.
- Repeated no-progress becomes typed recovery/stop condition.

    ## Acceptance Criteria

    - Old shallow behavior fails.
    - New production behavior passes.
    - No prompt-only fixes.
    - No SQLite runtime reintroduction.
    - Bundle proof files are updated.
