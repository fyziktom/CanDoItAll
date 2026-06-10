# Runtime Host Roadmap

## Stage A: Verification-only alpha (current)
Implemented. Synchronous host, explicit lane selector, in-memory audit.

## Stage B: Verification-only beta (this bundle)
Async/cancellable host, durable audit, options, manager-readonly facade, live process-run smoke.

## Stage C: Verification job integration
Read-only scheduled/workflow verification jobs over already-produced process evidence. No driver execution hooks.

## Stage D: Execution-capable proposal
Only after sandbox, authorization, audit persistence, emergency stop, command/network/storage policy, lifecycle ownership, and red-team proof.

## Stage E: Domain driver packs
Only after stable host contracts. Driver packs must declare capabilities and cannot self-register or execute unless approved by policy.
