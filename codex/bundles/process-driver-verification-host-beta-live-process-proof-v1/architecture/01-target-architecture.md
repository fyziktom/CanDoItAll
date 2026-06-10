# Target Architecture

## Current runtime ownership
Process execution remains owned by the Process Module:

- UI/API/project-structure launch surfaces create process runs through `ProcessesService`.
- Durable outbox, claims, route execution, MAF/direct-agent/workflow execution, finalizer, artifact projection, recovery, and readback remain process-owned.
- Process Core remains pure/deterministic and must not know about drivers.

## New target: verification host beta
The next safe step is a **verification-only host beta**, not an execution-capable generic driver host.

Allowed:
- typed read-only host requests;
- explicit lane registry and selector;
- async/cancellable verify calls;
- structured denials instead of expected exceptions;
- host options and emergency disable;
- durable audit records with redacted hashes;
- manager-readonly diagnostics and query surfaces;
- source-backed scheduler/workflow readiness for read-only verification.

Denied:
- shell/package restore;
- file/workspace/storage writes;
- Office/Graph/CRM/network calls;
- provider repair;
- process, claim, transition, finalizer, retry mutation;
- fallback selectors;
- runtime plugin discovery;
- object payload dispatch;
- Process Core references to drivers or modules.

## Future execution-capable host
Execution-capable process drivers can be proposed only after a separate approval bundle provides: sandboxing, command allowlists, workspace isolation, durable audit persistence, explicit authorization, lifecycle ownership, failure handoff, timeout/cancellation semantics, emergency stop, public API snapshots, and red-team proof.
