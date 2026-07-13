# SB03 Semantic Invariants

- Runtime tool-provider access must use the shared capability policy evaluator.
- Provider keys and tool names must remain validated before attachment.
- Mutation tools requiring approval must still be approval-wrapped unless approval suppression is explicit.
- Existing `MafAgentRuntimeToolProviderCompositionTests` must continue to pass.
