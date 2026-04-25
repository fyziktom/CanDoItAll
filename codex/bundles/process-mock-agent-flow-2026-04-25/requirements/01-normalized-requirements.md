# Normalized Requirements

## Functional Requirements

- R1: The application must support deterministic mock agents for process automation runs.
- R2: The mock agents must be disabled by default and controlled by configuration.
- R3: The mock agents must not call real LLM providers.
- R4: The mock catalog must include multiple role-specific agents for a calculator delivery process.
- R5: Mock agents must create artifacts through the existing AgentFramework workspace artifact path.
- R6: The deterministic flow must include at least one QA rejection that sends developer work back for repair.
- R7: The repaired work must be reviewed and approved by QA in a later step.
- R8: The implementation must exercise existing process transitions, branch outcomes, and artifact projection rather than special-casing the process dispatcher.

## Non-Functional Requirements

- R9: Keep the change small and isolated to AgentFramework mock runtime/catalog plumbing plus tests.
- R10: Keep behavior strongly typed where practical by using constants/options/classes for provider IDs, tags, branch keys, and artifact paths.
- R11: Fail predictably when a mock provider is invoked while the feature is disabled.

## Validation Requirements

- R12: Add targeted automated proof for the settings gate.
- R13: Add targeted automated proof for deterministic mock runtime responses and artifact creation.
- R14: Add targeted automated proof for the QA rejection and repair loop.
