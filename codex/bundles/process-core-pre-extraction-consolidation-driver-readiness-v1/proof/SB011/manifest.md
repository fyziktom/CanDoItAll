# SB011 Proof Manifest

## Summary

- Subbundle: `SB011 - Direct-agent binding/recovery/cooperation collaborator split`
- Result: `Completed`
- Production source changed: `No - existing branch implementation already satisfied the split`
- Owned requirements: direct-agent binding, project-structure access mutation, recoverable execution id resolution, manual recovery directive readback, and cooperation metadata are explicit collaborators.
- Semantic invariant contract: `bundle://proof/SB011/semantic-invariants.md`
- Browser validation: `N/A - runtime/service refactor only`

## Relevant File Hashes

- `8d48cb5cd099538b8d9ec4cbcbc8b0843f651b097b1bfe119270bc8a81d60284` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`
- `80e7a3062ec5f036badcd31a5630439d8b4b923ddaf849bf1c26e4607d90f4b9` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchHydratedCandidateAssembler.cs`
- `0ecaef5303a4e5a51e96ac9dc8459bcbafe92212fabad675dbb121af2ac400cb` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentCandidateAssembler.cs`
- `6bd64908e1051c650690570d50abee3841a897b5d6ad0598c495dee69a8f10ae` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchTechnicalAgentBindingCoordinator.cs`
- `247cffb6be05cb20eaf6851909bb812377714d582aac5485bcaa0c795fa519f1` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRecoveryQueryHelper.cs`
- `09f8bbdc6b2e375c2bb3ada0ab0d79ffc1870ffb665c4941ef03177c4b8ebc70` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCooperationMetadataResolver.cs`
- `4ab78791efc42346faa6b6bae2a098e274a0d14a21a9a3247f502bc38882af93` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `e6772aa6f9343780a707297e515163bb556669f12b10dae1cf45bad6e33c9474` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts

- Build: `bundle://proof/SB011/transcripts/direct-agent-collaborator-split-build.txt`
- Architecture test: `bundle://proof/SB011/transcripts/direct-agent-collaborator-architecture-test.txt`
- Focused integration tests: `bundle://proof/SB011/transcripts/direct-agent-collaborator-focused-integration-tests.txt`
- Source assertions and anti-stub audit: `bundle://proof/SB011/transcripts/direct-agent-collaborator-source-assertions.txt`

## Source-Level Assertions

- `ProcessDispatchDirectAgentCandidateAssembler` coordinates direct-agent binding, recovery id, manual recovery directive, cooperation metadata, and direct-agent candidate creation.
- `ProcessDispatchCandidateHydrationService` and `ProcessDispatchHydratedCandidateAssembler` do not call binding, recovery id, or cooperation metadata helpers.
- `ProcessDispatchTechnicalAgentBindingCoordinator` owns agent lookup/save and project-structure read access mutation.
- `ProcessDispatchRecoveryQueryHelper` owns read-only recovery queries and does not save changes.
- `ProcessDispatchCooperationMetadataResolver` owns cooperation mode and workspace tool profile selection.

## Semantic Adequacy Gate

- Shallow-pass trap: collaborator classes could exist while hydration or generic assembly still performs binding, recovery, or cooperation work.
- Adversarial negative proof: the architecture guard fails if binding/recovery/cooperation calls move back into hydration service or hydrated candidate assembly.
- Semantic positive proof: build, architecture guard, direct-agent candidate facts, project-structure access mutation, and source assertions passed.
- Anti-stub audit: `bundle://proof/SB011/transcripts/direct-agent-collaborator-source-assertions.txt`

## Reopen Triggers

- Reopen `SB011` if hydration service or hydrated assembler regains direct-agent binding, recovery id, manual directive, or cooperation metadata work; if binding mutates agent access outside its coordinator; or if forbidden Core/driver/UI/stub scans fail.
