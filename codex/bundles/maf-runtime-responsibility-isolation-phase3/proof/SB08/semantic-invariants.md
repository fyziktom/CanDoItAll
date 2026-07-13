# SB08 Semantic Invariants

| ID | Invariant | Proof |
| --- | --- | --- |
| SB08-INV-01 | Old hotspots are smaller or have explicit follow-up blockers. | `bundle://proof/SB08/transcripts/final-codeanalytics.txt`: `WorkspaceRuntimePlugin` reduced to 964 lines/89 members; `RuntimeCapabilityComposer` and `MafAgentRuntime` remain explicit follow-up blockers. |
| SB08-INV-02 | Extracted behavior has direct tests. | `bundle://proof/SB08/transcripts/final-focused-unit-tests.txt`: 56/56 passed, including direct approval, session persistence, descriptor catalog, and image model resolver tests. |
| SB08-INV-03 | Runtime wiring still works through public entry points. | `bundle://proof/SB08/transcripts/final-integration.txt`: 3/3 `MafAgentRuntimeHandoffTests` passed. |
| SB08-INV-04 | No prose-only closure. | `bundle://proof/SB08/manifest.md`, `bundle://proof/SB08/changed-file-hashes.txt`, source assertions, test transcripts, and CodeAnalytics snapshot are captured. |
| SB08-INV-05 | No runtime partial class expansion remains in the MAF runtime folder. | `bundle://proof/SB08/transcripts/source-assertions.txt`: `rg` found no runtime partial declarations. |
| SB08-INV-06 | Full-suite failures are not hidden as success. | `bundle://proof/SB08/transcripts/full-unit-tests.txt`: 13 unrelated failures are recorded separately from focused passing proof. |

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Approval continuation records | `MafApprovalContinuationDriver` | Runtime approval continuation flow | Approval request through response | Direct driver tests would fail if runtime-owned cache logic returned. |
| Runtime session compatibility state | `MafRuntimeSessionPersistenceDriver` | Runtime response/session persistence | Provider completion through response | Direct skip-policy test would fail if driver were bypassed. |
| Capability access plan | `RuntimeCapabilityAccessPlanner` | Capability composer and tool-provider attachment | Capability composition | Source assertion blocks composer partial regression; composition tests prove runtime path. |
| Workspace image-analysis model selection | `WorkspaceImageAnalysisModelResolver` | Workspace plugin and input attachment preparation | Image analysis model selection | Source assertion fails if plugin regains local resolver implementation. |
| Final gate result | Architecture review | Bundle closure | End of phase | Closure is pass with follow-up required, not full completion. |
