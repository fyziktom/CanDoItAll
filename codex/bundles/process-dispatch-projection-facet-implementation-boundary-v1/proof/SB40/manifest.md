# SB40 Proof Manifest

## Gate

- Subbundle: SB40
- Gate: Gate H: source-family and duplicate proof
- Semantic invariant contract: bundle://proof/SB40/semantic-invariants.md
- Invariant ID: SB40_INV_001

## Changed File Hashes

| Path | After SHA-256 |
| --- | --- |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacetImplementations.cs | d71cc59f589db784d8a680522a946d65e4d0dfe7e4a3880a5a4b24d6d7c4f5b1 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacets.cs | cc7bdc18df79f212f0ea128e7524ef1199eb131c261d2080d39f73584dff155d |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs | db1ba00e35a92a67812dca32cf8b6369102ec8645b2b27846230c289494e5d8a |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionUtilities.cs | 1e346602802abe665b9daba750e48c51cb18c1b265e4138587484dab75c40ec6 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs | 3a7cd74a6c6f1e6ee450bb413c2e565417f0f2b0b5ec57cec6cb75c23287edc2 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs | 0b52ec133d5b0a3eb253bb003795e1ffe9032da61a5b858dff45af0347938e26 |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactKinds.cs | b1110c01ce8ccb4b3dc5ef583a8b108c25dd5ac777b6efc8aa3b7949639576a4 |
| repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs | 681eb02919ccb8ac9536a48a5a024b1202c567b28e4031a26adc395b17d9138e |


## Source Evidence

- Source file: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacetImplementations.cs
- Source file: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacets.cs
- Source file: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs
- Source file: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionUtilities.cs
- Source file: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs
- Source file: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs
- Source file: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactKinds.cs
- Source file: repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Deleted all-facet service proof: source assertions verify the old nested artifact-projection service file is absent.

## Command Evidence

- Command transcript: bundle://proof/shared/transcripts/prepared-validator.txt
- Command transcript: bundle://proof/shared/transcripts/unit-projection-tests.txt
- Command transcript: bundle://proof/shared/transcripts/integration-projection-tests.txt
- Command transcript: bundle://proof/shared/transcripts/full-build.txt
- Command transcript: bundle://proof/shared/transcripts/source-scan-no-core-driver-ui.txt
- Command transcript: bundle://proof/shared/transcripts/source-scan-no-all-facet.txt
- Command transcript: bundle://proof/shared/transcripts/source-scan-coordinator-boundaries.txt
- Command transcript: bundle://proof/shared/transcripts/source-scan-no-stubs.txt
- Command transcript: bundle://proof/shared/transcripts/source-scan-source-family-order.txt
- Command transcript: bundle://proof/shared/transcripts/source-assertions.txt
- Passing transcript: bundle://proof/shared/transcripts/unit-projection-tests.txt
- Semantic positive proof: bundle://proof/shared/transcripts/integration-projection-tests.txt and bundle://proof/shared/transcripts/source-assertions.txt
- Anti-stub audit transcript: bundle://proof/shared/transcripts/source-scan-no-stubs.txt
- Failing-first transcript: N/A - no production behavior delta; process-only architecture refactor uses source assertions and guardrail scans instead of a behavior fixture.

## Closure Claim

- Focused projection facets are implemented by separate module-local classes created through `ProcessArtifactProjectionFacetFactory`.
- The dispatcher entry point passes only the claim-guard delegate needed by the focused facet set.
- The source-family order is preserved and checked by source assertion plus focused architecture tests.
- No Process Core project, driver API, UI file, broad projection host, all-facet implementation, or stub marker was introduced.
- Existing focused projection unit tests, focused projection integration tests, and the full solution build are green in the cited transcripts.

