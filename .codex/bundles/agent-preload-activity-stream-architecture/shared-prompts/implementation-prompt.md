# Implementation Prompt

Implement only the named subbundle from `agent-preload-activity-stream-architecture`.

Before editing, verify its prerequisite checkpoint in `plan/architecture-checkpoints.md`, current worktree ownership, exact source references, architecture contracts, and proof tier. Preserve canonical run/module storage, immutable snapshot rules, strongly typed identities, explicit errors, safe DI lifetimes, and the backend-before-UI gate. Make the smallest coherent change; do not pool live runtime resources, add string topics/cache keys, parallelize one DbContext/store write/ordered runtime composition, or introduce silent fallback.

Write failing-first tests for the subbundle’s semantic positive and adversarial negative. Run targeted build/tests, capture required proof, update `reviews/01-execution-report.md`, and record the progression decision. Governed work also requires a manifest, transcripts, architecture snapshot, producer/consumer/lifecycle coverage where applicable, semantic invariants, and anti-stub audit.

Stop and reopen the owning earlier subbundle if the checkpoint cannot honestly pass. Do not continue into UI before A5 or closure before A7.
