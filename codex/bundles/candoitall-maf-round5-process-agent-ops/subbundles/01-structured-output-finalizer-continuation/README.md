# 01 Structured Output, Finalizer, and Continuation Enforcement

## Goal

Machine-critical agent output must remain structured and finalized through initial runs and approval continuations.

## Tasks

1. Persist the requested structured output contract on execution runs or recover it deterministically from request metadata.
2. Pass the contract to `RespondToPendingApprovalsAsync(...)` and `ContinueAutoApprovedRunAsync(...)` instead of `structuredOutput: null`.
3. Add finalizer policy for governed process outputs.
4. Required finalizer mode must enforce exactly one finalizer call.
5. Finalized output must replace raw assistant response before chat transcript persistence and run completion.
6. Add tests for initial run, approval continuation, auto-approved continuation, missing finalizer, duplicate finalizer, and invalid finalizer payload.

## Acceptance criteria

- No governed process continuation loses schema response format.
- Technical run cannot complete successfully with invalid/missing governed machine output.
- Assistant transcript and execution result agree on the finalized machine output.
