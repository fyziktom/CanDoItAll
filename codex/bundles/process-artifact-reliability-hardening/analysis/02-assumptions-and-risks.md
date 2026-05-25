# Assumptions And Risks

## Assumptions

- PostgreSQL is the only supported runtime database target in the current branch.
- Process runtime failures are more often caused by contract mismatch or missing evidence than by transient model failure.
- Re-running the same executor with the same prompt and same missing artifact condition is usually wasteful after a small retry budget.
- A workflow-backed role should be treated as one executor kind inside a process, not as a replacement for process finalization.
- Some narrative artifacts can be synthesized from an agent response, but evidence/deliverable artifacts need stricter source proof.

## Critical Path Risks

- If workflow-backed process roles bypass a common process finalizer, artifact validation will remain inconsistent even if direct agent execution is hardened.
- If `ArtifactExpectationId` presence remains the only completion signal, placeholders or weak projections can falsely satisfy required artifacts.
- If recovery continues to rely on shared mutable `HashSet` state inside `DispatchCandidate`, future refactoring may silently break recovery completion detection.
- If manager recovery can select a generic `lead`, the wrong agent may synthesize artifacts and hide process failures.
- If invalid format/schema is treated as a normal retry condition, steps can continue retrying without progress.
- If response-text projection remains unrestricted, final chat text can accidentally satisfy artifacts that require real evidence.

## Validation Risks

- Some current tests use reflection against private runtime methods. New public/internal seams may be needed to reduce fragile testing.
- Workflow-backed role tests may require a fake or controlled workflow coordinator outcome to avoid live workflow complexity.
- PostgreSQL integration tests can be slower than unit tests; focused scenarios should be created before broad end-to-end runs.
- Artifact validity must be tested semantically, not only by checking non-empty artifact records.

## Reopen Triggers

Reopen `SB01` if any executor kind can transition a process step without the common process-owned finalizer.

Reopen `SB02` if a required artifact expectation can be satisfied without passing artifact-mode validation.

Reopen `SB03` if manager recovery can complete a missing artifact without source evidence, or if a generic unrelated manager can be selected.

Reopen `SB04` if placeholder/proxy/subprocess artifacts can satisfy required expectations.

Reopen `SB05` if a step repeats the same artifact failure more than the configured retry budget without producing a diagnostic, recovery attempt, or blocked state.

Reopen `SB06` if any SQLite residue is introduced or validation depends on SQLite.
