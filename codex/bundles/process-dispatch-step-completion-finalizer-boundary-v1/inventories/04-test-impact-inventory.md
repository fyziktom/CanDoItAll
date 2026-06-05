# Test Impact Inventory

Expected affected slices:

- `ProcessAgentExecutionBoundaryArchitectureTests`
- `ProcessRunAutomationDispatchServiceTests` finalizer/artifact validation slices:
  - `ArtifactContractValidation_*`
  - `ProcessCompletionArtifactValidator_*`
  - `ProcessStepRunBlockState_*`
  - `ArtifactDispositionRouter_*`
  - `SatisfiedArtifactDispositionCompletion_recovers_failed_writeback_with_explicit_repair_branch`
- artifact validation/projection regression filters
- manager artifact recovery tests
- runtime invariant tests
- process-filtered integration smoke
- full solution build

Codex must add exact test names and transcripts during SB02/SB04/SB08/SB12/SB15.
