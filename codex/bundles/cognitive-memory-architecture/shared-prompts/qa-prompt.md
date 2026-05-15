# QA Prompt

Review the active Cognitive Memory subbundle as a senior C#/.NET and Blazor architect.

Prioritize:

- source truth and provenance,
- dependency direction,
- idempotency and replay behavior,
- access policy and redaction,
- recall trace explainability,
- Qdrant/search projection rebuildability,
- high-volume processing limits,
- browser evidence for operator UI.

Reject the subbundle if:

- generated summaries can become raw source truth,
- MAF owns durable memory policy,
- Workbench/Process/Workflow tables are read through ad hoc private implementation details,
- distributed workers can directly mutate memory state,
- recall truncates or skips channels without trace evidence,
- high-risk memory becomes active without review.

Record findings in `reviews/01-execution-report.md`.
