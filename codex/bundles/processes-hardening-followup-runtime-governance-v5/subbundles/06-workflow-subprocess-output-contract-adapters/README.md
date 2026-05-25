# SB06 - Workflow/Subprocess Output Contract Adapters

    ## Mission

    Replace loose kind/title/summary matching with explicit output mapping for workflow and subprocess roles.

    ## Requirements

    - Add output mapping model for workflow executor assignments.
- Add child-to-parent artifact mapping for subprocess parent steps.
- Validate mappings at publish/start.
- Use mappings in projection adapters and finalizer validation.

    ## Implementation Guidance

    - Start with failing-first or red-team tests.
    - Implement production runtime changes.
    - Keep behavior generic; avoid software-only assumptions.
    - Update proof manifest and semantic invariants.
    - Add source assertions and changed-file hashes.

    ## Required Tests

    - Two same-kind workflow artifacts do not bind to the wrong expectation.
- Subprocess child artifact maps only through declared mapping.
- Missing mapping produces lint error or blocked start depending mode.

    ## Acceptance Criteria

    - Old shallow behavior fails.
    - New production behavior passes.
    - No prompt-only fixes.
    - No SQLite runtime reintroduction.
    - Bundle proof files are updated.
