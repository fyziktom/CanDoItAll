# SB018 Proof Manifest

## Scope
- Subbundle: SB018 - Gate F - subprocess lifecycle/projection parity.
- Changed source: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPersistenceService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessLifecycleRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPlanBuilder.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionWriterCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionGapJournalCoordinator.cs`.
- Source inspected: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessRunObservationCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessCapabilityGapInspector.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerInputs.cs`, and the changed source files above.
- Changed tests: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Bundle status files: `bundle://inventories/02-source-hotspots.md`, `bundle://reviews/01-execution-report.md`, `bundle://subbundles/SB018/README.md`, `bundle://proof/SB018/semantic-invariants.md`.

## Changed File Hashes
| Path | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeModels.cs` | `F77CED35FBF3CCA10089B2EFBCB8808170D94C09CBFF0FEE653D70A2D820888F` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs` | `3332A1F082A6995E70B197E1020F454556F4C666CECD767BE90A9B789A9DBD34` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPersistenceService.cs` | `4645FC12C88ADBF7714E5C555E2DD66BE799F7CBC0AB9A238E0567BB354D7785` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` | `AD31B3F9531CE494AB64722779A3D2F58066E7049A2639B22104A6550E9BE0EF` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessLifecycleRules.cs` | `3110E8BE55E092148137698BF78490D62243B2B34BD84C18A808D79A6E212524` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPlanBuilder.cs` | `C8521E9E3DD4D116485649D1D658FC8C189E5C779E25F8C1162A27507A742CEB` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionWriterCoordinator.cs` | `3EDF8DC48748A8CBCC62957EF77747B239B823A0E464AF417EC10A2BFBDAFF91` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionGapJournalCoordinator.cs` | `48763D7A0F64F8E0739E9D53C561DC6F47DE176A322652AAFDC9C2DE59D6DACD` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | `A3B5DDE284A2951E5F7E9C7BA05DDDE50F768A3A1EFEF66C870A89E00FBC76DA` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `992DDBE56A8A80943C00B3472AFFA8D0030BB55B685130E2F39A7EFB8379533E` |
| `bundle://inventories/02-source-hotspots.md` | `D89A9CC64107CB7BABB3D716CC18FE24EF6AE0AFB71FD215E9F564E4270BBF3B` |
| `bundle://reviews/01-execution-report.md` | `CBB7625121F5DCD9BCEC58E7AA905A8AEC3B42D836D9AAB9D18E8B649887397D` |
| `bundle://subbundles/SB018/README.md` | `3888114C0D542E6D5BD40FB4E47CD7B1D9423DB3FC298255E8606F0C573DE1AE` |
| `bundle://proof/SB018/semantic-invariants.md` | `7BFEF122208654296062B7BCCE23AF73312E676EDEF92CAC1757861E9B28D30B` |

## Command Transcripts
- Critical solution build: `bundle://proof/SB018/transcripts/critical-build.txt`
- Passing focused unit architecture proof: `bundle://proof/SB018/transcripts/subprocess-boundary-unit-tests.txt`
- Passing focused integration behavior proof: `bundle://proof/SB018/transcripts/subprocess-lifecycle-projection-integration-tests.txt`
- Source assertions: `bundle://proof/SB018/transcripts/source-assertions-and-scans.txt`
- Anti-stub audit: `bundle://proof/SB018/transcripts/source-assertions-and-scans.txt`
- No-Core/no-driver scan: `bundle://proof/SB018/transcripts/source-assertions-and-scans.txt`
- Hash proof: `bundle://proof/SB018/transcripts/changed-file-hashes.txt`
- Failing-first proof: N/A - process refactor with no behavior change; behavioral negative proof is captured by `ProcessSubprocessBoundary_SB18_INV_001_dispatch_delegates_runtime_projection_side_effects`, subprocess projection mapping tests, and source negative proof in `bundle://proof/SB018/transcripts/source-assertions-and-scans.txt`.

