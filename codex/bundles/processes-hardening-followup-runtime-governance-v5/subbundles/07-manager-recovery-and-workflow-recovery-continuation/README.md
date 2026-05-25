# SB07 - Recovery Continuation

    ## Mission

    Make recovery continuation work consistently for direct agent, workflow-backed, subprocess-backed, and manager-recovered artifacts.

    ## Requirements

    - Allow manager recovery for workflow-backed completed steps when evidence exists but process artifacts are missing.
- Carry recovery lineage into finalizer context.
- Do not rerun broad implementation for artifact-only recovery.
- Provide typed recovery decision records.

    ## Implementation Guidance

    - Start with failing-first or red-team tests.
    - Implement production runtime changes.
    - Keep behavior generic; avoid software-only assumptions.
    - Update proof manifest and semantic invariants.
    - Add source assertions and changed-file hashes.

    ## Required Tests

    - Workflow completed but missing mapped process artifact can be recovered by manager.
- Recovery cannot invent missing source evidence.
- Recovery artifact validates against recovered-for execution/run.

    ## Acceptance Criteria

    - Old shallow behavior fails.
    - New production behavior passes.
    - No prompt-only fixes.
    - No SQLite runtime reintroduction.
    - Bundle proof files are updated.
