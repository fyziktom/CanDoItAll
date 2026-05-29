# Normalized requirements

| Id | Requirement | Verification |
| --- | --- | --- |
| R1 | Establish a fresh MAF package/API baseline and either upgrade to the latest compatible line or record exact blockers in an ADR. | Package scan, restore/build, MAF compiler/runtime tests. |
| R2 | Replace graph-level preemptive `HumanInput` waiting with execution-position-aware HITL/request handling. | Unit/integration workflow where a human node exists but is not reached; workflow must complete without waiting. |
| R3 | Implement a product approval gate for approval-required workflow executors. | Approval-required executor succeeds after approved response and fails safely after denial/timeout. |
| R4 | Consume streaming MAF events where needed and persist typed event metadata with executor/node identity. | Event tests assert node/executor IDs, request IDs, output payload, and redacted error data. |
| R5 | Add checkpoint abstraction and initial trusted storage implementation for in-process preview/runtime. | Superstep checkpoint records are persisted and can be enumerated; resume contract is validated or explicitly disabled. |
| R6 | Enforce artifact and payload policy consistently across started events, outputs, executor payloads, plugin logs, and artifacts. | Oversized payload tests prove inline truncation/artifact split and no secret leakage. |
| R7 | Make plugin observer registration deterministic and order-independent. | DI tests prove plugin executor audit records reach plugin logs regardless of module registration order. |
| R8 | Validate plugin executor permission policy against plugin manifest capabilities and connection metadata. | Manifest validation tests for HostCommand, OAuth/secret/network/external write scenarios. |
| R9 | Align backend catalog with actually registered/runnable backends. | UI/API tests show DurableTask/AzureFunctions are unavailable/planned unless a backend is registered. |
| R10 | Decide and document the `BindAsExecutor` vs source-generated executor strategy. | ADR plus test/benchmark/proof or implementation of static adapter executors for stable node families. |
| R11 | Keep live external effects disabled by default in proof. | Test settings prove Gmail/O365/Docker live calls are not executed during default test runs. |
| R12 | Keep final evidence concise and reproducible. | Execution report references targeted commands and source-level tests rather than large raw scans. |
