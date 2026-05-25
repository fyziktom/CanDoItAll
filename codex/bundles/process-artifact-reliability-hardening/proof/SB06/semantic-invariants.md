# SB06 Semantic Invariants

## Status

Completed.

## Invariants

- Invariant ID: `SB06-INV-001`
- Source raw note: N003, N006, and N007 require PostgreSQL-only validation, red-team regression coverage, and no SQLite residue.
- Expected behavior: The hardened process runtime passes focused dispatch integration tests, builds the solution, records PostgreSQL model scope, and proves no SQLite references were introduced.
- Disallowed shallow implementation: Closing with only source edits and no command transcripts.
- Failing-first test: N/A process validation subbundle; SB01-SB03 contain failing-first source assertions for the changed behavior.
- Passing test: `ProcessRunAutomationDispatchServiceTests` class run in `bundle://proof/SB06/transcripts/focused-integration-tests.txt` and full build in `bundle://proof/SB06/transcripts/solution-build.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs`, and `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: Focused process dispatch tests, full solution build, PostgreSQL model audit, and SQLite residue audit are recorded in `bundle://proof/SB06/transcripts/`.
- Red-team negative case: Wrong producer, placeholder, missing artifact, and generic lead manager recovery cases are included in the focused integration transcript.
- Downstream dependency check: Final closure validator runs after all proof manifests and execution report updates.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| Process runtime validation suite | `dotnet test` in `bundle://proof/SB06/transcripts/focused-integration-tests.txt` | Bundle closure report | Runs after all SB01-SB05 source changes; proof is the transcript exit code | Red-team test names are included in transcript headers |
| PostgreSQL-only validation audit | PostgreSQL model and SQLite residue transcript commands | Bundle closure report | Runs after source edits and before completed validator | SQLite residue audit proves no introduced SQLite references |

## Red-Team Negative Cases

- Response text as runtime evidence is rejected.
- Placeholder required artifact records are rejected.
- Missing required artifacts remain unsatisfied.
- Generic lead recovery fallback is rejected.
