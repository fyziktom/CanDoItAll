# Next Bundle Decision

## Decision
- Prepare the next bundle for a read-only runtime evidence consistency verifier.
- Do not implement a generic runtime driver host, registry, selector, manager command, scheduler hook, workflow hook, or execution-capable driver yet.

## Why
- The `.NET/Rust` transcript verifier is now safely consumable through a narrow process-module adapter.
- The adapter proof shows explicit supplied-payload validation, no-mutation observations, lane denial, and runtime-hook absence.
- The next useful step is consistency checking across existing Core descriptor families, not execution-capable driver infrastructure.

## Proposed Next Scope
- Normalize supplied Core descriptor payloads into a read-only consistency verification request.
- Detect contradictory execution/finalizer/retry/artifact projection descriptors.
- Return diagnostics and audit facts only.
- Prove no process state, claims, transitions, finalizers, retries, storage, workspace, Office, Graph, or provider repair mutation.

## Out Of Scope For The Next Bundle
- Command execution.
- Package restore or tool invocation.
- Office/Graph calls.
- Business-record mutation.
- Generic driver runtime registration.
- Scheduler or workflow integration.
- Manager commands.
