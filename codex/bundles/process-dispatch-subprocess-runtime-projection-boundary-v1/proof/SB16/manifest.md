# SB16 Proof Manifest

## Status

- Completed

## Objective

Artifact projection parity

## Changed File Hashes

- `c0d6b11ebf24be4ffe73cb763f2ff6bb3c398e44a39abe3d63d9ce7cb8380bb9`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `b2d02df96ec635706db00a3b0c120c039e98b5fa55be61f68df3ba51623be181`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs`
- `6c5b0632b0213dfb7520fa2fe82220570651c257ff97d46db0421d25c7fbf868`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessArtifactSourceResolver.cs`
- `d0a2af212a7ca31eeee1e97a57cae9a24e2e0dc5af19a689b8135fcb4f3513ac`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessCapabilityGapInspector.cs`
- `48d025e416404cf4ffc5ba831955ec51bd0e52c76c414fc197445ee3992af451`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessLifecycleRules.cs`
- `4f74479e22ff375520c6f7abaedcf1411e93be8b311d81a1310905bb9d1bc882`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionGapJournalCoordinator.cs`
- `d4f7179ac8c787045dd063ebcaa9ae963dcc27b39594cc827d7caaf8d6462426`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPlanBuilder.cs`
- `b1df7a30618a14a0cedbd0e23a60bc09c5e8c76c91d7c84e7272281a95ff9d7d`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionWriterCoordinator.cs`
- `e6e1b1182f4e182dd0df8ae58b7133581fb0ada2f55975b7aa41c0fd620aa80d`  `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessRunObservationCoordinator.cs`
- `be797d20a00cde51be2d78ab996e625ca29d48bda18139f4b2ea8578dadafbf0`  `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Artifact References

- Source proof: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` and module-local subprocess helper files listed above.
- Semantic invariants: `bundle://proof/SB16/semantic-invariants.md`.
- Build transcript: `bundle://proof/SB16/transcripts/build.txt`.
- Passing transcript: `bundle://proof/SB16/transcripts/focused-tests.txt`.
- Semantic positive proof transcript: `bundle://proof/SB16/transcripts/focused-tests.txt`.
- Source scan transcript: `bundle://proof/SB16/transcripts/source-scan.txt`.
- Anti-stub audit transcript: `bundle://proof/SB16/transcripts/anti-stub.txt`.
- Failing-first proof: N/A - process refactor with no production behavior expansion; parity is guarded by source scans and focused tests instead of a behavior-changing red test.
- Adversarial negative proof: N/A - process refactor with no production behavior expansion; prohibited Core/driver/UI/stub scans stayed clean.

## Cited Tests

- Test name: `ProcessSubprocessLifecycleRules_SB05_INV_001_preserves_transition_field_parity`
- Test name: `ProcessSubprocessCapabilityGapInspector_SB09_INV_001_formats_unassigned_gap_steps`
- Test name: `ProcessSubprocessBoundary_SB18_INV_001_dispatch_delegates_runtime_projection_side_effects`
- Test name: `WorkflowSubprocessArtifactMapper_SB11_INV_001_resolves_explicit_mappings_without_dispatch_partials`
- Test name: `SubprocessArtifactProjectionMapping_SB09_INV_001_uses_child_expectation_id_when_same_kind_titles_conflict`
- Test name: `SubprocessArtifactProjectionMapping_SB09_INV_001_blocks_same_kind_heuristic_without_child_mapping`
- Test name: `SubprocessArtifactProjectionMapping_SB09_INV_001_warns_when_legacy_same_kind_fallback_maps`