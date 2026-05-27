# Assumptions And Risks

## Working Assumptions

- The branch under test is the current checkout at execution time, not only the reviewed `phase10` commit.
- Existing prior proof files may be placeholders and must be replaced or verified during execution.
- Integration tests are the primary proof surface for process runtime behavior; component/Playwright proof is required only where UI-visible behavior changes.

## Critical Path Risks

- Read-model parity is a critical foundation; if SB10 or SB11 is wrong, SB12 through SB18 cannot be trusted.
- Tool approval and finalizer proof can produce false confidence if tests only assert enum values or seeded diagnostics.
- Step0 live smoke can be unsafe if it accidentally runs more than the intended first step.

## Validation Risks

- Long-running integration and component tests may expose unrelated repo instability; capture exact command and exit code in proof.
- UI proof may require a running app or seeded data; if browser proof cannot be produced, document the blocker and do not claim UI completion.
- Placeholder proof files from preparation must not be treated as passing evidence.

## Reopen Triggers

- Any invalid artifact status appears as `Satisfied` or `AutoProjected` in the read model.
- A downstream test demonstrates policy bypass, stale session replay, or finalizer state mismatch.
- The step0 smoke harness cannot prove finalizer result, read model, diagnostics, artifact content, content hash, and tool receipts agree.
