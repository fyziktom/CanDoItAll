# SB009 Proof Manifest
## Summary
- Subbundle: SB009 - Gate C subprocess artifact mapping parity.
- Status: Completed.
- Invariant ID: SB009-INV-001
- Hash reference: repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessSubprocessArtifactSourceResolver.cs SHA-256 924c8f5d3ad6b52ed9e7585cfd07b91a258c9e081d370e57bccd888ad88b5e12
- Semantic invariant contract: bundle://proof/SB009/semantic-invariants.md
- Changed file: repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessSubprocessArtifactSourceResolver.cs
- Changed file: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessArtifactSourceResolver.cs
## Evidence
- Source assertion transcript: bundle://proof/shared/transcripts/source-assertions.txt
- Source proof artifact: repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessSubprocessArtifactSourceResolver.cs
- Passing transcript: bundle://proof/shared/transcripts/focused-integration.txt
- Failing-first transcript: bundle://proof/SB009/transcripts/failing-first.txt
- Anti-stub audit transcript: bundle://proof/shared/transcripts/anti-stub-scan.txt
- Dependency scan transcript: bundle://proof/shared/transcripts/core-forbidden-scan.txt
- Build transcript: bundle://proof/shared/transcripts/build.txt
- Driver token scan transcript: bundle://proof/shared/transcripts/driver-token-scan.txt
- No UI/media transcript: bundle://proof/shared/transcripts/no-ui-media-drift-scan.txt
## Closure
- Expected behavior: Child expectation mapping rejects ambiguous parents and selects only eligible latest artifacts through Core snapshots.
- Disallowed shallow implementation: Choosing the first artifact or ignoring ambiguous child expectation matches.
- Downstream dependency check: bundle://proof/shared/transcripts/build.txt
