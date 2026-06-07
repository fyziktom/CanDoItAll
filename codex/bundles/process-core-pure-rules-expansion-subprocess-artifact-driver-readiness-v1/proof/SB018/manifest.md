# SB018 Proof Manifest
## Summary
- Subbundle: SB018 - Gate F adapter boundary proof.
- Status: Completed.
- Invariant ID: SB018-INV-001
- Hash reference: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCoreArtifactModelAdapters.cs SHA-256 e5838ad9d3dcc5106de3b1bad0f5072094332205af67cf4ab81c52136c8bf9ba
- Semantic invariant contract: bundle://proof/SB018/semantic-invariants.md
- Changed file: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCoreArtifactModelAdapters.cs
- Changed file: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs
## Evidence
- Source assertion transcript: bundle://proof/shared/transcripts/source-assertions.txt
- Source proof artifact: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCoreArtifactModelAdapters.cs
- Passing transcript: bundle://proof/shared/transcripts/unit-architecture.txt
- Failing-first transcript: bundle://proof/SB018/transcripts/failing-first.txt
- Anti-stub audit transcript: bundle://proof/shared/transcripts/anti-stub-scan.txt
- Dependency scan transcript: bundle://proof/shared/transcripts/core-forbidden-scan.txt
- Build transcript: bundle://proof/shared/transcripts/build.txt
- Driver token scan transcript: bundle://proof/shared/transcripts/driver-token-scan.txt
- No UI/media transcript: bundle://proof/shared/transcripts/no-ui-media-drift-scan.txt
## Closure
- Expected behavior: Module wrappers convert entities to Core snapshots at the boundary while runtime services remain module-local.
- Disallowed shallow implementation: Injecting Core rules through side-effect services without a clear adapter boundary.
- Downstream dependency check: bundle://proof/shared/transcripts/build.txt
