# Next Bundle Decision

## Decision
- Recommended next work: driver-contract prerequisite bundle.
- Do not start a production driver implementation yet.
- Do not start broad Core runtime extraction.

## Proposed Next Bundle Objective
Create executable permission/audit/sandbox prerequisite tests for a future `VerificationOnly` driver alpha.

## Initial Scope
- Convert `VerificationOnly`, `ManagerReadonly`, and denied `ExecutionCapableFuture` semantics into tests.
- Define audit fact persistence and redaction expectations.
- Define sandbox and command denial policy.
- Choose the .NET/Rust transcript verifier as the first candidate only after prerequisites are executable.

## Out Of Scope
- Production driver runtime.
- Driver registry or dependency-injection registration.
- Manager command.
- Shell, Graph, storage, workspace, or process-state mutation.
- Additional Core runtime extraction.
