# SB10 - Generic Scenario Harness and Red-Team Pack

    ## Mission

    Build broad validation scenarios across software and non-software processes.

    ## Requirements

    - Add non-software scenarios: purchasing, HR onboarding, incident response, legal review, manufacturing QA, business planning.
- Add software scenarios: architecture-only, implementation, QA, repair, release gate.
- Run scenario harness under PostgreSQL.
- Assert process completion/blocking/branch outcomes and artifacts.

    ## Implementation Guidance

    - Start with failing-first or red-team tests.
    - Implement production runtime changes.
    - Keep behavior generic; avoid software-only assumptions.
    - Update proof manifest and semantic invariants.
    - Add source assertions and changed-file hashes.

    ## Required Tests

    - Architecture-only Blazor step cannot implement.
- Business plan report can write external artifact destination without product mutation.
- Legal review can no-go without browser/build evidence.
- Manufacturing QA can route defect to repair branch.

    ## Acceptance Criteria

    - Old shallow behavior fails.
    - New production behavior passes.
    - No prompt-only fixes.
    - No SQLite runtime reintroduction.
    - Bundle proof files are updated.
