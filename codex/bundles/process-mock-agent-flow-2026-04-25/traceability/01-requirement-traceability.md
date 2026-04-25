# Requirement Traceability

| Requirement | Owning subbundle | Proof target |
| --- | --- | --- |
| R1 deterministic mock agents | 02 | `ProcessMockAgentRuntimeIntegrationTests` |
| R2 disabled by default | 02 | `Process_mock_catalog_is_not_seeded_when_disabled` |
| R3 no real LLM calls | 02 | `process-mock://agents` provider adapter and runtime decorator |
| R4 role-specific agents | 02 | `Process_mock_catalog_seeds_role_agents_when_enabled` |
| R5 workspace artifacts | 02, 03 | Runtime writes and artifact assertions |
| R6 QA rejection | 03 | `branchOutcomeKey` `repairs-required` in targeted runtime flow |
| R7 QA approval after repair | 03 | `branchOutcomeKey` `approved` in targeted runtime flow |
| R8 existing process transitions | 03 | Not fully closed; current proof uses AgentFramework `process-step` context without dispatcher special-casing |
| R9 narrow implementation | 01, 04 | Diff review and web project build |
| R10 typed constants/options | 02 | `ProcessMockAgentCatalog`, role key constants, and `ProcessMockAgentOptions` |
| R11 predictable disabled failure | 02 | Disabled catalog test and runtime disabled guard |
| R12 settings gate proof | 02 | `Process_mock_catalog_is_not_seeded_when_disabled` |
| R13 runtime response proof | 02 | `Process_mock_runtime_runs_deterministic_calculator_rejection_repair_and_approval` |
| R14 QA repair proof | 03 | `Process_mock_runtime_runs_deterministic_calculator_rejection_repair_and_approval` |
