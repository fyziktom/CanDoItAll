# SB14 Semantic Invariants

## Invariant SB14-INV-001

- Invariant ID: `SB14-INV-001`
- Source raw note: RN10 "add generic red-team coverage across software and non-software processes".
- Expected behavior: The final red-team harness must prove the runtime remains generic across software and non-software processes, blocks architecture/planning product mutation, permits explicit external artifact destinations, validates manual/API completion artifacts through real managed content, and routes own-output recovery separately from upstream-input waiting.
- Disallowed shallow implementation: Prompt-only change, source-assertion-only proof, tests that do not exercise production code paths, branch-specific hardcoding, software-only behavior in generic process runtime, or fragile text heuristics without typed contract state.
- Failing-first test: N/A for this process/non-production final closure harness; SB14 adds adversarial red-team coverage over existing runtime boundaries rather than shipping a new production behavior fix.
- Passing test: `bundle://proof/SB14/transcripts/passing.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`; `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs`; `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`; `repo://src/CanDoItAll.Modules.Processes/Development/ProcessDevelopmentSeedService.RuntimeSeeds.Complex.cs`; `repo://Templates/Processes/processes/software-delivery/definition.json`
- Production assertions: Architecture-only steps use `ManagedProcessArtifactsOnly` without `MutateProductTarget`; external artifact destinations require `WriteExternalArtifactDestination`; direct artifact validation rejects missing and placeholder records; recovery routing returns distinct own-output and upstream-input next actions; seeded and manual completion artifacts write managed files before validation.
- Red-team negative case: `bundle://proof/SB14/transcripts/red-team.txt` exercises architecture-only mutation denial, missing artifact rejection, placeholder-only artifact rejection, and recovery misrouting prevention.
- Downstream dependency check: Final closure has no downstream feature subbundle; the completed-stage validator and execution report close RN10 and preserve SB01-SB13 proof dependencies.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB14 generic red-team harness | repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs | bundle://proof/SB14/manifest.md | bundle://proof/SB14/transcripts/passing.txt | bundle://proof/SB14/transcripts/red-team.txt |
| SB14 managed artifact content validation | repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs | bundle://proof/SB14/manifest.md | bundle://proof/SB14/transcripts/final-integration.txt | bundle://proof/SB14/transcripts/source-assertions.txt |
