# SB017 Proof Manifest

## Summary

- Subbundle: `SB017 - Subprocess projection persistence service boundary`
- Result: `Completed`
- Production source changed: `No - existing branch implementation already satisfied the boundary`
- Owned requirements: child-artifact query, gap journal, parent artifact write, and save changes are separated into projection persistence and helper coordinators.
- Semantic invariant contract: `bundle://proof/SB017/semantic-invariants.md`
- Browser validation: `N/A - runtime/service refactor only`

## Relevant File Hashes

- `3332a1f082a6995e70b197e1020f454556f4c666cecd767be90a9b789a9dbd34` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`
- `4645fc12c88adbf7714e5c555e2dd66be799f7cbc0ab9a238e0567bb354d7785` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPersistenceService.cs`
- `c8521e9e3dd4d116485649d1d658fc8c189e5c779e25f8c1162a27507a742ceb` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPlanBuilder.cs`
- `3edf8dc48748a8cbcc62957ef77747b239b823a0e464af417ec10a2bfbdaff91` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionWriterCoordinator.cs`
- `48763d7a0f64f8e0739e9d53c561dc6f47de176a322652aafdc9c2de59d6dacd` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionGapJournalCoordinator.cs`
- `a77ed0a2f5314c1eed678b91159a5af0242fb47f3ce31645784ab62cf9f2624b` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`
- `4ab78791efc42346faa6b6bae2a098e274a0d14a21a9a3247f502bc38882af93` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `e6772aa6f9343780a707297e515163bb556669f12b10dae1cf45bad6e33c9474` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts

- Build: `bundle://proof/SB017/transcripts/subprocess-projection-persistence-build.txt`
- Architecture test: `bundle://proof/SB017/transcripts/subprocess-projection-persistence-architecture-test.txt`
- Focused integration tests: `bundle://proof/SB017/transcripts/subprocess-projection-focused-integration-tests.txt`
- Source assertions and anti-stub audit: `bundle://proof/SB017/transcripts/subprocess-projection-persistence-source-assertions.txt`

## Source-Level Assertions

- Runtime delegates completed-artifact projection to `ProcessSubprocessProjectionPersistenceService`.
- Projection persistence service owns EF context creation, expectation/artifact queries, gap journal, writer, plan builder, claim renewal, and save changes.
- Gap journal, writer, and plan builder consume route-owned subprocess runtime input.
- Runtime avoids projection builder/writer and persistence side effects.

## Semantic Adequacy Gate

- Shallow-pass trap: projection helpers could exist while runtime still owns EF queries, writer calls, or save changes.
- Adversarial negative proof: the architecture test fails if runtime regains projection plan building, writer calls, EF context creation, save changes, or projection helper dependencies.
- Semantic positive proof: build, architecture guard, focused subprocess boundary test, and source assertions passed.
- Anti-stub audit: `bundle://proof/SB017/transcripts/subprocess-projection-persistence-source-assertions.txt`

## Reopen Triggers

- Reopen `SB017` if subprocess runtime regains child-artifact query, gap journal, parent artifact write, save changes, route adapter leaks, or forbidden Core/driver/UI/stub scans fail.
