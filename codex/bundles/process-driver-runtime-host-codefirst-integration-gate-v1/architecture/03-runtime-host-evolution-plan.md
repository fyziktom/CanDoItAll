# Runtime host evolution plan

## Current beta
Verification-only host with exact lanes, structured denials, EF audit, manager facade and dry-run planning.

## Next state after this bundle
Generic runtime-host readiness with:

- stable contracts for verification and dry-run execution,
- durable audit and status readback,
- scheduler/workflow read-only job execution,
- static driver capability descriptors,
- sandbox/authorization emergency-stop model,
- manager/operator readback.

## Still future-gated
Execution-capable host remains blocked until a separate approval bundle proves:

- sandbox boundary,
- allowlisted operations,
- authorization and revocation,
- emergency stop,
- immutable audit persistence,
- cancellation and timeout semantics,
- failure handoff,
- operator controls,
- malicious corpus and red-team proof.
