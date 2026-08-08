# Rollback strategy

Each subbundle must be independently revertible.

- SB01: revert only metadata read result and execution validation.
- SB02: revert only store coordinator/locking changes.
- SB03: revert only conversation state-machine changes.
- SB04: revert only usage aggregation and workflow failure projection.
- SB05: re-adding production registration is a future feature action, not a rollback of runtime behavior.
- SB06: contains validation evidence only.

Never roll back by restoring broad `IAgentRuntime`, moving process recovery into MAF, disabling
governance checks, or deleting failing tests.
