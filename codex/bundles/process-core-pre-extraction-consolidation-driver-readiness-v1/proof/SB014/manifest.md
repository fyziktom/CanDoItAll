# SB014 Proof Manifest

## Summary

- Subbundle: `SB014 - Upstream materialization facts/rules vs journal/rerun side effects`
- Result: `Completed`
- Production source changed: `No - existing branch implementation already satisfied the split`
- Owned requirements: upstream materialization facts, blocker, fingerprint, and rerun directive remain pure; journal persistence and rerun requests remain application-local side effects.
- Semantic invariant contract: `bundle://proof/SB014/semantic-invariants.md`
- Browser validation: `N/A - runtime/service refactor only`

## Relevant File Hashes

- `85948b01d66ab4790211563ece290c3761cb551a1c5e8785906f25cbef6f9948` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs`
- `227987736dc4e0885c57fa85a3aa1577af4717b53ca50e7341c1a7ba7a4b1e18` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterializationSideEffects.cs`
- `31f38e82a8338ba8a097021c6473755b5eac0da51667431e280fbcfb390646b6` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionGuardHandler.cs`
- `57e053ff4e04449e5370efd706da2a63de6884326584a346be019902f2c43bf7` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`
- `4ab78791efc42346faa6b6bae2a098e274a0d14a21a9a3247f502bc38882af93` `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `e6772aa6f9343780a707297e515163bb556669f12b10dae1cf45bad6e33c9474` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts

- Build: `bundle://proof/SB014/transcripts/upstream-materialization-build.txt`
- Architecture test: `bundle://proof/SB014/transcripts/upstream-materialization-architecture-test.txt`
- Focused integration tests: `bundle://proof/SB014/transcripts/upstream-materialization-focused-integration-tests.txt`
- Source assertions and anti-stub audit: `bundle://proof/SB014/transcripts/upstream-materialization-source-assertions.txt`

## Source-Level Assertions

- `ProcessMissingUpstreamArtifactMaterialization.cs` owns pure facts, target selection, block reason/request, fingerprint, and rerun directive building.
- Pure materialization rules avoid EF context, persistence, service scopes, and rerun execution.
- `ProcessMissingUpstreamArtifactMaterializationSideEffects.cs` owns journal persistence and `ProcessesService.RerunAgentStepAsync`.
- Side-effect code reuses the pure rerun request builder.

## Semantic Adequacy Gate

- Shallow-pass trap: a split that leaves journal writes or rerun execution in pure facts/rules would still couple Core candidates to application side effects.
- Adversarial negative proof: architecture/source assertions fail if pure materialization rules regain EF/service-scope side effects or if side effects stop using the pure request builder.
- Semantic positive proof: build, architecture test, facts/fingerprint/rerun directive integration tests, and source assertions passed.
- Anti-stub audit: `bundle://proof/SB014/transcripts/upstream-materialization-source-assertions.txt`

## Reopen Triggers

- Reopen `SB014` if pure materialization code gains EF/service-scope side effects, journal/rerun code stops using pure builders, materialization target/fingerprint/directive behavior changes, or forbidden Core/driver/UI/stub scans fail.
