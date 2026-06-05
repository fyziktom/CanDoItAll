# Candidate Assembly Parity Proof Manifest

## Changed File Hashes

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchArtifactInputAssembler.cs` SHA-256 `b61d04d1294d8f5d6362f2df384ad41b6e3194b665fa6da77d50c628981da2a9`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchBranchDependencyContext.cs` SHA-256 `1587b913f878c82a14ac7f14a97f03a6d69c4916a987a2d3f63211f76310abb4`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchAssignmentRouteHelper.cs` SHA-256 `2481c9ff912334cf2dd806de154eacc6b7750b1101a2b948a67c8fff950bd8aa`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` SHA-256 `b97a3548311d1a5645830e38ff9e2dd0c88f020cc0f53a0d870ae6cb3aa1616f`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs` SHA-256 `8ea0d5290d8834aa6c8399d0d1beb9aafa46cbdd975a4ec131b7c70854c84f4d`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Cooperation.cs` SHA-256 `7f2b8bfa9c976929be0da4cb71809fe07f040e7a15d6d84594ccd2075fcfd199`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` SHA-256 `70a866b2ef7b6faf0e22d47ca0101791b7b5244175d62403167be48671c0d3f4`

## Command Transcripts

- Failing-first: `proof/SB12/transcripts/sb12-failing-first-assembly-helper-trap.txt`
- Passing transcript: `proof/current/transcripts/candidate-hydration-integration-wrapper-tests.txt`
- Anti-stub audit transcript: `proof/current/transcripts/candidate-hydration-anti-stub-and-scope-scan.txt`
- Source assertion: `proof/SB12/source-assertions/gate-c-candidate-assembly-parity.md`
- Semantic invariant contract: `proof/SB12/semantic-invariants.md`
- Bundle reference: `bundle://proof/SB12/manifest.md`

## Source Assertions

- Raw note owned: preserve artifact-input, branch outcome, and assignment/workflow route behavior.
- Shipped behavior: artifact input assembly, branch dependency context, and assignment route recognition are behind local helpers while dispatcher wrappers remain available.
- Shallow-pass trap: a helper-exists-only change that leaves shaping inline, changes artifact input filtering, or hides route assignment semantics.
- Semantic positive proof: `proof/current/transcripts/candidate-hydration-integration-wrapper-tests.txt`
- Adversarial negative proof: `proof/SB12/transcripts/sb12-failing-first-assembly-helper-trap.txt`
- Anti-stub audit: `proof/current/transcripts/candidate-hydration-anti-stub-and-scope-scan.txt`
- Downstream dependency check: Unlocks side-effectful technical-agent binding and recovery query isolation in SB13-SB16.
