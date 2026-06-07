# SB012 Proof Manifest
## Summary
- Subbundle: SB012 - Gate D artifact expectation snapshot parity.
- Status: Completed.
- Invariant ID: SB012-INV-001
- Hash reference: repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessCoreArtifactModels.cs SHA-256 656075a2db894e26de2f4d2dd523972386f2b4623055858d583fa88c826ebf71
- Semantic invariant contract: bundle://proof/SB012/semantic-invariants.md
- Changed file: repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessCoreArtifactModels.cs
- Changed file: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCoreArtifactModelAdapters.cs
## Evidence
- Source assertion transcript: bundle://proof/shared/transcripts/source-assertions.txt
- Source proof artifact: repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessCoreArtifactModels.cs
- Passing transcript: bundle://proof/shared/transcripts/unit-architecture.txt
- Failing-first transcript: bundle://proof/SB012/transcripts/failing-first.txt
- Anti-stub audit transcript: bundle://proof/shared/transcripts/anti-stub-scan.txt
- Dependency scan transcript: bundle://proof/shared/transcripts/core-forbidden-scan.txt
- Build transcript: bundle://proof/shared/transcripts/build.txt
- Driver token scan transcript: bundle://proof/shared/transcripts/driver-token-scan.txt
- No UI/media transcript: bundle://proof/shared/transcripts/no-ui-media-drift-scan.txt
## Closure
- Expected behavior: Core receives strongly typed artifact snapshots without depending on module entities or persistence types.
- Disallowed shallow implementation: Passing module entities directly into Core or using loose string dictionaries for artifact identity.
- Downstream dependency check: bundle://proof/shared/transcripts/build.txt
