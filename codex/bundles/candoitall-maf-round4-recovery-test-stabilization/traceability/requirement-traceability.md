# Requirement Traceability

| Requirement | Subbundle(s) | Acceptance proof |
|---|---|---|
| R00 | 00, 15 | Snapshot check test/script, execution report paths |
| R01 | 00 | Secret scan test passes, appsettings redacted, rotation note |
| R02 | 01 | Unit tests for every process tool classification |
| R03 | 01, 07 | Policy/approval behavior tests |
| R04 | 02 | `AgentRecoveryDecision` models and tests |
| R05 | 02, 04 | `AgentReworkPacket` models, persisted packet tests |
| R06 | 03 | Context strategy tests |
| R07 | 04 | QA rejection to rework packet integration test |
| R08 | 05 | Proof fingerprint reuse/invalidation tests |
| R09 | 06 | Retry ledger/backoff/loop tests |
| R10 | 07 | Finalizer sequence trace behavior tests |
| R11 | 08 | Default green gate command passes or documented categories |
| R12 | 09 | Playwright Release/no-build tests/fixtures fixed |
| R13 | 10 | MCP stdio path tests pass in Release/no-build |
| R14 | 11 | ProjectStructure host tests pass |
| R15 | 12 | Obsolete/brittle tests updated or documented removed |
| R16 | 13 | Storage/project integration tests isolated and green |
| R17 | 14 | Live-process tests gated or deterministic |
| R18 | 15 | Docs and execution report verified |
