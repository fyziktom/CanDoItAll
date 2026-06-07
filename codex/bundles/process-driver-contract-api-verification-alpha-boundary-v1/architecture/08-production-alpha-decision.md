# Production Alpha Decision

## Decision
- Production verification-driver alpha is deferred.
- This bundle approves only `CanDoItAll.Processes.Drivers.Abstractions` as a contract-only package.
- The `.NET/Rust transcript verifier` remains a test-only rehearsal.

## Reasoning
- Permission modes, capability scopes, denial reasons, audit facts, redaction descriptors, evidence references, transcript references, verification requests/responses, diagnostics, and version metadata now have strongly typed contracts.
- Runtime ownership is still intentionally absent: no registry, selector, DI registration, manager command, shell execution, Office connector, storage/workspace writer, process mutator, finalizer applier, or retry scheduler is approved.
- A future production alpha still needs sandboxing, command allowlists, network/filesystem policy, audit persistence, secret masking, lifecycle ownership, and executable negative tests.

## Next Alpha Boundary
- If a future bundle approves alpha implementation, it must start with a verification-only reader over existing evidence artifacts.
- It must not execute commands, restore packages, call Office/Graph, write workspace/storage, mutate process state, claim dispatch, apply transitions, apply finalizers, or schedule retries.
- It must ship with production producer and consumer proof for every new production signal, state, record, or event it introduces.
