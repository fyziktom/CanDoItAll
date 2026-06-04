# SB01 Proof Manifest

Status: Completed.
Objective: Entry audit, artifact boundary baseline, previous smoke proof.
Critical foundation: False.

## Changed Files And Hashes

- Changed-file hash index: bundle://proof/SB12/source-assertions/changed-file-hashes.txt
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactExpectationMatcher.cs sha256:7f1caffa3d957533543645f1157cc5a6b3809bbf218f10d28161fbda515e6035
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionExpectation.cs sha256:05b4b31c4405a5b9d52a12517bab6460e0d26e16d0380d16b655653ba45e9f6e
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs sha256:848e4dc7335764289d0a4d257a61b437647da929c493c7b123b82f654a7110fa
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs sha256:af5f320b093c9582896e1fd4a1bf898844d432c8bed459d5894bc40b17174ffc
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs sha256:7a8266db8e1b6dd922d9c188bca39558da1e37dc5f26d921378b2a1d1aa9f316
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs sha256:af1e6160ca7baa70dbc9629412c99338896f928abed148ab04a9de683c14d7a5
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs sha256:425352291f662d770e4e6906d5795af2b4bc28dfac6d701930ff85db1af950bf
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs sha256:a3ff822c4bb0a77eca12130fb73e41708dc28d37b8bc3f2c5dd9e2770a49a160
- repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs sha256:2c8ca8c7f913760917f3802143176c16dad0f663de0cc6cbdd4a403e90d25bfe

## Semantic Invariant Contract

- Contract: bundle://proof/SB01/semantic-invariants.md

## Command Transcripts

- passing transcript: bundle://proof/SB03/transcripts/focused-unit-architecture.txt
- passing transcript: bundle://proof/SB05/transcripts/focused-integration-projection-slice.txt
- passing transcript: bundle://proof/SB11/transcripts/full-solution-build.txt
- anti-stub audit transcript: bundle://proof/SB03/transcripts/focused-unit-architecture.txt

## Source Assertions

- bundle://proof/SB03/source-assertions/failing-first-helper-dependency.md
- bundle://proof/SB12/source-assertions/final-source-scans.txt
- bundle://proof/SB11/source-assertions/line-counts.txt
- bundle://proof/SB12/source-assertions/red-team-audit.md

## Failing-First And Negative Proof

- failing-first: N/A process/non-production proof; shallow failures are static architecture-source constraints captured in bundle://proof/SB03/source-assertions/failing-first-helper-dependency.md and rejected by the focused architecture transcript.
- adversarial negative proof: helper dependency drift, adapter key drift, and write-coordinator scope drift are rejected by ProcessAgentExecutionBoundaryArchitectureTests and ProcessArtifactProjectionSourceAdapters_SB05_SB08_preserve_key_and_lineage_parity.

## Passing Proof

- semantic positive proof: focused unit architecture tests passed in bundle://proof/SB03/transcripts/focused-unit-architecture.txt.
- semantic positive proof: focused integration projection slice passed in bundle://proof/SB05/transcripts/focused-integration-projection-slice.txt.
- semantic positive proof: full solution build passed in bundle://proof/SB11/transcripts/full-solution-build.txt.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| ProcessArtifactRecord projection request | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs and repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs | bundle://proof/SB05/transcripts/focused-integration-projection-slice.txt and bundle://proof/SB11/transcripts/full-solution-build.txt | ProcessAgentExecutionBoundaryArchitectureTests in bundle://proof/SB03/transcripts/focused-unit-architecture.txt rejects shallow coordinator and helper-boundary drift. |

## Browser Validation

N/A. No rendered UI route changed; bundle://proof/SB12/source-assertions/final-source-scans.txt records no prohibited viewport proof artifacts.
