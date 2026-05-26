# Assumptions And Risks

## Working Assumptions

- The current branch is `processes-hardening`.
- PostgreSQL remains the only runtime database provider.
- The process runtime must stay generic across software and non-software process definitions.
- Legacy heuristics may remain only as compatibility fallback with explicit warnings or tests.

## Critical Path Risks

- Alias ledger, artifact validation, block classification, and recovery routing are critical foundations because later phases rely on their typed semantics.
- Large partial classes can keep absorbing policy and recovery logic unless the checkpoint subbundles extract cohesive services.
- Manual/API completion parity can regress if automated finalizer validation is not shared through a single validator path.

## Validation Risks

- Happy-path fixture tests can pass while still allowing placeholder artifacts, wrong-run artifacts, or heuristic artifact matching.
- Regex-only script inspection can miss nested scripts, encoded commands, shell delegation, or static IO APIs.
- Completed-stage proof will fail if command transcripts, changed-file hashes, semantic invariants, and source assertions are not recorded under `proof/SBxx/`.

## Reopen Triggers

- Reopen SB01 if a writable alias also appears in read-only metadata or read-only policy still denies trusted writable roots.
- Reopen SB03 or SB08 if manual/API transition and automated finalizer do not share content-backed validation.
- Reopen SB05 or SB10 if recovery options derive from human-readable reason text instead of typed block causes.
- Reopen SB09 if workflow or subprocess projection can still satisfy a process expectation by ambiguous title/kind matching.
