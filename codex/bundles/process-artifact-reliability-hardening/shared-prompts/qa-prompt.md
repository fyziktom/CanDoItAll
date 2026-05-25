# Shared QA Prompt

You are QA-reviewing one or more subbundles from `process-artifact-reliability-hardening`.

Challenge the implementation with these questions:

1. Can any process step transition to Completed without the process-owned finalizer?
2. Can a workflow-backed role bypass artifact validation?
3. Can a `ProcessArtifactRecord` with an expectation id satisfy completion even when content/provenance is invalid?
4. Can final assistant response text satisfy evidence or deliverable artifacts incorrectly?
5. Can a placeholder/gap/subprocess proxy record satisfy a required artifact?
6. Can a stale managed file from a previous run satisfy a current run expectation?
7. Can a generic `lead` agent be selected for recovery without explicit recovery capability?
8. Can recovery complete without source evidence or only with prose?
9. Can the same artifact failure trigger repeated identical retries?
10. Was any SQLite code, migration, or test introduced?

Required QA output:

- source assertions
- failing-first/passing transcript review
- anti-stub audit
- red-team negative results
- raw note closure status
- explicit blockers if any proof is missing
