# Normalized Requirements

| Requirement ID | Requirement |
| --- | --- |
| RQ01 | Add a generic step execution boundary model that distinguishes artifact-only, analysis/design, decision/review, product mutation, validation, launch/browser proof, external action, and recovery work. |
| RQ02 | Enforce step execution boundaries through invocation metadata and tool policy, not prompt text only. |
| RQ03 | Ensure workflow-backed process role steps load expected artifacts, artifact inputs, branch outcomes, and finalizer validation context. |
| RQ04 | Ensure subprocess parent completion goes through the same process-owned finalizer and cannot satisfy required expectations with source-less placeholders. |
| RQ05 | Route negative findings to modeled branch outcomes when possible; reserve `Blocked` for inability to make a governed disposition. |
| RQ06 | Add upstream artifact materialization lifecycle that unblocks or requeues downstream steps when the missing artifact is produced. |
| RQ07 | Replace artifact mode/format/placeholder heuristics with explicit or conservative validation rules suitable for generic process types. |
| RQ08 | Strengthen current-run artifact lineage for all producer kinds. |
| RQ09 | Compress no-progress retries using fingerprints and stop repeated attempts that have no new evidence, mutation, or decision. |
| RQ10 | Add process definition lint/simulation for step boundaries, artifacts, role assignments, workflow/subprocess contracts, branch dispositions, and tool policies. |
| RQ11 | Add red-team tests covering software and non-software processes. |
| RQ12 | Keep PostgreSQL-only assumptions; do not reintroduce SQLite. |
