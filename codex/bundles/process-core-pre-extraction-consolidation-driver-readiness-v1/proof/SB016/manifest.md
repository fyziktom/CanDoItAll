# SB016 Proof Manifest

## Summary

- Subbundle: `SB016 - Subprocess lifecycle input/read model stabilization`
- Result: `Completed`
- Production source changed: `No - existing branch implementation already satisfied the route-input split`
- Owned requirements: subprocess runtime consumes route-owned input/read models without dispatcher aliases.
- Semantic invariant contract: `bundle://proof/SB016/semantic-invariants.md`
- Browser validation: `N/A - runtime/service refactor only`

## Relevant File Hashes

- `f77ced35fbf3cca10089b2efbcb8808170d94c09cbff0fee653d70a2d820888f` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeModels.cs`
- `3332a1f082a6995e70b197e1020f454556f4c666cecd767be90a9b789a9dbd34` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`
- `3fdbf5714ed7dc824d01f4c1c7df67395e1ba5e98edfad2704c3dbfccaf64fb9` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteFacets.cs`
- `7f8f3ef2b89128bebaa1b1263154054385531fa15299f7abf8fab32aac44fc7c` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`
- `57e053ff4e04449e5370efd706da2a63de6884326584a346be019902f2c43bf7` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`
- `a77ed0a2f5314c1eed678b91159a5af0242fb47f3ce31645784ab62cf9f2624b` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`
- `c8521e9e3dd4d116485649d1d658fc8c189e5c779e25f8c1162a27507a742ceb` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPlanBuilder.cs`
- `3edf8dc48748a8cbcc62957ef77747b239b823a0e464af417ec10a2bfbdaff91` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionWriterCoordinator.cs`
- `48763d7a0f64f8e0739e9d53c561dc6f47de176a322652aafdc9c2de59d6dacd` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionGapJournalCoordinator.cs`
- `4ab78791efc42346faa6b6bae2a098e274a0d14a21a9a3247f502bc38882af93` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `e6772aa6f9343780a707297e515163bb556669f12b10dae1cf45bad6e33c9474` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts

- Build: `bundle://proof/SB016/transcripts/subprocess-runtime-input-build.txt`
- Architecture test: `bundle://proof/SB016/transcripts/subprocess-runtime-input-architecture-test.txt`
- Focused integration tests: `bundle://proof/SB016/transcripts/subprocess-runtime-focused-integration-tests.txt`
- Source assertions and anti-stub audit: `bundle://proof/SB016/transcripts/subprocess-runtime-source-assertions.txt`

## Source-Level Assertions

- `ProcessDispatchSubprocessRuntimeInput` carries `ProcessRouteCandidate` and `ProcessRouteDispatchClaim`.
- Subprocess runtime consumes route-owned input and avoids dispatcher aliases/adapters.
- Projection plan/writer/gap journal helpers consume route-owned subprocess runtime input.
- Route handler creates subprocess runtime input at the route boundary.

## Semantic Adequacy Gate

- Shallow-pass trap: subprocess runtime could accept a new wrapper while still leaking dispatcher aliases or adapter calls into runtime/projection code.
- Adversarial negative proof: the architecture guard fails if subprocess runtime regains dispatcher aliases, route adapter calls, or direct finalizer adapter ownership.
- Semantic positive proof: build, architecture guard, focused subprocess boundary test, and source assertions passed.
- Anti-stub audit: `bundle://proof/SB016/transcripts/subprocess-runtime-source-assertions.txt`

## Reopen Triggers

- Reopen `SB016` if subprocess runtime or projection helpers regain dispatcher aliases, route adapters, direct finalizer adapters, or forbidden Core/driver/UI/stub scans fail.
