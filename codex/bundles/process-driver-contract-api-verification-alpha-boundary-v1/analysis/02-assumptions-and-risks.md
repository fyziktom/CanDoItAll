# Assumptions And Risks

## Assumptions
- The latest branch contains the completed prerequisite bundle and all proof transcripts referenced by the execution report.
- The next step may introduce production **contract-only** driver abstractions, but not runtime implementations.
- The first future domain lane should be `.NET/Rust transcript verifier` because it can inspect existing artifacts without command execution.

## Critical Path Risks
1. Contract-only API accidentally becomes a runtime API through registry/DI/selector naming.
2. The `.NET/Rust` rehearsal accidentally allows command execution or workspace writes.
3. Audit/redaction models become optional instead of required.
4. Core starts referencing driver abstractions, reversing dependency direction.
5. Tests allow broad `Driver` token exemptions and weaken earlier safety gates.
6. Domain lane docs imply Office/Graph or business-record mutation before permission gates exist.

## Validation Risks
- Build-only proof is insufficient. The bundle must include architecture tests that fail on runtime driver APIs, DI hooks, registries, manager commands, shell/Graph/storage mutation, and broad Core dependencies.
- Source scans must distinguish allowed contract-only type names from forbidden runtime/registry/selector names.
- Proof must include separate SB001-SB042 rows and must not collapse execution report rows.

## Reopen Triggers
- Any new project references `CanDoItAll.Modules.*`, `Infrastructure`, `AgentFramework`, EF, storage/workspace, UI, or external connector packages from Core.
- Any production driver abstraction includes an execution method, runtime selector, service registration, shell command, Graph/Office operation, or artifact/process mutation method.
- Any test fixture or contract permits state mutation under `VerificationOnly` or `ManagerReadonly`.
- Any bundle proof is status-only without transcripts.
