# SB03 Semantic Invariants

- Invariant ID: `SB03-INV-001`
- Source raw note: "Add abstractions/seams first, then use them for concrete paths."
- Expected behavior: A process-module-local typed validation snapshot exists before rule movement and does not introduce Process Core, driver packs, EF/storage/UI dependencies, or driver APIs.
- Disallowed shallow implementation: Creating only documentation or a helper that still exposes dispatcher orchestration details as the public rule boundary.
- Failing-first test: The architecture guard would fail before the new snapshot and builder files existed.
- Passing test: `bundle://proof/SB03/transcripts/focused-architecture-test.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshot.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshotBuilder.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB03/source-assertions/validation-snapshot-seam.md`
- Red-team negative case: `bundle://proof/SB03/transcripts/anti-stub-and-guardrail-scan.txt` proves the new production files contain no `ProcessDriver`, `DriverPack`, `IProcessDriverPack`, `CanDoItAll.Processes.Core`, `TODO`, or `NotImplemented` tokens.
- Downstream dependency check: SB04 can guard the boundary and SB05 can migrate nested expectation dependencies to `ProcessArtifactValidationExpectation`.

- Raw note owned: Add seams before concrete extraction.
- Shipped behavior: Snapshot seam added; existing runtime validation behavior remains unchanged because dispatcher matching code has not yet been rewired.
- Source proof: `bundle://proof/SB03/source-assertions/validation-snapshot-seam.md`
- Test proof: `bundle://proof/SB03/transcripts/focused-architecture-test.txt`
- Shallow-pass trap: A new type that lives in Core/driver space or depends on dispatcher runtime state would look like a seam but age badly.
- Adversarial negative proof: `bundle://proof/SB03/transcripts/anti-stub-and-guardrail-scan.txt`
- Semantic positive proof: `bundle://proof/SB03/source-assertions/validation-snapshot-seam.md`
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub-and-guardrail-scan.txt`
