# Critical Invariants

1. A current-run artifact written by the current step execution run must not be rejected as `StaleOrWrongRun` if it has matching run, step, expectation, source execution run, and resolvable content.
2. If content cannot be read or content hash is missing, the diagnostic must say that explicitly, not misclassify it as wrong run.
3. The artifact satisfaction read model and finalizer validation must agree on required artifact status.
4. MAF upgrade must not lose tool receipts, finalizer capture, structured output validation, approval state, execution logs, metrics, or workflow/subprocess mapping.
5. A2A/handoff changes must not mutate process role boundaries or bypass process-owned artifacts.
6. Processes remain above Workflows.
7. The process core remains generic.
