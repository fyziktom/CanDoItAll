# Rollback strategy

Each implementation subbundle must be independently revertible.

- SB00: contains bundle readiness, tests, and baseline proof only; it changes no production behavior.
- SB01: revert only authority-projection parsing and restoration validation.
- SB02: revert only source-authority provider ownership, registration, and resolver composition.
- SB03: revert only effective policy-context propagation and process enrichment validation.
- SB04: revert only scope-aware process-lease cleanup composition.
- SB05: revert only cross-instance file-store coordination and temp-file hygiene.
- SB06: revert only ordinary-conversation compensation, active-turn invariants, and capacity admission.
- SB07: revert only usage aggregation and workflow failure projection.
- SB08: re-adding production registration is a future feature activation, not a rollback of runtime behavior.
- SB09: contains validation and merge-decision evidence only.

Never roll back by restoring broad `IAgentRuntime`, moving process recovery into MAF, disabling
governance checks, or deleting failing tests.
