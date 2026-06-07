# SB022 Semantic Invariants

## Invariants

- Invariant ID: `SB022-INV-001`
- Source raw note: `Remove remaining duplicate expectation/projection/validation DTO conversions.`
- Expected behavior: Projection run data is represented by a slim run snapshot; execution-detail-derived observation facts are represented separately and consumed by projection context/facets/coordinators.
- Disallowed shallow implementation: Keeping `ProcessAutomationExecutionRunDetail` or `.Detail` access in projection context, facets, workspace projection, or provider-native browser projection while adding new DTO names.
- Failing-first test: `N/A - no production behavior change was intended; this subbundle validates an existing behavior-preserving projection observation boundary.`
- Passing test: `bundle://proof/SB022/transcripts/projection-observation-architecture-test.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProjectionModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionContext.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacets.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacetImplementations.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkspaceWrittenArtifactProjectionCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB022/transcripts/projection-observation-source-assertions.txt`
- Red-team negative case: Reintroducing `ProcessAutomationExecutionRunDetail` in `ProcessArtifactProjectionContext` or making provider-native browser projection depend on `IProcessProjectionSessionObservationSource` fails SB022 guards.
- Downstream dependency check: `SB023` may group matcher/satisfaction rules because projection observations are separated from full execution detail.

## Raw Note Closure

- Projection observation convergence: `Solved for SB022 with slim run snapshots and explicit observation snapshots.`
- Preserve artifact projection behavior: `Partially proved here; SB024 owns critical artifact parity.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
