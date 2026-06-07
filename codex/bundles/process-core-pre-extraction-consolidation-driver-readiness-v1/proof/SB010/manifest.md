# SB010 Proof Manifest

## Summary

- Subbundle: `SB010 - Hydration query and artifact-input readback service`
- Result: `Completed`
- Production source changed: `No - existing branch implementation already satisfied the split`
- Owned requirements: EF readback separated from candidate assembly, artifact-input preparation separated from candidate assembly, no Process Core, no production process-driver API, no UI/mobile drift.
- Semantic invariant contract: `bundle://proof/SB010/semantic-invariants.md`
- Browser validation: `N/A - runtime/service refactor only`

## Relevant File Hashes

- `8d48cb5cd099538b8d9ec4cbcbc8b0843f651b097b1bfe119270bc8a81d60284` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`
- `c680abd24af3404d0cd85ec39749184ab873993dba61601a13c2f7dd7c63222b` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationLoader.cs`
- `5c953a29412ed9c3a99c398529be37f47787641c9fd452b1d314d843da431507` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateArtifactInputPreparationService.cs`
- `80e7a3062ec5f036badcd31a5630439d8b4b923ddaf849bf1c26e4607d90f4b9` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchHydratedCandidateAssembler.cs`
- `0ecaef5303a4e5a51e96ac9dc8459bcbafe92212fabad675dbb121af2ac400cb` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentCandidateAssembler.cs`
- `4ab78791efc42346faa6b6bae2a098e274a0d14a21a9a3247f502bc38882af93` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `e6772aa6f9343780a707297e515163bb556669f12b10dae1cf45bad6e33c9474` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts

- Build: `bundle://proof/SB010/transcripts/hydration-split-build.txt`
- Architecture test: `bundle://proof/SB010/transcripts/hydration-split-architecture-test.txt`
- Focused integration tests: `bundle://proof/SB010/transcripts/candidate-factory-focused-integration-tests.txt`
- Source assertions and anti-stub audit: `bundle://proof/SB010/transcripts/hydration-split-source-assertions.txt`

## Source-Level Assertions

- `ProcessDispatchCandidateHydrationService` orchestrates loader, artifact-input preparation, direct-agent assembler, and hydrated candidate assembler creation without owning the dispatchable-step loop.
- `ProcessDispatchCandidateHydrationLoader` owns EF readback through no-tracking queries and avoids persistence or agent-side effects.
- `ProcessDispatchCandidateArtifactInputPreparationService` owns resolved artifact-input and prompt-path preparation without depending on `AppDbContext`.
- `ProcessDispatchHydratedCandidateAssembler` owns candidate assembly and delegates direct-agent side-effect work to `ProcessDispatchDirectAgentCandidateAssembler`.

## Semantic Adequacy Gate

- Shallow-pass trap: moving code into files without changing ownership would still leave EF readback, artifact-input path preparation, and candidate assembly coupled.
- Adversarial negative proof: the architecture test fails if hydration service regains dispatchable-step assembly, artifact-input assembly, direct-agent binding, or recovery query ownership.
- Semantic positive proof: build, architecture guard, candidate factory defaults, and source assertions passed.
- Anti-stub audit: `bundle://proof/SB010/transcripts/hydration-split-source-assertions.txt`

## Reopen Triggers

- Reopen `SB010` if hydration service regains EF query details beyond loader orchestration, artifact-input assembly returns to candidate assembly, loader starts mutating state, direct-agent side effects leak into the hydrated assembler, or forbidden Core/driver/UI/stub scans fail.
