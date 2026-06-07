# SB012 Proof Manifest

## Summary

- Subbundle: `SB012 - Gate D hydration parity`
- Result: `Completed`
- Production source changed: `No - critical gate proof only after SB010/SB011`
- Owned requirements: subprocess/workflow/direct-agent candidate defaults, project-structure access mutation, recovery ids, cooperation metadata, no Process Core, no production process-driver API, no UI/mobile drift.
- Semantic invariant contract: `bundle://proof/SB012/semantic-invariants.md`
- Browser validation: `N/A - runtime/service refactor only`

## Relevant File Hashes

- `8d48cb5cd099538b8d9ec4cbcbc8b0843f651b097b1bfe119270bc8a81d60284` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`
- `c680abd24af3404d0cd85ec39749184ab873993dba61601a13c2f7dd7c63222b` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationLoader.cs`
- `5c953a29412ed9c3a99c398529be37f47787641c9fd452b1d314d843da431507` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateArtifactInputPreparationService.cs`
- `80e7a3062ec5f036badcd31a5630439d8b4b923ddaf849bf1c26e4607d90f4b9` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchHydratedCandidateAssembler.cs`
- `0ecaef5303a4e5a51e96ac9dc8459bcbafe92212fabad675dbb121af2ac400cb` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentCandidateAssembler.cs`
- `6bd64908e1051c650690570d50abee3841a897b5d6ad0598c495dee69a8f10ae` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchTechnicalAgentBindingCoordinator.cs`
- `247cffb6be05cb20eaf6851909bb812377714d582aac5485bcaa0c795fa519f1` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRecoveryQueryHelper.cs`
- `09f8bbdc6b2e375c2bb3ada0ab0d79ffc1870ffb665c4941ef03177c4b8ebc70` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCooperationMetadataResolver.cs`
- `d76d58ff1cea5fe3c6f464d753ac98e9e7a5e3f99f85cc3643303151882cf8e6` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs`
- `4ab78791efc42346faa6b6bae2a098e274a0d14a21a9a3247f502bc38882af93` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `e6772aa6f9343780a707297e515163bb556669f12b10dae1cf45bad6e33c9474` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts

- Critical build: `bundle://proof/SB012/transcripts/critical-build.txt`
- Focused architecture tests: `bundle://proof/SB012/transcripts/focused-architecture-tests.txt`
- Hydration parity focused integration tests: `bundle://proof/SB012/transcripts/hydration-parity-focused-integration-tests.txt`
- Source assertions and anti-stub audit: `bundle://proof/SB012/transcripts/source-assertions-and-scans.txt`

## Source-Level Assertions

- Hydration service remains orchestration-only for loader, artifact-input preparation, direct-agent assembler, and hydrated candidate assembler.
- Loader remains no-tracking EF readback and avoids mutation.
- Artifact preparation remains separate from `AppDbContext`.
- Hydrated assembler preserves subprocess/workflow/direct-agent routing and delegates direct-agent side-effect facts.
- Direct-agent assembler preserves binding, recovery id, manual recovery, and cooperation metadata ownership.
- Candidate factory keeps subprocess, workflow, and direct-agent default/fact contracts.

## Semantic Adequacy Gate

- Shallow-pass trap: hydration code could compile after file movement while losing candidate defaults, project-structure access mutation, recoverable execution ids, or cooperation profile selection.
- Adversarial negative proof: architecture guards and focused integration tests fail if hydration service reclaims side effects, if candidate factory defaults drift, if project access is not mutated, or if recoverable ids/cooperation profiles change.
- Semantic positive proof: build, full process-boundary architecture tests, 15 focused hydration parity integration tests, and source assertions passed.
- Anti-stub audit: `bundle://proof/SB012/transcripts/source-assertions-and-scans.txt`

## Reopen Triggers

- Reopen `SB012` if subprocess/workflow/default direct-agent candidate behavior changes, project-structure access mutation changes, recoverable execution id selection changes, cooperation metadata profile selection changes, hydration side-effect ownership regresses, or forbidden Core/driver/UI/stub scans fail.
