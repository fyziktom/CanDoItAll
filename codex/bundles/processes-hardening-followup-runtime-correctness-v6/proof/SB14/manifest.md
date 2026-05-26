# SB14 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`: SB14 red-team harness covers architecture-only software planning, business-plan external artifact destination, legal approval, manufacturing QA, incident response, workflow-backed role output, subprocess parent output, manager recovery, placeholder rejection, missing artifact rejection, and typed recovery routing.
- `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs`: manual business-plan step completion writes real managed artifact files before recording `ManagedStoragePath`.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`: workflow repair completion helpers write managed artifact content before manual/API transitions.
- `repo://src/CanDoItAll.Modules.Processes/Development/ProcessDevelopmentSeedService.cs`: development seeding receives `IWorkspacePathResolver` so seed artifacts can be materialized through the workspace storage boundary.
- `repo://src/CanDoItAll.Modules.Processes/Development/ProcessDevelopmentSeedService.RuntimeSeeds.Complex.cs`: complex runtime seeds write deterministic markdown/SVG managed artifact files before storing artifact metadata.
- `repo://Templates/Processes/processes/software-delivery/definition.json`: strict operation metadata and artifact-recovery exception summaries cover implementation and QA validation steps.
- Source assertion transcript: `bundle://proof/SB14/transcripts/source-assertions.txt`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB14 verified runtime behavior | repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs | bundle://proof/SB14/manifest.md | bundle://proof/SB14/transcripts/passing.txt | bundle://proof/SB14/transcripts/red-team.txt |
## Semantic Invariant Contract

- `bundle://proof/SB14/semantic-invariants.md`

## Failing-First or Red-Team Proof

- Failing-first: N/A for this process/non-production final closure harness; SB14 adds adversarial red-team coverage over existing runtime boundaries rather than shipping a new production behavior fix.
- Red-team transcript: `bundle://proof/SB14/transcripts/red-team.txt`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessRedTeamScenarioHarness_SB14_INV_001_blocks_architecture_mutation_and_allows_external_destination`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessRedTeamScenarioHarness_SB14_INV_001_validates_generic_artifact_producers_and_recovery_actions`

## Passing Proof

- Transcript: `bundle://proof/SB14/transcripts/passing.txt`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessRedTeamScenarioHarness_SB14_INV_001_blocks_architecture_mutation_and_allows_external_destination`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessRedTeamScenarioHarness_SB14_INV_001_validates_generic_artifact_producers_and_recovery_actions`

## Anti-Stub Audit

- Transcript: `bundle://proof/SB14/transcripts/anti-stub-audit.txt`
- No production stubs, fake implementations, unresolved `NotImplementedException`, or unresolved proof placeholders were introduced by SB14.

## Changed-File Hashes

- Transcript: `bundle://proof/SB14/transcripts/changed-file-hashes.txt`
- `D4C1C46BE7E3CE55C42D08D2BBF65753F555462B1E352D4049EECD8DCA4A1A2A` `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `DE70499461691FFA2E30598D06896FD73FBBB9FDF58E1D9DCDD30C7A49BC654C` `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs`
- `2A2C301A4F1280C722E5921F50C2697577F59EAAB53C49608BE6ED313BFF5FB7` `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- `E7CBE989CDF1E6C8CD5B92C0E6406AC496AD98C954C44D011DFCDC391020F78A` `repo://src/CanDoItAll.Modules.Processes/Development/ProcessDevelopmentSeedService.cs`
- `3C709E81701DC372BA9A6CF7CD89BC2A2A6352A32B1189E47D417EEE3B410BCF` `repo://src/CanDoItAll.Modules.Processes/Development/ProcessDevelopmentSeedService.RuntimeSeeds.Complex.cs`
- `759D5D5425EC4669023938CEA73F2195099C091D4E3C6669675C78E8739951DC` `repo://Templates/Processes/processes/software-delivery/definition.json`

## Validation

- Focused red-team integration tests passed: `bundle://proof/SB14/transcripts/red-team.txt`.
- Final focused unit tests passed: `bundle://proof/SB14/transcripts/final-unit.txt`.
- Final focused integration tests passed: `bundle://proof/SB14/transcripts/final-integration.txt`.
- Final component proof passed: `bundle://proof/SB14/transcripts/final-components.txt`.
- Solution build passed with known EF Core version-conflict warnings and no errors: `bundle://proof/SB14/transcripts/final-build.txt`.
- PostgreSQL-only audit found only existing retired/legacy quarantine strings and bundle prohibition text; no `UseSqlite`, SQLite migration, or provider-switching runtime path was added: `bundle://proof/SB14/transcripts/postgresql-only-audit.txt`.
- Completed-stage bundle validator passed: `bundle://proof/SB14/transcripts/completed-validator.txt`.

## Blockers

None.


