# Assumptions And Risks

## Working Assumptions

- The local repository branch contains the same MAF 1.6 package upgrade referenced by the bundle.
- PostgreSQL-backed tests are the primary integration-path proof unless a subbundle explicitly needs host or browser proof.
- Some MAF 1.6 features may be adapter compatibility decisions rather than immediate production adoption.

## Critical Path Risks

- A package-only upgrade could pass restore/build while leaving tool-loop, finalizer, A2A, telemetry, and session-file behavior unchanged.
- Artifact validation could accept a current-run record for the wrong step, expectation, execution run, or producer mode.
- Read model and finalizer validation could drift if they compute satisfaction independently.
- Recovery approval flow could create decision evidence and accidentally satisfy the original deliverable.

## Validation Risks

- Mock harnesses can miss storage-backed content and lineage failures.
- Source-only checks can miss runtime MAF adapter behavior.
- Browser screenshots prove only route visibility unless they are paired with route actions and assertions.
- Agent communication smoke tests may require provider configuration; if unavailable, use the existing mock/scenario harness and record the limitation.

## Reopen Triggers

- Any targeted test shows `Satisfied` in a read model while finalizer validation rejects the same artifact.
- Any validation path reports `StaleOrWrongRun` for readable current-run content that should instead pass or report content/hash-specific status.
- Any tool type bypasses `AgentToolInvocationPolicy`.
- Any package audit finds active MAF 1.3 references in `src` or `tests`.
- Web app startup fails or agent communication cannot be proven through a real runtime or approved mock runtime.