## Semantic Invariants
- Contract: `bundle://proof/SB018/semantic-invariants.md`
- Invariant ID: `SB018-INV-001`
- Test name: `Process_core_contract_candidate_driver_readiness_SB016_INV_001_moves_subprocess_runtime_to_route_input_model`
- Test name: `Process_core_contract_candidate_driver_readiness_SB017_INV_001_extracts_subprocess_projection_persistence`
- Test name: `ProcessSubprocessLifecycleRules_SB05_INV_001_preserves_transition_field_parity`
- Test name: `ProcessSubprocessCapabilityGapInspector_SB09_INV_001_formats_unassigned_gap_steps`
- Test name: `ProcessSubprocessBoundary_SB18_INV_001_dispatch_delegates_runtime_projection_side_effects`
- Test name: `WorkflowSubprocessArtifactMapper_SB11_INV_001_resolves_explicit_mappings_without_dispatch_partials`
- Test name: `SubprocessArtifactProjectionMapping_SB09_INV_001_uses_child_expectation_id_when_same_kind_titles_conflict`
- Test name: `SubprocessArtifactProjectionMapping_SB09_INV_001_blocks_same_kind_heuristic_without_child_mapping`
- Test name: `SubprocessArtifactProjectionMapping_SB09_INV_001_warns_when_legacy_same_kind_fallback_maps`
- Test name: `ProcessDispatchFinalizerAdapter_SB009_INV_001_preserves_route_dto_context_parity_and_apply_conditions`
- Test name: `ArtifactContractValidation_accepts_subprocess_artifact_with_current_child_lineage`

## Source Assertions
- `ProcessDispatchSubprocessRuntimeInput` carries route candidate, trigger metadata, and route dispatch claim into subprocess runtime without dispatcher candidate aliases.
- `ProcessDispatchSubprocessRuntimeService` still observes or starts child runs, builds lifecycle start/block/terminal mirror transitions, handles capability-gap blocks, and finalizes completed subprocess parents through route-owned subprocess finalizer input.
- `ProcessSubprocessProjectionPersistenceService` owns completed-child projection database queries, claim-renewal checks, source artifact resolution, gap journal recording, projection plan building, writer coordination, and the single `SaveChangesAsync` call.
- `ProcessSubprocessProjectionPlanBuilder`, `ProcessSubprocessProjectionWriterCoordinator`, and `ProcessSubprocessProjectionGapJournalCoordinator` still preserve child expectation selection, lineage-sensitive projection mapping, workspace writes, and duplicate gap-journal protection.
- Source scans found no completed-projection EF/write side effects in subprocess runtime, no subprocess projection side effects in dispatch partials, no Process Core project, no production process driver API, no stubs, and no UI/mobile proof drift beyond N/A runtime-service documentation.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumers | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `ProcessDispatchSubprocessRuntimeInput` | `SubprocessRouteHandler` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs` | `ProcessDispatchSubprocessRuntimeService` and `ProcessSubprocessProjectionPersistenceService` | Built from the current route candidate and route claim for one subprocess dispatch path; not persisted or published. | `Process_core_contract_candidate_driver_readiness_SB016_INV_001_moves_subprocess_runtime_to_route_input_model` and `Process_core_contract_candidate_driver_readiness_SB017_INV_001_extracts_subprocess_projection_persistence` in `bundle://proof/SB018/transcripts/subprocess-boundary-unit-tests.txt`. |
| `ProcessSubprocessProjectionPersistenceService` | `CreateSubprocessRuntimeService` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` | `ProcessDispatchSubprocessRuntimeService` | Instantiated with existing EF, workspace, profile, clock, and route-claim guard dependencies for runtime use; owns completed-child projection query/write/save side effects. | `ProcessSubprocessBoundary_SB18_INV_001_dispatch_delegates_runtime_projection_side_effects` and source negative scans in `bundle://proof/SB018/transcripts/source-assertions-and-scans.txt`. |

## Gate Result
- Entry gate: Passed after SB017 closure.
- Closure gate: Passed with critical build, focused unit architecture proof, focused integration behavior proof, source assertions, anti-stub audit, no-Core/no-driver scan, hash proof, semantic invariant contract, and production behavior artifact matrix.
- Downstream dependency check: SB019-SB033 may proceed only while subprocess runtime input, projection persistence extraction, lifecycle rules, projection mapping, gap journaling, and subprocess finalizer context remain guarded before direct-agent execution, projection/validation DTO, pure-rule, driver-readiness, and final red-team phases continue.
